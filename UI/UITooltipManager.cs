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

            header = resourceItem.Resource.GetResourceType().ToString() ?? "Resource";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Resource: {resourceItem.Resource.ResourceName}");
            sb.AppendLine($"{resourceItem.Resource.SubType}" + $" {resourceItem.Resource.Type}");
            sb.AppendLine($"{resourceItem.Resource.resourceTemplate.Family}");
            sb.AppendLine("");
            sb.AppendLine($"Quality: {resourceItem.Resource.Quality}");
            if (CheckIfResourceIsMineral(resourceItem))
            {
                sb.AppendLine($"Toughness: {resourceItem.Resource.Toughness}");
                sb.AppendLine($"Strength: {resourceItem.Resource.Strength}");
                sb.AppendLine($"Density: {resourceItem.Resource.Density}"); 
                sb.AppendLine($"Aura: {resourceItem.Resource.Aura}");
            }
            else
            {
                sb.AppendLine($"Aura: {resourceItem.Resource.Aura}");
                sb.AppendLine($"Energy: {resourceItem.Resource.Energy}");
                sb.AppendLine($"Protein: {resourceItem.Resource.Protein}");
                sb.AppendLine($"Carbohydrate: {resourceItem.Resource.Carbohydrate}");
                sb.AppendLine($"Flavour: {resourceItem.Resource.Flavour}");
            }
            if (resourceItem.Resource.Type == ResourceType.Coal)
            {
                sb.AppendLine($"Energy: {resourceItem.Resource.Energy}");
            }

            sb.AppendLine($"Quantity: {resourceItem.CurrentStackSize} / {resourceItem.StackSizeMax}");
            sb.AppendLine($"Weight (Stack): {resourceItem.Weight:F1}");
            sb.AppendLine($"Value: ({resourceItem.Resource.Value}) - {resourceItem.Price}");
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
        if (currentTooltip == null || canvasRectTransform == null) return;

        Vector2 mousePosition = Input.mousePosition;
        RectTransform tooltipRect = currentTooltip.GetComponent<RectTransform>();

        // 1. Set Pivot to Top-Right for alignment
        tooltipRect.pivot = new Vector2(1, 1); 

        // 2. Convert mouse position to anchored position within the canvas
        Vector2 cursorAnchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, 
            mousePosition, 
            null, // Use Canvas's assigned camera or null for overlay
            out cursorAnchoredPosition);

        // 3. Define the desired offset (e.g., 5 pixels left of the cursor)
        Vector2 desiredOffset = new Vector2(-5f, 0f); 

        // 4. Calculate the target position for the tooltip's pivot (top-right)
        Vector2 targetPosition = cursorAnchoredPosition + desiredOffset;

        // 5. Get sizes for clamping
        Vector2 tooltipSize = tooltipRect.sizeDelta; // Use current sizeDelta
        Vector2 canvasSize = canvasRectTransform.sizeDelta;
        Vector2 halfCanvasSize = canvasSize * 0.5f;

        // 6. Calculate min/max allowed positions for the pivot (top-right)
        // MinX = left edge of canvas + tooltip width
        float minX = -halfCanvasSize.x + tooltipSize.x; 
        // MaxX = right edge of canvas
        float maxX = halfCanvasSize.x;
        // MinY = bottom edge of canvas + tooltip height
        float minY = -halfCanvasSize.y + tooltipSize.y;
        // MaxY = top edge of canvas
        float maxY = halfCanvasSize.y;

        // 7. Clamp the target position
        float clampedX = Mathf.Clamp(targetPosition.x, minX, maxX);
        float clampedY = Mathf.Clamp(targetPosition.y, minY, maxY);

        // 8. Apply the clamped position
        tooltipRect.anchoredPosition = new Vector2(clampedX, clampedY);
    }

    private bool CheckIfResourceIsMineral(ResourceItem resourceItem)
    {
        if (resourceItem.Resource.resourceTemplate.Family == ResourceFamily.Meat)
        {
            return false;
        }
        else
        {
            return true;
        }

    }
}