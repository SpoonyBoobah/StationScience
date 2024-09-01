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
        // Your custom status field
        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "researchStatus", groupName = "StationScience", groupDisplayName = "Research")]
        public string researchStatus = "Inactive";

        // Field to manage the lights mode: 0 = off, 1 = auto, 2 = on
        [KSPField(isPersistant = true)]
        public int lightsMode = 1;

        // Field to specify the required skills for the module to function properly
        [KSPField]
        public string requiredSkills = "NA"; // Skills required for the module to function properly

        // Field for experience bonus multiplier
        [KSPField]
        public double experienceBonus = 0.5;

        private float lastCheck = 0; // Last time the skill check was performed
        private bool actuallyProducing = false; // Whether the module is actively producing resources
        //private ConversionRecipe lastRecipe = null; // Last conversion recipe used
        private ModuleAnimateGeneric animator = null; // Animation module for controlling lights
        public IEnumerable<string> skills; // Parsed skills from the requiredSkills string
        private string lastStatus = string.Empty;  // Track the last status to avoid unnecessary updates

        /*
        // Fields for Kibbal-to-Bioproduct conversion
        [KSPField(isPersistant = true)]
        public float kibbalConsumptionRate = 1.0f; // Base rate at which Kibbal is consumed

        [KSPField(isPersistant = true)]
        public float bioproductConversionRate = 0.5f; // Conversion rate from Kibbal to Bioproducts

        // Field to track the status of Kibbal consumption increase
        //[KSPField(guiActive = true, guiName = "Kibbal Consumption Status", groupName = "StationScience", groupDisplayName = "Research", groupStartCollapsed = false)]
        //public string kibbalConsumptionStatus = "Normal"; // Default status is "Normal"
        */

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
                }
                // If the vessel is on the home planet, stop the converter
                else if (StationExperiment.CheckBoring(vessel, false))
                {
                    StopResourceConverter();
                    this.status = "Inactive; on home planet";
                }
                else
                {
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
            }

            // Call base class to handle standard pre-processing tasks
            base.PreProcessing();
        }

        /*
        // Prepare the conversion recipe for Kibbal to Bioproducts conversion
        protected override ConversionRecipe PrepareRecipe(double deltaTime)
        {
            var recipe = new ConversionRecipe();

            // Add Kibbal consumption and Bioproduct production to the recipe
            recipe.Inputs.Add(new ResourceRatio { ResourceName = "Kibbal", Ratio = kibbalConsumptionRate, FlowMode = ResourceFlowMode.ALL_VESSEL });
            recipe.Outputs.Add(new ResourceRatio { ResourceName = "Bioproducts", Ratio = kibbalConsumptionRate * bioproductConversionRate, FlowMode = ResourceFlowMode.ALL_VESSEL });

            // Call the base class's PrepareRecipe to handle any other resource conversions
            lastRecipe = base.PrepareRecipe(deltaTime);

            return recipe; // Return the prepared recipe
        }
        */

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

        /*
        // Button to increase the Kibbal consumption rate and Bioproduct conversion rate
        [KSPEvent(guiName = "Increase Kibbal Consumption", active = true, guiActive = true)]
        public void IncreaseKibbalConsumption()
        {
            // Ensure this action only happens when the part is the "StnSciZoo"
            if (this.part.name == "StnSciZoo")
            {
                kibbalConsumptionRate = 1.0f; // Set to normal consumption rate
                bioproductConversionRate = 0.5f; // Set to normal conversion rate

                // Update the status field
                kibbalConsumptionStatus = "Normal"; // Change status to "Normal"
            }
            else
            {
                kibbalConsumptionRate += 0.5f; // Increase Kibbal consumption rate
                bioproductConversionRate += 0.25f; // Increase Bioproduct conversion rate

                kibbalConsumptionStatus = "Increased"; // Change status to "Increased"
            }
        }
        */

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

            // Don't do anything if we're in the editor
            if (state == StartState.Editor)
                return;

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
                        field.guiActive = false; // Hide animator fields
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
            UpdateLights(); // Apply the current lights mode
        }

        // Event to change the lights mode between off, auto, and on
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
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
                        }
                    }
                }

        }

        private void UpdateCustomStatus()
        {
            // Access the base class's status field
            BaseField baseStatusField = Fields["status"];

            // Get the current value of the base status
            string currentBaseStatus = baseStatusField.GetValue(this).ToString();

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
    }
}
                                                                                                