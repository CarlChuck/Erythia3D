using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject charListParent;
    [SerializeField] private GameObject playerArmature;
    [SerializeField] private GameObject playerFollowCam;
    [SerializeField] private List<PlayerCharacter> playerCharacters;
    [SerializeField] private PlayerCharacter selectedPlayerCharacter;
    private int accountID;
    private string accountName = "ainianu";
    private string email = "Aini@Erythia";
    private ulong steamID = 1; //TODO set in Awake()
    private string familyName = "";
    [SerializeField] private CharacterModelManager characterModelManager;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private UIManager uiManager;
    public static PlayerManager Instance { get; private set; }
    private void Awake()
    {        
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate instance
            return;
        }
        Instance = this;
        //TODO Steam Set ID
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded; // Subscribe to the event
        Login();
        playerCharacters = new List<PlayerCharacter>();
    }

    private void Login()
    {
        Dictionary<string, object> account = AccountManager.Instance.GetAccountBySteamID(steamID);
        Debug.Log($"Found account: {account}");

        if (account != null)
        {
            Debug.Log($"Auto-logged in with Steam account: {accountName}");
        }
        else
        {
            AccountManager.Instance.CreateNewAccount(accountName, AccountManager.Instance.GenerateRandomPassword(), email, steamID);
            Debug.Log($"Created new account for Steam user: {accountName}");
            account = AccountManager.Instance.GetAccountByUsername(accountName);
        }
        if (account.TryGetValue("AccountID", out object idToPass))
        {
            accountID = (int)idToPass;
            Debug.Log(accountID);
        }
        if (account.TryGetValue("Username", out object nameToPass))
        {
            accountName = nameToPass.ToString();
            Debug.Log(accountName);
        }
    }

    public void SetCharactersList()
    {
        ClearPlayerListExceptSelected();
        List<Dictionary<string, object>> characterList = CharactersManager.Instance.GetCharactersbyAccountID(accountID);
        foreach (Dictionary<string, object> character in characterList)
        {
            int newCharacterID = 0;
            if (character.TryGetValue("CharID", out object newCharID))
            {
                newCharacterID = (int)newCharID;
            }
            if (newCharacterID == 0)
            {
                Debug.Log("No character found");
                continue;
            }
            else if (selectedPlayerCharacter != null)
            {
                if (newCharacterID == selectedPlayerCharacter.GetCharacterID())
                {
                    Debug.Log("Character already exists");
                }
                continue;
            }
            foreach (PlayerCharacter characterCheck in playerCharacters)
            {
                if (characterCheck.GetCharacterID() == newCharacterID)
                {
                    Debug.Log("Character already exists");
                    continue;
                }
            }

            string newCharacterName = "";
            string title = "";
            int zoneID = 1;
            int xLoc = 0;
            int yLoc = 0;
            int zLoc = 0;
            int race = 1;
            int face = 1;
            int gender = 1;
            int combatxp = 0;
            int craftingxp = 0;
            int arcaneexp = 0;
            int spiritxp = 0;
            int veilexp = 0;

            if (familyName == "")
            {
                if (character.TryGetValue("Familyname", out object newFamName))
                {
                    familyName = newFamName.ToString();
                }
            }
            if (character.TryGetValue("Name", out object newCharName))
            {
                newCharacterName = newCharName.ToString();
            }
            if (character.TryGetValue("Title", out object newTitle))
            {
                title = newTitle.ToString();
            }
            if (character.TryGetValue("ZoneID", out object newZoneID))
            {
                zoneID = (int)newZoneID;
            }
            if (character.TryGetValue("XLoc", out object newXLoc))
            {
                xLoc = (int)newXLoc;
            }
            if (character.TryGetValue("YLoc", out object newYLoc))
            {
                yLoc = (int)newYLoc;
            }
            if (character.TryGetValue("ZLoc", out object newZLoc))
            {
                zLoc = (int)newZLoc;
            }
            if (character.TryGetValue("Race", out object newRace))
            {
                race = (int)newRace;
            }
            if (character.TryGetValue("Face", out object newFace))
            {
                face = (int)newFace;
            }
            if (character.TryGetValue("Gender", out object newGender))
            {
                gender = (int)newGender;
            }
            if (character.TryGetValue("CombatExp", out object newCombatExp))
            {
                combatxp = (int)newCombatExp;
            }
            if (character.TryGetValue("CraftingExp", out object newCraftingExp))
            {
                craftingxp = (int)newCraftingExp;
            }
            if (character.TryGetValue("ArcaneExp", out object newArcaneExp))
            {
                arcaneexp = (int)newArcaneExp;
            }
            if (character.TryGetValue("SpiritExp", out object newSpiritExp))
            {
                spiritxp = (int)newSpiritExp;
            }
            if (character.TryGetValue("VeilExp", out object newVeilExp))
            {
                veilexp = (int)newVeilExp;
            }
            PlayerCharacter newCharacter = Instantiate(characterPrefab, charListParent.transform).GetComponent<PlayerCharacter>();
            GameObject charGameObject = Instantiate(characterModelManager.GetCharacterModel(race, gender), newCharacter.transform);
            newCharacter.AddModel(charGameObject);
            playerCharacters.Add(newCharacter);
            newCharacter.SetUpCharacter(newCharacterName, newCharacterID, title, zoneID, race, face, gender, combatxp, craftingxp, arcaneexp, spiritxp, veilexp);
            if (selectedPlayerCharacter == null)
            {
                selectedPlayerCharacter = newCharacter;
                SetSelectedCharacter();
                selectedPlayerCharacter.ActivateModel(true);
                selectedPlayerCharacter.transform.SetParent(playerArmature.transform);
            }
            if (selectedPlayerCharacter != null)
            {
                uiManager.SetupUI(selectedPlayerCharacter);
            }
        }
        Debug.Log(familyName);
    }

    private List<PlayerCharacter> GetCharacters()
    {
        if (playerCharacters == null)
        {
            SetCharactersList();
        }
        return playerCharacters;
    }

    public void OnSetFamilyName(string newFamilyName)
    {
        if (GetCharacters().Count == 0)
        {
            familyName = newFamilyName;
        }
    }

    public string GetFamilyName()
    {
        return familyName;
    }

    public void OnSetAccountName(string newAccountName)
    {
        accountName = newAccountName;
    }

    public void OnCreateCharacter(string characterName, int charRace, int charGender, int charFace)
    {
        if (familyName == "" || characterName == "")
        {
            //Make a popup
            Debug.Log("Character or Family Name cannot be empty");
        }
        else
        {
            CharactersManager.Instance.CreateNewCharacter(accountID, familyName, characterName, null, 1, charRace, charGender, charFace);
            SetCharactersList();
        }
    }

    public void SetSelectedCharacter()
    {
        AccountManager.Instance.SetAccountLastPlayedCharacter(accountID, selectedPlayerCharacter.GetCharacterID());
    }

    public PlayerCharacter GetSelectedPlayerCharacter()
    {
        if (selectedPlayerCharacter == null)
        {
            SetCharactersList();
        }
        return selectedPlayerCharacter;
    }

    private void ClearPlayerListExceptSelected()
    {
        if (selectedPlayerCharacter != null)
        {
            playerCharacters.Remove(selectedPlayerCharacter);
        }
        if (playerCharacters != null)
        {
            foreach (PlayerCharacter character in playerCharacters)
            {
                Destroy(character.gameObject);
            }
            playerCharacters.Clear();
        }
        if (selectedPlayerCharacter != null)
        {
            playerCharacters.Add(selectedPlayerCharacter);
        }
    }

    public void PlayerManagerControlSetActive(bool isActive)
    {
        if (mainCamera != null)
        {
            mainCamera.SetActive(isActive);
        }
        if (playerArmature != null)
        {
            playerArmature.SetActive(isActive);
        }
        if (playerFollowCam != null)
        {
            playerFollowCam.SetActive(isActive);
        }
    }

    private void SetWaypoint()
    {
        ZoneManager zoneManager = FindFirstObjectByType<ZoneManager>();
        if (zoneManager != null)
        {
            Transform waypoint = zoneManager.GetWaypoint();
            if (waypoint != null && playerArmature != null)
            {
                playerArmature.transform.position = waypoint.position;
                playerArmature.transform.rotation = waypoint.rotation;
            }
            else
            {
                Debug.LogWarning("Waypoint or playerArmature is null.");
            }
        }
        else
        {
            Debug.LogError("ZoneManager not found in the scene.");
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetWaypoint(); // Call SetWaypoint when a new scene is loaded
        uiManager.StartHUD();
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe to avoid memory leaks
    }

    public UIManager GetUIManager()
    {
        return uiManager;
    }
}
