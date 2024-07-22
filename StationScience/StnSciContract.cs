/*
    This file is part of the Station Science mod for Kerbal Space Program.

    Station Science is an extension mod for Kerbal Space Program (KSP) that enhances the game by introducing new scientific experiments and contract types centered around space station operations. 

    The `StnSciContract` class within this script is responsible for defining and managing contracts related to conducting scientific experiments aboard space stations. These contracts are dynamically generated based on the experiments available to the player and the celestial bodies that can be reached.

    **Class Overview:**
    - **Contract Generation**: The class is designed to create contracts based on currently unlocked experiments and reachable celestial bodies in the game. Contracts are generated with varying difficulty and rewards depending on the type of experiment and target body.
    - **Contract Requirements**: Ensures that contracts are only offered if the player has met specific prerequisites, such as unlocking certain technologies or reaching particular celestial bodies.
    - **Reward System**: Calculates and assigns rewards for completing contracts, including science points, reputation, funds, and deadlines. The rewards are based on the difficulty and value of the contract.
    - **Persistence**: Handles saving and loading of contract data to ensure that contracts persist between game sessions.
    - **Customization**: Allows for customization of which experiments can be offered as contracts and the conditions under which they are available.

    **Key Features:**
    - **Experiment and Body Filtering**: Contracts are filtered based on available experiments and celestial bodies, ensuring that only valid and meaningful contracts are generated.
    - **Dynamic Difficulty Adjustment**: The difficulty of contracts is adjusted based on the player's current progress and the challenge of the tasks.
    - **Integration with KSP Contracts System**: Fully integrates with the KSP Contracts System to handle contract states, such as active, completed, or failed.

    **License Information:**
    Station Science is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

    Station Science is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

    You should have received a copy of the GNU General Public License along with Station Science. If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Contracts;
using KSP.Localization;
using KSPAchievements;

namespace StationScience.Contracts
{
    public class StnSciContract : Contract, Parameters.PartRelated, Parameters.BodyRelated
    {
        CelestialBody targetBody = null;
        AvailablePart experimentType = null;

        // Returns the part type associated with the contract
        public AvailablePart GetPartType()
        {
            return experimentType;
        }

        // Returns the celestial body associated with the contract
        public CelestialBody GetBody()
        {
            return targetBody;
        }

        double value = 0;

        // Generates a random number uniformly between 0 and 1
        static double GetUniform()
        {
            return UnityEngine.Random.value;
        }

        // Generates a random sample from a normal (Gaussian) distribution
        static double GetNormal(double mean = 0.0, double standardDeviation = 1.0)
        {
            if (standardDeviation <= 0.0)
            {
                StnSciScenario.LogWarning("Invalid standard deviation: " + standardDeviation);
                return 0;
            }
            // Use Box-Muller algorithm for normal distribution
            double u1 = GetUniform();
            double u2 = GetUniform();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double theta = 2.0 * Math.PI * u2;
            return mean + standardDeviation * r * Math.Sin(theta);
        }

        // Generates a random sample from a Gamma distribution
        static double GetGamma(double shape, double scale)
        {
            double d, c, x, xsquared, v, u;

            if (shape >= 1.0)
            {
                d = shape - 1.0 / 3.0;
                c = 1.0 / Math.Sqrt(9.0 * d);
                for (;;)
                {
                    do
                    {
                        x = GetNormal();
                        v = 1.0 + c * x;
                    }
                    while (v <= 0.0);
                    v = v * v * v;
                    u = GetUniform();
                    xsquared = x * x;
                    if (u < 1.0 - .0331 * xsquared * xsquared || Math.Log(u) < 0.5 * xsquared + d * (1.0 - v + Math.Log(v)))
                        return scale * d * v;
                }
            }
            else if (shape <= 0.0)
            {
                StnSciScenario.LogWarning("Invalid Gamma shape: " + shape);
                return 0;
            }
            else
            {
                double g = GetGamma(shape + 1.0, 1.0);
                double w = GetUniform();
                return scale * g * Math.Pow(w, 1.0 / shape);
            }
        }

        // Checks if all parts in the given set are unlocked
        bool AllUnlocked(HashSet<string> set)
        {
            foreach (string entry in set)
            {
                AvailablePart part = PartLoader.getPartInfoByName(entry);
                if (!(ResearchAndDevelopment.PartTechAvailable(part)))
                    return false;
            }
            return true;
        }

        // Returns a list of unlocked experiments
        List<string> GetUnlockedExperiments()
        {
            List<string> ret = new List<string>();
            foreach (var exp in StnSciScenario.Instance.settings.experimentPrereqs)
            {
                if (AllUnlocked(exp.Value))
                    ret.Add(exp.Key);
            }
            return ret;
        }

        // Defines a candidate for a contract
        private class ContractCandidate
        {
            public string experiment;
            public CelestialBody body;
            public double value;
            public double weight;
        }

        // Generates a new contract based on available experiments and bodies
        protected override bool Generate()
        {
            if (ActiveCount() >= StnSciScenario.Instance.settings.maxContracts)
            {
                return false;
            }

            double xp = StnSciScenario.Instance.xp + Reputation.Instance.reputation * StnSciScenario.Instance.settings.reputationFactor;
            if (this.Prestige == ContractPrestige.Trivial)
                xp *= StnSciScenario.Instance.settings.trivialMultiplier;
            if (this.Prestige == ContractPrestige.Significant)
                xp *= StnSciScenario.Instance.settings.significantMultiplier;
            if (this.Prestige == ContractPrestige.Exceptional)
                xp *= StnSciScenario.Instance.settings.exceptionalMultiplier;
            if (xp <= 0.5)
                xp = 0.5;

            List<string> experiments = GetUnlockedExperiments();
            List<CelestialBody> bodies = GetBodies_Reached(true, false);
            List<ContractCandidate> candidates = new List<ContractCandidate>();
            double totalWeight = 0.0;

            // Generate candidates for contracts
            foreach (var exp in experiments)
            {
                double expValue;
                try
                {
                    expValue = StnSciScenario.Instance.settings.experimentChallenge[exp];
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }
                foreach (var body in bodies)
                {
                    int acount = ActiveCount(exp, body);
                    if (acount > 0)
                    {
                        continue;
                    }
                    double plaValue;
                    try
                    {
                        plaValue = StnSciScenario.Instance.settings.planetChallenge[body.name];
                    }
                    catch (KeyNotFoundException)
                    {
                        plaValue = body.scienceValues.InSpaceLowDataValue;
                    }
                    ContractCandidate candidate = new ContractCandidate
                    {
                        body = body,
                        experiment = exp,
                        value = expValue * plaValue,
                        weight = Math.Exp(-Math.Pow(Math.Log(candidate.value / xp, 2), 2) / (2 * Math.Pow(2 / 2.355, 2)))
                    };
                    candidates.Add(candidate);
                    totalWeight += candidate.weight;
                }
            }

            double rand = GetUniform() * totalWeight;
            ContractCandidate chosen = null;
            foreach (var cand in candidates)
            {
                if (rand <= cand.weight)
                {
                    chosen = cand;
                    break;
                }
                rand -= cand.weight;
            }

            if (chosen == null)
            {
                return false;
            }

            if (!SetExperiment(chosen.experiment))
                return false;
            targetBody = chosen.body;

            this.value = chosen.value;
            this.AddParameter(new Parameters.StnSciParameter(experimentType, targetBody), null);

            int ccount = CompletedCount(experimentType.name, targetBody);
            bool first_time = (ccount == 0);
            float v = (float)this.value;

            base.SetExpiry();

            float sciReward = StnSciScenario.Instance.settings.contractScience.calcReward(v, first_time);
            base.SetScience(sciReward, targetBody);

            base.SetDeadlineYears(StnSciScenario.Instance.settings.contractDeadline.calcReward(v, first_time), targetBody);

            base.SetReputation(StnSciScenario.Instance.settings.contractReputation.calcReward(v, first_time),
                               StnSciScenario.Instance.settings.contractReputation.calcFailure(v, first_time), targetBody);

            base.SetFunds(StnSciScenario.Instance.settings.contractFunds.calcAdvance(v, first_time),
                          StnSciScenario.Instance.settings.contractFunds.calcReward(v, first_time),
                          StnSciScenario.Instance.settings.contractFunds.calcFailure(v, first_time), targetBody);

            return true;
        }

        // Counts active contracts matching the given experiment and body
        private int ActiveCount(string exp = null, CelestialBody body = null)
        {
            int ret = 0;
            if (ContractSystem.Instance == null || ContractSystem.Instance.Contracts == null)
            {
                return 0;
            }
            foreach (Contract con in ContractSystem.Instance.Contracts)
            {
                StnSciContract sscon = con as StnSciContract;
                if (sscon != null && (sscon.ContractState == Contract.State.Active || sscon.ContractState == Contract.State.Offered) &&
                  (exp == null || sscon.experimentType != null) &&
                  (body == null || sscon.targetBody != null) &&
                  ((exp == null || exp == sscon.experimentType.name) &&
                   (body == null || body.name == sscon.targetBody.name)))
                    ret += 1;
            }
            return ret;
        }

        // Counts completed contracts matching the given experiment and body
        private int CompletedCount(string exp = null, CelestialBody body = null)
        {
            int ret = 0;
            if (ContractSystem.Instance == null || ContractSystem.Instance.ContractsFinished == null)
            {
                return 0;
            }
            foreach (Contract con in ContractSystem.Instance.ContractsFinished)
            {
                StnSciContract sscon = con as StnSciContract;
                if (sscon != null && sscon.ContractState == Contract.State.Completed &&
                  sscon.experimentType != null && sscon.targetBody != null &&
                  (exp == null || sscon.experimentType != null) &&
                  (body == null || sscon.targetBody != null) &&
                  ((exp == null || exp == sscon.experimentType.name) &&
                   (body == null || body.name == sscon.targetBody.name)))
                    ret += 1;
            }
            return ret;
        }

        // Sets the experiment type for the contract
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

        // Sets the target celestial body for the contract
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

        public override bool CanBeCancelled()
        {
            return true;
        }
        public override bool CanBeDeclined()
        {
            return true;
        }

        protected override string GetHashString()
        {
            return targetBody.bodyName + ":" + experimentType.name;
        }
        protected override string GetTitle()
        {
            return Localizer.Format("#autoLOC_StatSciContract_Title", experimentType.title, targetBody.name);
        }
        protected override string GetDescription()
        {
            return TextGen.GenerateBackStories("Station Science", Agent.Name, "station science experiment", experimentType.title, new System.Random().Next(), true, true, true);
        }
        protected override string GetSynopsys()
        {
            return Localizer.Format("#autoLOC_StatSciContract_Blurb", experimentType.title, targetBody.name);
        }
        protected override string MessageCompleted()
        {
            return Localizer.Format("#autoLOC_StatSciContract_Completed", experimentType.title, targetBody.name);
        }

        protected override void OnCompleted()
        {
            base.OnCompleted();
            StnSciScenario.Instance.xp += (float)this.value * StnSciScenario.Instance.settings.progressionFactor;
        }

        protected override void OnLoad(ConfigNode node)
        {
            string expID = node.GetValue("experimentType");
            SetExperiment(expID);
            string bodyID = node.GetValue("targetBody");
            SetTarget(bodyID);
            this.value = float.Parse(node.GetValue("value"));
        }

        protected override void OnSave(ConfigNode node)
        {
            string bodyID = targetBody.bodyName;
            node.AddValue("targetBody", bodyID);
            string expID = experimentType.name;
            node.AddValue("experimentType", expID);
            node.AddValue("value", (float)value);
        }

        // Checks if the specified part is unlocked
        bool IsPartUnlocked(string name)
        {
            AvailablePart part = PartLoader.getPartInfoByName(name);
            if (part != null && ResearchAndDevelopment.PartTechAvailable(part))
                return true;
            return false;
        }

        // Determines if the contract meets the requirements to be offered
        public override bool MeetRequirements()
        {
            CelestialBodySubtree progress = null;
            foreach (var node in ProgressTracking.Instance.celestialBodyNodes)
            {
                if (node.Body == Planetarium.fetch.Home)
                    progress = node;
            }
            if (progress == null)
            {
                StnSciScenario.LogError("ProgressNode for Kerbin not found, terminating");
                return false;
            }
            if (progress.orbit.IsComplete &&
                  (IsPartUnlocked("dockingPort1") ||
                   IsPartUnlocked("dockingPort2") ||
                   IsPartUnlocked("dockingPort3") ||
                   IsPartUnlocked("dockingPortLarge") ||
                   IsPartUnlocked("dockingPortLateral"))
                  && (IsPartUnlocked("StnSciLab") || IsPartUnlocked("StnSciCyclo")))
                return true;
            return false;
        }
    }
}
