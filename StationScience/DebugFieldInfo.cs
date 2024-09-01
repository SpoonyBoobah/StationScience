using System.Collections.Generic;
using UnityEngine;

namespace StationScience
{
    public class DebugFieldInfo : PartModule
    {
        private Dictionary<BaseField, (PartModule module, bool initialStatus)> fieldActiveStatus;

        private void Start()
        {
            fieldActiveStatus = new Dictionary<BaseField, (PartModule, bool)>();

            // Gather fields from the part itself
            if (part.Fields.Count > 0)
            {
                foreach (BaseField bf in part.Fields)
                {
                    // Store the initial active status
                    fieldActiveStatus[bf] = (null, bf.guiActive);

                    // Log the initial state of the fields
                    Debug.Log($"[STNSCI-DBUG-INITIAL] Part: {part.name}, Field Name: {bf.name}, GUI Name: {bf.guiName}, Value: {bf.GetValue(bf.host)}, Active: {bf.guiActive}");
                }
            }

            // Gather fields from part modules
            if (part.Modules.Count > 0)
            {
                foreach (PartModule pm in part.Modules)
                {
                    if (pm.Fields.Count > 0)
                    {
                        foreach (BaseField bf in pm.Fields)
                        {
                            // Store the initial active status for module fields
                            fieldActiveStatus[bf] = (pm, bf.guiActive);

                            // Log the initial state of the module fields
                            Debug.Log($"[STNSCI-DBUG-INITIAL] Module: {pm.moduleName}, Part: {part.name}, Field Name: {bf.name}, GUI Name: {bf.guiName}, Value: {bf.GetValue(bf.host)}, Active: {bf.guiActive}");
                        }
                    }
                }
            }
        }

        private void Update()
        {
            // Create a list of fields to safely iterate over
            var fieldsToCheck = new List<BaseField>(fieldActiveStatus.Keys);

            foreach (var field in fieldsToCheck)
            {
                if (fieldActiveStatus.TryGetValue(field, out var status))
                {
                    bool previousActiveStatus = status.initialStatus;
                    bool currentActiveStatus = field.guiActive;

                    // If the active status has changed, log a debug message
                    if (currentActiveStatus != previousActiveStatus)
                    {
                        var moduleInfo = status.module != null ? $"Module: {status.module.moduleName}" : "Part";
                        Debug.Log($"[STNSCI-DBUG-CHANGE] {moduleInfo}, Part: {part.name}, Field Name: {field.name}, GUI Name: {field.guiName}, New Active Status: {currentActiveStatus}");

                        // Update the stored active status
                        fieldActiveStatus[field] = (status.module, currentActiveStatus);
                    }
                }
            }
        }
    }
}
