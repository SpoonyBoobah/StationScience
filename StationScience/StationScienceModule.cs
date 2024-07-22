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
    public class StationScienceModule : ModuleResourceConverter
    {
        [KSPField(isPersistant = true)]
        public int lightsMode = 1; // 0: force off; 1: auto; 2: force on

        [KSPField]
        public string requiredSkills = "NA";

        public IEnumerable<string> skills;

        [KSPField]
        public double experienceBonus = 0.5;

        // Check if any crew member has the required skills for this module to operate
        public bool CheckSkill()
        {
            if (string.IsNullOrEmpty(requiredSkills) || requiredSkills == "NA")
                return true;

            if (skills == null)
            {
                skills = requiredSkills.Split(',').Select(s => s.Trim());
            }

            foreach (var crew in part.protoModuleCrew)
            {
                foreach (string skill in skills)
                {
                    if (crew.HasEffect(skill))
                        return true;
                }
            }

            return false;
        }

        private float lastCheck = 0;

        protected override void PreProcessing()
        {
            float curTime;
            if (IsActivated && (curTime = UnityEngine.Time.realtimeSinceStartup) - lastCheck > 0.1)
            {
                lastCheck = curTime;

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
                    int nsci = 0;
                    int nstars = 0;
                    foreach (var crew in part.protoModuleCrew)
                    {
                        foreach (string skill in skills)
                        {
                            if (crew.HasEffect(skill))
                            {
                                nsci += 1;
                                nstars += crew.experienceLevel;
                            }
                        }
                    }
                    SetEfficiencyBonus((float)Math.Max(nsci + nstars * experienceBonus, 1.0));
                }
            }

            base.PreProcessing();
        }

        // Update the status and experiment progress based on the global data
        private void UpdateExperimentProgress()
        {
            // Example experiment name, you might need to replace this with actual experiment identifiers
            string experimentName = "SampleExperiment";
            
            // Retrieve the current experiment progress from global data
            var progress = GlobalData.GetExperimentProgress(experimentName);

            if (progress != null)
            {
                // Use the progress information (e.g., update UI or internal state)
                this.status = $"Experiment '{progress.experimentName}' progress: {progress.progress * 100}%";
            }
            else
            {
                // Handle case where experiment progress is not found
                this.status = $"Experiment '{experimentName}' not found.";
            }
        }

        private V GetOrDefault<K, V>(Dictionary<K, V> dict, K key)
        {
            try
            {
                return dict[key];
            }
            catch (KeyNotFoundException)
            {
                return default(V);
            }
        }

        protected override ConversionRecipe PrepareRecipe(double deltatime)
        {
            // Example: Update experiment progress when preparing the recipe
            UpdateExperimentProgress();

            return base.PrepareRecipe(deltatime);
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);

            if (lightsMode == 1)
                UpdateLights();

            // Example: Save or update the experiment progress based on the results
            string experimentName = "SampleExperiment";
            double progress = CalculateProgress(result, deltaTime); // Implement your progress calculation logic
            GlobalData.SaveExperimentProgress(experimentName, progress);
        }

        private void UpdateLights()
        {
            if (animator != null)
            {
                bool animActive = this.IsActivated && actuallyProducing;
                if (lightsMode == 2) animActive = true;
                if (lightsMode == 0) animActive = false;
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

        private ModuleAnimateGeneric animator = null;

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            var animators = this.part.FindModulesImplementing<ModuleAnimateGeneric>();
            if (animators != null && animators.Count >= 1)
            {
                this.animator = animators[0];
                foreach (var field in animator.Fields)
                {
                    if (field != null)
                        field.guiActive = false;
                }
            }
            if (state == StartState.Editor) { return; }
            this.part.force_activate();

            UpdateLightsMode();
        }

        string lightsOn = Localizer.Format("#autoLOC_StatSci_LightsOn");
        string lightsOff = Localizer.Format("#autoLOC_StatSci_LightsOff");
        string lightsAuto = Localizer.Format("#autoLOC_StatSci_LightsAuto");

        public void UpdateLightsMode()
        {
            switch (lightsMode)
            {
                case 0:
                    Events["LightsMode"].guiName = lightsOff;
                    break;
                case 1:
                    Events["LightsMode"].guiName = lightsAuto;
                    break;
                case 2:
                    Events["LightsMode"].guiName = lightsOn;
                    break;
            }
            UpdateLights();
        }

        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
        public void LightsMode()
        {
            lightsMode += 1;
            if (lightsMode > 2)
                lightsMode = 0;
            UpdateLightsMode();
        }

        public override string GetInfo()
        {
            string ret = base.GetInfo();
            if (requiredSkills != "" && requiredSkills != "NA")
            {
                ret += Localizer.Format("#autoLOC_StatSci_skillReq", requiredSkills);
            }
            return ret;
        }

        // Implement your logic for calculating progress here
        private double CalculateProgress(ConverterResults result, double deltaTime)
        {
            // Example implementation, replace with actual progress calculation
            return result.TimeFactor * deltaTime;
        }
    }
}
