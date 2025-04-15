using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIInventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler // No drop handling here
{
    [SerializeField] private Image itemIconImage;   // Assign the child Image used to show the icon
    [SerializeField] private Image backgroundImage; // Optional: for visual feedback

    private Item currentItem;
    private Inventory parentInventory; // Reference to the backend inventory

    public void Setup(Item item, Inventory inventory)
    {
        currentItem = item;
        parentInventory = inventory;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (currentItem != null && itemIconImage != null)
        {
            itemIconImage.sprite = currentItem.GetIcon();
            itemIconImage.color = Color.white; // Make sure it's visible
            itemIconImage.enabled = true;
        }
        else if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear; // Make invisible
            itemIconImage.enabled = false;
        }
    }

    // --- Drag Handling ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentItem != null && UIDragDropManager.Instance != null)
        {
            // Try to start dragging via the manager
            bool started = UIDragDropManager.Instance.StartDragging(currentItem, this, currentItem.GetIcon());
            if (started)
            {
                // Make the item icon in *this* slot invisible during drag
                itemIconImage.color = Color.clear;
            }
        }
        else
        {
            eventData.pointerDrag = null; // Cancel drag if no item
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (UIDragDropManager.Instance != null)
        {
            // Update the managers drag icon position
            UIDragDropManager.Instance.UpdateDragIconPosition(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // The UIDragDropManager handles telling us if the drop failed via OnDragCancelled
        // It determines success based on whether OnDrop was successfully called on a target.
        // We just need to reset the drag state in the manager.
        // Check if it was actually dragging something from this slot potentially
        if (UIDragDropManager.Instance != null && UIDragDropManager.Instance.GetSourceSlot() == this)
        {
            // Check if the drop occurred on a valid target UI element
            // eventData.pointerEnter is the UI element the pointer is over *now*
            bool droppedOnValidTarget = eventData.pointerEnter != null && eventData.pointerEnter.GetComponent<IDropHandler>() != null;

            // If dropped on something that isn't a drop handler OR the item wasn't successfully processed by OnDrop,
            // the manager's StopDragging call below will have 'dropSuccessful' as false.
            // The manager calls OnDragCancelled in that case.

            // Tell the manager to stop, regardless of success. It figures out if it needs to call OnDragCancelled.
            // We cannot reliably know here if the drop logic within OnDrop succeeded (e.g. inventory full).
            // The drop handler (UIEquipmentSlot) is responsible for the backend logic and triggering UI updates.
            // For simplicity, we assume if dropped on *any* drop handler, it *might* succeed.
            // A more robust system might involve the drop handler setting a flag in the manager.
            UIDragDropManager.Instance.StopDragging(droppedOnValidTarget);
        }

        // If the drag was cancelled (handled by OnDragCancelled), the icon visibility is restored there.
        // If successful, the parentInventory.OnInventoryChanged event will cause UpdateDisplay to run later.
    }

    // Called by UIDragDropManager if drop failed or was outside a target
    public void OnDragCancelled()
    {
        // Restore visibility if the item still logically belongs here
        if (currentItem != null && itemIconImage != null)
        {
            // Double check if the item is still in the backend inventory
            // This check might be redundant if backend logic is sound, but adds safety
            if (parentInventory != null && parentInventory.GetAllItems().Contains(currentItem))
            {
                itemIconImage.color = Color.white; // Make visible again
                itemIconImage.enabled = true;
            }
            else
            {
                // Item was somehow removed - ensure display is clear
                UpdateDisplay();
            }

        }
        Debug.Log($"Drag cancelled for slot with item: {currentItem?.GetItemName()}");
    }

    // Required for OnDragCancelled to work if item is successfully dropped
    public void ClearDisplayAfterDrop()
    {
        // Called by UIInventoryPanel after a successful drag originating from this slot
        // Ensures the slot appears empty immediately after dropping elsewhere.
        // The OnInventoryChanged event will formally update it later.
        currentItem = null; // Assume item is gone
        UpdateDisplay();
    }
}