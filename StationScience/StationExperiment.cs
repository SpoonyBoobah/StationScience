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
        public class Requirement
        {
            internal string Name { get; set; }
            internal float Amount { get; set; }

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
            Inoperable,  // Experiment is inoperable due to data being transmitted and will need resetting.
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

        //GUI field for experiment Status, IMPORTANT THIS IS THE FIELD THAT PUTS IN THE STATUS IN THE CONTEXT MENU IN-GAME AND ALSO DEFAULT SET THE EXPERIMENT STATUS TO IDLE! IT ALSO SAVES WHATEVER STATE THE EXPERIMENT IS IN FOR NEXT LOAD
        [KSPField(isPersistant = true, guiName = "Status", guiActive = true, guiActiveEditor = false, groupName = "StationScience", groupDisplayName = "Experiment", groupStartCollapsed = false)]
        public Status currentStatus = Status.Idle;
        // GUI fields for kuarq decay.
        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiActiveEditor = false, guiFormat = "F2", groupName = "StationScience", groupDisplayName = "StationScience", groupStartCollapsed = false)]
        public float kuarqDecay;

        // Persistent fields for experiment progress tracking in KSP contracts
        [KSPField(isPersistant = true)] public float launched = 0;
        [KSPField(isPersistant = true)] public float completed = 0;
        [KSPField(isPersistant = true)] public string last_subjectId = "";

        // Field for specifying required parts
        [KSPField(isPersistant = false)] public string requiredParts = ""; // Comma-separated list of part names

        // Logging instance ??Uses KSP_Log.dll from SpaceTuxLibrary??
        //static Log Debug.Log;

        // Method to check if the vessel is in a "boring" location for experiments
        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            //Debug.Log($"[STNSCI-EXP] {vessel.Landed}, {vessel.landedAt}, {vessel.launchTime}, {vessel.situation}, {vessel.orbit.referenceBody.name}"); //DISABLED due to log spam!
            if (vessel.orbit.referenceBody == FlightGlobals.GetHomeBody() &&
                (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH ||
                vessel.situation == Vessel.Situations.SPLASHED || vessel.altitude <= vessel.orbit.referenceBody.atmosphereDepth))
            {
                if (msg) //If experiment is in bad location then create a pop-up message on screen saying so!
                    PopUpMessage("#autoLOC_StatSci_screen_boring"); //"Too boring here, go to space!"
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

        private void RemoveAllReqs()
        {
            var reqsToRemove = new List<string>();

            foreach (var req in requirements)
            {
                SetResourceMaxAmount(req.Value.Name, 0);
                reqsToRemove.Add(req.Key);
            }

            foreach (var key in reqsToRemove)
            {
                requirements.Remove(key);
            }

        }

        // Helper methods to get and set resources on the part.
        public PartResource GetResource(string name) => ResourceHelper.getResource(part, name);
        public double GetResourceAmount(string name) => ResourceHelper.getResourceAmount(part, name);
        public double GetResourceMaxAmount(string name) => ResourceHelper.getResourceMaxAmount(part, name);
        public PartResource SetResourceMaxAmount(string name, double max) => ResourceHelper.setResourceMaxAmount(part, name, max);

        // Overrides the GetInfo method to include the requirement info in the returned string.
        public override string GetInfo()
        {
            // Generate the requirement information string using the existing method
            string requirementInfo = GenerateRequirementInfo(requirements, kuarqHalflife, part.mass);

            // Return the combined string of requirement info and the base class info
            return requirementInfo + base.GetInfo();
        }
        

        //Method to disable all KSPEvent buttons from the mod. TESTING PURPOSES ONLY!
        private void DisableAllEvents()
        {
            Events[nameof(StartExperiment)].active = false;
            Events[nameof(FinishExperiment)].active = false;
            Events[nameof(DeployExperiment)].active = false;

        }

        // Method called when the experiment module is started.
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor || state == StartState.None)

                return;

            part.force_activate();
            StartCoroutine(UpdateStatusCoroutine());
        }

        // Coroutine to periodically update the status and is constantly monitored and managed in real-time, allowing the game to react appropriately to changes in the experiment's state.
        private IEnumerator UpdateStatusCoroutine()
        {
            while (true)
            {
                // Call status-specific update functions.
                switch (currentStatus)
                {
                    case Status.Running:
                        UpdateRunning();
                        UpdateKuarqDecay();
                        break;
                    case Status.Idle:
                        UpdateIdle();
                        //DisableAllEvents(); //For debugging purposes, this will disable all Event UI buttons whilst in Idle state.
                        break;
                    case Status.Finished:
                        UpdateFinished();
                        break;
                    case Status.Storage:
                        UpdateStorage();
                        //RemoveAllReqs(); //For debugging purposes, this will remove all requirement resources from the experiment without any checks being done.
                        break;
                    case Status.Inoperable:
                        UpdateInoperable();
                        break;
                }

                yield return new WaitForSeconds(0.1f); // Update every second or adjust as needed
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
                case Status.Storage:
                    OnEnterStorage();
                    break;
                case Status.Inoperable:
                    break;
            }

        }

        private void UpdateIdle()
        {
            int scienceCount = GetScienceCount(); // Retrieve the count of stored ScienceData reports

            // Check if the current status of the experiment is "Idle"
            if (currentStatus == Status.Idle && scienceCount == 0 && !Inoperable)
            {
                // Enable the "Start Experiment" UI button by setting its 'active' property to true
                Events[nameof(StartExperiment)].active = true;

                // Disable the "Deploy Experiment" UI button by setting its 'active' property to false
                // This button is typically used for starting or deploying the experiment in stock KSP.
                Events[nameof(DeployExperiment)].active = false;

                // Set the 'Deployed' state to false indicating that the experiment is not deployed
                Deployed = false;

                Inoperable = false;

                //Debug.Log("[STNSCI-EXP] Deployed field set to false in Idle status");
            }
        }


        private bool UpdateRunning()
        {
            // Ensure that the experiment is in the running state.
            // If the current status is not 'Running', there's nothing to update, so return false.
            if (currentStatus != Status.Running)
            {
                return false;
            }

            // Disable the "Start Experiment" and "Deploy Experiment" buttons when the experiment is running.
            Events[nameof(StartExperiment)].active = false;
            Events[nameof(DeployExperiment)].active = false;

            // Iterate over each requirement in the 'requirements' dictionary.
            foreach (var r in requirements)
            {
                // Check if the requirement or its name is null.
                // If either is null, log an error and revert the experiment status to 'Idle'.
                if (r.Value?.Name == null)
                {
                    Debug.Log($"[STNSCI-EXP] {part.partInfo.title} ERROR! required resource is null in Running");
                    SetStatus(Status.Idle); // Use SetStatus to properly handle state transitions.
                    return false; // Return false as the experiment cannot run without valid resources.
                }

                // Get the current amount of the required resource from the part.
                double amount = GetResourceAmount(r.Value.Name);
                // Round the amount down to 2 decimal places.
                double roundedAmount = Math.Floor(amount * 100) / 100;
                // Log the current amount and required amount for debugging purposes.
                //Debug.Log($"[STNSCI-EXP] {part.partInfo.title} {r.Value.Name}: {roundedAmount:F2}/{r.Value.Amount:F1}"); //Disabled as only for debugging purposes!


                // Check if the available amount of the resource is less than the required amount.
                // If the resource amount is insufficient, return false to indicate that the experiment cannot continue.
                if (roundedAmount < r.Value.Amount)
                {
                    return false;
                }
            }
            //Ensure the experiment is not "Deployed"
            Deployed = false;
            // If all requirements are met (sufficient resources for each requirement), call the FinishExperiment method to conclude the experiment.
            FinishExperiment();
            // Return true to indicate that the experiment has successfully finished
            return true;

        }

        public void UpdateFinished()
        {
            // Check if the experiment is currently deployed
            if (Deployed) // Using the inherited stock KSP Deployed property/field
            {
                // Set the status of the experiment to "Storage"
                SetStatus(Status.Storage);

                // Disable the "Deploy Experiment" UI button by setting its 'active' property to false
                // This button is not needed once the experiment has finished and is being stored
                Events[nameof(DeployExperiment)].active = false;

                // Disable the "Start Experiment" UI button by setting its 'active' property to false
                // This ensures that no further actions can be initiated after the experiment is finished
                Events[nameof(StartExperiment)].active = false;

                // Remove all requirements related to the experiment
                // This could involve clearing dependencies, conditions, or other constraints
                RemoveAllReqs();
                Debug.Log("[STNSCI-EXP] Deployed field set to true in Finished status");
            }
        }


        // Method called to update the experiment when it's in the Storage state.
        public void UpdateStorage()
        {
            int scienceCount = GetScienceCount(); // Retrieve the count of stored ScienceData reports

            if (scienceCount > 0) // If there is any stored ScienceReports...
            {
                Events[nameof(StartExperiment)].active = false; // Disable the "Start Experiment" button
                Deployed = true;
            }
            else // No stored science data
            {
                // Set experiment status to "Storage"
                SetStatus(Status.Idle);
                Deployed = false;

                // Check if the experiment is rerunnable. If not, make it inoperable when it leaves Storage
                if (!rerunnable)
                {
                    SetStatus(Status.Inoperable);
                    Debug.Log($"[STNSCI-EXP] {part.partInfo.title} is now inoperable after being stored.");
                    Events[nameof(StartExperiment)].active = false; // Disable start experiment action
                    Inoperable = true;
                    Debug.Log("[STNSCI-EXP] Inoperable field set to true in Storage status");
                }
            }
        }

        // New method to check if the experiment should become inoperable when leaving Storage
        public void UpdateInoperable()
        {
            int scienceCount = GetScienceCount(); // Retrieve the count of stored ScienceData reports

            // Log the retrieved science count
            //Debug.Log($"[STNSCI-EXP] Retrieved Science Count: {scienceCount}");
            //Debug.Log($"[STNSCI-EXP] Current Status: {currentStatus}, Inoperable Flag: {Inoperable}");

            // Check if the current status is Inoperable
            if (currentStatus == Status.Inoperable)
            {
                if (scienceCount == 0 && Inoperable)
                {
                    // Keep the experiment in Inoperable status if science count is zero and Inoperable is true
                    //Debug.Log("[STNSCI-EXP] Status is Inoperable and Science Count is zero. Keeping status as Inoperable.");
                    Events[nameof(StartExperiment)].active = false; // Disable start experiment action
                }
                else
                {
                    // Science count is greater than zero
                    //Debug.Log("[STNSCI-EXP] Status is Inoperable, but Science Count is greater than zero. Resetting status to Idle.");
                    Inoperable = false; // Set to operable
                    SetStatus(Status.Idle); // Set status to Idle
                }
            }
            else
            {
                // If the current status is not Inoperable
                if (scienceCount > 0)
                {
                    // Set to Inoperable if science count is zero
                    //Debug.Log("[STNSCI-EXP] Status is not Inoperable and Science Count is zero. Setting status to Inoperable.");
                    Inoperable = true;
                    SetStatus(Status.Storage); // Set status to Inoperable
                    Events[nameof(StartExperiment)].active = false; // Disable start experiment action

                }
                else
                {
                    // Science count is greater than zero
                    //Debug.Log("[STNSCI-EXP] Status is not Inoperable and Science Count is greater than zero. Setting status to Idle.");
                    Inoperable = false; // Ensure it is operable
                    SetStatus(Status.Inoperable); // Set status to Idle
                }
            }

            

            // Log final state after updates
            //Debug.Log($"[STNSCI-EXP] Final Status: {currentStatus}, Inoperable Flag: {Inoperable}");
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "#autoLOC_StatSci_startExp", active = true)]
        public void StartExperiment()
        {
            if (currentStatus != Status.Idle)
            {
                Debug.Log($"[STNSCI-EXP] Cannot Start Experiment because we are not idle currentStatus = {currentStatus}"); //This should'nt ever happen, its just in case there is an error in the code which is showing the StartExp button in wrong state.
                return;
            }

            if (CheckBoring(vessel, true)) // Check if the experiment is boring; if it is, remain idle (return false) and disable the Start Experiment button.
            {
                Debug.Log("[STNSCI-EXP] Cannot start Experiment here!");
                return;

            }

            SetStatus(Status.Running); //When button is pressed the Staus of experiment will change to "Running" as long as the 2 checks above don't apply.

        }

        public void FinishExperiment()
        {

            // Transition the experiment status to "Finished" after the requirements have been met.
            SetStatus(Status.Finished);

            PopUpMessage($"{part.partInfo.title} has completed");
            Debug.Log($"[STNSCI-EXP] {part.partInfo.title} has completed");

            // Disable the "Start Experiment" button since the experiment is now completed.
            Events[nameof(StartExperiment)].active = false;

            // Enable the "Deploy Experiment" button, allowing the player to deploy the results or store them.
            Events[nameof(DeployExperiment)].active = true;
            Events[nameof(DeployExperiment)].guiName = "#autoLOC_statsci_finishExp";

            // The following two lines ensure the game engine refreshes the vessel's state and updates the UI accordingly.
            vessel.GoOffRails();
            vessel.GoOnRails();
        }

        private void OnIdleExit()
        {
            // Log the transition for debugging purposes
            Debug.Log($"[STNSCI-EXP] Exiting Idle state for {part.partInfo.title}");

            PopUpMessage("#autoLOC_StatSci_screen_started"); // Pop-up message "Started experiment!"

            // Successfully exited idle, so disable the StartExperiment event and return true
            Events[nameof(StartExperiment)].active = false;

            // Update the experiment status to Running
            currentStatus = Status.Running;

        }
        private void OnRunningEnter()
        {
            // Add default requirements for the experiment.
            // This could involve setting up initial conditions or constraints.
            AddDefaultRequirements();

            // Temporarily take the vessel "off rails".
            // This is often done to ensure that changes to the vessel's state or status are processed correctly.
            vessel.GoOffRails();

            // Return the vessel "on rails" to refresh the UI.
            // This ensures that the vessel's state is updated in the game's UI after being taken off rails.
            vessel.GoOnRails();

            // Iterate over each requirement in the requirements collection.
            foreach (var r in requirements)
            {
                // Debug line to output information about the current requirement.
                //Debug.Log($"[STNSCI-EXP] Processing requirement: Resource Name = {r.Value.Name}, Amount = {r.Value.Amount}");

                // Set or update the maximum amount of the specified resource for each requirement.
                // The resource is identified by its name, and the maximum amount is set as specified.
                SetResourceMaxAmount(r.Value.Name, r.Value.Amount);

                // Debug line to confirm the resource has been updated.
                //Debug.Log($"[STNSCI-EXP] Resource '{r.Value.Name}' max amount set to {r.Value.Amount}");

            }
        }

        public void OnEnterStorage()
        {
            // Ensure the experiment is in a valid state before transitioning to Storage.
            if (currentStatus == Status.Storage)
            {
                Debug.Log("[STNSCI-EXP] Experiment is already in Storage status.");
                return; // No action needed if already in Storage.
            }

            // Transition the experiment status to 'Storage'.
            SetStatus(Status.Storage);

            // Mark the experiment as deployed. This might be necessary based on the Storage logic. This required to trigger Stock Science UI!
            Deployed = true;

            // Disable the StartExperiment event as the experiment should not be started from this state.
            Events[nameof(StartExperiment)].active = false;

            // Optionally, disable the DeployExperiment event if it should not be active in Storage.
            // Uncomment if necessary based on your application’s logic.
            Events[nameof(DeployExperiment)].active = false;

            // Remove all resource requirements as they are no longer relevant in Storage.
            RemoveAllReqs();

            // Check if the experiment is rerunnable, and if not, set it to inoperable
            if (!rerunnable)
            {
                SetStatus(Status.Inoperable);
                Debug.Log($"[STNSCI-EXP] {part.partInfo.title} is now inoperable after storage.");
            }

            // Log the transition for debugging and record-keeping.
            //Debug.Log($"[STNSCI-EXP] Experiment {part.partInfo.title} has entered Storage state and all requirements have been removed.");

            // Temporarily take the vessel "off rails".
            // This is often done to ensure that changes to the vessel's state or status are processed correctly.
            vessel.GoOffRails();

            // Return the vessel "on rails" to refresh the UI.
            // This ensures that the vessel's state is updated in the game's UI after being taken off rails.
            vessel.GoOnRails();
        }

        // Define the new method to calculate the requirement "kuarq" decay
        public void UpdateKuarqDecay()
        {
            // Check if requirements contain "KUARQS"
            bool hasKuarqs = requirements.ContainsKey(KUARQS);

            if (hasKuarqs)
            {
                // Update the visibility of kuarqDecay
                Fields[nameof(kuarqDecay)].guiActive = true;

                // Perform decay calculation if halflife is greater than 0
                if (kuarqHalflife > 0)
                {
                    var kuarqs = GetResource(KUARQS);
                    float kuarqsRequired = requirements[KUARQS].Amount;

                    // If the Kuarqs amount is equal or less than 99% of the required amount, continue decay calculations
                    if (kuarqs != null && kuarqs.amount <= (.99 * kuarqsRequired))
                    {
                        // Calculate decay
                        double decay = Math.Pow(.5, TimeWarp.fixedDeltaTime / kuarqHalflife);
                        kuarqDecay = (float)((kuarqs.amount * (1 - decay)) / TimeWarp.fixedDeltaTime) * 0.1f;
                        kuarqs.amount *= decay;
                    }
                    else
                    {
                        // If the requirement is met, hide the kuarqDecay field
                        Fields[nameof(kuarqDecay)].guiActive = false;
                        kuarqDecay = 0;
                    }
                }
                else
                {
                    kuarqDecay = 0;
                }
            }
            else
            {
                // Hide the kuarqDecay field if requirements do not contain "KUARQS"
                Fields[nameof(kuarqDecay)].guiActive = false;
                kuarqDecay = 0;
            }
        }

        //This acts as a shortcut method to making pop-up messages on screen.
        protected static void PopUpMessage(string message)
        {
            ScreenMessages.PostScreenMessage(message, 6, ScreenMessageStyle.UPPER_CENTER);
        }

        // Method to check if the required parts are present on the vessel
        private bool CheckRequiredParts()
        {
            // Assuming requiredParts is a class-level or accessible field
            if (string.IsNullOrEmpty(requiredParts))
                return true;

            // Split the requiredParts string into individual part names
            var partNames = requiredParts.Split(',').Select(name => name.Trim()).ToHashSet();

            // Get a HashSet of part names currently on the vessel
            var partsOnVessel = vessel.parts.Select(p => p.partInfo.title).ToHashSet();

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


        // Generates a formatted string containing information about the requirements,
        // including additional details about specific requirements like KUARQS and BIOPRODUCTS to be placed in the part menu in the VAB or SPH.
        public string GenerateRequirementInfo(Dictionary<string, Requirement> requirements, double kuarqHalflife, double partMass)
        {
            // Initialize the return string and placeholders for additional requirement details
            string ret = "";
            string reqLab = "", reqCyclo = "", reqZoo = "", reqSol = "";

            // Check if the required parts are present on the vessel
            if (!CheckRequiredParts())
            {
                // If required parts are missing, return a message indicating this
                return Localizer.Format("#autoLOC_StatSci_screen_missing_parts");
            }

            // Iterate over each requirement in the dictionary
            foreach (var r in requirements)
            {
                // If ret is not empty, append a new line to separate entries
                if (ret != "") ret += "\n";

                // Append the requirement name and amount to the return string
                ret += r.Value.Name + " " + Localizer.Format("#autoLOC_StatSci_Req", r.Value.Amount);

                // Debug output to ensure switch statement is entered
                Debug.Log($"[STNSCI-EXP] Processing Requirement: {r.Value.Name}");

                // Handle special requirements that need additional information or formatting
                switch (r.Value.Name)
                {
                    case "Eurekas":
                        // For EUREKAS, specify the lab requirement
                        reqLab = Localizer.Format("#autoLOC_StatSci_LabReq");
                        break;

                    case "Kuarqs":
                        // For KUARQS, generate and append specific info including cyclical requirements
                        ret += GenerateKuarqsInfo(r.Value.Amount, kuarqHalflife, ref reqCyclo);
                        break;

                    case "Bioproducts":
                        // For BIOPRODUCTS, generate and append specific info including zoo requirements
                        ret += GenerateBioproductsInfo(r.Value.Amount, partMass, ref reqZoo);
                        break;

                    case "Solutions":
                        // For SOLUTIONS, specify the lab requirement
                        reqSol = Localizer.Format("#autoLOC_StatSci_SolReq");
                        break;

                    default:
                        Debug.Log($"[STNSCI-EXP] Unhandled Requirement: {r.Value.Name}");
                        break;
                }
            }

            // Return the full requirement info, including any additional details appended from specific requirements
            string finalResult = ret + reqLab + reqCyclo + reqZoo + reqSol + "\n\n";
            Debug.Log($"[STNSCI-EXP] Final Result: {finalResult}");
            return finalResult;
        }

        // Generates specific information related to KUARQS, including their half-life and the 
        // required production, and updates the Cyclotron requirement reference.
        private string GenerateKuarqsInfo(double kuarqsAmount, double kuarqHalflife, ref string reqCyclo)
        {
            string result = "";
            double productionRequired = 0.01; // Default value for production requirement

            // If the half-life of KUARQS is greater than 0, calculate the required production
            if (kuarqHalflife > 0)
            {
                result += "\n" + Localizer.Format("#autoLOC_StatSci_KuarkHalf", kuarqHalflife);

                // Calculate the production required based on the half-life formula
                productionRequired = kuarqsAmount * (1 - Math.Pow(0.5, 1.0 / kuarqHalflife));
                result += "\n" + Localizer.Format("#autoLOC_StatSci_KuarkProd", productionRequired.ToString("F3"));
            }

            // Determine the Cyclotron requirement based on the calculated production
            reqCyclo = productionRequired > 1
                ? Localizer.Format("#autoLOC_StatSci_CycReqM", Math.Ceiling(productionRequired))
                : Localizer.Format("#autoLOC_StatSci_CycReq");

            return result; // Return the generated KUARQS info
        }

        // Generates specific information related to BIOPRODUCTS, including their total mass,
        // and updates the Zoology Bay requirement reference.
        private string GenerateBioproductsInfo(double bioproductsAmount, double partMass, ref string reqZoo)
        {
            string result = "";

            // Retrieve the density of BIOPRODUCTS from a resource helper
            double bioproductDensity = ResourceHelper.getResourceDensity("BIOPRODUCTS");

            // If the density is valid, calculate the total biomass
            if (bioproductDensity > 0)
            {
                double bioMass = Math.Round(bioproductsAmount * bioproductDensity + partMass, 2);
                result += Localizer.Format("#autoLOC_StatSci_BioMass", bioMass);
            }

            // Set the zoo requirement string
            reqZoo = Localizer.Format("#autoLOC_StatSci_ZooReq");
            return result; // Return the generated BIOPRODUCTS info
        }
    }
}