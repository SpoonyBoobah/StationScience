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

using System;
using UnityEngine; // Assuming you're using UnityEngine for Vessel and CelestialBody

namespace StationScience
{
    /// <summary>
    /// Provides utility methods for handling science-related calculations and operations
    /// in Kerbal Space Program (KSP).
    /// </summary>
    public static class ScienceHelper
    {
        /// <summary>
        /// Determines the scientific situation of a vessel based on its altitude and situation.
        /// </summary>
        /// <param name="vessel">The vessel to check.</param>
        /// <returns>The scientific situation of the vessel.</returns>
        public static ExperimentSituations GetScienceSituation(Vessel vessel)
        {
            var body = vessel.mainBody;
            return GetScienceSituation(vessel.altitude, vessel.situation, body);
        }

        /// <summary>
        /// Determines the scientific situation based on altitude, vessel situation, and celestial body.
        /// </summary>
        /// <param name="altitude">The altitude of the vessel.</param>
        /// <param name="situation">The current situation of the vessel.</param>
        /// <param name="body">The celestial body the vessel is interacting with.</param>
        /// <returns>The scientific situation.</returns>
        public static ExperimentSituations GetScienceSituation(double altitude, Vessel.Situations situation, CelestialBody body)
        {
            var pars = body.scienceValues; // Science parameters specific to the celestial body

            if (situation == Vessel.Situations.LANDED || situation == Vessel.Situations.PRELAUNCH)
                return ExperimentSituations.SrfLanded; // Vessel is on the surface or prelaunch

            if (situation == Vessel.Situations.SPLASHED)
                return ExperimentSituations.SrfSplashed; // Vessel is splashed down in water

            if (body.atmosphere)
            {
                if (altitude <= pars.flyingAltitudeThreshold)
                    return ExperimentSituations.FlyingLow; // Vessel is flying low in the atmosphere

                if (altitude <= body.atmosphereDepth)
                    return ExperimentSituations.FlyingHigh; // Vessel is flying high in the atmosphere
            }

            if (altitude <= pars.spaceAltitudeThreshold)
                return ExperimentSituations.InSpaceLow; // Vessel is in space at a low altitude

            return ExperimentSituations.InSpaceHigh; // Vessel is in space at a high altitude
        }

        /// <summary>
        /// Gets the science multiplier based on the vessel's current situation and celestial body.
        /// </summary>
        /// <param name="vessel">The vessel to check.</param>
        /// <returns>The science multiplier for the vessel's situation.</returns>
        public static float GetScienceMultiplier(Vessel vessel)
        {
            var body = vessel.mainBody;
            var situation = GetScienceSituation(vessel);
            return GetScienceMultiplier(situation, body);
        }

        /// <summary>
        /// Retrieves the science multiplier for a specific scientific situation and celestial body.
        /// </summary>
        /// <param name="situation">The scientific situation to check.</param>
        /// <param name="body">The celestial body the vessel is interacting with.</param>
        /// <returns>The science multiplier for the given situation and body.</returns>
        public static float GetScienceMultiplier(ExperimentSituations situation, CelestialBody body)
        {
            var pars = body.scienceValues; // Science parameters for the celestial body

            // Return the appropriate science multiplier based on the situation
            return situation switch
            {
                ExperimentSituations.SrfLanded => pars.LandedDataValue,
                ExperimentSituations.SrfSplashed => pars.SplashedDataValue,
                ExperimentSituations.FlyingLow => pars.FlyingLowDataValue,
                ExperimentSituations.FlyingHigh => pars.FlyingHighDataValue,
                ExperimentSituations.InSpaceLow => pars.InSpaceLowDataValue,
                ExperimentSituations.InSpaceHigh => pars.InSpaceHighDataValue,
                _ => 1f // Default multiplier if situation is unknown
            };
        }

        /// <summary>
        /// Retrieves the science subject for a given experiment name and vessel.
        /// </summary>
        /// <param name="experimentName">The name of the experiment.</param>
        /// <param name="vessel">The vessel conducting the experiment.</param>
        /// <returns>The science subject related to the experiment, or null if the experiment is not found.</returns>
        public static ScienceSubject GetScienceSubject(string experimentName, Vessel vessel)
        {
            var experiment = ResearchAndDevelopment.GetExperiment(experimentName); // Get the experiment details
            if (experiment == null) return null; // Return null if the experiment does not exist

            var situation = GetScienceSituation(vessel);
            var body = vessel.mainBody;
            var biome = vessel.LandedOrSplashed ? vessel.landedAt : string.Empty; // Get the biome if landed or splashed

            return ResearchAndDevelopment.GetExperimentSubject(experiment, situation, body, biome, biome); // Get the science subject
        }
    }
}