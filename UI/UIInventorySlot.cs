using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; // Added for TextMeshPro

public class UIInventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image itemIconImage;   // Assign the child Image used to show the icon
    [SerializeField] private TMP_Text quantityText; // Assign the child TextMeshPro UGUI for quantity
    [SerializeField] private Image backgroundImage; // Optional: for visual feedback

    // Store references to the different types of items this slot might hold
    private Item currentItem;
    private ResourceItem currentResourceItem;
    private SubComponent currentSubComponent;

    // --- Display Methods --- Called by UIInventoryPanel.UpdateDisplay ---

    public void DisplayItem(Item item)
    {
        ClearInternalReferences(); // Clear other types
        currentItem = item;

        if (item != null && itemIconImage != null)
        {
            itemIconImage.sprite = item.IconSprite;
            itemIconImage.color = Color.white;
            itemIconImage.enabled = true;
        }
        else
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }

        // Hide quantity text for regular items
        if (quantityText != null)
        {
            quantityText.enabled = false;
        }
        itemIconImage.gameObject.SetActive(true);
    }

    public void DisplayResource(ResourceItem resourceItem)
    {
        ClearInternalReferences();
        currentResourceItem = resourceItem;

        if (resourceItem != null && resourceItem.Resource != null && itemIconImage != null)
        {
            // Use IconLibrary to get the sprite based on resource type
            itemIconImage.sprite = resourceItem.IconSprite;

            // Check if the sprite was successfully retrieved
            if (itemIconImage.sprite != null)
            {
                itemIconImage.color = Color.white;
                itemIconImage.enabled = true;
            }
            else
            {
                 // If icon not found in library, maybe show a default or hide?
                Debug.LogWarning($"Icon not found in IconLibrary for ResourceType: {resourceItem.Resource.GetResourceType()}");
                itemIconImage.color = Color.clear; // Hide if not found
                itemIconImage.enabled = false;
            }
        }
        else
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }

        // Show and update quantity text
        if (quantityText != null)
        {
            if (resourceItem != null && resourceItem.CurrentStackSize > 1) // Only show quantity if > 1
            {
                quantityText.text = resourceItem.CurrentStackSize.ToString();
                quantityText.enabled = true;
            }
            else
            {
                quantityText.enabled = false;
            }
        }
        itemIconImage.gameObject.SetActive(true);
    }

    public void DisplaySubComponent(SubComponent subComponent)
    {
        ClearInternalReferences();
        currentSubComponent = subComponent;

        // ASSUMPTION: SubComponent Template has an IconSprite
        Sprite icon = null;
        if (subComponent?.Template != null)
        {
            icon = subComponent.IconSprite; // Example: Get icon from template
        }

        if (icon != null && itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.color = Color.white;
            itemIconImage.enabled = true;
        }
        else if (subComponent != null && itemIconImage != null) // If no icon, maybe show a default or hide?
        { 
            // Fallback? For now, hide if no specific icon
            itemIconImage.sprite = null; 
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }
        else if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }

        // Hide quantity text for subcomponents
        if (quantityText != null)
        {
            quantityText.enabled = false;
        }
        itemIconImage.gameObject.SetActive(true);
    }

    public void Clear()
    {
        ClearInternalReferences();

        if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }
        if (quantityText != null)
        { 
            quantityText.text = "";
            quantityText.enabled = false;
        }
        itemIconImage.gameObject.SetActive(false); // Hide the icon image
    }

    private void ClearInternalReferences()
    {
        currentItem = null;
        currentResourceItem = null;
        currentSubComponent = null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // ONLY allow dragging if it's a regular Item for now
        if (currentItem != null && currentResourceItem == null && currentSubComponent == null && UIDragDropManager.Instance != null)
        {
            // Try to start dragging via the manager
            bool started = UIDragDropManager.Instance.StartDragging(currentItem, this, currentItem.IconSprite);
            if (started)
            {
                // Make the item icon in *this* slot invisible during drag
                itemIconImage.color = Color.clear;
                if (quantityText != null) quantityText.enabled = false; // Hide quantity too
            }
        }
        else
        {
            Debug.Log("Drag cancelled: Slot does not contain a draggable Item.");
            eventData.pointerDrag = null; // Cancel drag if not a draggable item
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Only update if dragging an Item
        if (currentItem != null && UIDragDropManager.Instance != null && UIDragDropManager.Instance.IsDragging())
        {
            // Update the managers drag icon position
            UIDragDropManager.Instance.UpdateDragIconPosition(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Only process if we were dragging an Item
        if (currentItem != null && UIDragDropManager.Instance != null && UIDragDropManager.Instance.GetSourceSlot() == this)
        {
             // Check if the drop occurred on a valid target UI element
            bool droppedOnValidTarget = eventData.pointerEnter != null && eventData.pointerEnter.GetComponent<IDropHandler>() != null;

            UIDragDropManager.Instance.StopDragging(droppedOnValidTarget);
        }
        // If not dragging an item from this slot, do nothing.
    }

    public void OnDragCancelled()
    {
        // Restore visibility ONLY if it's an item and it still logically belongs here
        if (currentItem != null && itemIconImage != null)
        {
            // TODO: Re-evaluate if parentInventory check is needed/possible here
            // if (parentInventory != null && parentInventory.GetAllItems().Contains(currentItem))
            // For now, assume if cancelled, it returns visually
            itemIconImage.color = Color.white; // Make visible again
            itemIconImage.enabled = true;
            // No quantity for items
            // if (quantityText != null) quantityText.enabled = false;
        }
        // Don't restore visual for ResourceItems or SubComponents as they aren't draggable yet

        Debug.Log($"Drag cancelled for slot.");
    }

    // Required for OnDragCancelled to work if item is successfully dropped
    public void ClearDisplayAfterDrop() // Might need renaming if only for Items
    {
        // Called by UIDragDropManager after a successful drag originating from this slot
        // Ensures the slot appears empty immediately after dropping elsewhere.
        // The OnInventoryChanged event will formally update it later.
        // For now, just clear everything visually
        Clear();
    }

    // --- Tooltip Handling ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Determine which item type is currently displayed
        object itemToShow = null;
        if (currentItem != null) 
        {
            itemToShow = currentItem;
        }
        else if (currentResourceItem != null) 
        {
            itemToShow = currentResourceItem;
        }
        else if (currentSubComponent != null) 
        {
            itemToShow = currentSubComponent;
        }

        // If there's something to show, request the tooltip
        if (itemToShow != null && UITooltipManager.Instance != null)
        {
            Debug.Log($"Pointer Enter on slot with: {itemToShow.GetType().Name}");
            UITooltipManager.Instance.RequestShowTooltip(itemToShow);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Always hide tooltip on exit
        if (UITooltipManager.Instance != null)
        {
            Debug.Log("Pointer Exit from slot");
            UITooltipManager.Instance.RequestHideTooltip();
        }
    }
}