using UnityEngine;

public class BuildOnTarget : MonoBehaviour
{
    public enum BuildState
    {
        None,
        PreviewingPlacement,
        // ConfirmingPlacement could be a brief state or integrated into Previewing if placement is instant
    }

    [Header("Building Configuration")]
    [SerializeField] private GameObject buildablePrefab; // The actual object to build
    [SerializeField] private GameObject previewPrefab;   // The visual preview/ghost of the object
    [SerializeField] private LayerMask groundLayerMask = 1 << 0; // Default to layer 0, adjust as needed

    [Header("State")]
    [SerializeField] private BuildState currentBuildState = BuildState.None; // Keep SerializeField for debugging initially

    private GameObject currentPreviewInstance;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("BuildOnTarget: Main Camera not found! Please ensure a camera is tagged 'MainCamera'.");
            enabled = false; // Disable script if no camera
            return;
        }
    }

    void Update()
    {
        // State-dependent continuous updates (like preview positioning)
        switch (currentBuildState)
        {
            case BuildState.PreviewingPlacement:
                UpdatePreviewPosition();
                break;
            case BuildState.None:
                // Idle state, do nothing continuously related to building
                break;
        }
    }


    public void InitiateBuilding()
    {
        if (currentBuildState != BuildState.None)
        {
            Debug.LogWarning("BuildOnTarget: Already in a build state. Cannot initiate again.");
            return;
        }

        if (buildablePrefab != null && previewPrefab != null)
        {
            EnterBuildMode(buildablePrefab, previewPrefab);
        }
        else
        {
            Debug.LogWarning("BuildOnTarget: BuildablePrefab or PreviewPrefab not assigned in Inspector. Cannot initiate building.");
        }
    }


    public void ConfirmBuildingPlacement()
    {
        if (currentBuildState == BuildState.PreviewingPlacement)
        {
            PlaceActualObject();
        }
        else
        {
            Debug.LogWarning("BuildOnTarget: Not in PreviewingPlacement state. Cannot confirm placement.");
        }
    }


    public void CancelBuildingProcess()
    {
        if (currentBuildState != BuildState.None)
        {
            ExitBuildMode();
        }
        // Optionally, log if called when not in a build state, though often this is fine.
    }

    // --- Internal Building Logic --- 

    private void EnterBuildMode(GameObject objectToBuild, GameObject previewObject)
    {
        if (objectToBuild == null || previewObject == null)
        {
            Debug.LogError("BuildOnTarget: Attempted to enter build mode with null prefabs.");
            return;
        }

        buildablePrefab = objectToBuild; // In case it's called externally with different prefabs
        previewPrefab = previewObject;

        currentBuildState = BuildState.PreviewingPlacement;
        Debug.Log("Entering PreviewingPlacement state.");

        if (currentPreviewInstance == null)
        {
            currentPreviewInstance = Instantiate(previewPrefab);
            // Potentially disable colliders or other components on the preview
            // For example:
            // Collider[] colliders = currentPreviewInstance.GetComponentsInChildren<Collider>();
            // foreach (Collider col in colliders) col.enabled = false;
        }
        else
        {
            // If re-entering build mode with an existing preview (e.g. if EnterBuildMode could be spammed)
            // ensure it uses the correct preview prefab and is active.
            // However, current design with InitiateBuilding guards against this.
            currentPreviewInstance.SetActive(true); 
        }
    }

    private void UpdatePreviewPosition()
    {
        if (currentPreviewInstance == null)
        {
            Debug.LogWarning("BuildOnTarget: currentPreviewInstance is null during UpdatePreviewPosition. Exiting build mode.");
            ExitBuildMode(); 
            return;
        }
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, groundLayerMask))
        {
            currentPreviewInstance.transform.position = hitInfo.point;
            // Optional: Rotate preview to match surface normal:
            // currentPreviewInstance.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            currentPreviewInstance.SetActive(true); // Ensure it's visible
        }
        else
        {
            // If not hitting ground, maybe hide the preview or keep it at last valid spot
            currentPreviewInstance.SetActive(false); // Hide if not on valid ground
        }
    }

    private void PlaceActualObject()
    {
        if (currentPreviewInstance == null || !currentPreviewInstance.activeSelf)
        {
            Debug.LogWarning("BuildOnTarget: Cannot place object, preview is invalid or not active.");
            return; // Don't place if preview wasn't visible (e.g., cursor off ground)
        }

        if (buildablePrefab == null)
        {
            Debug.LogError("BuildOnTarget: BuildablePrefab is null. Cannot place object.");
            ExitBuildMode();
            return;
        }

        Debug.Log($"Placing {buildablePrefab.name} at {currentPreviewInstance.transform.position}");
        Instantiate(buildablePrefab, currentPreviewInstance.transform.position, currentPreviewInstance.transform.rotation);
        
        // Potentially, if you want to build multiple objects without re-pressing 'B':
        // Keep currentPreviewInstance alive for the next placement, just update its prefab if needed.
        // Or destroy it and re-create if `EnterBuildMode` handles it. For now, we exit.
        ExitBuildMode(); 
    }

    private void ExitBuildMode()
    {
        if (currentPreviewInstance != null)
        {
            Destroy(currentPreviewInstance);
            currentPreviewInstance = null;
        }
        currentBuildState = BuildState.None;
        Debug.Log("Exited build mode, returned to None state.");
    }

    // Optional: Add a method to set the buildable object dynamically if needed
    // public void SetBuildable(GameObject newBuildablePrefab, GameObject newPreviewPrefab)
    // {
    //     buildablePrefab = newBuildablePrefab;
    //     previewPrefab = newPreviewPrefab;
    //     Debug.Log($"BuildTarget set to: {buildablePrefab.name}");
    // }
}
