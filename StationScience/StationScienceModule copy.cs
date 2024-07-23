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
    /// <summary>
    /// This module represents a science module for a space station.
    /// It handles skill checks, efficiency bonuses, and lights animation.
    /// </summary>
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

        /// <summary>
        /// Checks if the crew has the required skills.
        /// </summary>
        /// <returns>True if at least one crew member has the required skills, otherwise false.</returns>
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

        /// <summary>
        /// Pre-processes the module before conversion.
        /// </summary>
        protected override void PreProcessing()
        {
            float curTime = UnityEngine.Time.realtimeSinceStartup;

            // Only check skills periodically
            if (IsActivated && (curTime - lastCheck > 0.1))
            {
                lastCheck = curTime;

                // Check if the module should be active
                if (!CheckSkill())
                {
                    StopResourceConverter();
                    this.status = "Inactive; no " + requiredSkills;
                }
                else if (StationExperiment.CheckBoring(vessel, false))
                {
                    StopResourceConverter();
                    this.status = "Inactive; on home planet";
                }
                else
                {
                    // Calculate efficiency bonus based on crew skills and experience
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
                    SetEfficiencyBonus((float)Math.Max(numScienceCrew + totalExperience * experienceBonus, 1.0));
                }
            }
            base.PreProcessing();
        }

        /// <summary>
        /// Updates the lights based on the current lights mode and activation state.
        /// </summary>
        private void UpdateLights()
        {
            if (animator != null)
            {
                bool animActive = this.IsActivated && actuallyProducing;
                animActive = lightsMode switch
                {
                    2 => true, // Force on
                    0 => false, // Force off
                    _ => animActive // Auto mode
                };

                // Toggle animation based on the active state
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

        /// <summary>
        /// Prepares the recipe for conversion.
        /// </summary>
        /// <param name="deltaTime">Time since the last update.</param>
        /// <returns>The prepared conversion recipe.</returns>
        protected override ConversionRecipe PrepareRecipe(double deltaTime)
        {
            lastRecipe = base.PrepareRecipe(deltaTime);
            return lastRecipe;
        }

        /// <summary>
        /// Post-processes the results of the conversion.
        /// </summary>
        /// <param name="result">The results of the conversion.</param>
        /// <param name="deltaTime">Time since the last update.</param>
        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            actuallyProducing = (result.TimeFactor > 0);
            if (lightsMode == 1)
                UpdateLights();
        }

        /// <summary>
        /// Called when the module is started.
        /// </summary>
        /// <param name="state">The state in which the module is started.</param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (state == StartState.Editor)
                return;

            // Force activate the part
            this.part.force_activate();

            // Find and configure the animator module
            animator = this.part.FindModulesImplementing<ModuleAnimateGeneric>().FirstOrDefault();
            if (animator != null)
            {
                foreach (var field in animator.Fields)
                {
                    if (field != null)
                        field.guiActive = false;
                }
            }

            UpdateLightsMode();
        }

        /// <summary>
        /// Updates the lights mode based on the current setting.
        /// </summary>
        private void UpdateLightsMode()
        {
            string lightsModeName = lightsMode switch
            {
                0 => Localizer.Format("#autoLOC_StatSci_LightsOff"),
                1 => Localizer.Format("#autoLOC_StatSci_LightsAuto"),
                2 => Localizer.Format("#autoLOC_StatSci_LightsOn"),
                _ => Localizer.Format("#autoLOC_StatSci_LightsAuto")
            };
            Events["LightsMode"].guiName = lightsModeName;
            UpdateLights();
        }

        /// <summary>
        /// Toggles the lights mode between off, auto, and on.
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
        public void LightsMode()
        {
            lightsMode = (lightsMode + 1) % 3;
            UpdateLightsMode();
        }

        /// <summary>
        /// Provides information about the module.
        /// </summary>
        /// <returns>A string describing the module's functionality.</returns>
        public override string GetInfo()
        {
            string info = base.GetInfo();
            if (!string.IsNullOrEmpty(requiredSkills) && requiredSkills != "NA")
            {
                info += Localizer.Format("#autoLOC_StatSci_skillReq", requiredSkills);
            }
            return info;
        }
    }
}
