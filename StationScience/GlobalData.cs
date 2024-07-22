using System;
using System.Collections.Generic;

namespace StationScience
{
    // Represents the progress of an experiment with details about its current status.
    [Serializable]
    public class ExperimentProgress
    {
        // The name of the experiment.
        public string experimentName;

        // The current progress of the experiment, represented as a percentage (0.0 to 1.0).
        public double progress;

        // The timestamp of when the experiment progress was last updated.
        public DateTime lastUpdateTime;

        // Initializes a new instance of the ExperimentProgress class with the given experiment name and initial progress.
        public ExperimentProgress(string name, double progress)
        {
            this.experimentName = name;
            this.progress = progress;
            this.lastUpdateTime = DateTime.Now;
        }
    }

    // Provides global access to experiment progress data, allowing persistence across vessel unloads and game sessions.
    public static class GlobalData
    {
        // Dictionary storing the progress of experiments. The key is the experiment name, and the value is an ExperimentProgress object.
        public static Dictionary<string, ExperimentProgress> ExperimentProgressData = new Dictionary<string, ExperimentProgress>();

        // Saves or updates the progress of a specific experiment in the global dictionary.
        // If the experiment already exists, its progress is updated. Otherwise, a new entry is created.
        public static void SaveExperimentProgress(string name, double progress)
        {
            if (ExperimentProgressData.ContainsKey(name))
            {
                // Update existing experiment progress
                ExperimentProgressData[name].progress = progress;
                ExperimentProgressData[name].lastUpdateTime = DateTime.Now;
            }
            else
            {
                // Create a new entry for the experiment
                ExperimentProgressData[name] = new ExperimentProgress(name, progress);
            }
        }

        // Retrieves the progress information for a specific experiment from the global dictionary.
        // Returns null if the experiment is not found.
        public static ExperimentProgress GetExperimentProgress(string name)
        {
            // Attempt to get the experiment progress from the dictionary
            ExperimentProgressData.TryGetValue(name, out var progress);
            return progress;
        }
    }
}
