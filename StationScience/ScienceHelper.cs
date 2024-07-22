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

namespace StationScience
{
    /// <summary>
    /// Provides utility methods for science calculations and data retrieval in Kerbal Space Program.
    /// </summary>
    public static class ScienceHelper
    {
        /// <summary>
        /// Gets the science situation for the given vessel.
        /// </summary>
        /// <param name="vessel">The vessel to check.</param>
        /// <returns>The experiment situation based on the vessel's location and state.</returns>
        public static ExperimentSituations GetScienceSituation(Vessel vessel)
        {
            return GetScienceSituation(vessel.altitude, vessel.situation, vessel.mainBody);
        }

        /// <summary>
        /// Determines the science situation based on altitude, vessel situation, and celestial body.
        /// </summary>
        /// <param name="altitude">The vessel's altitude.</param>
        /// <param name="situation">The vessel's current situation.</param>
        /// <param name="body">The celestial body the vessel is on or near.</param>
        /// <returns>The corresponding experiment situation.</returns>
        public static ExperimentSituations GetScienceSituation(double altitude, Vessel.Situations situation, CelestialBody body)
        {
            CelestialBodyScienceParams scienceParams = body.scienceValues;

            // Determine the science situation based on the vessel's state and location.
            switch (situation)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.PRELAUNCH:
                    return ExperimentSituations.SrfLanded;

                case Vessel.Situations.SPLASHED:
                    return ExperimentSituations.SrfSplashed;

                default:
                    if (body.atmosphere)
                    {
                        if (altitude <= scienceParams.flyingAltitudeThreshold)
                            return ExperimentSituations.FlyingLow;
                        else if (altitude <= body.atmosphereDepth)
                            return ExperimentSituations.FlyingHigh;
                    }

                    if (altitude <= scienceParams.spaceAltitudeThreshold)
                        return ExperimentSituations.InSpaceLow;

                    return ExperimentSituations.InSpaceHigh;
            }
        }

        /// <summary>
        /// Retrieves the science multiplier for the given vessel.
        /// </summary>
        /// <param name="vessel">The vessel to check.</param>
        /// <returns>The science multiplier based on the vessel's current science situation.</returns>
        public static float GetScienceMultiplier(Vessel vessel)
        {
            ExperimentSituations situation = GetScienceSituation(vessel);
            return GetScienceMultiplier(situation, vessel.mainBody);
        }

        /// <summary>
        /// Retrieves the science multiplier for a specific science situation and celestial body.
        /// </summary>
        /// <param name="situation">The science situation.</param>
        /// <param name="body">The celestial body.</param>
        /// <returns>The science multiplier for the given situation and body.</returns>
        public static float GetScienceMultiplier(ExperimentSituations situation, CelestialBody body)
        {
            CelestialBodyScienceParams scienceParams = body.scienceValues;

            // Retrieve the science multiplier based on the situation.
            return situation switch
            {
                ExperimentSituations.SrfLanded => scienceParams.LandedDataValue,
                ExperimentSituations.SrfSplashed => scienceParams.SplashedDataValue,
                ExperimentSituations.FlyingLow => scienceParams.FlyingLowDataValue,
                ExperimentSituations.FlyingHigh => scienceParams.FlyingHighDataValue,
                ExperimentSituations.InSpaceLow => scienceParams.InSpaceLowDataValue,
                ExperimentSituations.InSpaceHigh => scienceParams.InSpaceHighDataValue,
                _ => 1 // Default multiplier
            };
        }

        /// <summary>
        /// Retrieves the science subject for a given experiment name and vessel.
        /// </summary>
        /// <param name="name">The name of the experiment.</param>
        /// <param name="vessel">The vessel to check.</param>
        /// <returns>The corresponding science subject.</returns>
        public static ScienceSubject GetScienceSubject(string name, Vessel vessel)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(name);
            if (experiment == null)
                return null;

            ExperimentSituations situation = GetScienceSituation(vessel);
            CelestialBody body = vessel.mainBody;
            string biome = vessel.LandedOrSplashed ? vessel.landedAt : string.Empty;

            return ResearchAndDevelopment.GetExperimentSubject(experiment, situation, body, biome, biome);
        }
    }
}
