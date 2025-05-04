// Assets/Scripts/UI/UITooltipManager.cs
using UnityEngine;
using System.Text; // For StringBuilder
using System.Collections;
using Unity.VisualScripting;

public class UITooltipManager : MonoBehaviour
{

    [SerializeField] private UITooltip tooltipPrefab; // Assign your tooltip UI prefab here
    [SerializeField] private float showDelay = 0.5f; // Delay before showing tooltip
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f); // Offset from mouse cursor

    private UITooltip currentTooltip;
    private Coroutine showCoroutine;
    [SerializeField] private RectTransform canvasRectTransform; // Parent canvas RectTransform

    #region Singleton
    public static UITooltipManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    void Start()
    {
        if (tooltipPrefab == null)
        {
            Debug.LogError("UITooltipManager: Tooltip Prefab is not assigned!");
            enabled = false;
            return;
        }
        // Instantiate the tooltip and disable it
        currentTooltip = Instantiate(tooltipPrefab, transform); // Instantiate under the manager
        currentTooltip.Hide();

        if (canvasRectTransform == null)
        {
            Debug.LogError("UITooltipManager: Could not find parent Canvas RectTransform!");
            enabled = false;
        }
    }

    void Update()
    {
        // Update tooltip position if visible
        if (currentTooltip != null && currentTooltip.gameObject.activeSelf)
        {
            PositionTooltip();
        }
    }

    // Called by UIInventorySlot OnPointerEnter
    public void RequestShowTooltip(object itemData)
    {
        StopCurrentTooltip(); // Stop any previous timers/hiding

        showCoroutine = StartCoroutine(ShowTooltipAfterDelay(itemData));
    }

    // Called by UIInventorySlot OnPointerExit
    public void RequestHideTooltip()
    {
        StopCurrentTooltip();
    }

    private IEnumerator ShowTooltipAfterDelay(object itemData)
    {
        yield return new WaitForSecondsRealtime(showDelay); // Use realtime to avoid pause issues

        string header = "";
        string content = "";
        bool canShow = false;

        // --- Build Tooltip Content Based on Type ---
        if (itemData is Item item)
        {
            header = item.ItemName ?? "Item";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Type: {item.Type}"); // Example type info
            sb.AppendLine($"Weight: {item.Weight:F1}");
            // Add more item-specific details
            if (!string.IsNullOrEmpty(item.GetDescription())) sb.AppendLine($"\n<i>{item.GetDescription()}</i>");
            content = sb.ToString();
            canShow = true;
        }
        else if (itemData is ResourceItem resourceItem)
        {
            if (resourceItem.Resource == null) 
            { 
                yield break; 
            }

            header = resourceItem.Resource.ResourceName ?? "Resource";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Type: {resourceItem.Resource.GetResourceType()}"); // Assuming method exists
            sb.AppendLine($"Quantity: {resourceItem.CurrentStackSize} / {resourceItem.StackSizeMax}");
            sb.AppendLine($"Quality: {resourceItem.Resource.Quality}");
            sb.AppendLine($"Weight (Stack): {resourceItem.Weight:F1}"); // Show stack weight
                                                                        // Add more resource-specific details
            if (!string.IsNullOrEmpty(resourceItem.GetDescription())) 
            { 
                sb.AppendLine($"\n<i>{resourceItem.GetDescription()}</i>"); 
            }
            content = sb.ToString();
            canShow = true;
        }
        else if (itemData is SubComponent subComponent)
        {
            header = subComponent.Name ?? "Sub-Component";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Quality: {subComponent.Quality}");
            sb.AppendLine($"Toughness: {subComponent.Toughness}");
            sb.AppendLine($"Strength: {subComponent.Strength}");
            // Add more subcomponent-specific details
            if (!string.IsNullOrEmpty(subComponent.GetDescription())) 
            {
                sb.AppendLine($"\n<i>{subComponent.GetDescription()}</i>");                                                                                                                               
            }
            content = sb.ToString();
            canShow = true;
        }
        // --- End Content Building ---

        if (canShow)
        {
            currentTooltip.SetText(header, content);
            PositionTooltip(); // Position before showing
            currentTooltip.Show();
        }
        showCoroutine = null; // Mark coroutine as finished
    }

    private void StopCurrentTooltip()
    {
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }
        if (currentTooltip != null) currentTooltip.Hide();
    }

    private void PositionTooltip()
    {
        if (currentTooltip == null || canvasRectTransform == null) 
        { 
            return; 
        }

        Vector2 mousePosition = Input.mousePosition;
        Vector2 anchoredPosition;

        // Convert screen point to canvas local point
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            mousePosition,
            null, // Use Canvas's render camera (or null for Screen Space Overlay)
            out anchoredPosition);

        // Apply offset
        anchoredPosition += offset;

        // Keep tooltip within canvas bounds
        Vector2 tooltipSize = currentTooltip.GetSize();
        Vector2 canvasSize = canvasRectTransform.sizeDelta;

        // Adjust Pivot based on which side of the mouse we place it (simple example: always bottom-right)
        // A more complex pivot adjustment would be needed to flip sides near screen edges
        currentTooltip.GetComponent<RectTransform>().pivot = new Vector2(0, 1); // Top-left pivot

        // Clamp position
        float clampedX = Mathf.Clamp(anchoredPosition.x, -canvasSize.x / 2f, (canvasSize.x / 2f) - tooltipSize.x);
        float clampedY = Mathf.Clamp(anchoredPosition.y, (-canvasSize.y / 2f) + tooltipSize.y, canvasSize.y / 2f);

        currentTooltip.GetComponent<RectTransform>().anchoredPosition = new Vector2(clampedX, clampedY);
    }
}