using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIEquipmentSlot : MonoBehaviour, IDropHandler // Only needs drop handling
{
    [SerializeField] private Image itemIconImage;   // Assign the child Image used to show the icon
    [SerializeField] private Image backgroundImage; // Optional: background frame
    [SerializeField] private ItemType expectedSlotType; // *** Set this in Inspector for each slot ***

    private EquipmentSlot backendSlot; // Reference to the actual data slot
    private EquipmentProfile parentProfile; // Reference to the backend profile

    public void Setup(EquipmentSlot dataSlot, EquipmentProfile profile)
    {
        backendSlot = dataSlot;
        parentProfile = profile;
        // Sanity check - ensure UI expected type matches backend slot type
        if (backendSlot != null && backendSlot.GetSlotType() != expectedSlotType)
        {
            Debug.LogError($"UIEquipmentSlot Mismatch! UI expects {expectedSlotType} but backend slot is {backendSlot.GetSlotType()} on {gameObject.name}");
        }
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        Item itemInSlot = (backendSlot != null) ? backendSlot.GetItemInSlot() : null;

        if (itemInSlot != null && itemIconImage != null)
        {
            itemIconImage.sprite = itemInSlot.IconSprite;
            itemIconImage.color = Color.white;
            itemIconImage.enabled = true;
        }
        else if (itemIconImage != null)
        {
            itemIconImage.sprite = null;
            itemIconImage.color = Color.clear;
            itemIconImage.enabled = false;
        }
    }

    // --- Drop Handling ---

    public void OnDrop(PointerEventData eventData)
    {
        if (UIDragDropManager.Instance == null || backendSlot == null || parentProfile == null) return;

        Item draggedItem = UIDragDropManager.Instance.GetDraggedItem();
        Component sourceSlotComp = UIDragDropManager.Instance.GetSourceSlot();

        if (draggedItem != null && sourceSlotComp != null)
        {
            // Check if the item came from an Inventory slot
            if (sourceSlotComp is UIInventorySlot sourceInventorySlot)
            {
                Debug.Log($"Attempting to drop {draggedItem.ItemName} from Inventory onto {expectedSlotType} slot.");

                // Check compatibility via EquipmentProfile method
                if (parentProfile.IsItemCompatibleWithSlot(draggedItem.Type, expectedSlotType))
                {
                    // Attempt the equip via the backend profile method
                    // This handles removing from inventory, swapping, adding old to inventory
                    Item previouslyEquipped = parentProfile.EquipItemToSlot(draggedItem, backendSlot);

                    // If EquipItemToSlot returns null AND the dragged item is still in the backendSlot,
                    // it implies the swap failed (e.g., inventory full).
                    // If it returns an item OR null but the dragged item IS in the slot, assume success for drag/drop purposes.
                    bool equipLikelySucceeded = backendSlot.GetItemInSlot() == draggedItem;

                    if (equipLikelySucceeded)
                    {
                        Debug.Log($"Successfully dropped {draggedItem.ItemName} onto {expectedSlotType}. Swapped: {previouslyEquipped?.ItemName ?? "Nothing"}");
                        // Tell the source inventory slot to clear its display immediately
                        sourceInventorySlot.ClearDisplayAfterDrop();
                        // The EquipmentProfile and Inventory events will handle the final UI refresh.
                        // StopDragging(true) will be called by the source slot's OnEndDrag.
                    }
                    else
                    {
                        Debug.LogWarning($"Equip operation failed for {draggedItem.ItemName} onto {expectedSlotType} (likely inventory full on swap).");
                        // Drag will be cancelled by OnEndDrag seeing no success.
                    }
                }
                else
                {
                    Debug.Log($"Item {draggedItem.ItemName} ({draggedItem.Type}) is not compatible with slot {expectedSlotType}.");
                    // Drag will be cancelled.
                }
            }
            // else if (sourceSlotComp is UIEquipmentSlot sourceEquipmentSlot)
            // {
            //     // Logic for dragging from one equipment slot to another (swapping)
            //     // Would need more complex backend methods in EquipmentProfile
            //     Debug.Log("Dragging from equipment to equipment - not implemented yet.");
            // }
        }
        // Note: We don't call StopDragging here. OnEndDrag on the *source* slot handles that.
    }

    // Called by UIDragDropManager if a drag started from here was cancelled
    // (Relevant if implementing dragging *from* equipment slots)
    public void OnDragCancelled()
    {
        // Restore visibility if the item still belongs here
        UpdateDisplay();
        Debug.Log($"Drag cancelled for equipment slot {expectedSlotType}");
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        gameObject.name = $"Slot_{expectedSlotType}";
    }
#endif
}