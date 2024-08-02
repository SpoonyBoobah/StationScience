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

using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;
using KSP_Log;
using System.Diagnostics.Eventing.Reader;

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
            Idle,        // Experiment is idle, meaning it has not been started and has no requirements prepared.
            Running,     // Experiment is running, so requirements have been added and are collecting the requirements from the appropriate lab.
            Finished,    // Experiment is completed and ready to be stored or transmitted for science points.
            BadLocation, // Vessel is in a bad location for the experiment and cannot be started.
            Storage,     // Experiment is in storage, meaning the experiment was finished but instead of transmitting the results the result is "stored / saved" to be returned home, in "Storage" the experiment has to be "reset" or transmitted before doing anything else.
            Inoperable,  // Experiment is inoperable (NOTE: Do not think this is properly used at any point???)
            Starved,     // Experiment is starved of resources (NOTE: Do not think this is properly used at any point???)
            Failed,      // Experiment failed and will have to be restarted (FUTURE UPDATE: Select "Hard Difficulty" and the experiment will have a slight chance to fail it it does, player will have to reset the pod and start again.
            Dead,        // Experiment failed and cannot be reused (FUTURE UPDATE: Select "Extreme Difficulty" and then if the experiment "fails", the pod is rendered useless and a new pod will have to be sent up.
        }

        // Dictionary to hold experiment requirements
        internal Dictionary<string, Requirement> requirements = new();

        // Experiment requirements fields
        [KSPField(isPersistant = false)] public int eurekasRequired;
        [KSPField(isPersistant = false)] public int kuarqsRequired;
        [KSPField(isPersistant = false)] public int bioproductsRequired;
        [KSPField(isPersistant = false)] public int solutionsRequired;
        [KSPField(isPersistant = false)] public float kuarqHalflife;

        // GUI fields for kuarq decay.
        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")] public float kuarqDecay;
        //GUI field for experiment Status, IMPORTANT THIS IS THE FIELD THAT PUTS IN THE STATUS IN THE CONTEXT MENU IN-GAME AND ALSO DEFAULT SET THE EXPERIMENT STATUS TO IDLE! IT ALSO SAVES WHATEVER STATE THE EXPERIMENT IS IN FOR NEXT LOAD
        [KSPField(isPersistant = true, guiName = "Status", guiActive = true)] public Status currentStatus = Status.Idle;

        // Persistent fields for experiment progress tracking in KSP contracts
        [KSPField(isPersistant = true)] public float launched = 0;
        [KSPField(isPersistant = true)] public float completed = 0;
        [KSPField(isPersistant = true)] public string last_subjectId = "";

        // Field for specifying required parts
        [KSPField(isPersistant = false)] public string requiredParts = ""; // Comma-separated list of part names

        // Logging instance ??Uses KSP_Log.dll from SpaceTuxLibrary??
        static Log Log;

        // Method to check if the vessel is in a "boring" location for experiments
        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            Log?.Info($"{vessel.Landed}, {vessel.landedAt}, {vessel.launchTime}, {vessel.situation}, {vessel.orbit.referenceBody.name}"); //DISABLED due to log spam!
            if (vessel.orbit.referenceBody == FlightGlobals.GetHomeBody() &&
                (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH ||
                vessel.situation == Vessel.Situations.SPLASHED || vessel.altitude <= vessel.orbit.referenceBody.atmosphereDepth))
            {
                if (msg) //If experiment is in bad location then create a pop-up message on screen saying so!
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_boring"), 6, ScreenMessageStyle.UPPER_CENTER);
                return true;
            }
            return false;
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
              
            
        }

        // Method to add default requirements if they are not already added.
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

        // Helper methods to get and set resources on the part.
        public PartResource GetResource(string name) => ResourceHelper.getResource(part, name);
        public double GetResourceAmount(string name) => ResourceHelper.getResourceAmount(part, name);
        public double GetResourceMaxAmount(string name) => ResourceHelper.getResourceMaxAmount(part, name);
        public PartResource SetResourceMaxAmount(string name, double max) => ResourceHelper.setResourceMaxAmount(part, name, max);

        //Method to disable all KSPEvent buttons from the mod. TESTING PURPOSES ONLY!
        private void DisableAllEvents()
        {
            //Events[nameof(StartExperiment)].active = false;
            Events[nameof(FinishExperiment)].active = false;
            Events[nameof(DeployExperiment)].active = false;

        }

        // Method called when the experiment module is started.
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None)
                
                return;

            //part.force_activate();
            StartCoroutine(UpdateStatusCoroutine());
        }

        // Coroutine to periodically update the status and is constantly monitored and managed in real-time, allowing the game to react appropriately to changes in the experiment's state.
        private IEnumerator UpdateStatusCoroutine()
        {
            while (true)
            {
                // Call status-specific update function
                switch (currentStatus)
                {
                    case Status.Running:
                        UpdateRunning();
                        break;
                }

                DisableAllEvents();

                yield return new WaitForSeconds(1.0f); // Update every second or adjust as needed
            }
        }

        //Method to manage state transitions for an experiment, usually triggered by KSPEvent UI.
        private void SetStatus(Status newStatus)
        {
            if (currentStatus == newStatus) return;
            switch (currentStatus)
            {
                case Status.Idle:
                    OnIdleExit();
                    break;
            }
            currentStatus = newStatus;
            switch (currentStatus)
            {
                case Status.Running:
                    OnRunningEnter();
                    break;
            }

        }

        private void UpdateRunning()
        {


        }

        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_startExp", active = true)]
        public void StartExperiment()
        {
            if (currentStatus != Status.Idle)
            {
                Log?.Info($"Cannot Start Experiment because we are not idle currentStatus = {currentStatus}"); //This should'nt ever happen, its just in case there is an error in the code which is showing the StartExp button in wrong state.
                return;
            }

            if (CheckBoring(vessel, true)) // Check if the experiment is boring; if it is, remain idle (return false) and disable the Start Experiment button.
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_boring"), 6, ScreenMessageStyle.UPPER_CENTER);
                Log?.Info("Cannot start Experiment here!");
                return;

            }

            SetStatus(Status.Running); //When button is pressed the Staus of experiment will change to "Running" as long as the 2 checks above don't apply.
        }

        [KSPEvent(guiActive = true, guiName = "#autoLOC_statsci_finishExp", active = true)]
        public void FinishExperiment()
        {

        }

        private void OnIdleExit()
        {
            // Log the transition for debugging purposes
            Log?.Info($"Exiting Idle state for {part.partInfo.title}");

            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_started"), 6, ScreenMessageStyle.UPPER_CENTER); // Pop-up message "Started experiment!"

            // Successfully exited idle, so disable the StartExperiment event and return true
            Events[nameof(StartExperiment)].active = false;

            // Update the experiment status to Running
            currentStatus = Status.Running;

        }

        private void OnRunningEnter()
        {
            foreach (var r in requirements)
            {
                var resource = SetResourceMaxAmount(r.Value.Name, r.Value.Amount);
                if (resource.amount == 0 && r.Value.Name == BIOPRODUCTS)
                    SetResourceMaxAmount(EUREKAS, 0);
            }
        }

        
    }
}