using UnityEngine;
using System.Collections.Generic;

public class UICraftingTab : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Transform uiWorkbenchesParent;
    [SerializeField] private Transform uiRecipesParent;
    [SerializeField] private GameObject uiWorkBenchPrefab;
    [SerializeField] private GameObject uiRecipePrefab;

    [SerializeField] private GameObject uiCraftingWindow;

    private UIWorkBench currentWorkBench;
    #endregion

    public void UpdateWorkBenchWindow() 
    { 
        if (PlayerManager.Instance != null)
        {
            List<WorkBench> workbenches = PlayerManager.Instance.GetOwnedWorkbenches();
            if (workbenches != null)
            {
                foreach (WorkBench workbench in workbenches)
                {
                    GameObject workbenchObject = Instantiate(uiWorkBenchPrefab, uiWorkbenchesParent);
                    UIWorkBench uiWorkBench = workbenchObject.GetComponent<UIWorkBench>();
                    uiWorkBench.SetWorkbench(workbench);
                }
            }
        }
    }

    public void UpdateRecipesWindow() 
    { 
    
    }

    public void SelectWorkBench(UIWorkBench workbench)
    {

    }


}
