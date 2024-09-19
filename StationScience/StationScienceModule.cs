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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StationScience
{
    public class StationScienceModule : ModuleResourceConverter
    {
        // Displays the research status in the UI
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "researchStatus", groupName = "StationScience", groupDisplayName = "Research")]
        public string researchStatus = "Inactive";

        // Manages light modes: 0 = off, 1 = auto, 2 = on
        [KSPField(isPersistant = true)]
        public int lightsMode = 1;

        // Required skills for the module to operate
        [KSPField]
        public string requiredSkills = "NA";

        // Experience bonus multiplier for skilled crew
        [KSPField]
        public double experienceBonus = 0.5;

        // Time tracker for skill checks
        private float lastCheck = 0;

        // Flag to indicate if resources are being produced
        private bool actuallyProducing = false;

        // Animator module to control light animations
        private ModuleAnimateGeneric animator = null;

        // Tracks the required skills as a collection
        public IEnumerable<string> skills;

        // Tracks the last known status to avoid unnecessary updates
        private string lastStatus = string.Empty;

        // Checks if the crew has the required skills to operate the module
        public bool CheckSkill()
        {
            // If no specific skills are required, return true
            if (string.IsNullOrEmpty(requiredSkills) || requiredSkills == "NA")
                return true;

            // Parse skills if not already done
            if (skills == null)
            {
                skills = requiredSkills.Split(',').Select(s => s.Trim());
            }

            // Check if any crew member has the required skill
            foreach (var crew in part.protoModuleCrew)
            {
                if (skills.Any(skill => crew.HasEffect(skill)))
                    return true; // Found a crew member with the required skill
            }
            return false; // No crew member has the required skill
        }

        // Pre-processing logic before resource conversion takes place
        protected override void PreProcessing()
        {
            // Perform skill check periodically to avoid constant checks
            float curTime = UnityEngine.Time.realtimeSinceStartup;
            if (IsActivated && (curTime - lastCheck > 0.1f))
            {
                lastCheck = curTime;

                // If the crew doesn't have the required skills, stop the converter
                if (!CheckSkill())
                {
                    StopResourceConverter();
                    this.status = "Inactive; no " + requiredSkills;
                    return; // Exit early if skill check fails
                }

                // If the vessel is on the home planet, stop the converter
                if (StationExperiment.CheckBoring(vessel, false))
                {
                    StopResourceConverter();
                    this.status = "Inactive; on home planet";
                    return; // Exit early if on home planet
                }

                // Stop if no storage is available for the output resources
                if (!CheckOutputResourceStorage())
                {
                    StopResourceConverter();
                    return; // Exit early if no storage for output resources is available
                }

                // Calculate efficiency bonus based on crew's science skills and experience levels
                int numScienceCrew = 0;
                int totalExperience = 0;

                foreach (var crew in part.protoModuleCrew)
                {
                    foreach (var skill in skills)
                    {
                        if (crew.HasEffect(skill))
                        {
                            numScienceCrew += 1;
                            totalExperience += crew.experienceLevel;
                        }
                    }
                }

                // Set the efficiency bonus based on the number of skilled crew members and their experience
                SetEfficiencyBonus((float)Math.Max(numScienceCrew + totalExperience * experienceBonus, 1.0f));
            }

            // Call base class to handle standard pre-processing tasks
            base.PreProcessing();
        }

        // Post-processing after resource conversion is done
        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            // Call the base class's PostProcess to handle standard post-processing tasks
            base.PostProcess(result, deltaTime);

            // Determine if the module is actually producing resources based on the result
            actuallyProducing = (result.TimeFactor > 0);

            // Update lights automatically if the lights mode is set to "auto"
            if (lightsMode == 1)
                UpdateLights();
        }

        // Updates the lights based on the current state of production and lights mode
        private void UpdateLights()
        {
            if (animator != null)
            {
                // Determine if the lights should be active based on lights mode and production status
                bool animActive = this.IsActivated && actuallyProducing;
                animActive = lightsMode switch
                {
                    2 => true,  // Lights always on
                    0 => false, // Lights always off
                    _ => animActive // Auto mode: lights based on production
                };

                // Toggle lights on or off based on the current animation state
                if (animActive && animator.Progress == 0 && animator.status.StartsWith("Locked", StringComparison.OrdinalIgnoreCase))
                {
                    animator.allowManualControl = true;
                    animator.Toggle();
                    animator.allowManualControl = false;
                }
                else if (!animActive && animator.Progress == 1 && animator.status.StartsWith("Locked", StringComparison.OrdinalIgnoreCase))
                {
                    animator.allowManualControl = true;
                    animator.Toggle();
                    animator.allowManualControl = false;
                }
            }
        }

        // Called when the module is started (in the game)
        public override void OnStart(StartState state)
        {
            base.OnStart(state);


            // Skip further execution if in the editor
            if (state == StartState.Editor)
            {
                HideFieldsAndEventsInEditor();
                
            }

            // Force the part to activate and start processing resources immediately
            this.part.force_activate();

            // Find the existing ModuleOverheatDisplay instance
            overheatDisplay = this.part.FindModuleImplementing<ModuleOverheatDisplay>();
            if (overheatDisplay != null)
            {
                // Modify the overheatDisplay fields here
                UpdateHeatDisplayFields();
            }

            // Find the animator module and disable manual control
            animator = this.part.FindModulesImplementing<ModuleAnimateGeneric>().FirstOrDefault();
            if (animator != null)
            {
                foreach (var field in animator.Fields)
                {
                    if (field != null)
                        field.guiActive = false; // Hide fields in the UI
                        field.guiActiveEditor = false; // Hide fields in the Editor UI
                }

                foreach (var events in animator.Events)
                {
                    if (events != null)
                        events.guiActive = false; // Hide animator events
                        events.guiActiveEditor = false; // Hide events in the Editor UI
                }
            }

            // Update the lights mode to reflect the current setting
            UpdateLightsMode();

            // Copy the value of the base class's status field to the custom status field
            BaseField baseStatusField = Fields["status"];  // Access the base class's field
            researchStatus = baseStatusField.GetValue(this).ToString();  // Copy its value to the custom status field

            // Access the ConverterName and dynamically set the guiName
            if (!string.IsNullOrEmpty(ConverterName))
            {
                Fields["researchStatus"].guiName = $"{ConverterName}";
            }

            // Hide the base class's status field in PAW
            baseStatusField.guiActive = false;
            baseStatusField.guiActiveEditor = false;

            // Optionally log the value to verify the copy
            Debug.Log($"[STNSCI-MOD] Copied status from base class: {researchStatus}");

        }

        // Continuously update the custom status field based on the base class status
        public override void OnUpdate()
        {

            // Continuously update the custom status field based on the base class status
            UpdateCustomStatus();

        }

        // Updates the lights mode display in the part's right-click menu
        private void UpdateLightsMode()
        {
            // Set the display name of the lights mode event based on the current lightsMode setting
            string lightsModeName = lightsMode switch
            {
                0 => Localizer.Format("#autoLOC_StatSci_LightsOff"),   // Lights off
                1 => Localizer.Format("#autoLOC_StatSci_LightsAuto"),  // Lights automatic
                2 => Localizer.Format("#autoLOC_StatSci_LightsOn"),    // Lights on
                _ => Localizer.Format("#autoLOC_StatSci_LightsAuto")   // Default to automatic
            };

            // Update the right-click menu display
            Events["LightsMode"].guiName = lightsModeName;
            Events["LightsMode"].guiActiveEditor = false;
            UpdateLights(); // Apply the current lights mode
        }

        // Event to change the lights mode between off, auto, and on
        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
        public void LightsMode()
        {
            lightsMode = (lightsMode + 1) % 3; // Cycle through 0, 1, 2
            UpdateLightsMode(); // Update the lights mode display
        }

        // Provides additional information about the module in the part's right-click menu
        public override string GetInfo()
        {
            string info = base.GetInfo();

            // Append the required skills to the info if applicable
            if (!string.IsNullOrEmpty(requiredSkills) && requiredSkills != "NA")
            {
                info += Localizer.Format("#autoLOC_StatSci_skillReq", requiredSkills);
            }

            return info;
        }

        private ModuleOverheatDisplay overheatDisplay; // Correct declaration

        public void UpdateHeatDisplayFields()
        {

            // Check and modify field visibility
            if (overheatDisplay != null)
            {
                var fieldNamesToHide = new[] { "heatDisplay", "coreTempDisplay" }; // Replace with actual field names

                foreach (var field in overheatDisplay.Fields)
                {
                    if (fieldNamesToHide.Contains(field.name))
                    {
                        field.guiActive = false; // Ensure the field remains hidden
                        field.guiActiveEditor = false;
                    }
                }
            }

        }

        // Update the custom status field from the base class status field
        private void UpdateCustomStatus()
        {
            // Try to access the base class's status field
            BaseField baseStatusField = Fields["status"];

            // Check if the baseStatusField is null
            if (baseStatusField == null)
            {
                Debug.LogError("[STNSCI-MOD] Error: 'status' field not found in base class.");
                return; // Exit the method if the field is null
            }

            // Get the current value of the base status field
            object baseStatusValue = baseStatusField.GetValue(this);

            // Check if the base status value is null
            if (baseStatusValue == null)
            {
                Debug.LogError("[STNSCI-MOD] Error: 'status' field value is null.");
                return; // Exit the method if the value is null
            }

            string currentBaseStatus = baseStatusValue.ToString();

            // Only update if the status has changed to avoid unnecessary updates
            if (currentBaseStatus != lastStatus)
            {
                // Update the custom status field with the base class's status
                researchStatus = currentBaseStatus;

                // Optionally update the guiName or any other dynamic property
                Fields["researchStatus"].guiName = $"{ConverterName}";

                // Update the last status tracking
                lastStatus = currentBaseStatus;
            }
        }

        // Check if there is available storage space for the output resource on the vessel
        public bool CheckOutputResourceStorage()
        {
            // Get the output resources from the conversion recipe
            var recipe = GetCurrentRecipe();
            if (recipe == null || recipe.Outputs.Count == 0)
            {
                Debug.LogError("[STNSCI-MOD] Error: No output resources found in the recipe.");
                return true;  // If no output resources, we can continue running
            }

            foreach (var output in recipe.Outputs)
            {
                // Get the resource definition based on the resource name
                var resourceDef = PartResourceLibrary.Instance.GetDefinition(output.ResourceName);
                if (resourceDef == null)
                {
                    Debug.LogError($"[STNSCI-MOD] Error: Resource '{output.ResourceName}' not found.");
                    continue;
                }

                // Check all parts in the vessel for storage capacity for this resource
                double totalAvailableStorage = 0.0;
                double totalMaxStorage = 0.0;

                // Get the connected resource totals for this resource
                part.GetConnectedResourceTotals(resourceDef.id, out totalAvailableStorage, out totalMaxStorage);

                // Log details for debugging
                //Debug.Log($"[STNSCI-MOD] Checking storage for {output.ResourceName}: available = {totalAvailableStorage}, max = {totalMaxStorage}");

                // Check if there is any max storage for this resource (null check included for safety)
                if (totalMaxStorage <= 0 || totalMaxStorage == double.NaN)
                {
                    this.status = $"Inactive; no storage for {output.ResourceName}";
                    Debug.Log($"[STNSCI-MOD] No storage for {output.ResourceName}, stopping converter.");
                    return false;  // Stop the converter if no storage exists or if the value is unexpected
                }

                // Skip checking if storage is full, we only care that storage exists
                // If max storage exists, we don't care about available storage
            }

            return true;  // Storage is available for all output resources
        }

        // Get the current recipe for this converter
        protected ConversionRecipe GetCurrentRecipe()
        {
            // This returns the current recipe; override if you have a custom recipe mechanism
            return base.PrepareRecipe(0);
        }

        // Helper method to hide fields and events in the editor
        private void HideFieldsAndEventsInEditor()
        {
            foreach (var field in Fields)
            {
                if (field != null)
                {
                    field.guiActiveEditor = false; // Hide fields in the editor
                    Debug.Log($"[STNSCI-MOD] Hiding field {field.name} in editor.");
                }
            }

            foreach (var eventBase in Events)
            {
                if (eventBase != null)
                {
                    eventBase.guiActiveEditor = false; // Hide events in the editor
                    Debug.Log($"[STNSCI-MOD] Hiding event {eventBase.name} in editor.");
                }
            }
        }
    }
}
                                                                                                