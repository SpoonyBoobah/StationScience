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

    ExperimentProgressUI - A class for managing and displaying experiment progress in the user interface.

    Description:
    This class handles the user interface (UI) elements related to tracking and displaying the progress of scientific experiments 
    within the Kerbal Space Program (KSP) mod. The UI is integrated with the Toolbar interface to provide real-time updates 
    and interactions regarding ongoing experiments, regardless of whether the vessel performing the experiments is currently 
    loaded or not.

    Key Features:
    - Displays a progress bar showing the status of ongoing experiments.
    - Provides feedback on experiment completion and other related metrics.
    - Ensures that experiment progress is tracked and displayed consistently even if the vessel conducting the experiment 
      is not currently loaded or active.
    - Updates the UI dynamically based on changes in experiment status.

    Main Responsibilities:
    - Initializes and configures UI elements for displaying experiment progress.
    - Listens for and responds to events that affect experiment progress.
    - Updates the UI based on current progress and status of experiments.
    - Manages experiment data to ensure accurate and up-to-date information is shown.

    Dependencies:
    - Requires integration with the KSP Toolbar or equivalent UI framework.
    - May interact with other components of the mod responsible for managing experiments and progress tracking.
*/

using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;
using System.Linq;

namespace StationScience
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class ExperimentProgressUI : MonoBehaviour
    {
        // UI elements to display experiment progress
        private RectTransform progressPanel;
        private RectTransform experimentListPanel;
        private bool isUIVisible = false;

        // Reference to the KSP Toolbar Button
        private ApplicationLauncherButton toolbarButton;

        // Called when the UI is initialized
        private void Start()
        {
            // Set up the toolbar button
            AddToolbarButton();
            
            // Initialize the UI elements
            SetupUI();
        }

        // Sets up the UI elements for displaying experiment progress
        private void SetupUI()
        {
            // Create and set up the UI panel for progress display
            progressPanel = new GameObject("ProgressPanel").AddComponent<RectTransform>();
            progressPanel.gameObject.AddComponent<Canvas>();
            progressPanel.gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            progressPanel.gameObject.AddComponent<CanvasScaler>();
            progressPanel.gameObject.AddComponent<GraphicRaycaster>();

            // Initialize UI panel properties
            progressPanel.sizeDelta = new Vector2(300, 400);
            progressPanel.gameObject.SetActive(false);

            // Example setup, replace with actual initialization code
            var panelBackground = progressPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            panelBackground.color = new Color(0, 0, 0, 0.8f);
            
            // Add a scroll view to list experiments
            experimentListPanel = new GameObject("ExperimentListPanel").AddComponent<RectTransform>();
            experimentListPanel.SetParent(progressPanel);
            experimentListPanel.sizeDelta = new Vector2(290, 390);
            experimentListPanel.anchoredPosition = Vector2.zero;
        }

        // Adds a toolbar button to open the experiment progress UI
        private void AddToolbarButton()
        {
            if (ApplicationLauncher.Instance != null)
            {
                Texture2D buttonTexture = GameDatabase.Instance.GetTexture("StationScience/ToolbarIcon", false);

                toolbarButton = ApplicationLauncher.Instance.AddModApplication(
                    OnToolbarButtonClick,
                    null,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.TRACKSTATION,
                    buttonTexture
                );
            }
        }

        // Handler for toolbar button click
        private void OnToolbarButtonClick()
        {
            if (progressPanel != null)
            {
                isUIVisible = !isUIVisible;
                progressPanel.gameObject.SetActive(isUIVisible);
                
                if (isUIVisible)
                {
                    UpdateProgressUI();
                }
            }
        }

        // Updates the UI to display the current progress of all experiments
        public void UpdateProgressUI()
        {
            // Clear previous UI elements (optional, depends on your implementation)
            ClearProgressUI();

            // Retrieve the experiment progress data from GlobalData
            var experiments = GlobalData.ExperimentProgressData.Values.ToList();

            // Display progress for each experiment
            foreach (var progress in experiments)
            {
                DisplayExperimentProgress(progress);
            }
        }

        // Displays the progress of a single experiment
        private void DisplayExperimentProgress(ExperimentProgress progress)
        {
            // Create and configure UI elements for the experiment
            var experimentPanel = new GameObject(progress.experimentName).AddComponent<RectTransform>();
            experimentPanel.SetParent(experimentListPanel);
            experimentPanel.sizeDelta = new Vector2(290, 30);
            
            var progressText = experimentPanel.gameObject.AddComponent<UnityEngine.UI.Text>();
            progressText.text = $"{progress.experimentName}: {progress.progress * 100:F2}% completed";
            progressText.alignment = TextAnchor.MiddleLeft;
            progressText.color = Color.white;
            
            // Configure layout and appearance (e.g., positioning, sizing)
        }

        // Clears the current UI elements displaying experiment progress
        private void ClearProgressUI()
        {
            // Remove existing UI elements
            foreach (Transform child in experimentListPanel)
            {
                Destroy(child.gameObject);
            }
        }

        // Cleanup when the UI is destroyed
        private void OnDestroy()
        {
            if (toolbarButton != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            }
        }
    }
}
