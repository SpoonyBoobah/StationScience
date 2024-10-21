using UnityEngine;
using KSP;
using System.Reflection;

namespace StationScience
{
    public class SettingsUI : GameParameters.CustomParameterNode
    {
        public override string Title => "Debugging";  // The title that appears in the settings menu

        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;  // Available in any game mode

        public override string Section => "StationScience";  // Section under which the settings appear
        public override string DisplaySection => "StationScience";  // How it displays in the UI
        public override int SectionOrder => 1;  // Order within the section

        // Setting fields
        //[GameParameters.CustomFloatParameterUI("Example Float Setting", minValue = 0f, maxValue = 100f, stepCount = 100)]
        //public float exampleFloatSetting = 50f;

        //[GameParameters.CustomIntParameterUI("Example Int Setting", minValue = 0, maxValue = 10)]
        //public int exampleIntSetting = 5;

        //[GameParameters.CustomStringParameterUI("Example String Setting")]
        //public string exampleStringSetting = "Default Text";

        [GameParameters.CustomParameterUI("Enable Experiment Debugging")]
        public bool expDebugging = false;

        [GameParameters.CustomParameterUI("Enable Science Module Debugging")]
        public bool sciDebugging = false;

        public override bool HasPresets => false;  // Whether your settings have presets

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            // Here you can adjust the default values based on the game's difficulty preset (easy, normal, etc.)
        }

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;  // Determines if the setting is enabled or disabled based on other conditions
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;  // Determines if the setting is interactible
        }
    }
}