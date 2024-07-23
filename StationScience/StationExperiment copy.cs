/*
    This file is part of Station Science.

    Station Science is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Station Science is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Station Science.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections;
using UnityEngine;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using KSP_Log;

namespace StationScience
{
    public class StationExperiment : ModuleScienceExperiment
    {
        // Constants representing resource names
        public const string EUREKAS = "Eurekas";
        public const string KUARQS = "Kuarqs";
        public const string BIOPRODUCTS = "Bioproducts";
        public const string SOLUTIONS = "Solutions";

        // Inner class to represent a requirement with a name and amount
        internal class Requirement
        {
            internal string Name { get; }
            internal float Amount { get; }

            internal Requirement(string name, float amount)
            {
                Name = name;
                Amount = amount;
            }
        }

        // Enumeration to represent the status of the experiment
        public enum Status
        {
            Idle,       // Experiment is idle
            Running,    // Experiment is running
            Completed,  // Experiment is completed
            BadLocation,// Vessel is in a bad location for the experiment
            Storage,    // Experiment is in storage
            Inoperable, // Experiment is inoperable
            Starved     // Experiment is starved of resources
        }

        // Logging instance
        static Log Log;

        // Dictionary to hold experiment requirements
        internal Dictionary<string, Requirement> requirements = new();

        // Experiment requirements fields
        [KSPField(isPersistant = false)] public int eurekasRequired;
        [KSPField(isPersistant = false)] public int kuarqsRequired;
        [KSPField(isPersistant = false)] public int bioproductsRequired;
        [KSPField(isPersistant = false)] public int solutionsRequired;
        [KSPField(isPersistant = false)] public float kuarqHalflife;

        // GUI fields for kuarq decay and experiment status
        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")] public float kuarqDecay;
        [KSPField(isPersistant = false, guiName = "Status", guiActive = true)] public Status currentStatus = Status.Idle;

        // Persistent fields for experiment progress tracking
        [KSPField(isPersistant = true)] public float launched = 0;
        [KSPField(isPersistant = true)] public float completed = 0;
        [KSPField(isPersistant = true)] public string last_subjectId = "";

        // Field for specifying required parts
        [KSPField(isPersistant = false)] public string requiredParts = ""; // Comma-separated list of part names

        // Method to check if the vessel is in a boring location for experiments
        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            Log?.Info($"{vessel.Landed}, {vessel.landedAt}, {vessel.launchTime}, {vessel.situation}, {vessel.orbit.referenceBody.name}");
            if (vessel.orbit.referenceBody == FlightGlobals.GetHomeBody() &&
                (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH ||
                vessel.situation == Vessel.Situations.SPLASHED || vessel.altitude <= vessel.orbit.referenceBody.atmosphereDepth))
            {
                if (msg)
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_boring"), 6, ScreenMessageStyle.UPPER_CENTER);
                return true;
            }
            return false;
        }

        // Helper methods to get and set resources on the part
        public PartResource GetResource(string name) => ResourceHelper.getResource(part, name);
        public double GetResourceAmount(string name) => ResourceHelper.getResourceAmount(part, name);
        public double GetResourceMaxAmount(string name) => ResourceHelper.getResourceMaxAmount(part, name);
        public PartResource SetResourceMaxAmount(string name, double max) => ResourceHelper.setResourceMaxAmount(part, name, max);

        // Method to check if the experiment is finished
        public bool Finished()
        {
            foreach (var r in requirements)
            {
                double amount = GetResourceAmount(r.Value.Name);
                Log.Info($"{part.partInfo.title} {r.Value.Name}: {amount}/{r.Value.Amount:F1}");
                if (Math.Round(amount, 2) < r.Value.Amount)
                    return false;
            }
            return true;
        }

        // Method to check if the required parts are present on the vessel
        private bool CheckRequiredParts()
        {
            // If requiredParts is empty or null, return true as no parts are required
            if (string.IsNullOrEmpty(requiredParts))
                return true;

            // Split the requiredParts string into individual part names
            var partNames = requiredParts.Split(',').Select(name => name.Trim()).ToList();

            // Get a list of part names currently on the vessel
            var partsOnVessel = vessel.parts.Select(p => p.partInfo.title).ToList();

            // Check if all required parts are present
            foreach (var requiredPart in partNames)
            {
                if (!partsOnVessel.Contains(requiredPart))
                {
                    // Notify the player and return false if any required part is missing
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_missing_part", requiredPart), 6, ScreenMessageStyle.UPPER_CENTER);
                    return false;
                }
            }

            // All required parts are present
            return true;
        }

        // Method to load experiment data from a ConfigNode
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (part.partInfo != null)
            {
                node = GameDatabase.Instance.GetConfigs("PART")
                    .Single(c => part.partInfo.name == c.name.Replace('_', '.'))
                    .config.GetNodes("MODULE")
                    .Single(n => n.GetValue("name") == moduleName);
            }

            var resourceDefinitions = PartResourceLibrary.Instance.resourceDefinitions;
            foreach (ConfigNode resNode in node.GetNodes("REQUIREMENT"))
            {
                try
                {
                    string name = resNode.GetValue("name");
                    float amount = float.Parse(resNode.GetValue("maxAmount"));
                    requirements.Add(name, new Requirement(name, amount));

                    var def = resourceDefinitions[name];
                    if (def.resourceTransferMode != ResourceTransferMode.NONE)
                    {
                        var resource = part.AddResource(resNode);
                        part.Resources.Remove(resource);
                    }
                }
                catch (Exception ex)
                {
                    Log?.Error($"Error loading resource requirements: {ex.Message}");
                }
            }

            // Load the requiredParts field if present
            if (node.HasValue("requiredParts"))
            {
                requiredParts = node.GetValue("requiredParts");
            }

            AddDefaultRequirements();
        }

        // Method to add default requirements if they are not already added
        private void AddDefaultRequirements()
        {
            if (eurekasRequired > 0 && !requirements.ContainsKey(EUREKAS))
                requirements.Add(EUREKAS, new Requirement(EUREKAS, eurekasRequired));
            if (kuarqsRequired > 0 && !requirements.ContainsKey(KUARQS))
                requirements.Add(KUARQS, new Requirement(KUARQS, kuarqsRequired));
            if (bioproductsRequired > 0 && !requirements.ContainsKey(BIOPRODUCTS))
                requirements.Add(BIOPRODUCTS, new Requirement(BIOPRODUCTS, bioproductsRequired));
            if (solutionsRequired > 0 && !requirements.ContainsKey(SOLUTIONS))
                requirements.Add(SOLUTIONS, new Requirement(SOLUTIONS, solutionsRequired));
        }

        // Method called when the experiment module is started
        public override void OnStart(StartState state)
        {
#if DEBUG
            Log = new Log("StationScience", Log.LEVEL.INFO);
#else
            Log = new Log("StationScience", Log.LEVEL.ERROR);
#endif
            base.OnStart(state);
            if (state == StartState.Editor) return;

            UpdateStatus(); // Call to update status based on current conditions

            this.part.force_activate();
            StartCoroutine(UpdateStatusCoroutine());
        }

        // Event to start the experiment
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_startExp", active = true)]
        public void StartExperiment()
        {
            if (GetScienceCount() > 0)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalize"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            if (!CheckRequiredParts())
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_missing_parts"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            if (CheckBoring(vessel, true)) return;

            foreach (var r in requirements)
            {
                var resource = SetResourceMaxAmount(r.Value.Name, r.Value.Amount);
                if (resource.amount == 0 && r.Value.Name == BIOPRODUCTS)
                    SetResourceMaxAmount(EUREKAS, 0);
            }

            Events["StartExperiment"].active = false;
            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_started"), 6, ScreenMessageStyle.UPPER_CENTER);
            currentStatus = Status.Running;
        }

        // Action to start the experiment, usable in action groups
        [KSPAction("#autoLOC_StatSci_startExp")]
        public void StartExpAction(KSPActionParam p) => StartExperiment();

        // Method to check if deployment conditions are met
        public bool DeployChecks()
        {
            if (CheckBoring(vessel, true)) return false;
            if (Finished())
            {
                Events["DeployExperiment"].active = false;
                Events["StartExperiment"].active = false;
                return true;
            }
            ScreenMessages.PostScreenMessage("#autoLOC_StatSci_screen_notfinished", 6, ScreenMessageStyle.UPPER_CENTER);
            return false;
        }

        // Method to deploy the experiment
        public void DeployExperimentCustom()
        {
            if (DeployChecks())
            {
                // Custom logic for deployment
                currentStatus = Status.Storage;
            }
        }

        // Method to reset the experiment
        public void ResetExperimentCustom()
        {
            // Custom logic to reset the experiment
            currentStatus = Status.Idle;
            // Optionally reset other fields or states as needed
        }

        // Method to update the status of the experiment
        private void UpdateStatus()
        {
            Log.Info($"Updating status for {part.partInfo.title}");

            // Check if the science count is greater than zero
            if (GetScienceCount() > 0)
            {
                Log.Info("Science count is greater than 0, setting status to Completed");
                currentStatus = Status.Completed;
                return;
            }

            switch (vessel.situation)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.SPLASHED:
                case Vessel.Situations.PRELAUNCH:
                    Log.Info("Vessel in a bad location, setting status to BadLocation");
                    currentStatus = Status.BadLocation;
                    break;
                default:
                    if (Finished())
                    {
                        Log.Info("Experiment finished, setting status to Storage");
                        currentStatus = Status.Storage;
                    }
                    else if (currentStatus != Status.Running)
                    {
                        Log.Info("Experiment not running, setting status to Idle");
                        currentStatus = Status.Idle;
                    }
                    break;
            }

            if (currentStatus == Status.Running)
            {
                foreach (var r in requirements)
                {
                    double amount = GetResourceAmount(r.Value.Name);
                    if (amount < r.Value.Amount)
                    {
                        Log.Info($"{r.Value.Name} resource starved. Current amount: {amount}, required: {r.Value.Amount}");
                        currentStatus = Status.Starved;
                        return;
                    }
                }
            }

            if (currentStatus == Status.Starved)
            {
                Log.Info("Experiment starved, setting status to Idle");
                currentStatus = Status.Idle;
            }
        }

        // Method to get the science count, with added logging for debugging
        public new int GetScienceCount()
        {
            int scienceCount = base.GetScienceCount(); // Call base class method
            Log.Info($"Science count: {scienceCount}");
            return scienceCount;
        }

        // Coroutine to periodically update the status
        private IEnumerator UpdateStatusCoroutine()
        {
            while (true)
            {
                UpdateStatus();
                yield return new WaitForSeconds(1.0f); // Update every second or adjust as needed
            }
        }
    }
}
