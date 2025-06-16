using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.VisualScripting;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private GameObject spotLight;
    [SerializeField] private GameObject characterWP;
    [SerializeField] private GameObject mainPane;
    [SerializeField] private GameObject charactersPane;
    [SerializeField] private GameObject settingsPane;
    [SerializeField] private Animator animator;

    [SerializeField] private GameObject playButton;
    [SerializeField] private GameObject createCharButton;
    [SerializeField] private GameObject genderSButton;

    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private CharacterCreator characterCreator;
    [SerializeField] private GameObject menuCamera;
    [SerializeField] private GameObject menuCanvas;

    [SerializeField] private TMP_InputField accountNumberField;
    [SerializeField] private TMP_InputField accountNameField;
    [SerializeField] private TMP_InputField accountPasswordField;

    [SerializeField] private GameObject starterPane;

    #region Singleton
    public static MenuManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Consider if MenuManager should persist across scenes
        }
        else if (Instance != this)
        {
            Debug.LogWarning("MenuManager Awake: Another instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }

        // MenuManager is client-side, so UI and camera should generally be active.
        // If NetworkManager is present and this somehow ends up on a dedicated server,
        // specific server-only setup might be handled elsewhere or by disabling this component.
        if (menuCanvas != null) 
        {
            menuCanvas.SetActive(true);
        }
        if (menuCamera != null) 
        {
            menuCamera.SetActive(true);
        }
    }
    #endregion

    private void Start()
    {
        if (playerManager == null)
        {
            playerManager = PlayerManager.LocalInstance;
        }
        
        if (menuCanvas != null && menuCanvas.activeSelf)
        {
            OnAccountLoginPane();
        }
    }


    #region Panes
    public void OnAccountLoginPane()
    {
        animator.SetBool("Menu", false);
        animator.SetBool("Characters", false);
        animator.SetBool("Settings", false);
        animator.SetBool("Account", true);
    }
    public void OnCreateButtonPane()
    {
        animator.SetBool("Menu", false);
        animator.SetBool("Characters", true);
        animator.SetBool("Settings", false);
        spotLight.SetActive(true);
    }
    public void OnSettingsButtonPane()
    {
        animator.SetBool("Menu", false);
        animator.SetBool("Characters", false);
        animator.SetBool("Settings", true);
        OnOpenSettings();
    }
    public void OnMainMenuPane()
    {
        spotLight.SetActive(false);
        animator.SetBool("Menu", true);
        animator.SetBool("Characters", false);
        animator.SetBool("Settings", false);
        animator.SetBool("Account", false);
    }
    #endregion

    public async void OnLoginButton()
    {
        if (playerManager == null)
        {
            playerManager = PlayerManager.LocalInstance;
        }
        string accountNumberText = accountNumberField.text.Trim();
        string accountNameText = accountNameField.text.Trim();
        string accountPasswordText = accountPasswordField.text.Trim();

        int accountNumber = 0;
        bool hasValidAccountNumber = int.TryParse(accountNumberText, out accountNumber) && accountNumber > 0;
        bool hasValidAccountCredentials = !string.IsNullOrEmpty(accountNameText) && !string.IsNullOrEmpty(accountPasswordText);

        if (!hasValidAccountNumber && !hasValidAccountCredentials)
        {
            Debug.LogError("Login failed: Please enter a valid account number or both account name and password.");
            return;
        }

        if (hasValidAccountNumber)
        {
            playerManager.OnStartInitialization(accountNumber, 0, "");
            OnMainMenuPane();
            return;
        }
        else
        {
            // Check if account exists
            var accountData = await AccountManager.Instance.GetAccountByUsernameAsync(accountNameText);
            if (accountData == null)
            {
                Debug.LogError($"Login failed: No account found with username '{accountNameText}'.");
                return;
            }
            // Verify password
            bool passwordValid = await AccountManager.Instance.VerifyPasswordAsync(accountNameText, accountPasswordText);
            if (!passwordValid)
            {
                Debug.LogError("Login failed: Incorrect password.");
                return;
            }
            // Get AccountID
            if (!accountData.TryGetValue("AccountID", out object accountIdObj) || accountIdObj == null)
            {
                Debug.LogError("Login failed: Could not retrieve AccountID from account data.");
                return;
            }
            int accountId = 0;
            if (!int.TryParse(accountIdObj.ToString(), out accountId) || accountId <= 0)
            {
                Debug.LogError("Login failed: Invalid AccountID retrieved from account data.");
                return;
            }
            playerManager.OnStartInitialization(accountId, 0, accountNameText);
            OnMainMenuPane();
            return;
        }
    }
    public void OnCreateCharacter()
    {
        if (characterCreator.GetName() == "")
        {
            Debug.Log("No Name");
            return;
        }
        if (characterCreator.GetRace() == 0)
        {
            Debug.Log("No Race");
            return;
        }
        if (characterCreator.GetGender() == 0)
        {
            Debug.Log("No Gender");
            return;
        }
        if (characterCreator.GetFace() == 0)
        {
            Debug.Log("No Face");
            return;
        }
        playerManager.OnCreateCharacter(characterCreator.GetName(), characterCreator.GetRace(), characterCreator.GetGender(), characterCreator.GetFace());
        OnEnterWorld();
    }
    public async void OnEnterWorld()
    {
        try
        {
            menuCamera.SetActive(false);
            playerManager.PlayerManagerControlSetActive(true);
            
            // Use the new zone loading system through PlayerManager
            await playerManager.SetupAndSpawnSelectedCharacterAsync();
            
            Debug.Log("MenuManager: Character zone loading completed");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MenuManager: Error in OnEnterWorld: {ex.Message}");
            
            // Fallback: Re-enable menu camera if zone loading fails
            menuCamera.SetActive(true);
            playerManager.PlayerManagerControlSetActive(false);
        }
    }
    public void OnExitButton()
    {
        Application.Quit();
    }
    public void OnOpenSettings()
    {
        //TODO open settings
    }
    public void OnStartClient()
    {
        starterPane.SetActive(false);
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
        }
    }
    public void SetFamilyName(string familyName)
    {
        playerManager.OnSetFamilyName(familyName);
    }
    public void OnSelectRace(int raceNumber)
    {
        switch (raceNumber)
        {
            case 1:
                characterCreator.SetRace(1);
                break;
            case 2:
                characterCreator.SetRace(2);
                break;
            case 3:
                characterCreator.SetRace(3);
                break;
            case 4:
                characterCreator.SetRace(4);
                break;
            case 5:
                characterCreator.SetRace(5);
                break;
            case 6:
                characterCreator.SetRace(6);
                break;
            case 7:
                characterCreator.SetRace(7);
                break;
            case 8:
                characterCreator.SetRace(8);
                break;
            case 9:
                characterCreator.SetRace(9);
                break;
            default:
                characterCreator.SetRace(1);
                break;
        }
    }
    public void OnSetGenderS(bool setS)
    {
        if (setS)
        {
            genderSButton.SetActive(true);
        }
        else
        {
            genderSButton.SetActive(false);
        }
    }
    public void OnSetGender(int genderToSet)
    {
        characterCreator.SetGender(genderToSet);
    }
    public void SetCharCreationButton(bool isThereASelectedPlayerCharacter)
    {
        if (isThereASelectedPlayerCharacter)
        {
            playButton.SetActive(true);
            createCharButton.SetActive(false);
        }
        else
        {
            playButton.SetActive(false);
            createCharButton.SetActive(true);
        }
    }
    public void SetPlayerManager(PlayerManager newPlayerManager)
    {
        playerManager = newPlayerManager;
    }   
}
