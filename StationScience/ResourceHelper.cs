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

    ResourceHelper Class

    Purpose:
    This class provides utility methods for managing and interacting with resources 
    in the Station Science mod for Kerbal Space Program (KSP). It is designed to 
    facilitate the handling of resource consumption, production, and checking 
    for specific resource-related conditions within the mod's context.

    Key Functionalities:
    - Provides methods to get the available amount of a specific resource in a part.
    - Offers utility methods to determine if a part contains a particular resource.
    - Includes methods for calculating resource consumption and production rates.
    - Provides helper functions for interacting with resource containers and checking
      their status.

    Usage:
    - The methods in this class are intended to be used by other classes within the 
      Station Science mod to handle resource-related logic, such as verifying if 
      required resources are present or managing the resource usage during experiments.
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StationScience
{
    public static class ResourceHelper
    {
        // Gets the available amount of a specified resource in a given part
        public static double GetResourceAmount(Part part, string resourceName)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName == resourceName)
                {
                    return resource.amount;
                }
            }
            return 0;
        }

        // Checks if a part contains a specified resource
        public static bool HasResource(Part part, string resourceName)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName == resourceName)
                {
                    return true;
                }
            }
            return false;
        }

        // Calculates the consumption rate of a specified resource in a part
        public static double GetResourceConsumptionRate(Part part, string resourceName)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName == resourceName)
                {
                    return resource.flowState == ResourceFlowState.ALL_VESSEL ? resource.maxAmount / resource.maxResourceAmount : 0;
                }
            }
            return 0;
        }

        // Calculates the production rate of a specified resource in a part
        public static double GetResourceProductionRate(Part part, string resourceName)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName == resourceName)
                {
                    return resource.flowState == ResourceFlowState.ALL_VESSEL ? resource.maxAmount / resource.maxResourceAmount : 0;
                }
            }
            return 0;
        }

        // Checks if a part has a sufficient amount of a specified resource
        public static bool HasSufficientResource(Part part, string resourceName, double requiredAmount)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(resourceName)) throw new ArgumentNullException(nameof(resourceName));

            double availableAmount = GetResourceAmount(part, resourceName);
            return availableAmount >= requiredAmount;
        }

        // Provides a summary of resources in a part, including name and amount
        public static Dictionary<string, double> GetResourceSummary(Part part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            Dictionary<string, double> summary = new Dictionary<string, double>();
            foreach (PartResource resource in part.Resources)
            {
                summary[resource.resourceName] = resource.amount;
            }
            return summary;
        }
    }
}
