using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerCharacter playerCharacter;

    [Header("HUD Elements")]
    [SerializeField] private UIHealthBar healthBar;

    [Header("Character Window Elements")]
    [SerializeField] private UIInventoryPanel inventoryPanel;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Transform statsContainer;
    [SerializeField] private GameObject statDisplayPrefab;
    [SerializeField] private GameObject UICanvas;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text healthTextMax;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text manaTextMax;
    private List<StatDisplayUI> statDisplays = new List<StatDisplayUI>();

    [Header("UI Tabs")]
    [SerializeField] private GameObject characterWindow;
    [SerializeField] private GameObject skillsWindow;
    [SerializeField] private GameObject craftingWindow;
    [SerializeField] private GameObject rosterWindow;
    [SerializeField] private GameObject socialWindow;


    private bool isInitialized = false;


    #region Initialization
    public void SetupUI(PlayerCharacter targetPlayer)
    {
        if (isInitialized)
        {
            CleanupBindings();
        }
        CloseAllWindows();
        playerCharacter = targetPlayer;
        Inventory playerInventory = playerCharacter.GetInventory();
        if (playerCharacter == null)
        {
            Debug.LogError("UIManager: SetupUI called with null PlayerCharacter!");
            //Error handling
            return;
        }
        if (playerInventory == null)
        {
            Debug.LogError($"UIManager: PlayerCharacter '{playerCharacter.GetCharacterName()}' is missing Inventory component!");
            return;
        }

        // Initial UI population
        SetupCharacterWindowStructure();    // Creates the stat entries
        UpdateHUD();                        // Sets initial health/mana bars
        // Initial stats AND name update now happens here:
        UpdateCharacterWindowStatsList();

        // Setup Inventory Panel if reference exists
        if (inventoryPanel != null)
        {
            inventoryPanel.Setup(playerInventory);
        }
        else
        {
            Debug.LogWarning("UIManager: UIInventoryPanel reference not set in Inspector.");
        }

        // Subscribe to events
        playerCharacter.OnVitalsChanged += UpdateHUD;
        playerCharacter.OnStatsChanged += UpdateCharacterWindowStatsList;

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
        if (playerCharacter == null || statsContainer == null || statDisplayPrefab == null) return;

        foreach (Transform child in statsContainer) 
        { 
            Destroy(child.gameObject); 
        }
        statDisplays.Clear();

        List<Stat> stats = playerCharacter.GetAllStats();
        if (stats == null) return;

        foreach (Stat stat in stats)
        {
            if (stat == null) continue;
            GameObject statInstance = Instantiate(statDisplayPrefab, statsContainer);
            StatDisplayUI displayUI = statInstance.GetComponent<StatDisplayUI>();
            if (displayUI != null)
            {
                // Just setup the link and the static name part
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

            healthText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentHealth())}";
            healthTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxHealth())}";
            manaText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentMana())}";
            manaTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxMana())}";


        // Only proceed with stats if initialized and window is active (or during setup)
        if (!isInitialized || !characterWindow.activeSelf)
        {
            // If called during SetupUI, isInitialized might be false,
            // or window might be inactive, but we still want to update the internal state.
            if (!isInitialized && statDisplays.Count == 0 && playerCharacter != null && playerCharacter.GetAllStats()?.Count > 0)
            {
                // If called during initial setup and structure is missing, rebuild.
                SetupCharacterWindowStructure();
            }
            else if (!characterWindow.activeSelf && isInitialized)
            {
                // Don't update stat values if window is closed *after* initialization
                // but DO update the name above regardless of window state.
                Debug.Log("UIManager: Character window closed, skipping stat value updates.");
                return;
            }

        }


        // Ensure structure exists if stats are expected
        if (statDisplays.Count == 0 && playerCharacter != null && playerCharacter.GetAllStats()?.Count > 0)
        {
            Debug.LogWarning("UIManager: UpdateCharacterWindowStatsList called but structure not set up. Rebuilding.");
            SetupCharacterWindowStructure(); // Attempt recovery
        }

        // Update individual stat values
        foreach (StatDisplayUI display in statDisplays)
        {
            display.UpdateValue();
        }
        Debug.Log("UIManager: Character window name and stats updated via OnStatsChanged event.");
    }
    #endregion

    public void ToggleCharacterWindow()
    {
        ToggleSpecificWindow(characterWindow, true);
    }
    public void ToggleSkillsWindow()
    {
        ToggleSpecificWindow(skillsWindow);
    }
    public void ToggleCraftingWindow()
    {
        ToggleSpecificWindow(craftingWindow);
    }
    public void ToggleRosterWindow()
    {
        ToggleSpecificWindow(rosterWindow);
    }
    public void ToggleSocialWindow()
    {
        ToggleSpecificWindow(socialWindow);
    }


    #region Helpers
    private void CloseAllWindows()
    {
        if (characterWindow != null) characterWindow.SetActive(false);
        if (skillsWindow != null) skillsWindow.SetActive(false);
        if (craftingWindow != null) craftingWindow.SetActive(false);
        if (rosterWindow != null) rosterWindow.SetActive(false);
        if (socialWindow != null) socialWindow.SetActive(false);

        if (UITooltipManager.Instance != null)
        {
            UITooltipManager.Instance.RequestHideTooltip();
        }
    }
    private void ToggleSpecificWindow(GameObject windowToToggle, bool isCharacterWindow = false)
    {
        if (windowToToggle == null) return;

        bool wasActive = windowToToggle.activeSelf;

        CloseAllWindows(); // Close all windows and the tooltip

        if (!wasActive) // If the window was not active, open it
        {
            windowToToggle.SetActive(true);
            if (isCharacterWindow && isInitialized)
            {
                UpdateCharacterWindowStatsList(); // Update stats only for the character window
            }
        }
        // If it was active, CloseAllWindows() has already taken care of closing it.
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
            playerCharacter.OnStatsChanged -= UpdateCharacterWindowStatsList; // Handles stats & name// 
            Debug.Log("UIManager: Event bindings removed.");
        }
        isInitialized = false;
    }
    public void StartHUD()
    {
        if (UICanvas != null)
        {
            UICanvas.SetActive(true);
        }
    }
    #endregion
}