using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems; // Required for EventSystem access

public class UIManager : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerStatBlock playerCharacter;

    [Header("HUD Elements")]
    [SerializeField] private GameObject hudSection;
    [SerializeField] private UIHealthBar healthBar;

    [Header("Character Window Elements")]
    [SerializeField] private UIInventoryPanel inventoryPanel;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Transform statsContainer;
    [SerializeField] private GameObject statDisplayPrefab;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text healthTextMax;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text manaTextMax;
    private List<StatDisplayUI> statDisplays = new List<StatDisplayUI>();

    [Header("UI Tabs & Windows")]
    [SerializeField] private GameObject topBar;
    [SerializeField] private GameObject characterWindow;
    [SerializeField] private GameObject skillsWindow;
    [SerializeField] private GameObject craftingWindow;
    [SerializeField] private GameObject rosterWindow;
    [SerializeField] private GameObject socialWindow;

    private bool isInitialized = false;
    private GameObject currentlyOpenWindow = null; // Track the currently open main window


    #region Initialization
    private void Start()
    {
        hudSection.SetActive(false); // Start with HUD hidden
        CloseAllWindowsAndTabs(); // Ensure all windows are closed initially
    }
    public void SetupUI(PlayerStatBlock targetPlayer)
    {
        if (isInitialized)
        {
            CleanupBindings();
        }
        CloseAllWindowsAndTabs(); // Initial state: all closed
        playerCharacter = targetPlayer;
        Inventory playerInventory = playerCharacter.GetInventory();
        if (playerCharacter == null)
        {
            Debug.LogError("UIManager: SetupUI called with null PlayerCharacter!");
            return;
        }
        if (playerInventory == null)
        {
            Debug.LogError($"UIManager: PlayerCharacter '{playerCharacter.GetCharacterName()}' is missing Inventory component!");
            return;
        }

        SetupCharacterWindowStructure();
        UpdateHUD();
        UpdateCharacterWindowStatsList();

        if (inventoryPanel != null)
        {
            inventoryPanel.Setup(playerInventory);
        }
        else
        {
            Debug.LogWarning("UIManager: UIInventoryPanel reference not set in Inspector.");
        }

        playerCharacter.OnVitalsChanged += UpdateHUD;
        playerCharacter.OnStatsChanged += UpdateCharacterWindowStatsList;

        // Ensure character window is initially false, other windows handled by CloseAllWindowsAndTabs
        if (characterWindow != null) 
        { 
            characterWindow.SetActive(false); 
        }

        isInitialized = true;
        this.enabled = true;
        Debug.Log("UIManager: Initialized and event bindings set.");
    }
    private void SetupCharacterWindowStructure()
    {
        if (playerCharacter == null || statsContainer == null || statDisplayPrefab == null) 
        { 
            return; 
        }

        foreach (Transform child in statsContainer) 
        { 
            Destroy(child.gameObject); 
        }
        statDisplays.Clear();

        List<Stat> stats = playerCharacter.GetAllStats();
        if (stats == null) return;

        foreach (Stat stat in stats)
        {
            if (stat == null) 
            { 
                continue; 
            }
            GameObject statInstance = Instantiate(statDisplayPrefab, statsContainer);
            StatDisplayUI displayUI = statInstance.GetComponent<StatDisplayUI>();
            if (displayUI != null)
            {
                displayUI.Setup(stat.gameObject.name, stat);
                statDisplays.Add(displayUI);
            }
            else
            {
                Destroy(statInstance);
                Debug.LogError($"UIManager: StatDisplayPrefab is missing the StatDisplayUI script!");
            }
        }
    }
    #endregion

    #region UI Updates
    void UpdateHUD()
    {
        Debug.Log("UIManager: UpdateHUD called.");
        if (playerCharacter == null) 
        {
            Debug.LogError("UIManager: UpdateHUD called but playerCharacter is null.");
            return;
        }
        float currentHealth = playerCharacter.GetCurrentHealth();
        float maxHealth = playerCharacter.GetMaxHealth();
        float currentMana = playerCharacter.GetCurrentMana();
        float maxMana = playerCharacter.GetMaxMana();
        Debug.Log($"UIManager: Current Health: {currentHealth}, Max Health: {maxHealth}, Current Mana: {currentMana}, Max Mana: {maxMana}");
        if (healthBar != null)
        {
            healthBar.UpdateDisplay(currentHealth, maxHealth, currentMana, maxMana);
        }
    }
    void UpdateCharacterWindowStatsList()
    {
        // Update Name first
        if (characterNameText != null && playerCharacter != null)
        {
            characterNameText.text = playerCharacter.GetCharacterName();
        }

        if (healthText != null) 
        { 
            healthText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentHealth())}"; 
        }
        if (healthTextMax != null) 
        { 
            healthTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxHealth())}"; 
        }
        if (manaText != null) 
        { 
            manaText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentMana())}"; 
        }
        if (manaTextMax != null) 
        { 
            manaTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxMana())}"; 
        }

        if (!isInitialized) return; // Don't proceed if not initialized

        // Only update detailed stats if the character window is the currently open one.
        if (currentlyOpenWindow != characterWindow && characterWindow != null && characterWindow.activeSelf)
        {
            // This case might happen if characterWindow was left active but is not the 'currentlyOpenWindow'
            // For safety, ensure stats are updated if it IS active.
        }
        else if (currentlyOpenWindow != characterWindow)
        {
            Debug.Log("UIManager: Character window not active, skipping detailed stat value updates.");
            return;
        }


        if (statDisplays.Count == 0 && playerCharacter != null && playerCharacter.GetAllStats()?.Count > 0)
        {
            Debug.LogWarning("UIManager: UpdateCharacterWindowStatsList called but structure not set up. Rebuilding.");
            SetupCharacterWindowStructure();
        }

        foreach (StatDisplayUI display in statDisplays)
        {
            display.UpdateValue();
        }
        Debug.Log("UIManager: Character window name and stats updated.");
    }
    #endregion

    public void RequestWindowToggle(TabWindowType windowType, UITabButton requestingButton)
    {
        GameObject targetWindowGameObject = GetWindowObjectByType(windowType);
        if (targetWindowGameObject == null && windowType != TabWindowType.None)
        {
            Debug.LogError($"UIManager: No window configured for TabWindowType.{windowType}");
            return;
        }

        if (windowType == TabWindowType.None || (targetWindowGameObject != null && targetWindowGameObject == currentlyOpenWindow && targetWindowGameObject.activeSelf))
        {
            CloseAllWindowsAndTabs();
        }
        else if (targetWindowGameObject != null)
        {
            CloseAllWindowsAndTabsInternal();

            targetWindowGameObject.SetActive(true);
            currentlyOpenWindow = targetWindowGameObject;
            if (topBar != null) 
            { 
                topBar.SetActive(true); 
            }

            // Handle window-specific updates
            if (windowType == TabWindowType.Character && isInitialized)
            {
                UpdateCharacterWindowStatsList();
            }

            UITabButton tabToSelect = requestingButton ?? FindTabForWindowType(windowType);
            UpdateAllTabLooks(tabToSelect);
        }
    }

    private UITabButton FindTabForWindowType(TabWindowType windowType)
    {
        if (UITabButton.AllRegisteredTabs == null) return null;
        foreach (UITabButton tab in UITabButton.AllRegisteredTabs)
        {
            if (tab.AssociatedWindowType == windowType)
            {
                return tab;
            }
        }
        return null;
    }

    private GameObject GetWindowObjectByType(TabWindowType windowType)
    {
        switch (windowType)
        {
            case TabWindowType.Character: 
                return characterWindow;
            case TabWindowType.Skills: 
                return skillsWindow;
            case TabWindowType.Crafting: 
                return craftingWindow;
            case TabWindowType.Roster: 
                return rosterWindow;
            case TabWindowType.Social: 
                return socialWindow;
            default: return null;
        }
    }

    private void UpdateAllTabLooks(UITabButton selectedButton)
    {
        if (UITabButton.AllRegisteredTabs == null) 
        { 
            return; 
        }
        foreach (UITabButton tab in UITabButton.AllRegisteredTabs)
        {
            tab.SetSelectedLook(tab == selectedButton);
        }
    }
    
    // Closes all windows, top bar, and deselects all tabs visually and in EventSystem
    private void CloseAllWindowsAndTabs()
    {
        CloseAllWindowsAndTabsInternal();
        UpdateAllTabLooks(null); // Ensure all tabs are visually deselected

    }

    // Internal version that just closes windows and top bar
    private void CloseAllWindowsAndTabsInternal()
    {
        if (characterWindow != null) 
        { 
            characterWindow.SetActive(false); 
        }
        if (skillsWindow != null) 
        { 
            skillsWindow.SetActive(false); 
        }
        if (craftingWindow != null) 
        { 
            craftingWindow.SetActive(false); 
        }
        if (rosterWindow != null) 
        { 
            rosterWindow.SetActive(false); 
        }
        if (socialWindow != null) 
        { 
            socialWindow.SetActive(false); 
        }
        if (topBar != null) 
        { 
            topBar.SetActive(false); 
        }

        currentlyOpenWindow = null;

        if (UITooltipManager.Instance != null)
        {
            UITooltipManager.Instance.RequestHideTooltip();
        }
    }

    private void OnDestroy()
    {
        CleanupBindings();
    }
    private void CleanupBindings()
    {
        if (playerCharacter != null && isInitialized)
        {
            playerCharacter.OnVitalsChanged -= UpdateHUD;
            playerCharacter.OnStatsChanged -= UpdateCharacterWindowStatsList;
            Debug.Log("UIManager: Event bindings removed.");
        }
        isInitialized = false;
    }
    public void StartHUD()
    {
        if (hudSection != null)
        {
            hudSection.SetActive(true);
        }
    }
}