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

    void Start()
    {
        // Hide panel initially, perhaps? Or setup called externally.
        // inventoryPanelRoot?.SetActive(false);
    }

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
        if (targetInventory == null) return;

        // Ensure correct number of slots exist first
        if (uiSlots.Count != targetInventory.GetBagSpace())
        {
            CreateOrUpdateSlots();
        }


        List<Item> items = targetInventory.GetAllItems();

        for (int i = 0; i < uiSlots.Count; i++)
        {
            if (i < items.Count)
            {
                // Pass item and inventory reference to the slot
                uiSlots[i].Setup(items[i], targetInventory);
            }
            else
            {
                // Clear slot if no item exists at this index
                uiSlots[i].Setup(null, targetInventory);
            }
        }
        Debug.Log("UIInventoryPanel updated.");
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