using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // If using layout groups

public class UIInventoryPanel : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanelRoot; // The parent panel GameObject
    [SerializeField] private Transform slotsParent;      // The GameObject with the Grid Layout Group
    [SerializeField] private GameObject inventorySlotPrefab; // Prefab for UIInventorySlot

    private Inventory targetInventory;
    private List<UIInventorySlot> uiSlots = new List<UIInventorySlot>();


    public void Setup(Inventory inventory)
    {
        targetInventory = inventory;
        if (targetInventory == null || slotsParent == null || inventorySlotPrefab == null)
        {
            Debug.LogError("UIInventoryPanel: Missing references!");
            gameObject.SetActive(false);
            return;
        }

        // Subscribe to inventory changes
        targetInventory.OnInventoryChanged -= UpdateDisplay; // Unsubscribe first (safety)
        targetInventory.OnInventoryChanged += UpdateDisplay;

        CreateOrUpdateSlots();
        UpdateDisplay(); // Initial population

        // inventoryPanelRoot?.SetActive(true); // Show panel after setup
    }

    void OnDestroy()
    {
        // Unsubscribe when UI panel is destroyed
        if (targetInventory != null)
        {
            targetInventory.OnInventoryChanged -= UpdateDisplay;
        }
    }

    void CreateOrUpdateSlots()
    {
        int requiredSlots = targetInventory.GetBagSpace();
        int currentSlots = uiSlots.Count;

        // Add missing slots
        for (int i = currentSlots; i < requiredSlots; i++)
        {
            GameObject slotGO = Instantiate(inventorySlotPrefab, slotsParent);
            UIInventorySlot uiSlot = slotGO.GetComponent<UIInventorySlot>();
            if (uiSlot != null)
            {
                uiSlots.Add(uiSlot);
            }
            else
            {
                Debug.LogError("Inventory Slot Prefab missing UIInventorySlot script!");
                Destroy(slotGO); // Cleanup invalid prefab instance
            }
        }

        // Remove excess slots (if bag space decreased)
        for (int i = requiredSlots; i < currentSlots; i++)
        {
            if (uiSlots.Count > requiredSlots) // Check needed as we remove from list
            {
                int lastIndex = uiSlots.Count - 1;
                Destroy(uiSlots[lastIndex].gameObject);
                uiSlots.RemoveAt(lastIndex);
            }
        }
    }

    // Called by the OnInventoryChanged event
    public void UpdateDisplay()
    {
        if (targetInventory == null || uiSlots == null)
        {
            Debug.LogError("UIInventoryPanel: UpdateDisplay called but targetInventory or uiSlots is null.");
            return;
        }

        // Ensure correct number of UI slots exist based on bag space
        if (uiSlots.Count != targetInventory.GetBagSpace())
        {
            Debug.Log($"Inventory bag space ({targetInventory.GetBagSpace()}) differs from UI slots ({uiSlots.Count}). Recreating slots.");
            CreateOrUpdateSlots();
        }

        // Get all item types from the inventory
        List<Item> items = targetInventory.GetAllItems();
        List<ResourceItem> resourceItems = targetInventory.GetAllResourceItems();
        List<SubComponent> subComponents = targetInventory.GetAllSubComponents();

        int currentItemIndex = 0;
        int currentResourceIndex = 0;
        int currentSubComponentIndex = 0;

        // Iterate through available UI slots and populate them sequentially
        for (int i = 0; i < uiSlots.Count; i++)
        {
            UIInventorySlot currentUISlot = uiSlots[i];
            if (currentUISlot == null) continue; // Skip if slot somehow became null

            // Try to place an Item
            if (currentItemIndex < items.Count)
            {
                currentUISlot.DisplayItem(items[currentItemIndex]);
                currentItemIndex++;
            }
            // Else, try to place a ResourceItem
            else if (currentResourceIndex < resourceItems.Count)
            {
                currentUISlot.DisplayResource(resourceItems[currentResourceIndex]);
                currentResourceIndex++;
            }
            // Else, try to place a SubComponent
            else if (currentSubComponentIndex < subComponents.Count)
            {
                currentUISlot.DisplaySubComponent(subComponents[currentSubComponentIndex]);
                currentSubComponentIndex++;
            }
            // Else, this slot is empty
            else
            {
                currentUISlot.Clear();
            }
        }

        // Log if not all items from inventory could be displayed (bag space < total items)
        int totalInventoryItems = items.Count + resourceItems.Count + subComponents.Count;
        if (totalInventoryItems > uiSlots.Count)
        {
            Debug.LogWarning($"UIInventoryPanel: Inventory contains {totalInventoryItems} items, but only {uiSlots.Count} UI slots are available based on bag space.");
        }

        Debug.Log("UIInventoryPanel updated display with Items, ResourceItems, and SubComponents.");
    }

    public void TogglePanel()
    {
        if (inventoryPanelRoot != null)
        {
            bool isActive = !inventoryPanelRoot.activeSelf;
            inventoryPanelRoot.SetActive(isActive);
            if (isActive) UpdateDisplay(); // Refresh when opened
        }
    }
}