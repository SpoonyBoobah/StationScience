using UnityEngine;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class DebugScienceStatus : MonoBehaviour
{
    private ModuleScienceExperiment scienceExperiment;

    private bool previousDeployed;
    private bool previousInoperable;

    public void Awake()
    {
        // Initialize reference to the science experiment module
        scienceExperiment = GetComponent<ModuleScienceExperiment>();

        if (scienceExperiment == null)
        {
            Debug.LogError("[STNSCI-DEBUG-SCI] No ModuleScienceExperiment found on this part.");
            return;
        }

        // Initialize tracking fields
        previousDeployed = scienceExperiment.Deployed;
        previousInoperable = scienceExperiment.Inoperable;

        Debug.Log("[STNSCI-DEBUG-SCI] Initialized and monitoring science experiment status.");
    }

    public void FixedUpdate()
    {
        // Only run if the scienceExperiment is valid
        if (scienceExperiment == null) return;

        // Check for status changes and log only if a change is detected
        CheckStatusChanges();
    }

    private void CheckStatusChanges()
    {
        if (scienceExperiment.Deployed != previousDeployed)
        {
            Debug.Log($"[STNSCI-DEBUG-SCI] Deployed status changed: {scienceExperiment.Deployed}");
            previousDeployed = scienceExperiment.Deployed;
        }

        if (scienceExperiment.Inoperable != previousInoperable)
        {
            Debug.Log($"[STNSCI-DEBUG-SCI] Inoperable status changed: {scienceExperiment.Inoperable}");
            previousInoperable = scienceExperiment.Inoperable;
        }
    }
}
