﻿/*
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

namespace StationScience
{
    public class StationScienceModule : ModuleResourceConverter
    {
        [KSPField(isPersistant = true)]
        public int lightsMode = 1;
        // 0: force off; 1: auto; 2: force on

        [KSPField]
        public string requiredSkills = "NA";

        public IEnumerable<String> skills;

        [KSPField]
        public double experienceBonus = 0.5;

        public bool CheckSkill()
        {
            if (requiredSkills == "" || requiredSkills == "NA")
                return true;
            if (skills == null)
            {
                skills = requiredSkills.Split(',').Select(s => s.Trim());
            }
            foreach (var crew in part.protoModuleCrew)
            {
                foreach (String skill in skills) {
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
                        foreach (String skill in skills)
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

#if false
        private System.Collections.IEnumerator doUpdateStatus()
        {
            while (true)
            {
                updateStatus();
                yield return new UnityEngine.WaitForSeconds(1f);
            }
        }
#endif

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

        private void UpdateStatus()
        {
            UpdateLights();
#if false
            bool animActive = false;
            if (!doResearch)
            {
                displayStatusMessage("Paused");
            }
            else if (minimumCrew > 0 && part.protoModuleCrew.Count < minimumCrew)
            {
                displayStatusMessage("Understaffed (" + part.protoModuleCrew.Count + "/" + minimumCrew + ")");
            }
            else if (StationExperiment.checkBoring(vessel, false))
            {
                displayStatusMessage("Go to space!");
            }
            else
            {
                Fields["researchStatus"].guiActive = false;
                foreach (ResearchGenerator generator in generators)
                {
                    generator.updateStatus();
                    animActive = animActive || (generator.last_time_step != 0);
                }
                /*
                eurekasStatus = "";
                var r = getOrDefault(EurekasGenerator.rates,EUREKAS);
                if (r != null)
                {
                    if (r.last_available == 0)
                        eurekasStatus = "No Experiments";
                    else
                    {
                        eurekasStatus = String.Format("{0:F2} per hour", -r.ratePerHour * r.rateMultiplier);
                        animActive = true;
                    }
                }
                Fields["eurekasStatus"].guiName = EUREKAS;
                Fields["eurekasStatus"].guiActive = (eurekasStatus != "");

                kuarqStatus = "";
                var qr = getOrDefault(KuarqGenerator.rates, KUARQS);
                var cr = getOrDefault(KuarqGenerator.rates, "ElectricCharge");
                if (qr != null)
                {
                    if (qr.last_available == 0)
                        kuarqStatus = "No Experiments";
                    else if (cr != null && cr.last_available < 0.000001)
                        kuarqStatus = "Not Enough Charge";
                    else if (qr.last_produced != 0)
                    {
                        animActive = true;
                        kuarqStatus = String.Format("{0:F2} per second", -qr.ratePerSecond * qr.rateMultiplier);
                    }
                }
                Fields["kuarqStatus"].guiActive = (kuarqStatus != "");

                bioproductsStatus = "";
                var br = getOrDefault(BioproductsGenerator.rates,BIOPRODUCTS);
                var kr = getOrDefault(BioproductsGenerator.rates,"Kibbal");
                if (br != null)
                {
                    if (br.last_available == 0)
                        bioproductsStatus = "No Experiments";
                    else if (kr != null && kr.last_available == 0)
                        bioproductsStatus = "Not Enough Kibbal";
                    else if (br.last_produced != 0)
                    {
                        animActive = true;
                        bioproductsStatus = String.Format("{0:F2} per hour", -br.ratePerHour * br.rateMultiplier);
                    }
                }
                Fields["bioproductsStatus"].guiActive = (bioproductsStatus != "");

                scienceStatus = "";
                var sr = getOrDefault(ScienceGenerator.rates,"__SCIENCE__zoologyBay");
                var skr = getOrDefault(ScienceGenerator.rates,"Kibbal");
                if (skr != null && skr.last_available == 0)
                    scienceStatus = "Hibernating";
                else if(sr != null && sr.last_produced != 0)
                    scienceStatus = String.Format("{0:F2} per day", -sr.ratePerDay * sr.rateMultiplier);
                Fields["scienceStatus"].guiActive = (scienceStatus != "");

                kibbalStatus = "";
                double total_produced = 0, total_rate = 0;
                if (kr != null && kr.last_produced != 0)
                {
                    total_produced += kr.last_produced; total_rate += kr.ratePerDay * kr.rateMultiplier;
                }
                if (skr != null && skr.last_produced != 0)
                {
                    total_produced += skr.last_produced; total_rate += skr.ratePerDay * skr.rateMultiplier;
                }
                if (total_produced != 0)
                    kibbalStatus = String.Format("{0:F2} per day", total_rate);
                Fields["kibbalStatus"].guiActive = (kibbalStatus != "");
                */
            }
            if (animator != null)
            {
                if (lightsMode == 2) animActive = true;
                if (lightsMode == 0) animActive = false;
                if (animActive && animator.Progress == 0 && animator.status.StartsWith("Locked", true, null))
                {
                    animator.allowManualControl = true;
                    animator.Toggle();
                    animator.allowManualControl = false;
                }
                else if (!animActive && animator.Progress == 1 && animator.status.StartsWith("Locked", true, null))
                {
                    animator.allowManualControl = true;
                    animator.Toggle();
                    animator.allowManualControl = false;
                }
            }
#endif
        }

        bool actuallyProducing = false;
        ConversionRecipe lastRecipe = null;

        protected override ConversionRecipe PrepareRecipe(double deltatime)
        {
            lastRecipe = base.PrepareRecipe(deltatime);
            return lastRecipe;
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            actuallyProducing = (result.TimeFactor > 0);
            if(lightsMode == 1)
                UpdateLights();
            if (lastRecipe == null)
                return;
            /*
            foreach (var ratio in lastRecipe.Inputs)
            {
                print(ratio.ResourceName + " output rate: " + ((60 * 60 * ratio.Ratio * result.TimeFactor) / deltaTime) + " per hour");
            }
            foreach (var ratio in lastRecipe.Outputs)
            {
                print(ratio.ResourceName + " output rate: " + ((60 * 60 * ratio.Ratio * result.TimeFactor) / deltaTime) + " per hour");
            }
            */
        }

        void UpdateLights()
        {
            if (animator != null)
            {
                bool animActive = this.IsActivated && actuallyProducing;
                if (lightsMode == 2) animActive = true;
                if (lightsMode == 0) animActive = false;
                if (animActive && animator.Progress == 0 && animator.status.StartsWith("Locked", true, null))
                {
                    animator.allowManualControl = true;
                    animator.Toggle();
                    animator.allowManualControl = false;
                }
                else if (!animActive && animator.Progress == 1 && animator.status.StartsWith("Locked", true, null))
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
                for (int i = 0; i < animator.Fields.Count; i++)
                {
                    if (animator.Fields[i] != null)
                        animator.Fields[i].guiActive = false;
                }
                /*for(int i = 0; i < animator.Actions.Count; i++) {
                    if(animator.Actions[i] != null)
                        animator.Actions[i].active = false;
                }
                for(int i = 0; i < animator.Events.Count; i++) {
                    if (animator.Events[i] != null)
                    {
                        animator.Events[i].guiActive = false;
                        animator.Events[i].active = false;
                    }
                }*/
            }
            if (state == StartState.Editor) { return; }
            this.part.force_activate();

            UpdateLightsMode();
            //StartCoroutine(doUpdateStatus());
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
#if false
            string ret = "";
            if (eurekasPerHour > 0)
                ret += (ret == "" ? "" : "\n") + "Eurekas per hour: " + eurekasPerHour;
            if (kuarqsPerSec > 0) {
                ret += (ret == "" ? "" : "\n") + "Kuarqs per second: " + kuarqsPerSec;
                if(chargePerKuarq > 0)
                    ret += (ret == "" ? "" : "\n") + "Electric Charge per Kuarq: " + chargePerKuarq;
            }
            if (sciPerDay > 0)
                ret += (ret == "" ? "" : "\n") + "Science per day: " + sciPerDay;
            if (kibbalPerDay > 0)
                ret += (ret == "" ? "" : "\n") + "Kibbal per day: " + kibbalPerDay;
            if(bioproductsPerHour > 0) {
                ret += (ret == "" ? "" : "\n") + "Bioproducts per hour: " + bioproductsPerHour;
                if(kibbalPerBioproduct > 0)
                    ret += (ret == "" ? "" : "\n") + "Kibbal per bioproduct: " + kibbalPerBioproduct;
            }
            return ret;
#endif
        }
    }
}