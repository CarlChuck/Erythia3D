using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIWorkBench : MonoBehaviour
{
    [SerializeField] private WorkBench workbenchReference;
    [SerializeField] private Image workbenchIcon;
    [SerializeField] private TMP_Text workbenchName;

    [SerializeField] private GameObject highlighted;
    [SerializeField] private GameObject selected;

    public void SetWorkbench(WorkBench workbench)
    {
        workbenchReference = workbench;
        // Update the UI elements with the workbench data
        // For example, set the name, icon, etc.
    }

    public WorkBench GetWorkBench()
    {
        return workbenchReference;
    }
}
