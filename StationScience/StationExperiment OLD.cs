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
            Starved,     // Experiment is starved of resources
            NotStarted  // Experiment has not been started
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
        [KSPField(isPersistant = false, guiName = "Status", guiActive = true)] public Status currentStatus = Status.NotStarted;

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
                // Check if the required resource is null
                if (r.Value == null || r.Value.Name == null)
                {
                    Log.Info($"{part.partInfo.title} required resource is null");
                    currentStatus = Status.Idle;
                    return false;
                }

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
            base.OnStart(state);
            if (state == StartState.Editor) return;

            UpdateStatus(); // Call to update status based on current conditions

            this.part.force_activate();
            StartCoroutine(UpdateStatusCoroutine());
        }

        // Event to start the experiment THIS IS "Start Experiment @ Load in
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
                // Set the maximum amount of each resource requirement to 0, indicating the experiment is done consuming resources
                foreach (var req in requirements)
                {
                    SetResourceMaxAmount(req.Value.Name, 0);
                }

                // Update the status to completed
                currentStatus = Status.Completed;

                // Notify the player that the experiment has been finalized
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalized"), 6, ScreenMessageStyle.UPPER_CENTER);

                // Create a new ScienceData object with the experiment results
                //ScienceData experimentResult = new ScienceData(
                    //dataAmount,               // The amount of data collected from the experiment
                    //xmitValue,                // The transmission value of the collected data
                    //xmitDataScalar,           // The scalar for data transmission efficiency
                    //subjectId,                // The unique identifier for the science subject
                    //title                     // The title of the science data
                //);

                 // Create a new dialog page to display the experiment results
                 //ExperimentResultDialogPage page = new ExperimentResultDialogPage(
                 //host: part,                    // The part where the experiment is conducted
                 //data: experimentResult,        // The collected science data
                 //transmitScalar: xmitDataScalar,// Scalar for data transmission
                 //onDiscard: null,               // Reference data (usually null)
                 //onKeep: null,                  // Transfer data (usually null)
                 //hideTransmit: false,           // Whether to hide experiment results
                 //xp: "",                        // Optional title for the experiment results
                 //showTransmit: true,            // Whether to show the transmit and keep buttons
                 //showLabOption: false,          // Whether to show the review data button
                 //flightId: part.flightID        // The flight ID of the part
                //);

                // If not in sandbox mode, submit the science data and show the results dialog
                if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX)
                {
                    // Submit the collected science data, triggering the display of the results dialog
                    ResearchAndDevelopment.Instance.SubmitScienceData(experimentResult, ResearchAndDevelopment.Instance.Science, null);
                }

                // Disable the "FinalizeExperiment" event to prevent further calls
                Events["FinalizeExperiment"].active = false;
            }
            else
            {
                // Notify the player that the experiment is not finished yet
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_notfinished"), 6, ScreenMessageStyle.UPPER_CENTER);
            }
        }


        // Action to finalize the experiment, usable in action groups
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_finalizeExp", active = false)]
        public void FinalizeExpAction(KSPActionParam p)
        {
            FinalizeExperiment();
        }

        // Method to check if deployment conditions are met
        public bool DeployChecks()
        {
            if (CheckBoring(vessel, true)) return false;
            if (Finished())
            {
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

            bool isSandbox = HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;

            // Check if resource requirements are met
            bool allRequirementsMet = true;
            bool allRequirementsZeroOrNull = true;

            foreach (var r in requirements)
            {
                var resource = GetResource(r.Value.Name);
                if (resource == null)
                {
                    Log.Info($"{r.Value.Name} resource is null.");
                    allRequirementsMet = false;
                    break;
                }
                
                double amount = resource.amount;
                if (amount < r.Value.Amount)
                {
                    Log.Info($"{r.Value.Name} resource requirement not met. Current amount: {amount}, required: {r.Value.Amount}");
                    allRequirementsMet = false;
                    break;
                }

                if (r.Value.Amount != 0)
                {
                    allRequirementsZeroOrNull = false;
                }
            }

            if (allRequirementsZeroOrNull)
            {
                Log.Info("All resource requirements are zero or null, setting status to Idle.");
                currentStatus = Status.Idle;
                Events["FinalizeExperiment"].active = currentStatus == Status.Completed;

            }
            else if (isSandbox)
            {
                if (allRequirementsMet)
                {
                    Log.Info("All resource requirements met in Sandbox mode, setting status to Completed.");
                    currentStatus = Status.Completed; // Change to Completed
                }
                else if (currentStatus != Status.Running)
                {
                    Log.Info("Experiment not running in Sandbox mode, setting status to Idle.");
                    currentStatus = Status.Idle;
                }
                Events["FinalizeExperiment"].active = currentStatus == Status.Completed;
                Log.Info($"FinalizeActive: {Events["FinalizeExperiment"].active}");
                return; // Skip science count check in Sandbox mode
            }
            else
            {
                // Non-Sandbox mode logic
                if (GetScienceCount() > 0)
                {
                    Log.Info("Science count is greater than 0, setting status to Completed.");
                    currentStatus = Status.Completed;
                }
                else
                {
                    // Check the vessel’s situation
                    switch (vessel.situation)
                    {
                        case Vessel.Situations.LANDED:
                        case Vessel.Situations.SPLASHED:
                        case Vessel.Situations.PRELAUNCH:
                            Log.Info("Vessel in a bad location, setting status to BadLocation.");
                            currentStatus = Status.BadLocation;
                            break;
                        default:
                            if (Finished())
                            {
                                Log.Info("Experiment finished, setting status to Storage.");
                                currentStatus = Status.Storage;
                            }
                            else if (currentStatus != Status.Running)
                            {
                                Log.Info("Experiment not running, setting status to Idle.");
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
                                break;
                            }
                        }
                    }

                    if (currentStatus == Status.Starved)
                    {
                        Log.Info("Experiment starved, setting status to Idle.");
                        currentStatus = Status.Idle;
                    }
                }

                // Update the visibility of the FinalizeExperiment button
                Events["FinalizeExperiment"].active = currentStatus == Status.Completed;
                Log.Info($"FinalizeActive: {Events["FinalizeExperiment"].active}");
                
                // Update the visibility of the default KSP Science UI trigger
                Events["StartExperiment"].active = currentStatus == Status.Idle;
            }
        }

        // Method to get the science count, with added logging for debugging
        public new int GetScienceCount()
        {
            // Check if the game is in Sandbox mode
            if (HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX)
            {
                Log.Info("Game mode is Sandbox, returning science count of 0.");
                return 0;
            }

            // Call base class method to get the science count
            int scienceCount = base.GetScienceCount();
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