using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private PlayerCharacter playerCharacter;
    [Header("HUD Elements")]
    [SerializeField] private Image healthBarImage;
    [SerializeField] private Image manaBarImage;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text healthTextMax;
    [SerializeField] private TMP_Text manaTextMax;
    [Header("Character Window Elements")]
    [SerializeField] private GameObject characterWindowPanel;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private Transform statsContainer;
    [SerializeField] private GameObject statDisplayPrefab;
    [SerializeField] private GameObject UICanvas;

    private List<StatDisplayUI> statDisplays = new List<StatDisplayUI>();
    private bool isInitialized = false;


    void UpdateHUD()
    {
        if (!isInitialized || playerCharacter == null) return;

        float currentHealth = playerCharacter.GetCurrentHealth();
        float maxHealth = playerCharacter.GetMaxHealth();
        float currentMana = playerCharacter.GetCurrentMana();
        float maxMana = playerCharacter.GetMaxMana();

        // Update Health Image Fill
        if (healthBarImage != null)
        {
            // Ensure maxHealth is not zero to avoid division by zero
            healthBarImage.fillAmount = (maxHealth > 0) ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        }

        // Update Mana Image Fill
        if (manaBarImage != null)
        {
            // Ensure maxMana is not zero
            manaBarImage.fillAmount = (maxMana > 0) ? Mathf.Clamp01(currentMana / maxMana) : 0f;
        }


    }

    public void SetupUI(PlayerCharacter targetPlayer)
    {
        if (isInitialized) 
        { 
            CleanupBindings(); 
        } 

        playerCharacter = targetPlayer;
        if (playerCharacter == null) 
        { 
            //Error handling
            return; 
        }

        // Initial UI population
        SetupCharacterWindowStructure();    // Creates the stat entries
        UpdateHUD();                        // Sets initial health/mana bars
        // Initial stats AND name update now happens here:
        UpdateCharacterWindowStatsList();

        // Subscribe to events
        playerCharacter.OnVitalsChanged += UpdateHUD;
        playerCharacter.OnStatsChanged += UpdateCharacterWindowStatsList; 

        if (characterWindowPanel != null) { characterWindowPanel.SetActive(false); }
        isInitialized = true;
        this.enabled = true;
        Debug.Log("UIManager: Initialized and event bindings set.");
    }

    void UpdateCharacterWindowStatsList()
    {
        // Update Name first
        if (characterNameText != null && playerCharacter != null)
        {
            characterNameText.text = playerCharacter.GetCharacterName();
        }

        // Update Optional Health Text
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentHealth())}";
        }
        if (healthText != null)
        {
            healthTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxHealth())}";
        }

        // Update Optional Mana Text
        if (manaText != null)
        {
            manaText.text = $"{Mathf.CeilToInt(playerCharacter.GetCurrentMana())}";
        }
        if (manaText != null)
        {
            manaTextMax.text = $"{Mathf.CeilToInt(playerCharacter.GetMaxMana())}";
        }

        // Only proceed with stats if initialized and window is active (or during setup)
        if (!isInitialized || !characterWindowPanel.activeSelf)
        {
            // If called during SetupUI, isInitialized might be false,
            // or window might be inactive, but we still want to update the internal state.
            if (!isInitialized && statDisplays.Count == 0 && playerCharacter != null && playerCharacter.GetAllStats()?.Count > 0)
            {
                // If called during initial setup and structure is missing, rebuild.
                SetupCharacterWindowStructure();
            }
            else if (!characterWindowPanel.activeSelf && isInitialized)
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

    void SetupCharacterWindowStructure()
    {
        if (playerCharacter == null || statsContainer == null || statDisplayPrefab == null) return;

        foreach (Transform child in statsContainer) { Destroy(child.gameObject); }
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

    public void ToggleCharacterWindow()
    {
        if (characterWindowPanel == null) 
        { 
            return; 
        }
        bool isActive = !characterWindowPanel.activeSelf;
        characterWindowPanel.SetActive(isActive);

        if (isActive && isInitialized)
        {
            // Refresh name AND stats when opened
            UpdateCharacterWindowStatsList();
        }
    }

    void OnDestroy()
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
}