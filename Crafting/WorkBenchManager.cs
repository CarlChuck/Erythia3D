using UnityEngine;
using System.Collections.Generic;

public class WorkBenchManager : MonoBehaviour
{
    #region Singleton
    public static WorkBenchManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate WorkBenchManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [SerializeField] private List<WorkBench> defaultWorkBenches;
    [SerializeField] private GameObject workBenchPrefab;

    public void InitializeDefaultWorkBenchesFromRecipes(Dictionary<int, List<Recipe>> allRecipesByWorkbenchType)
    {
        if (workBenchPrefab == null)
        {
            Debug.LogError("WorkBench Prefab is not assigned in WorkBenchManager. Cannot initialize default workbenches.");
            return;
        }

        // Clear existing default workbenches before populating
        // Destroy existing GameObjects if they were instantiated
        foreach (WorkBench wb in defaultWorkBenches)
        {
            if (wb != null && wb.gameObject != null)
            {
                Destroy(wb.gameObject);
            }
        }
        defaultWorkBenches.Clear();

        if (allRecipesByWorkbenchType == null)
        {
            Debug.LogWarning("Received null recipes dictionary from CraftingManager. No workbenches will be initialized.");
            return;
        }

        foreach (KeyValuePair<int, List<Recipe>> entry in allRecipesByWorkbenchType)
        {
            int workbenchType = entry.Key;
            List<Recipe> recipesForType = entry.Value;

            GameObject wbObject = Instantiate(workBenchPrefab, transform); // Parent to WorkBenchManager
            WorkBench newWorkBench = wbObject.GetComponent<WorkBench>();

            if (newWorkBench != null)
            {
                newWorkBench.SetWorkbenchType(workbenchType);
                newWorkBench.InitializeRecipes(recipesForType); // These are already Recipe instances
                newWorkBench.name = $"WorkBench_Type{workbenchType}";
                defaultWorkBenches.Add(newWorkBench);
            }
            else
            {
                Debug.LogError($"Failed to get WorkBench component from instantiated prefab for type {workbenchType}. Destroying object.");
                Destroy(wbObject);
            }
        }
        Debug.Log($"WorkBenchManager: Finished initializing {defaultWorkBenches.Count} default workbenches.");
    }
    public void GetAllWorkBenches()
    {
        List<WorkBench> allWorkBenches = new List<WorkBench>();
        foreach (var workBench in defaultWorkBenches)
        {
            allWorkBenches.Add(workBench);
        }
    }
    public WorkBench GetWorkbenchByType(int workbenchType) 
    {
        WorkBench workBench = null;
        foreach (WorkBench newWorkBench in defaultWorkBenches)
        {
            if (newWorkBench.GetWorkbenchType() != workbenchType)
            {
                continue;
            }

            workBench = newWorkBench;
            break;
        }
        return workBench;
    }
}
