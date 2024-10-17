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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        AvailablePart GetPartType();  // Method to retrieve the part type for the experiment
    }

    // Interface for parameters related to a celestial body
    public interface BodyRelated
    {
        CelestialBody GetBody();  // Method to retrieve the target celestial body for the experiment
    }

    // Main contract parameter class for Station Science
    public class StnSciParameter : ContractParameter, PartRelated, BodyRelated
    {
        AvailablePart experimentType;  // The experiment part involved in the contract
        CelestialBody targetBody;      // The target celestial body for the experiment

        // Method to get the experiment type (part) of the contract
        public AvailablePart GetPartType()
        {
            return experimentType;
        }

        // Method to get the target celestial body of the contract
        public CelestialBody GetBody()
        {
            return targetBody;
        }

        // Default constructor, initializes the parameter as enabled and not disabling on state change
        public StnSciParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Constructor for setting experiment part and celestial body target
        public StnSciParameter(AvailablePart type, CelestialBody body)
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
            this.experimentType = type;
            this.targetBody = body;
            // Adding sub-parameters related to experiment execution
            this.AddParameter(new Parameters.DoExperimentParameter(), null);
            this.AddParameter(new Parameters.ReturnExperimentParameter(), null);
        }

        // Unique hash string for this parameter (based on the experiment type)
        protected override string GetHashString()
        {
            return experimentType.name;
        }

        // Title of the parameter, localized with experiment type and celestial body name
        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Title", experimentType.title, targetBody.GetDisplayName());
        }

        // Additional notes for the parameter, localized with experiment type and celestial body name
        protected override string GetNotes()
        {
            return Localizer.Format("#autoLOC_StatSciParam_Notes", experimentType.title, targetBody.GetDisplayName());
        }

        // Sets the experiment part by name, logging an error if it fails
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

        // Marks this parameter as complete
        public void Complete()
        {
            SetComplete();
        }

        // Sets the target celestial body by name, logging an error if it fails
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

        // Saves the parameter's data to a config node
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.AddValue("targetBody", targetBody.name);
            node.AddValue("experimentType", experimentType.name);
        }

        // Loads the parameter's data from a config node
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
            string expID = node.GetValue("experimentType");
            SetExperiment(expID);
            string bodyID = node.GetValue("targetBody");
            SetTarget(bodyID);
        }

        // Static method to get the experiment type (part) from the parent contract parameter
        static public AvailablePart getExperimentType(ContractParameter o)
        {
            object par = o.Parent;
            if (par == null)
                par = o.Root;
            PartRelated parent = par as PartRelated;
            if (parent != null)
                return parent.GetPartType();
            else
                return null;
        }

        // Static method to get the target celestial body from the parent contract parameter
        static public CelestialBody getTargetBody(ContractParameter o)
        {
            BodyRelated parent = o.Parent as BodyRelated;
            if (parent != null)
                return parent.GetBody();
            else
                return null;
        }
    }

    // Parameter that tracks if a new experiment part is launched and sets it as complete
    public class NewPodParameter : ContractParameter
    {
        public NewPodParameter()
        {
            this.Enabled = true;
            this.DisableOnStateChange = false;
        }

        // Hash string for this parameter
        protected override string GetHashString()
        {
            return "new pod parameter " + this.GetHashCode();
        }

        // Title of this parameter
        protected override string GetTitle()
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return Localizer.Format("#autoLOC_StatSciNewPod_TitleA");
            return Localizer.Format("#autoLOC_StatSciNewPod_TitleB", experimentType.title);
        }

        // Registers event listeners for launch and vessel situation change
        protected override void OnRegister()
        {
            GameEvents.onLaunch.Add(OnLaunch);
            GameEvents.onVesselSituationChange.Add(OnVesselSituationChange);
        }

        // Unregisters event listeners for launch and vessel situation change
        protected override void OnUnregister()
        {
            GameEvents.onLaunch.Remove(OnLaunch);
            GameEvents.onVesselSituationChange.Remove(OnVesselSituationChange);
        }

        // Event handler for vessel situation changes, e.g., launch
        private void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> arg)
        {
            if (!((arg.from == Vessel.Situations.LANDED || arg.from == Vessel.Situations.PRELAUNCH) &&
                  (arg.to == Vessel.Situations.FLYING || arg.to == Vessel.Situations.SUB_ORBITAL)))
                return;
            if (arg.host.mainBody.name != "Kerbin")
                return;
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
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

        // Event handler for launch, marks the experiment as launched
        private void OnLaunch(EventReport report)
        {
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
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

        // Periodically checks if the experiment is launched and marks it complete
        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (lastUpdate > UnityEngine.Time.realtimeSinceStartup + .1)
                return;
            lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            Vessel vessel = FlightGlobals.ActiveVessel;
            AvailablePart experimentType = StnSciParameter.getExperimentType(this);
            if (experimentType == null)
                return;
            if (vessel != null)
                foreach (Part part in vessel.Parts)
                {
                    if (part.name == experimentType.name)
                    {
                        StationExperiment e = part.FindModuleImplementing<StationExperiment>();
                        if (e != null)
                        {
                            if (e.launched >= this.Root.DateAccepted)
                            {
                                SetComplete();
                                return;
                            }
                        }
                    }
                }
            SetIncomplete();
        }

        // Saves the parameter's state to a config node
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        // Loads the parameter's state from a config node
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            this.Enabled = true;
        }
    }
}
