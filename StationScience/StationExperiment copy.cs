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
            Starved,    // Experiment is starved of resources
            NotStarted  // Experiment has no resource requirements currently
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

            // Ensure part information is available
            if (part.partInfo != null)
            {
                node = GameDatabase.Instance.GetConfigs("PART")
                    .Single(c => part.partInfo.name == c.name.Replace('_', '.'))
                    .config.GetNodes("MODULE")
                    .Single(n => n.GetValue("name") == moduleName);
            }

            // Load the requiredParts field if present
            if (node.HasValue("requiredParts"))
            {
                requiredParts = node.GetValue("requiredParts");
            }

            // Add default requirements if they are not already added
            AddDefaultRequirements();

            // Update the status after loading
            UpdateStatus();
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

        // Event to finalize the experiment
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_finalizeExp", active = false)]
        public void FinalizeExperiment()
        {
            // Check if all experiment requirements are met
            if (Finished())
            {
                // Create the science data
                ScienceData data = new ScienceData(GetScienceCount(), 1f, 0f, subjectID, "Science Experiment");

                // Add the science data to the experiment
                DeployedScienceData = data;
                collectedScience.Add(data);
                last_subjectId = subjectID;
                currentStatus = Status.Completed;

                // Display a screen message to notify the player
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalized"), 6, ScreenMessageStyle.UPPER_CENTER);

                // Make the FinalizeExperiment event inactive
                Events["FinalizeExperiment"].active = false;
            }
            else
            {
                // Display a screen message if the experiment is not yet finished
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_incomplete"), 6, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        // Coroutine to periodically update the experiment status
        private IEnumerator UpdateStatusCoroutine()
        {
            while (true)
            {
                UpdateStatus();
                yield return new WaitForSeconds(1f); // Adjust the update frequency as needed
            }
        }

        // Method to update the experiment status
        private void UpdateStatus()
        {
            if (CheckBoring(vessel, false))
            {
                currentStatus = Status.BadLocation;
            }
            else if (!CheckRequiredParts())
            {
                currentStatus = Status.Inoperable;
            }
            else if (Finished())
            {
                currentStatus = Status.Completed;
            }
            else if (requirements.Count == 0)
            {
                currentStatus = Status.NotStarted;
            }
            else
            {
                currentStatus = Status.Running;
            }

            // Update the status display
            Fields["currentStatus"].guiActive = true;
            Fields["currentStatus"].guiName = Localizer.Format("#autoLOC_StatSci_status", currentStatus.ToString());
        }
    }
}
