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
using UnityEngine;

namespace StationScience
{
    class ResourceHelper
    {
        // Retrieves a specific resource by name from the given part's resource list
        public static PartResource getResource(Part part, string name)
        {
            // Returns the resource from the part's resource list using the name as the key
            return part.Resources.Get(name);
        }

        // Returns the current amount of a specific resource within the part
        public static double getResourceAmount(Part part, string name)
        {
            // Fetch the resource by name
            PartResource res = getResource(part, name);

            // If the resource is not found, return 0
            if (res == null)
                return 0;

            // Return the current amount of the resource
            return res.amount;
        }

        // Returns the maximum amount of a specific resource within the part
        public static double getResourceMaxAmount(Part part, string name)
        {
            // Fetch the resource by name
            PartResource res = getResource(part, name);

            // If the resource is not found, return 0
            if (res == null)
                return 0;

            // Return the maximum amount of the resource
            return res.maxAmount;
        }

        // Sets the maximum amount of a resource, adds the resource to the part if it doesn't exist
        public static PartResource setResourceMaxAmount(Part part, string name, double max)
        {
            // Fetch the resource by name
            PartResource res = getResource(part, name);

            // If the resource doesn't exist and max > 0, create a new resource
            if (res == null && max > 0)
            {
                // Get the resource definition from the library
                var resDef = PartResourceLibrary.Instance.resourceDefinitions[name];

                // If the resource definition doesn't exist, log an error and return null
                if (resDef == null)
                {
                    // Log an error message if resource definition is not found
                    Debug.LogError($"[STNSCI-RES] Error: Cannot add resource '{name}' because it is not defined.");
                    return null;
                }

                // Create a new resource node and add it to the part
                ConfigNode node = new ConfigNode("RESOURCE");
                node.AddValue("name", name);
                node.AddValue("amount", 0); // Set initial amount to 0
                node.AddValue("maxAmount", max); // Set the maximum amount to the given max
                res = part.AddResource(node); // Add the resource to the part
            }

            // If the resource exists and max > 0, update its maxAmount
            else if (res != null && max > 0)
            {
                res.maxAmount = max;
            }

            // If the resource exists but max <= 0, hide and remove the resource
            else if (res != null && max <= 0)
            {
                res.isVisible = false; // Hide the resource
                part.Resources.Remove(res); // Remove the resource from the part
            }
            return res;
        }

        // Returns the density of a resource by name
        public static double getResourceDensity(string name)
        {
            // Fetch the resource definition from the library
            var resDef = PartResourceLibrary.Instance.resourceDefinitions[name];

            // If the resource definition exists, return its density
            if (resDef != null)
                return resDef.density;

            // If the resource definition is not found, log an error and return 0
            Debug.LogError($"[STNSCI-RES] Error: Resource definition for '{name}' not found.");
            return 0;
        }

        // Calculates the total demand for resources in a given list (maxAmount - current amount)
        private static double sumDemand(IEnumerable<PartResource> list)
        {
            double ret = 0; // Initialize total demand

            // Iterate over each resource in the list
            foreach (PartResource pr in list)
            {
                // If the resource's flowState is enabled, add its demand (maxAmount - amount) to the total
                if (pr.flowState)
                    ret += (pr.maxAmount - pr.amount);
            }
            return ret; // Return the total demand
        }

        // Calculates the total available amount of resources in a given list
        private static double sumAvailable(IEnumerable<PartResource> list)
        {
            double ret = 0; // Initialize total available amount

            // Iterate over each resource in the list
            foreach (PartResource pr in list)
            {
                // If the resource's flowState is enabled, add its amount to the total
                if (pr.flowState)
                    ret += pr.amount;
            }
            return ret; // Return the total available amount
        }
    }
}