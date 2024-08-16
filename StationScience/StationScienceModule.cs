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

namespace StationScience
{
    // This module represents a science module for a space station.
    // It handles skill checks, efficiency bonuses, and lights animation.
    public class StationScienceModule : ModuleResourceConverter
    {
        // Determines the mode of the lights: 0 = off, 1 = auto, 2 = on
        [KSPField(isPersistant = true)]
        public int lightsMode = 1;

        // Required skills for the module to function properly
        [KSPField]
        public string requiredSkills = "NA";

        // Skills parsed from the requiredSkills string
        public IEnumerable<string> skills;

        // Bonus multiplier for experience
        [KSPField]
        public double experienceBonus = 0.5;

        // Time of the last skill check
        private float lastCheck = 0;

        // Flag to determine if the module is actively producing resources
        private bool actuallyProducing = false;

        // Last recipe used for conversion
        private ConversionRecipe lastRecipe = null;

        // Animator module for controlling lights
        private ModuleAnimateGeneric animator = null;

        // Checks if the crew has the required skills.
        // Returns true if at least one crew member has the required skills, otherwise false.
        public bool CheckSkill()
        {
            // If no specific skills are required, return true
            if (string.IsNullOrEmpty(requiredSkills) || requiredSkills == "NA")
                return true;

            // Parse skills if not already parsed
            if (skills == null)
            {
                skills = requiredSkills.Split(',').Select(s => s.Trim());
            }

            // Check each crew member for required skills
            foreach (var crew in part.protoModuleCrew)
            {
                if (skills.Any(skill => crew.HasEffect(skill)))
                    return true;
            }
            return false;
        }

        // Pre-processes the module before conversion.
        protected override void PreProcessing()
        {
            float curTime = UnityEngine.Time.realtimeSinceStartup;

            // Only check skills periodically to avoid constant checking
            if (IsActivated && (curTime - lastCheck > 0.1))
            {
                lastCheck = curTime;

                // Check if the module should be active based on crew skills and other conditions
                if (!CheckSkill())
                {
                    // Stop the conversion if the required skills are not present
                    StopResourceConverter();
                    this.status = "Inactive; no " + requiredSkills;
                }
                else if (StationExperiment.CheckBoring(vessel, false))
                {
                    // Stop the conversion if the vessel is on the home planet (e.g., Kerbin)
                    StopResourceConverter();
                    this.status = "Inactive; on home planet";
                }
                else
                {
                    // Calculate efficiency bonus based on crew skills and experience
                    int numScienceCrew = 0;
                    int totalExperience = 0;

                    // Iterate through the crew members to count those with the required skills
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
                    // Set the efficiency bonus based on the number of skilled crew and their experience
                    SetEfficiencyBonus((float)Math.Max(numScienceCrew + totalExperience * experienceBonus, 1.0));
                }
            }
            // Call the base class's PreProcessing method to ensure standard behavior
            base.PreProcessing();
        }

        // Updates the lights based on the current lights mode and activation state.
        private void UpdateLights()
        {
            if (animator != null)
            {
                // Determine if the lights should be active based on the lights mode and production state
                bool animActive = this.IsActivated && actuallyProducing;
                animActive = lightsMode switch
                {
                    2 => true,  // Force on
                    0 => false, // Force off
                    _ => animActive // Auto mode
                };

                // Toggle the light animation based on the active state
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

        // Prepares the recipe for conversion.
        // Returns the prepared conversion recipe.
        protected override ConversionRecipe PrepareRecipe(double deltaTime)
        {
            // Call the base class's method to prepare the conversion recipe
            lastRecipe = base.PrepareRecipe(deltaTime);
            return lastRecipe;
        }

        // Post-processes the results of the conversion.
        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            // Call the base class's PostProcess method to handle the conversion result
            base.PostProcess(result, deltaTime);

            // Update the actuallyProducing flag based on the conversion result
            actuallyProducing = (result.TimeFactor > 0);

            // Update the lights if the mode is set to automatic
            if (lightsMode == 1)
                UpdateLights();
        }

        // Called when the module is started.
        public override void OnStart(StartState state)
        {
            // Call the base class's OnStart method to handle initial setup
            base.OnStart(state);

            // If the module is started in the editor, exit the method early
            if (state == StartState.Editor)
                return;

            // Force activate the part so it starts processing resources immediately
            this.part.force_activate();

            // Find and configure the animator module for controlling lights
            animator = this.part.FindModulesImplementing<ModuleAnimateGeneric>().FirstOrDefault();
            if (animator != null)
            {
                // Disable the manual control of the animator through the GUI
                foreach (var field in animator.Fields)
                {
                    if (field != null)
                        field.guiActive = false;
                }
            }

            // Update the lights mode based on the current setting
            UpdateLightsMode();
        }

        // Updates the lights mode based on the current setting.
        private void UpdateLightsMode()
        {
            // Set the name of the lights mode based on the current setting
            string lightsModeName = lightsMode switch
            {
                0 => Localizer.Format("#autoLOC_StatSci_LightsOff"),   // Lights off
                1 => Localizer.Format("#autoLOC_StatSci_LightsAuto"),  // Lights automatic
                2 => Localizer.Format("#autoLOC_StatSci_LightsOn"),    // Lights on
                _ => Localizer.Format("#autoLOC_StatSci_LightsAuto")   // Default to automatic
            };

            // Update the GUI name for the LightsMode event to reflect the current setting
            Events["LightsMode"].guiName = lightsModeName;
            UpdateLights();
        }

        // Toggles the lights mode between off, auto, and on.
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
        public void LightsMode()
        {
            // Cycle through the lights mode (off, auto, on)
            lightsMode = (lightsMode + 1) % 3;
            UpdateLightsMode();
        }

        // Provides information about the module.
        // Returns a string describing the module's functionality.
        public override string GetInfo()
        {
            // Get the basic info from the base class
            string info = base.GetInfo();

            // Add information about the required skills if applicable
            if (!string.IsNullOrEmpty(requiredSkills) && requiredSkills != "NA")
            {
                info += Localizer.Format("#autoLOC_StatSci_skillReq", requiredSkills);
            }
            return info;
        }
    }
}
