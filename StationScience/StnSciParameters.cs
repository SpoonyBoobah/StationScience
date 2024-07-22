/*
    Station Science Contract Parameters

    This code defines various contract parameters used in the Station Science mod for Kerbal Space Program (KSP). 
    These parameters are used to track and evaluate scientific experiments conducted in space stations or 
    other spacecraft.

    The following contract parameters are defined:

    1. StnSciParameter - The base class for contract parameters related to scientific experiments. 
       It includes:
       - `experimentType`: The type of experiment part.
       - `targetBody`: The celestial body where the experiment is conducted.
       - Methods to save and load data, and to check and complete the contract based on the experiment's status.

    2. NewPodParameter - A parameter that tracks when a new pod is launched. 
       It listens for vessel creation and situation changes to update the status of the experiment.

    3. DoExperimentParameter - A parameter that tracks the completion of the scientific experiment. 
       It checks if the experiment has been conducted and completed based on the data collected.

    4. ReturnExperimentParameter - A parameter that tracks the return of experiment results. 
       It verifies if the experiment results have been recovered and marks the contract as complete if conditions are met.

    The code integrates with the KSP game events to monitor and update contract status dynamically as game conditions change.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using Contracts;
using Contracts.Parameters;
using KSP.Localization;

namespace StationScience.Contracts.Parameters
{
    // Interface for parameters related to a specific part
    public interface PartRelated
    {
        AvailablePart GetPartType();
    }

    // Interface for parameters related to a specific celestial body
    public interface BodyRelated
    {
        CelestialBody GetBody();
    }

    // Base class for Station Science contract parameters
    public class StnSciParameter : ContractParameter, PartRelated, BodyRelated
    {
        private AvailablePart experimentType; // The type of experiment part
        private CelestialBody targetBody;     // The celestial body where the experiment is conducted

        // Default constructor
        public StnSciParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Constructor with parameters to specify experiment type and target body
        public StnSciParameter(AvailablePart type, CelestialBody body)
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
            this.experimentType = type;
            this.targetBody = body;
            this.AddParameter(new Parameters.DoExperimentParameter(), null);
            this.AddParameter(new Parameters.ReturnExperimentParameter(), null);
        }

        // Get the type of part (experiment) associated with this parameter
        public AvailablePart GetPartType()
        {
            return experimentType;
        }

        // Get the celestial body associated with this parameter
        public CelestialBody GetBody()
        {
            return targetBody;
        }

        // Generates a unique hash string for this parameter
        protected override string GetHashString()
        {
            return experimentType.name;
        }

        // Provides a title for the parameter
        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Title", experimentType.title, targetBody.GetDisplayName());
        }

        // Provides additional notes for the parameter
        protected override string GetNotes()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Notes", experimentType.title, targetBody.GetDisplayName());
        }

        // Sets the experiment type based on the part name
        private bool SetExperiment(string exp)
        {
            experimentType = PartLoader.getPartInfoByName(exp);
            if (experimentType == null)
            {
                StnSciScenario.LogError("Couldn't find experiment part: " + exp);
                return false;
            }
            return true;
        }

        // Completes the parameter
        public void Complete()
        {
            SetComplete();
        }

        // Sets the target celestial body based on its name
        private bool SetTarget(string planet)
        {
            targetBody = FlightGlobals.Bodies.FirstOrDefault(body => body.bodyName.ToLower() == planet.ToLower());
            if (targetBody == null)
            {
                StnSciScenario.LogError("Couldn't find planet: " + planet);
                return false;
            }
            return true;
        }

        // Saves the parameter data to a config node
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("targetBody", targetBody.name);
            node.AddValue("experimentType", experimentType.name);
        }

        // Loads the parameter data from a config node
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
            string expID = node.GetValue("experimentType");
            SetExperiment(expID);
            string bodyID = node.GetValue("targetBody");
            SetTarget(bodyID);
        }

        // Retrieves the experiment type from a contract parameter
        public static AvailablePart getExperimentType(ContractParameter o)
        {
            object par = o.Parent ?? o.Root;
            PartRelated parent = par as PartRelated;
            return parent?.GetPartType();
        }

        // Retrieves the target body from a contract parameter
        public static CelestialBody getTargetBody(ContractParameter o)
        {
            BodyRelated parent = o.Parent as BodyRelated;
            return parent?.GetBody();
        }
    }

    // Parameter for tracking the launch of a new pod
    public class NewPodParameter : ContractParameter
    {
        // Constructor
        public NewPodParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Generates a unique hash string for this parameter
        protected override string GetHashString()
        {
            return "new pod parameter " + this.GetHashCode();
        }

        // Provides a title for the parameter
        protected override string GetTitle()
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            return experimentType == null 
                ? Localizer.Format("#autoLOC_StatSciNewPod_TitleA")
                : Localizer.Format("#autoLOC_StatSciNewPod_TitleB", experimentType.title);
        }

        // Registers event handlers for this parameter
        protected override void OnRegister()
        {
            GameEvents.onLaunch.Add(OnLaunch);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
        }

        // Unregisters event handlers for this parameter
        protected override void OnUnregister()
        {
            GameEvents.onLaunch.Remove(OnLaunch);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
        }

        // Updates the experiment status when a vessel is created
        private void OnVesselCreate(Vessel vessel)
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null) return;

            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        // Updates the experiment status when the vessel situation changes
        private void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> arg)
        {
            if (!((arg.from == Vessel.Situations.LANDED || arg.from == Vessel.Situations.PRELAUNCH) &&
                  (arg.to == Vessel.Situations.FLYING || arg.to == Vessel.Situations.SUB_ORBITAL)))
                return;

            if (arg.host.mainBody.name != "Kerbin") return;

            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null) return;

            foreach (Part part in arg.host.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null && e.launched == 0)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        // Updates the experiment status when the vessel is launched
        private void OnLaunch(EventReport report)
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null) return;

            Vessel vessel = FlightGlobals.ActiveVessel;
            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null && e.launched == 0)
                    {
                        e.launched = (float)Planetarium.GetUniversalTime();
                    }
                }
            }
        }

        private float lastUpdate = 0;

        // Checks if the experiment has been launched and marks the parameter as complete
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (lastUpdate > UnityEngine.Time.realtimeSinceStartup + .1) return;

            lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            Vessel vessel = FlightGlobals.ActiveVessel;
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null) return;

            if (vessel != null)
            {
                foreach (Part part in vessel.Parts)
                {
                    if (part.name == experimentType.name)
                    {
                        StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                        if (e != null && e.launched >= this.Root.DateAccepted)
                        {
                            SetComplete();
                            return;
                        }
                    }
                }
            }
            SetIncomplete();
        }

        // Saves parameter data (no additional data for this parameter)
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        // Loads parameter data
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }

    // Parameter for tracking the completion of experiments
    public class DoExperimentParameter : ContractParameter
    {
        private float lastUpdate = 0;

        // Constructor
        public DoExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Generates a unique hash string for this parameter
        protected override string GetHashString()
        {
            return Localizer.Format("#autoLOC_StatSciDoExp_Hash", this.GetHashCode());
        }

        // Provides a title for the parameter
        protected override string GetTitle()
        {
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            return targetBody == null 
                ? Localizer.Format("#autoLOC_StatSciDoExp_TitleA")
                : Localizer.Format("#autoLOC_StatSciDoExp_TitleB", targetBody.GetDisplayName());
        }

        // Checks if the experiment has been completed and marks the parameter as complete
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (lastUpdate > UnityEngine.Time.realtimeSinceStartup + .1) return;

            lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);

            if (targetBody == null || experimentType == null) return;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                foreach (Part part in vessel.Parts)
                {
                    if (part.name == experimentType.name)
                    {
                        StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                        if (e != null && e.completed >= this.Root.DateAccepted && e.completed > e.launched)
                        {
                            ScienceData[] data = e.GetData();
                            foreach (ScienceData datum in data)
                            {
                                if (datum.subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                                {
                                    SetComplete();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            SetIncomplete();
        }

        // Saves parameter data (no additional data for this parameter)
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        // Loads parameter data
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }

    // Parameter for tracking the return of experiment results
    public class ReturnExperimentParameter : ContractParameter
    {
        // Constructor
        public ReturnExperimentParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Called when the contract is accepted (currently does nothing)
        public void OnAccept(Contract contract)
        {
        }

        // Generates a unique hash string for this parameter
        protected override string GetHashString()
        {
            return "recover experiment " + this.GetHashCode();
        }

        // Provides a title for the parameter
        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciRetParam_Title");
        }

        // Registers event handlers for this parameter
        protected override void OnRegister()
        {
            GameEvents.onVesselRecovered.Add(OnRecovered);
        }

        // Unregisters event handlers for this parameter
        protected override void OnUnregister()
        {
            GameEvents.onVesselRecovered.Remove(OnRecovered);
        }

        // Checks the recovered vessel for experiment data and completes the parameter if conditions are met
        private void OnRecovered(ProtoVessel pv, bool dummy)
        {
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);

            if (targetBody == null || experimentType == null) return;

            foreach (ProtoPartSnapshot part in pv.protoPartSnapshots)
            {
                if (part.partName == experimentType.name)
                {
                    foreach (ProtoPartModuleSnapshot module in part.modules)
                    {
                        if (module.moduleName == "StationExperiment")
                        {
                            ConfigNode cn = module.moduleValues;
                            if (!cn.HasValue("launched") || !cn.HasValue("completed")) continue;

                            float launched, completed;
                            try
                            {
                                launched = float.Parse(cn.GetValue("launched"));
                                completed = float.Parse(cn.GetValue("completed"));
                            }
                            catch (Exception e)
                            {
                                StnSciScenario.LogError(e.ToString());
                                continue;
                            }

                            if (completed >= this.Root.DateAccepted)
                            {
                                foreach (ConfigNode datum in cn.GetNodes("ScienceData"))
                                {
                                    if (!datum.HasValue("subjectID")) continue;

                                    string subjectID = datum.GetValue("subjectID");
                                    if (subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                                    {
                                        StnSciParameter parent = this.Parent as StnSciParameter;
                                        SetComplete();
                                        parent?.Complete();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // (Deprecated) This method was intended to handle vessel recovery, but is not used
        private void OnRecovery(Vessel vessel)
        {
            StnSciScenario.Log("Recovering " + vessel.vesselName);
            CelestialBody targetBody = StnSciParameter.getTargetBody(this);
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);

            if (targetBody == null || experimentType == null) return;

            foreach (Part part in vessel.Parts)
            {
                if (part.name == experimentType.name)
                {
                    StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                    if (e != null && e.launched >= this.Root.DateAccepted && e.completed >= e.launched)
                    {
                        ScienceData[] data = e.GetData();
                        foreach (ScienceData datum in data)
                        {
                            if (datum.subjectID.ToLower().Contains("@" + targetBody.name.ToLower() + "inspace"))
                            {
                                StnSciParameter parent = this.Parent as StnSciParameter;
                                SetComplete();
                                parent?.Complete();
                                return;
                            }
                        }
                    }
                }
            }
            SetIncomplete();
        }

        // Saves parameter data (no additional data for this parameter)
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        // Loads parameter data
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }
}
