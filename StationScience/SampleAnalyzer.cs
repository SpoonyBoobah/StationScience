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
using UnityEngine;

namespace StationScience
{
    // Module for analyzing scientific samples in the game.
    class SampleAnalyzer : ModuleScienceContainer
    {
        // List of events for sample analysis
        private List<BaseEvent> sampleEvents = new List<BaseEvent>();

        // Fields for analyzer configuration
        [KSPField(isPersistant = false)]
        public int kuarqsRequired = 0;

        [KSPField(isPersistant = false)]
        public float kuarqHalflife = 0;

        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")]
        public float kuarqDecay = 0;

        [KSPField(isPersistant = false)]
        public float txValue = 0.8F;

        [KSPField(isPersistant = true)]
        public int lightsMode = 1; // 0: off, 1: auto, 2: on

        private string lightsOn = Localizer.Format("#autoLOC_StatSci_LightsOn");
        private string lightsOff = Localizer.Format("#autoLOC_StatSci_LightsOff");
        private string lightsAuto = Localizer.Format("#autoLOC_StatSci_LightsAuto");

        // Update lights mode based on the current setting
        private void UpdateLightsMode()
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

        // Toggle lights mode between off, auto, and on
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_LightsAuto", active = true)]
        public void ToggleLightsMode()
        {
            lightsMode = (lightsMode + 1) % 3; // Cycles between 0, 1, and 2
            UpdateLightsMode();
        }

        // Resource helper methods
        public PartResource GetResource(string name) => ResourceHelper.GetResource(part, name);
        public double GetResourceAmount(string name) => ResourceHelper.GetResourceAmount(part, name);
        public PartResource SetResourceMaxAmount(string name, double max) => ResourceHelper.SetResourceMaxAmount(part, name, max);

        // Analyze science data
        public void Analyze(IScienceDataContainer container, ScienceData data)
        {
            if (GetScienceCount() > 0)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_analyseFull"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            container.DumpData(data);
            AddData(data);

            if (kuarqsRequired > 0)
            {
                SetResourceMaxAmount(StationExperiment.KUARQS, kuarqsRequired);
                Events["ReviewDataEvent"].guiActive = false;
            }
            else
            {
                data.baseTransmitValue = txValue;
                ReviewData();
            }
            UpdateList();
        }

        // Review data based on kuarqs resource requirements
        public new void ReviewDataEvent()
        {
            if (GetResourceAmount(StationExperiment.KUARQS) < kuarqsRequired)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_analyseFull"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            base.ReviewDataEvent();
        }

        public new void ReviewData()
        {
            if (GetResourceAmount(StationExperiment.KUARQS) < kuarqsRequired)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_analyseAct"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            base.ReviewData();
        }

        // Add a button to analyze science data
        public void AddDataButton(ScienceData data, IScienceDataContainer container)
        {
            var experiment = ResearchAndDevelopment.GetExperiment(data.subjectID.Split('@')[0]);
            if (experiment != null && data.baseTransmitValue < txValue)
            {
                var kspEvent = new KSPEvent
                {
                    active = true,
                    name = data.subjectID,
                    guiActive = true,
                    guiName = data.title
                };
                var baseEvent = new BaseEvent(Events, data.subjectID, () => Analyze(container, data), kspEvent);
                sampleEvents.Add(baseEvent);
                Events.Add(baseEvent);
            }
        }

        // Update the state of lights based on lights mode
        private void UpdateLights()
        {
            if (animator == null) return;

            bool shouldActivate = lightsMode == 2 || 
                                  (lightsMode == 1 && GetResourceAmount(StationExperiment.KUARQS) < kuarqsRequired);

            if (shouldActivate != animator.Progress.Equals(shouldActivate))
            {
                animator.allowManualControl = true;
                animator.Toggle();
                animator.allowManualControl = false;
            }
        }

        // Update the list of available sample events and status
        public void UpdateList()
        {
            UpdateLights();
            foreach (var ev in sampleEvents)
            {
                ev.guiActive = false;
                Events.Remove(ev);
            }
            sampleEvents.Clear();

            if (GetScienceCount() > 0)
            {
                status = kuarqsRequired > 0 && GetResourceAmount(StationExperiment.KUARQS) < kuarqsRequired
                    ? GetResourceAmount(StationExperiment.KUARQS) == 0
                        ? Localizer.Format("#autoLOC_StatSci_analyseReady")
                        : Localizer.Format("#autoLOC_StatSci_analysing")
                    : Localizer.Format("#autoLOC_StatSci_readyTrans");
            }
            else
            {
                foreach (var container in vessel.FindPartModulesImplementing<IScienceDataContainer>())
                {
                    if (container is SampleAnalyzer) continue;

                    foreach (var data in container.GetData())
                    {
                        AddDataButton(data, container);
                    }
                }

                status = sampleEvents.Count == 0
                    ? "#autoLOC_StatSci_analyseNothing"
                    : Localizer.Format("#autoLOC_StatSci_analyseReady");
            }
            UpdateLights();
        }

        private double lastUpdate = 0;

        // Fixed update to handle periodic tasks
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            double curTime = Time.realtimeSinceStartup;
            if (curTime - lastUpdate >= 2)
            {
                UpdateList();
                lastUpdate = curTime;
            }

            if (kuarqsRequired > 0)
            {
                var kuarqsAmount = GetResourceAmount(StationExperiment.KUARQS);

                if (kuarqsAmount > 0)
                {
                    if (GetScienceCount() == 0)
                    {
                        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_transmitted"), 6, ScreenMessageStyle.UPPER_CENTER);
                        SetResourceMaxAmount(StationExperiment.KUARQS, 0);
                    }

                    if (kuarqsAmount >= kuarqsRequired && GetScienceCount() > 0)
                    {
                        var scienceData = GetData();
                        if (scienceData.Length == 1)
                        {
                            scienceData[0].baseTransmitValue = txValue;
                            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_anaComp"), 6, ScreenMessageStyle.UPPER_CENTER);
                            SetResourceMaxAmount(StationExperiment.KUARQS, 0);
                            Events["ReviewDataEvent"].guiActive = true;
                        }
                    }

                    if (kuarqHalflife > 0)
                    {
                        var kuarqs = GetResource(StationExperiment.KUARQS);
                        if (kuarqs != null && kuarqs.amount < 0.99 * kuarqsRequired)
                        {
                            double decay = Math.Pow(0.5, TimeWarp.fixedDeltaTime / kuarqHalflife);
                            kuarqDecay = (float)((kuarqs.amount * (1 - decay)) / TimeWarp.fixedDeltaTime);
                            kuarqs.amount *= decay;
                        }
                        else
                        {
                            kuarqDecay = 0;
                        }
                    }
                }
            }
        }

        private ModuleAnimateGeneric animator = null;

        // Initialize the module when the game starts
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            animator = part.FindModuleImplementing<ModuleAnimateGeneric>();
            if (animator != null)
            {
                foreach (var field in animator.Fields)
                {
                    if (field != null)
                        field.guiActive = false;
                }
            }

            capacity = 1;

            if (state == StartState.Editor) return;

            if (kuarqHalflife > 0)
                Fields["kuarqDecay"].guiActive = true;

            part.force_activate();
            UpdateLightsMode();
        }

        // Provide information about the analyzer
        public override string GetInfo()
        {
            string info = Localizer.Format("#autoLOC_StatSci_analyseImp", Math.Round(txValue * 100));

            if (kuarqsRequired > 0)
            {
                info += $"\n{Localizer.Format("#autoLOC_StatSci_KuarkReq", kuarqsRequired)}";

                double productionRequired = 0.01;
                if (kuarqHalflife > 0)
                {
                    info += $"\n{Localizer.Format("#autoLOC_StatSci_KuarkHalf", kuarqHalflife)}";
                    productionRequired = kuarqsRequired * (1 - Math.Pow(0.5, 1.0 / kuarqHalflife));
                    info += $"\n{Localizer.Format("#autoLOC_StatSci_KuarkProd", productionRequired)}";
                }

                info += productionRequired > 1
                    ? $"\n{Localizer.Format("#autoLOC_StatSci_CycReqM", Math.Ceiling(productionRequired))}"
                    : $"\n{Localizer.Format("#autoLOC_StatSci_CycReq")}";
            }

            return info;
        }
    }
}
