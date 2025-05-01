
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WoodNode : ResourceNode
{
    [Header("Wood Specific Settings")]
    [SerializeField] private float growthTime = 180f; // Time to fully grow
    [SerializeField] private Vector3 minScale = new Vector3(0.8f, 0.8f, 0.8f);
    [SerializeField] private Vector3 maxScale = new Vector3(1.1f, 1.1f, 1.1f);
    [SerializeField] private Transform treeModel;

    private Vector3 originalScale;
    private bool isFullyGrown = true;
    private Coroutine growthCoroutine;

    protected override void Awake()
    {
        base.Awake();

        if (treeModel == null)
        {
            treeModel = transform;
        }
        hType = HarvestType.Woodcutting;
        originalScale = treeModel.localScale;
    }

    public override void OnInteract(PlayerCharacter pCharacter)
    {
        base.OnInteract(pCharacter);
    }
    protected override void OnRespawn()
    {
        // Start as a sapling
        treeModel.localScale = minScale;
        isFullyGrown = false;

        // Start growth process
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
        }

        growthCoroutine = StartCoroutine(GrowOverTime());
    }

    private IEnumerator GrowOverTime()
    {
        float elapsedTime = 0f;

        while (elapsedTime < growthTime)
        {
            // Calculate growth progress (0 to 1)
            float growthProgress = elapsedTime / growthTime;

            // Apply scale based on growth
            treeModel.localScale = Vector3.Lerp(minScale, maxScale, growthProgress);

            // Wait for next frame
            yield return null;

            // Update elapsed time
            elapsedTime += Time.deltaTime;
        }

        // Ensure we reach full size
        treeModel.localScale = maxScale;
        isFullyGrown = true;
        growthCoroutine = null;
    }



    public float GetGrowthPercentage()
    {
        if (isFullyGrown) 
        { 
            return 1f; 
        }

        return (treeModel.localScale.y - minScale.y) / (maxScale.y - minScale.y);
    }
}
