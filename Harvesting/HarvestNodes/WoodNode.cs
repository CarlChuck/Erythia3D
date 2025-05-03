using System.Collections;
using System.Collections.Generic; // Needed for List
using UnityEngine;

public class WoodNode : ResourceNode
{
    [Header("Wood Specific Settings")]
    [SerializeField] private float growthTimePerStage = 180f; // Time for EACH stage to grow/mature
    [SerializeField] private List<GameObject> growthStageModels = new List<GameObject>(); // Models for each stage (Sapling -> Mature)

    private int currentStageIndex = -1;
    private bool isFullyGrown = true;
    private Coroutine growthCoroutine;

    protected override void Awake()
    {
        base.Awake();
        hType = HarvestType.Woodcutting;

        if (growthStageModels == null || growthStageModels.Count == 0)
        {
            Debug.LogError($"WoodNode '{name}' has no growth stage models assigned!", gameObject);
            this.enabled = false; // Disable node if misconfigured
            return;
        }

        // Initialize models - Deactivate all initially
        for (int i = 0; i < growthStageModels.Count; i++)
        {
            if (growthStageModels[i] != null)
            {
                growthStageModels[i].SetActive(false);
            }
            else
            {
                Debug.LogError($"WoodNode '{name}' has a null model at index {i}", gameObject);
            }
        }

        // Start fully grown by default - Activate the last model
        currentStageIndex = growthStageModels.Count - 1;
        if (currentStageIndex >= 0 && growthStageModels[currentStageIndex] != null)
        {
             growthStageModels[currentStageIndex].SetActive(true);
             // No scaling needed
        }
         isFullyGrown = true;
    }

    // OnInteract remains the same

    protected override void OnRespawn()
    {
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
            growthCoroutine = null;
        }

        if (currentStageIndex >= 0 && currentStageIndex < growthStageModels.Count && growthStageModels[currentStageIndex] != null)
        {
            growthStageModels[currentStageIndex].SetActive(false);
        }

        // Reset to first stage (sapling)
        currentStageIndex = 0;
        isFullyGrown = false;

        if (growthStageModels.Count > 0 && growthStageModels[0] != null)
        {
            growthStageModels[0].SetActive(true);
            // No scaling needed
            growthCoroutine = StartCoroutine(StartGrowthProcess());
            Debug.Log($"WoodNode '{name}' respawned as sapling. Starting growth.");
        }
        else
        {
             Debug.LogError($"WoodNode '{name}' cannot respawn - no valid model at stage 0.");
        }
    }

    // Main coroutine managing the progression through stages
    private IEnumerator StartGrowthProcess()
    {
        while (currentStageIndex < growthStageModels.Count - 1) // Loop until the final stage is reached
        {
            // --- Wait for current stage maturation time ---
            yield return new WaitForSeconds(growthTimePerStage);

            // --- Transition to next stage ---
            // Deactivate current
            if (growthStageModels[currentStageIndex] != null)
            {
                growthStageModels[currentStageIndex].SetActive(false);
            }

            // Increment and activate next
            currentStageIndex++;
            if (currentStageIndex < growthStageModels.Count && growthStageModels[currentStageIndex] != null)
            {
                 Debug.Log($"WoodNode '{name}' advancing to stage {currentStageIndex}.");
                growthStageModels[currentStageIndex].SetActive(true);
            }
            else
            {
                 Debug.LogWarning($"WoodNode '{name}' has null or invalid model for stage {currentStageIndex}. Growth may stop.");
                 // Decide how to handle missing intermediate models - stop growth?
                 break; // Stop the coroutine if a model is missing
            }
        }

        // --- Growth Complete ---
        // No extra wait needed for the final stage, it's active now.
        Debug.Log($"WoodNode '{name}' reached final stage {currentStageIndex}. Fully grown.");
        isFullyGrown = true;
        growthCoroutine = null;
    }

    // Removed GrowSingleStage coroutine as it's no longer needed

    // Updated to represent stage completion only
    public float GetGrowthPercentage()
    {
        if (growthStageModels.Count <= 1) return 1f; // If only one stage, it's always 100%
        if (isFullyGrown) return 1f; // If fully grown flag is set

        // Calculate progress based on current stage index relative to the final stage index
        return (float)currentStageIndex / (growthStageModels.Count - 1);
    }
}