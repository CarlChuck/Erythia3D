using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentProfile : MonoBehaviour
{
    private EquipmentSlot cuirassSlot;
    private EquipmentSlot greavesSlot;
    private EquipmentSlot vambracesSlot;
    private EquipmentSlot helmSlot;
    private EquipmentSlot hauberkSlot;
    private EquipmentSlot trouserSlot;
    private EquipmentSlot sleevesSlot;
    private EquipmentSlot coifSlot;
    private EquipmentSlot neckSlot;
    private EquipmentSlot waistSlot;
    private EquipmentSlot backSlot;
    private EquipmentSlot bootsSlot;

    private EquipmentSlot ear1Slot;
    private EquipmentSlot ear2Slot;
    private EquipmentSlot finger1Slot;
    private EquipmentSlot finger2Slot;

    private EquipmentSlot primarySlot;
    private EquipmentSlot secondarySlot;
    private EquipmentSlot miningTool;
    private EquipmentSlot woodTool;
    private EquipmentSlot harvestingTool;

    [SerializeField] private EquipmentSlot equipmentSlotPrefab; 
    private Dictionary<ItemType, List<EquipmentSlot>> equipmentSlotsByType = new Dictionary<ItemType, List<EquipmentSlot>>();
    private List<EquipmentSlot> allEquipmentSlots = new List<EquipmentSlot>();

    public event Action OnEquipmentChanged;

    // Reference to the character's inventory for swapping
    [SerializeField] private Inventory associatedInventory; // Assign this!

    void Awake()
    {
        equipmentSlotsByType = new Dictionary<ItemType, List<EquipmentSlot>>();
        allEquipmentSlots = new List<EquipmentSlot>();
    }
    public void SetupEquipmentProfile(Inventory inventory) // Pass inventory reference
    {
        associatedInventory = inventory;
        // Clear previous setup if any
        foreach (var slot in allEquipmentSlots)
        {
            if (slot != null && slot.gameObject != null) Destroy(slot.gameObject);
        }
        equipmentSlotsByType.Clear();
        allEquipmentSlots.Clear();

        // Helper function to create and register slots
        Action<ItemType, Action<EquipmentSlot>> CreateSlot = (type, assigner) => {
            EquipmentSlot newSlot = Instantiate(equipmentSlotPrefab.gameObject, transform).GetComponent<EquipmentSlot>();
            newSlot.gameObject.name = type.ToString() + " Slot";
            newSlot.SetSlot(type); // Set the type the backend slot expects

            if (!equipmentSlotsByType.ContainsKey(type))
            {
                equipmentSlotsByType[type] = new List<EquipmentSlot>();
            }
            // Handle slots allowing multiple items (Ear, Finger)
            equipmentSlotsByType[type].Add(newSlot);
            allEquipmentSlots.Add(newSlot);
            assigner?.Invoke(newSlot); // Assign to specific variable like helmSlot
        };

        // Create all slots using the helper
        CreateSlot(ItemType.Cuirass, s => cuirassSlot = s);
        CreateSlot(ItemType.Greaves, s => greavesSlot = s);
        CreateSlot(ItemType.Vambraces, s => vambracesSlot = s);
        CreateSlot(ItemType.Helm, s => helmSlot = s);
        CreateSlot(ItemType.Hauberk, s => hauberkSlot = s);
        CreateSlot(ItemType.Trousers, s => trouserSlot = s);
        CreateSlot(ItemType.Sleeves, s => sleevesSlot = s);
        CreateSlot(ItemType.Coif, s => coifSlot = s);
        CreateSlot(ItemType.Neck, s => neckSlot = s);
        CreateSlot(ItemType.Waist, s => waistSlot = s);
        CreateSlot(ItemType.Back, s => backSlot = s);
        CreateSlot(ItemType.Boots, s => bootsSlot = s);
        CreateSlot(ItemType.Ear, s => ear1Slot = s);    // First ear slot
        CreateSlot(ItemType.Ear, s => ear2Slot = s);    // Second ear slot
        CreateSlot(ItemType.Finger, s => finger1Slot = s); // First finger slot
        CreateSlot(ItemType.Finger, s => finger2Slot = s); // Second finger slot
        CreateSlot(ItemType.PrimaryHand, s => primarySlot = s);
        CreateSlot(ItemType.SecondaryHand, s => secondarySlot = s);
        CreateSlot(ItemType.MiningTool, s => miningTool = s);
        CreateSlot(ItemType.WoodTool, s => woodTool = s);
        CreateSlot(ItemType.HarvestingTool, s => harvestingTool = s);

        OnEquipmentChanged?.Invoke(); // Initial update
    }
    public void AutoEquipItem(Item newItem)
    {

    }
    public Item EquipItemToSlot(Item itemToEquip, EquipmentSlot targetSlot)
    {
        if (itemToEquip == null || targetSlot == null || associatedInventory == null)
        {
            Debug.LogError("EquipItemToSlot: Invalid input.");
            return null;
        }

        if (!IsItemCompatibleWithSlot(itemToEquip.Type, targetSlot.GetSlotType()))
        {
            Debug.LogWarning($"Item {itemToEquip.Type} ({itemToEquip.Type}) cannot be equipped in slot type {targetSlot.GetSlotType()}");
            return null; // Indicate failure without swapping
        }

        // Check if the item came from inventory and remove it first
        bool removedFromInventory = associatedInventory.RemoveItem(itemToEquip);
        if (!removedFromInventory)
        {
            Debug.LogError($"EquipItemToSlot: Item {itemToEquip.Type} not found in associated inventory.");
            // This case shouldn't happen if drag/drop logic is correct, but good failsafe
            return null;
        }


        // Get the item currently in the target slot
        Item previouslyEquippedItem = targetSlot.GetItemInSlot();

        // Place the new item in the slot
        targetSlot.SetItem(itemToEquip);

        // Try to add the previously equipped item back to inventory
        if (previouslyEquippedItem != null)
        {
            if (!associatedInventory.AddItem(previouslyEquippedItem))
            {
                // Inventory full! Revert the equip.
                Debug.LogWarning($"Inventory full! Could not unequip {previouslyEquippedItem.ItemName}. Reverting equip.");
                targetSlot.SetItem(previouslyEquippedItem); // Put old item back
                associatedInventory.AddItem(itemToEquip); // Put new item back in inventory (should succeed)
                OnEquipmentChanged?.Invoke(); // Trigger UI update for revert
                // Notify player inventory is full
                return null; // Indicate failure
            }
            // Else, item was successfully added to inventory
        }

        // Handle equipping 2H weapons potentially unequipping offhand
        HandleTwoHandedWeapon(itemToEquip, targetSlot);

        // Trigger UI update
        OnEquipmentChanged?.Invoke();

        // Return the item that was replaced (and is now in inventory or null)
        return previouslyEquippedItem;
    }

    // Helper to check compatibility based on your game's rules
    public bool IsItemCompatibleWithSlot(ItemType itemType, ItemType slotType)
    {
        // Exact match is always compatible
        if (itemType == slotType) return true;

        // Weapon/Shield specific rules
        switch (slotType)
        {
            case ItemType.PrimaryHand:
                return itemType == ItemType.Weapon1h || itemType == ItemType.Weapon2h;
            case ItemType.SecondaryHand:
                return itemType == ItemType.Weapon1h || itemType == ItemType.Shield;
                // Add other specific compatibility rules if needed (e.g., tools)
        }

        // Default to incompatible if no specific rule matches
        return false;
    }

    // Special handling for 2H weapons
    private void HandleTwoHandedWeapon(Item equippedItem, EquipmentSlot targetSlot)
    {
        if (equippedItem.Type == ItemType.Weapon2h && targetSlot.GetSlotType() == ItemType.PrimaryHand)
        {
            // Find the secondary hand slot (assuming only one for this logic)
            EquipmentSlot offHand = GetSlotForItemType(ItemType.SecondaryHand); // Get secondary slot
            if (offHand != null && offHand.GetItemInSlot() != null)
            {
                Item offHandItem = offHand.GetItemInSlot();
                offHand.SetItem(null); // Clear the offhand slot

                // Try adding the unequipped offhand item to inventory
                if (!associatedInventory.AddItem(offHandItem))
                {
                    // Inventory full - drop it on the ground? Send to stash? Log error?
                    Debug.LogError($"Inventory full! Could not return unequipped offhand item {offHandItem.ItemName} to inventory.");
                    // Handle this edge case appropriately for your game.
                }
            }
        }
        // Could add logic here: if equipping a shield/1H weapon in offhand, ensure primary isn't 2H.
    }


    // Get a specific slot (useful for UI mapping). Handles multiple slots like fingers/ears.
    // You might need a more robust way (e.g., passing index or specific variable like finger1Slot)
    public EquipmentSlot GetSlotForItemType(ItemType type, int index = 0)
    {
        if (equipmentSlotsByType.TryGetValue(type, out List<EquipmentSlot> slots))
        {
            if (index >= 0 && index < slots.Count)
            {
                return slots[index];
            }
        }
        return null;
    }

    // Get all slots (useful for UI iteration)
    public List<EquipmentSlot> GetAllEquipmentSlots()
    {
        return allEquipmentSlots;
    }
    public int GetTotalWeight()
    {
        int totalWeight = 0;
        foreach (EquipmentSlot itemSlot in allEquipmentSlots)
        {
            if (itemSlot.GetItemInSlot() != null)
            {
                totalWeight += (int)itemSlot.GetItemInSlot().Weight;
            }
        }
        return totalWeight;
    }

    public Item GetItemInSlot(ItemType itemType)
    {
        EquipmentSlot slot = GetSlotForItemType(itemType);
        if (slot != null)
        {
            return slot.GetItemInSlot();
        }
        return null;
    }
}
