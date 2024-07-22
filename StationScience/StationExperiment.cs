using KSP.Localization; // For localization support
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP_Log;

namespace StationScience
{
    public class StationExperiment : ModuleScienceExperiment
    {
        public const string EUREKAS = "Eurekas";
        public const string KUARQS = "Kuarqs";
        public const string BIOPRODUCTS = "Bioproducts";
        public const string SOLUTIONS = "Solutions";

        // Class to define requirements for the experiment
        internal class Requirement
        {
            internal string name;
            internal float amount;
            internal Requirement(string name, float amount)
            {
                this.name = name;
                this.amount = amount;
            }
        }

        // Enum to define the status of the experiment
        public enum Status
        {
            Idle,
            Running,
            Completed,
            BadLocation,
            Storage,
            Inoperable,
            Starved
        }

        static Log Log;

        // Dictionary to store experiment requirements
        internal Dictionary<string, Requirement> requirements = new Dictionary<string, Requirement>();

        // Fields for the number of resources required
        [KSPField(isPersistant = false)]
        public int eurekasRequired;

        [KSPField(isPersistant = false)]
        public int kuarqsRequired;

        [KSPField(isPersistant = false)]
        public int bioproductsRequired;

        [KSPField(isPersistant = false)]
        public int solutionsRequired;

        // Fields for kuarq decay parameters
        [KSPField(isPersistant = false)]
        public float kuarqHalflife;

        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")]
        public float kuarqDecay;

        // Field to display the current status of the experiment
        [KSPField(isPersistant = false, guiName = "Status", guiActive = true)]
        public Status currentStatus = Status.Idle;

        // Fields to track experiment progress
        [KSPField(isPersistant = true)]
        public float launched = 0;

        [KSPField(isPersistant = true)]
        public float completed = 0;

        [KSPField(isPersistant = true)]
        public string last_subjectId = "";

        // Method to check if the experiment is in a "boring" location
        public static bool CheckBoring(Vessel vessel, bool msg = false)
        {
            if (Log != null)
            {
                Log.Info($"{vessel.Landed}, {vessel.landedAt}, {vessel.launchTime}, {vessel.situation}, {vessel.orbit.referenceBody.name}");
            }

            // Check if the vessel is in a situation considered boring
            if (vessel.orbit.referenceBody == FlightGlobals.GetHomeBody() &&
                (vessel.situation == Vessel.Situations.LANDED ||
                 vessel.situation == Vessel.Situations.PRELAUNCH ||
                 vessel.situation == Vessel.Situations.SPLASHED ||
                 vessel.altitude <= vessel.orbit.referenceBody.atmosphereDepth))
            {
                if (msg)
                    ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_boring"), 6, ScreenMessageStyle.UPPER_CENTER);
                return true;
            }
            return false;
        }

        // Helper methods to get and set resource amounts
        public PartResource GetResource(string name)
        {
            return ResourceHelper.getResource(part, name);
        }

        public double GetResourceAmount(string name)
        {
            return ResourceHelper.getResourceAmount(part, name);
        }

        public double GetResourceMaxAmount(string name)
        {
            return ResourceHelper.getResourceMaxAmount(part, name);
        }

        public PartResource SetResourceMaxAmount(string name, double max)
        {
            return ResourceHelper.setResourceMaxAmount(part, name, max);
        }

        // Method to check if the experiment has all required resources
        public bool Finished()
        {
            bool finished = true;
            foreach (var r in requirements)
            {
                double num = GetResourceAmount(r.Value.name);
                Log.Info($"{part.partInfo.title} {r.Value.name}: {num}/{r.Value.amount:F1}");

                if (Math.Round(num, 2) < r.Value.amount)
                {
                    finished = false;
                }
            }
            return finished;
        }

        // Method to load the experiment's requirements from the configuration
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // Load the module configuration
            if (part.partInfo != null)
            {
                node = GameDatabase.Instance.GetConfigs("PART")
                    .Single(c => part.partInfo.name == c.name.Replace('_', '.'))
                    .config.GetNodes("MODULE")
                    .Single(n => n.GetValue("name") == moduleName);
            }

            var pList = PartResourceLibrary.Instance.resourceDefinitions;

            // Load resource requirements
            foreach (ConfigNode resNode in node.GetNodes("REQUIREMENT"))
            {
                try
                {
                    string name = resNode.GetValue("name");
                    float amt = float.Parse(resNode.GetValue("maxAmount"));
                    requirements.Add(name, new Requirement(name, amt));

                    var def = pList[name];
                    if (def.resourceTransferMode != ResourceTransferMode.NONE)
                    {
                        PartResource resource = part.AddResource(resNode);
                        part.Resources.Remove(resource);
                    }
                }
                catch
                {
                    // handle exceptions if necessary
                }
            }

            // Add default requirements if specified in fields
            if (eurekasRequired > 0 && !requirements.ContainsKey(EUREKAS))
                requirements.Add(EUREKAS, new Requirement(EUREKAS, eurekasRequired));
            if (kuarqsRequired > 0 && !requirements.ContainsKey(KUARQS))
                requirements.Add(KUARQS, new Requirement(KUARQS, kuarqsRequired));
            if (bioproductsRequired > 0 && !requirements.ContainsKey(BIOPRODUCTS))
                requirements.Add(BIOPRODUCTS, new Requirement(BIOPRODUCTS, bioproductsRequired));
            if (solutionsRequired > 0 && !requirements.ContainsKey(SOLUTIONS))
                requirements.Add(SOLUTIONS, new Requirement(SOLUTIONS, solutionsRequired));
        }

        // Method called when the experiment starts
        public override void OnStart(StartState state)
        {
#if DEBUG
            Log = new Log("StationScience", Log.LEVEL.INFO);
#else
            Log = new Log("StationScience", Log.LEVEL.ERROR);
#endif
            base.OnStart(state);
            if (state == StartState.Editor) { return; }

            // Update GUI fields based on kuarq requirements
            if (requirements.ContainsKey(KUARQS) && kuarqHalflife > 0)
            {
                Fields["kuarqDecay"].guiActive = true;
                Events["DeployExperiment"].active = Finished();
                Events["StartExperiment"].active = false;

                if (ResearchAndDevelopment.GetExperiment(experimentID).IsAvailableWhile(GetScienceSituation(vessel), vessel.mainBody))
                    currentStatus = Status.Completed;
                else
                    currentStatus = Status.BadLocation;
            }
            else
            {
                UpdateStatusBasedOnConditions();
            }

            this.part.force_activate();
            StartCoroutine(UpdateStatus());
        }

        // Method to update the status based on conditions
        private void UpdateStatusBasedOnConditions()
        {
            if (Inoperable)
                currentStatus = Status.Inoperable;
            else if (Deployed)
                currentStatus = Status.Storage;
            else if (GetResource(EUREKAS) != null || GetResource(SOLUTIONS) != null)
                currentStatus = Status.Running;
            else
                currentStatus = Status.Idle;

            Events["DeployExperiment"].active = !Deployed;
            Events["StartExperiment"].active = !Inoperable && GetScienceCount() == 0;
        }

        // Event to start the experiment
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_startExp", active = true)]
        public void StartExperiment()
        {
            if (GetScienceCount() > 0)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalized"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (CheckBoring(vessel, true)) return;

            PrepareResourcesForExperiment();

            Events["StartExperiment"].active = false;
            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_started"), 6, ScreenMessageStyle.UPPER_CENTER);

            currentStatus = Status.Running;
        }

        // Method to prepare resources for the experiment
        private void PrepareResourcesForExperiment()
        {
            PartResource eurekas = null;
            PartResource bioproducts = null;
            PartResource solutions = null;

            foreach (var r in requirements)
            {
                PartResource pr = SetResourceMaxAmount(r.Value.name, r.Value.amount);
                if (r.Value.name == EUREKAS) eurekas = pr;
                if (r.Value.name == BIOPRODUCTS) bioproducts = pr;
                if (r.Value.name == SOLUTIONS) solutions = pr;
            }

            if (solutions != null && solutions.amount == 0 && eurekas != null && eurekas.amount == 0 && bioproducts != null)
            {
                bioproducts.amount = 0;
            }
        }

        // Event to deploy the experiment
        [KSPEvent(guiActive = true, guiName = "#autoLOC_StatSci_deployExp", active = false)]
        public void DeployExperiment()
        {
            Log.Info("Deploy Experiment");

            if (!Finished()) return;

            if (Deployed)
            {
                ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_finalized"), 6, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            DeployAndStoreExperiment();

            ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_StatSci_screen_deployed"), 6, ScreenMessageStyle.UPPER_CENTER);

            // Reset kuarqs resource amount to zero
            if (requirements.ContainsKey(KUARQS))
            {
                PartResource pr = GetResource(KUARQS);
                if (pr != null)
                {
                    pr.amount = 0;
                }
            }

            Events["DeployExperiment"].active = false;
        }

        // Method to deploy and store the experiment
        private void DeployAndStoreExperiment()
        {
            DeployExperiment();
            StoreData(new List<ModuleScienceExperiment> { this }, true);
        }

        // Method to update the status in each frame
        private void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight == false) return;
            if (currentStatus != Status.Running) return;

            foreach (var r in requirements)
            {
                PartResource pr = GetResource(r.Value.name);
                if (pr != null && pr.amount == 0)
                {
                    currentStatus = Status.Starved;
                    Events["DeployExperiment"].active = false;
                    Events["StartExperiment"].active = true;
                    break;
                }
            }
        }

        // Coroutine to periodically update the status
        private System.Collections.IEnumerator UpdateStatus()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (currentStatus == Status.Running)
                {
                    foreach (var r in requirements)
                    {
                        PartResource pr = GetResource(r.Value.name);
                        if (pr != null && pr.amount < r.Value.amount)
                        {
                            pr.amount += TimeWarp.deltaTime / TimeWarp.CurrentRate * r.Value.amount / 60;
                        }
                    }
                }
                else if (currentStatus == Status.Starved)
                {
                    foreach (var r in requirements)
                    {
                        PartResource pr = GetResource(r.Value.name);
                        if (pr != null && pr.amount >= r.Value.amount)
                        {
                            currentStatus = Status.Running;
                            break;
                        }
                    }
                }
                else if (requirements.ContainsKey(KUARQS) && kuarqHalflife > 0)
                {
                    PartResource pr = GetResource(KUARQS);
                    if (pr != null && pr.amount > 0)
                    {
                        pr.amount -= kuarqDecay * TimeWarp.deltaTime / TimeWarp.CurrentRate;
                        if (pr.amount < 0)
                        {
                            pr.amount = 0;
                        }
                    }
                }
            }
        }
    }
}
