using System;
using System.Collections.Generic;
using UnityEngine;

public class UIEquipmentPanel : MonoBehaviour
{
    [SerializeField] private GameObject equipmentPanelRoot; // Parent panel GameObject

    // Assign these UIEquipmentSlot components in the Inspector
    [Header("UI Slot References")]
    [SerializeField] private UIEquipmentSlot cuirassSlotUI;
    [SerializeField] private UIEquipmentSlot greavesSlotUI;
    [SerializeField] private UIEquipmentSlot vambracesSlotUI;
    [SerializeField] private UIEquipmentSlot helmSlotUI;
    [SerializeField] private UIEquipmentSlot hauberkSlotUI;
    [SerializeField] private UIEquipmentSlot trouserSlotUI;
    [SerializeField] private UIEquipmentSlot sleevesSlotUI;
    [SerializeField] private UIEquipmentSlot coifSlotUI;
    [SerializeField] private UIEquipmentSlot neckSlotUI;
    [SerializeField] private UIEquipmentSlot waistSlotUI;
    [SerializeField] private UIEquipmentSlot backSlotUI;
    [SerializeField] private UIEquipmentSlot bootsSlotUI;
    [SerializeField] private UIEquipmentSlot ear1SlotUI;
    [SerializeField] private UIEquipmentSlot ear2SlotUI;
    [SerializeField] private UIEquipmentSlot finger1SlotUI;
    [SerializeField] private UIEquipmentSlot finger2SlotUI;
    [SerializeField] private UIEquipmentSlot primaryHandSlotUI;
    [SerializeField] private UIEquipmentSlot secondaryHandSlotUI;
    [SerializeField] private UIEquipmentSlot miningToolSlotUI;
    [SerializeField] private UIEquipmentSlot woodToolSlotUI;
    [SerializeField] private UIEquipmentSlot harvestingToolSlotUI;


    private EquipmentProfile targetProfile;
    // Optional: Store UI slots in a list/dictionary if iterating is needed
    private List<UIEquipmentSlot> allUISlots = new List<UIEquipmentSlot>();

    void Start()
    {
        // equipmentPanelRoot?.SetActive(false);
        CollectUISlots(); // Collect assigned slots into the list
    }

    // Helper to gather all assigned UI slots into the list
    private void CollectUISlots()
    {
        allUISlots.Clear();
        if (cuirassSlotUI != null) allUISlots.Add(cuirassSlotUI);
        if (greavesSlotUI != null) allUISlots.Add(greavesSlotUI);
        if (vambracesSlotUI != null) allUISlots.Add(vambracesSlotUI);
        if (helmSlotUI != null) allUISlots.Add(helmSlotUI);
        if (hauberkSlotUI != null) allUISlots.Add(hauberkSlotUI);
        if (trouserSlotUI != null) allUISlots.Add(trouserSlotUI);
        if (sleevesSlotUI != null) allUISlots.Add(sleevesSlotUI);
        if (coifSlotUI != null) allUISlots.Add(coifSlotUI);
        if (neckSlotUI != null) allUISlots.Add(neckSlotUI);
        if (waistSlotUI != null) allUISlots.Add(waistSlotUI);
        if (backSlotUI != null) allUISlots.Add(backSlotUI);
        if (bootsSlotUI != null) allUISlots.Add(bootsSlotUI);
        if (ear1SlotUI != null) allUISlots.Add(ear1SlotUI);
        if (ear2SlotUI != null) allUISlots.Add(ear2SlotUI);
        if (finger1SlotUI != null) allUISlots.Add(finger1SlotUI);
        if (finger2SlotUI != null) allUISlots.Add(finger2SlotUI);
        if (primaryHandSlotUI != null) allUISlots.Add(primaryHandSlotUI);
        if (secondaryHandSlotUI != null) allUISlots.Add(secondaryHandSlotUI);
        if (miningToolSlotUI != null) allUISlots.Add(miningToolSlotUI);
        if (woodToolSlotUI != null) allUISlots.Add(woodToolSlotUI);
        if (harvestingToolSlotUI != null) allUISlots.Add(harvestingToolSlotUI);
    }


    public void Setup(EquipmentProfile profile)
    {
        targetProfile = profile;
        if (targetProfile == null)
        {
            Debug.LogError("UIEquipmentPanel: Target Profile is null!");
            gameObject.SetActive(false);
            return;
        }

        // Subscribe to equipment changes
        targetProfile.OnEquipmentChanged -= UpdateDisplay; // Unsubscribe first (safety)
        targetProfile.OnEquipmentChanged += UpdateDisplay;

        LinkBackendSlotsToUI(); // Link UI slots to their data source
        UpdateDisplay();        // Initial display

        // equipmentPanelRoot?.SetActive(true);
    }

    void OnDestroy()
    {
        // Unsubscribe
        if (targetProfile != null)
        {
            targetProfile.OnEquipmentChanged -= UpdateDisplay;
        }
    }

    // Link the UI slot components to the actual EquipmentSlot data
    void LinkBackendSlotsToUI()
    {
        if (targetProfile == null) return;

        // Helper lambda
        Action<UIEquipmentSlot, ItemType, int> LinkSlot = (uiSlot, type, index) => {
            if (uiSlot != null)
            {
                EquipmentSlot backendSlot = targetProfile.GetSlotForItemType(type, index);
                if (backendSlot != null)
                {
                    uiSlot.Setup(backendSlot, targetProfile);
                }
                else
                {
                    Debug.LogError($"Could not find backend slot for {type} at index {index}!");
                }
            }
        };

        // Link all assigned UI slots
        LinkSlot(helmSlotUI, ItemType.Helm, 0);
        LinkSlot(cuirassSlotUI, ItemType.Cuirass, 0);
        LinkSlot(greavesSlotUI, ItemType.Greaves, 0);
        LinkSlot(vambracesSlotUI, ItemType.Vambraces, 0);
        LinkSlot(finger1SlotUI, ItemType.Finger, 0); // Assuming first Finger slot
        LinkSlot(finger2SlotUI, ItemType.Finger, 1); // Assuming second Finger slot
        LinkSlot(primaryHandSlotUI, ItemType.PrimaryHand, 0); // Assuming first Primary slot
        LinkSlot(secondaryHandSlotUI, ItemType.SecondaryHand, 0); // Assuming first Secondary slot
        LinkSlot(miningToolSlotUI, ItemType.MiningTool, 0);
        LinkSlot(woodToolSlotUI, ItemType.WoodTool, 0);
        LinkSlot(harvestingToolSlotUI, ItemType.HarvestingTool, 0);
        // ... Link ALL other slots similarly, adjusting index for duplicates (Ear, Alt Hand) ...

    }

    // Called by the OnEquipmentChanged event
    public void UpdateDisplay()
    {
        if (targetProfile == null) return;

        // Tell each linked UI slot to update its display
        foreach (var uiSlot in allUISlots)
        {
            if (uiSlot != null) // Check if slot was assigned in Inspector
            {
                uiSlot.UpdateDisplay();
            }

        }
        Debug.Log("UIEquipmentPanel updated.");
    }

    public void TogglePanel()
    {
        if (equipmentPanelRoot != null)
        {
            bool isActive = !equipmentPanelRoot.activeSelf;
            equipmentPanelRoot.SetActive(isActive);
            if (isActive) UpdateDisplay(); // Refresh when opened
        }
    }
}