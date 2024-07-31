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
using System.Linq;
using System.Text;
using UnityEngine;
using System.Collections;

namespace StationScience
{
    public class StationExperiment : ModuleScienceExperiment
    {
        // Constants representing resource names
        public const string EUREKAS = "Eurekas";
        public const string KUARQS = "Kuarqs";
        public const string BIOPRODUCTS = "Bioproducts";
        public const string SOLUTIONS = "Solutions";
    

        // Inner class to represent a requirement with a name and amount
        internal class Requirement
        {
            internal string Name { get; }
            internal float Amount { get; }

            internal Requirement(string name, float amount)
            {
                Name = name;
                Amount = amount;
            }
        }

        // Enumeration to represent the status of the experiment
        public enum Status
        {
            Idle,       // Experiment is idle
            Running,    // Experiment is running
            Finished,  // Experiment is completed and ready to be stored or transmitted for science points.
            BadLocation,// Vessel is in a bad location for the experiment
            Storage,    // Experiment is in storage, meaning the experiment was finished but instead of transmitting the results, in "Storage" the experiment has to be "reset" before doing anything else.
            Inoperable, // Experiment is inoperable (NOTE: Do not think this is used at any point???)
            Starved,     // Experiment is starved of resources (NOTE: Do not think this is used at any point???)
        }

        // Dictionary to hold experiment requirements
        internal Dictionary<string, Requirement> requirements = new();

        // Experiment requirements fields
        [KSPField(isPersistant = false)] public int eurekasRequired;
        [KSPField(isPersistant = false)] public int kuarqsRequired;
        [KSPField(isPersistant = false)] public int bioproductsRequired;
        [KSPField(isPersistant = false)] public int solutionsRequired;
        [KSPField(isPersistant = false)] public float kuarqHalflife;

        // GUI fields for kuarq decay and experiment status
        [KSPField(isPersistant = false, guiName = "#autoLOC_StatSci_Decay", guiUnits = "#autoLOC_StatSci_Decayrate", guiActive = false, guiFormat = "F2")] public float kuarqDecay;
        //IMPORTANT THIS IS THE FIELD THAT PUTS IN THE STATUS IN THE CONTEXT MENU IN-GAME AND ALSO DEFAULT SET THE EXPERIMENT STATUS TO IDLE! IT ALSO SAVES WHATEVER STATE THE EXPERIMENT IS IN FOR NEXT LOAD
        [KSPField(isPersistant = true, guiName = "Status", guiActive = true)] public Status currentStatus = Status.Idle;

        // Persistent fields for experiment progress tracking in KSP contracts
        [KSPField(isPersistant = true)] public float launched = 0;
        [KSPField(isPersistant = true)] public float completed = 0;
        [KSPField(isPersistant = true)] public string last_subjectId = "";

        // Field for specifying required parts
        [KSPField(isPersistant = false)] public string requiredParts = ""; // Comma-separated list of part names

        // Coroutine to periodically update the status and is constantly monitored and managed in real-time, allowing the game to react appropriately to changes in the experiment's state.
        private IEnumerator UpdateStatusCoroutine()
        {
            while (true)
            {
                UpdateStatus();

                // Call status-specific update function
                switch(currentStatus)
                {
                    case Status.Running:
                        UpdateRunning();
                        break;

                    case Status.Finished:
                        UpdateFinished();
                        break;
                }

                yield return new WaitForSeconds(1.0f); // Update every second or adjust as needed
            }
        }

        private void UpdateRunning()
        {
            // Do things that should happen during running stage

            // Definitely treat "Finished" as it's own state rather than checking every tick on every state
            if(Finished())
            {
                SetStatus(Status.Finished); // new "Finished" status
            }
        }

        private void UpdateFinished()
        {
            // Maybe every now and then we nudge the player or something?
        }

        private void SetStatus(Status status)
        {       
        // Nothing to do
        if(status == currentStatus)
            return;

        // If you need to do things when you leave a status do them here

        switch(currentStatus)
        {
            case Status.Idle: // We're leaving idle
                OnExitIdle();
                break;
        }

        currentStatus = status;

        // If you need to do things when you enter a status do them here
        switch(currentStatus)
        {
            case Status.Running:
                OnEnterRunning(); // Do things that should happen when you enter running
                break;
            case Status.Finished:
                OnEnterFinished();
                break;
        }
        }

        private void OnExitIdle()
        {
            // Do things you do when you leave the idle state
        }

        private void OnEnterRunning()
        {
            // Do things you do when you enter the running state
        }

        private void OnEnterFinished()
        {
            // Do things you do when you enter the finish state
        }


    }
}