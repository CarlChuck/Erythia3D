using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Linq;

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

    private bool isInitialized = false; // Flag for PlayerManager state
    private Task initializationTask; // Task for its own init
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
        SceneManager.sceneLoaded += OnSceneLoaded; // Subscribe to the event
        //TODO Steam Set ID
    }

    private async void Start()
    {
        if (playerCharacters == null)
        {
            playerCharacters = new List<PlayerCharacter>();
        }
        // Start own initialization process
        initializationTask = InitializePlayerManagerAsync();
        await initializationTask; // Wait for own initialization to complete
    }
    // Core initialization sequence for PlayerManager
    private async Task InitializePlayerManagerAsync()
    {
        Debug.Log("PlayerManager Initialization Started...");
        isInitialized = false;

        // 1. Wait for Dependencies (AccountManager, CharactersManager, UIManager)
        // Ensure instances exist
        if (AccountManager.Instance == null || CharactersManager.Instance == null || uiManager == null)
        {
            Debug.LogError("PlayerManager dependencies missing! Waiting...");
            // Wait until dependencies appear (simple yield loop)
            while (AccountManager.Instance == null || CharactersManager.Instance == null || uiManager == null)
            {
                await Task.Yield(); // Wait a frame
            }
            Debug.Log("PlayerManager dependencies found.");
        }

        // Wait for managers to finish their own table initializations
        Debug.Log("Waiting for AccountManager init...");
        await AccountManager.Instance.WaitForInitialization();
        Debug.Log("AccountManager initialized.");

        Debug.Log("Waiting for CharactersManager init...");
        await CharactersManager.Instance.WaitForInitialization();
        Debug.Log("CharactersManager initialized.");

        // 2. Perform Login Asynchronously
        Debug.Log("Performing login...");
        bool loginSuccess = await LoginAsync();
        if (!loginSuccess)
        {
            Debug.LogError("Login failed. PlayerManager initialization halted.");
            // Handle login failure (e.g., show error message, return to main menu)
            return; // Stop initialization
        }
        Debug.Log($"Login successful. AccountID: {accountID}, AccountName: {accountName}");

        // 3. Load Character List Asynchronously
        Debug.Log("Loading character list...");
        await SetCharactersListAsync();
        Debug.Log($"Character list loaded. Found {playerCharacters.Count} characters.");

        // 4. Final setup (e.g., initial UI setup)
        if (selectedPlayerCharacter != null)
        {
            // Make sure UIManager setup is called AFTER character is selected/loaded
            uiManager.SetupUI(selectedPlayerCharacter);
            Debug.Log($"UIManager setup for selected character: {selectedPlayerCharacter.GetCharacterName()}");
        }
        else
        {
            // Handle case where no character exists - maybe prompt creation?
            Debug.Log("No character selected after loading list.");
        }

        isInitialized = true;
        Debug.Log("PlayerManager Initialization Complete.");
        // Optionally invoke an event here if other systems depend on PlayerManager being ready

        if (MenuManager.Instance != null)
        {
            if (selectedPlayerCharacter != null)
            {
                MenuManager.Instance.SetCharCreationButton(true); // Notify MenuManager if needed
            }
            else
            {
                MenuManager.Instance.SetCharCreationButton(false); // Notify MenuManager if needed
            }
        }
    }
    private async Task<bool> LoginAsync()
    {
        try
        {
            Debug.Log($"Attempting login with SteamID: {steamID}");
            Dictionary<string, object> account = await AccountManager.Instance.GetAccountBySteamIDAsync(steamID);

            if (account != null)
            {
                Debug.Log($"Account found via SteamID.");
            }
            else
            {
                Debug.Log($"No account found for SteamID {steamID}. Attempting to create...");
                // Use the ASYNC version and await it
                bool created = await AccountManager.Instance.CreateNewAccountAsync(accountName, AccountManager.Instance.GenerateRandomPassword(), email, steamID);
                if (created)
                {
                    Debug.Log($"Created new account for Steam user: {accountName}. Fetching account info...");
                    // Fetch the newly created account info
                    account = await AccountManager.Instance.GetAccountByUsernameAsync(accountName);
                    if (account == null)
                    {
                        Debug.LogError("Failed to fetch newly created account info!");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to create new account for Steam user: {accountName}");
                    return false; // Failed to create account
                }
            }

            // Extract account info safely
            if (account.TryGetValue("AccountID", out object idObj) && idObj != DBNull.Value)
            {
                accountID = Convert.ToInt32(idObj); // Use Convert for robustness
            }
            else
            {
                Debug.LogError("Could not retrieve AccountID from account data."); return false;
            }

            if (account.TryGetValue("Username", out object nameObj) && nameObj != DBNull.Value)
            {
                accountName = nameObj.ToString();
            }
            else
            {
                Debug.LogError("Could not retrieve Username from account data."); return false;
            }

            // Potentially load family name here if stored on account?
            // if (account.TryGetValue("Familyname", out object famNameObj) && famNameObj != DBNull.Value) {
            //     familyName = famNameObj.ToString();
            // }

            return true; // Login successful
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during LoginAsync: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }
    public async Task SetCharactersListAsync() // Changed to async Task
    {
        if (!isInitialized && initializationTask != null && !initializationTask.IsCompleted)
        {
            Debug.LogWarning("SetCharactersListAsync called before initialization complete. Waiting...");
            await initializationTask; // Ensure initialization is done before proceeding
            if (!isInitialized)
            { // Check again after waiting
                Debug.LogError("Initialization failed, cannot set character list.");
                return;
            }
        }
        else if (!isInitialized)
        {
            Debug.LogError("SetCharactersListAsync called, but manager is not initialized and initialization hasn't started/completed.");
            return; // Should not happen if called after Start completes initializationTask
        }


        if (accountID <= 0)
        {
            Debug.LogError("Cannot set character list: Invalid AccountID.");
            return;
        }

        ClearPlayerListExceptSelected();

        // Use the async version from CharactersManager
        List<Dictionary<string, object>> characterList = await CharactersManager.Instance.GetCharactersbyAccountIDAsync(accountID);

        if (characterList == null)
        {
            Debug.LogError("Failed to retrieve character list from CharactersManager.");
            return; // Exit if query failed
        }

        Debug.Log($"Processing {characterList.Count} character data entries.");

        // We need to Instantiate on the main thread. Use TaskScheduler or process here if sure await resumes on main.
        // For simplicity, assuming await resumes on main thread for now. If issues arise, use marshalling.
        foreach (Dictionary<string, object> characterData in characterList)
        {
            // --- Parsing character data (use TryGetValue with default values) ---
            int newCharacterID = characterData.TryGetValue("CharID", out object idObj) && idObj != DBNull.Value ? Convert.ToInt32(idObj) : 0;
            if (newCharacterID == 0) { Debug.LogWarning("Character data found with invalid CharID 0."); continue; }

            // --- Check if character already loaded (avoid duplicates) ---
            bool alreadyExists = false;
            if (selectedPlayerCharacter != null && selectedPlayerCharacter.GetCharacterID() == newCharacterID)
            {
                Debug.Log($"Character ID {newCharacterID} is already the selected character.");
                // If it's selected, ensure it's in the list, but don't re-add/recreate.
                if (!playerCharacters.Contains(selectedPlayerCharacter))
                {
                    playerCharacters.Add(selectedPlayerCharacter);
                }
                alreadyExists = true;
                // Potentially update selected character's data here if needed
            }
            else if (playerCharacters.Any(pc => pc.GetCharacterID() == newCharacterID))
            { // Use Linq Any for efficiency
                Debug.Log($"Character ID {newCharacterID} already exists in the loaded list.");
                alreadyExists = true;
            }

            if (alreadyExists)
            {
                continue; // Skip to next character in the list
            }


            // --- Extract data ---
            string newCharacterName = characterData.TryGetValue("Name", out object nameObj) && nameObj != DBNull.Value ? nameObj.ToString() : "Unnamed";
            // Load FamilyName ONLY if not already set (e.g., from Account data)
            if (string.IsNullOrEmpty(familyName) && characterData.TryGetValue("Familyname", out object famNameObj) && famNameObj != DBNull.Value)
            {
                familyName = famNameObj.ToString();
            }
            string title = characterData.TryGetValue("Title", out object titleObj) && titleObj != DBNull.Value ? titleObj.ToString() : "";
            int zoneID = characterData.TryGetValue("ZoneID", out object zoneObj) && zoneObj != DBNull.Value ? Convert.ToInt32(zoneObj) : 1;
            int xLoc = characterData.TryGetValue("Xloc", out object xObj) && xObj != DBNull.Value ? Convert.ToInt32(xObj) : 0;
            int yLoc = characterData.TryGetValue("Yloc", out object yObj) && yObj != DBNull.Value ? Convert.ToInt32(yObj) : 0;
            int zLoc = characterData.TryGetValue("Zloc", out object zObj) && zObj != DBNull.Value ? Convert.ToInt32(zObj) : 0;
            int race = characterData.TryGetValue("Race", out object raceObj) && raceObj != DBNull.Value ? Convert.ToInt32(raceObj) : 1;
            int face = characterData.TryGetValue("Face", out object faceObj) && faceObj != DBNull.Value ? Convert.ToInt32(faceObj) : 1;
            int gender = characterData.TryGetValue("Gender", out object genderObj) && genderObj != DBNull.Value ? Convert.ToInt32(genderObj) : 1;
            int combatxp = characterData.TryGetValue("CombatExp", out object combatObj) && combatObj != DBNull.Value ? Convert.ToInt32(combatObj) : 0;
            int craftingxp = characterData.TryGetValue("CraftingExp", out object craftObj) && craftObj != DBNull.Value ? Convert.ToInt32(craftObj) : 0;
            int arcaneexp = characterData.TryGetValue("ArcaneExp", out object arcaneObj) && arcaneObj != DBNull.Value ? Convert.ToInt32(arcaneObj) : 0;
            int spiritxp = characterData.TryGetValue("SpiritExp", out object spiritObj) && spiritObj != DBNull.Value ? Convert.ToInt32(spiritObj) : 0;
            int veilexp = characterData.TryGetValue("VeilExp", out object veilObj) && veilObj != DBNull.Value ? Convert.ToInt32(veilObj) : 0;

            // --- Instantiate and Setup Character ---
            if (characterPrefab == null || charListParent == null || characterModelManager == null)
            {
                Debug.LogError("Missing prefabs or parent references for character instantiation!"); continue;
            }

            Debug.Log($"Instantiating new character object for ID: {newCharacterID}, Name: {newCharacterName}");
            PlayerCharacter newCharacter = Instantiate(characterPrefab, charListParent.transform).GetComponent<PlayerCharacter>();
            if (newCharacter == null) { Debug.LogError("Failed to get PlayerCharacter component from prefab!"); continue; }

            GameObject charModelInstance = characterModelManager.GetCharacterModel(race, gender); // Get model prefab
            if (charModelInstance != null)
            {
                GameObject charGameObject = Instantiate(charModelInstance, newCharacter.transform); // Instantiate model
                newCharacter.AddModel(charGameObject);
            }
            else { Debug.LogWarning($"Could not find character model for Race:{race} Gender:{gender}"); }

            newCharacter.SetUpCharacter(newCharacterName, newCharacterID, title, zoneID, race, face, gender, combatxp, craftingxp, arcaneexp, spiritxp, veilexp);
            playerCharacters.Add(newCharacter); // Add to the list

            // Set as selected if none currently selected
            if (selectedPlayerCharacter == null)
            {
                Debug.Log($"Setting first loaded character as selected: {newCharacterName}");
                selectedPlayerCharacter = newCharacter;
                // SetSelectedCharacter(); // This calls DB, maybe defer or make async? Let's call DB update only when explicitly selecting later.
                selectedPlayerCharacter.ActivateModel(true); // Activate model immediately
                if (playerArmature != null)
                {
                    selectedPlayerCharacter.transform.SetParent(playerArmature.transform, false); // Parent to armature
                }
                else { Debug.LogError("PlayerArmature reference missing!"); }
            }
        }

        // Ensure selected character exists and is in the list after processing
        if (selectedPlayerCharacter != null && !playerCharacters.Contains(selectedPlayerCharacter))
        {
            Debug.LogWarning("Selected character was not found in or added to the character list during processing. Adding now.");
            playerCharacters.Add(selectedPlayerCharacter);
        }
        else if (selectedPlayerCharacter == null && playerCharacters.Count > 0)
        {
            // Fallback: if still no character selected, pick the first one from the populated list
            Debug.LogWarning("No character was selected during load, selecting first from list.");
            selectedPlayerCharacter = playerCharacters[0];
            // SetSelectedCharacter(); // Defer DB update
            selectedPlayerCharacter.ActivateModel(true);
            if (playerArmature != null)
            {
                selectedPlayerCharacter.transform.SetParent(playerArmature.transform, false); // Parent to armature
            }
            else { Debug.LogError("PlayerArmature reference missing!"); }
        }


        Debug.Log($"Finished processing character list. Final count: {playerCharacters.Count}. Selected: {selectedPlayerCharacter?.GetCharacterName() ?? "None"}");
    }

    /*
    public void SetCharactersList()
    {
        ClearPlayerListExceptSelected();
        List<Dictionary<string, object>> characterList = CharactersManager.Instance.GetCharactersbyAccountID(accountID).GetAwaiter().GetResult();
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
    */

    private async Task<List<PlayerCharacter>> GetCharactersAsync()
    {
        // Ensure initialization is complete before returning list
        if (!isInitialized && initializationTask != null && !initializationTask.IsCompleted)
        {
            Debug.LogWarning("GetCharactersAsync called before initialization complete. Waiting...");
            await initializationTask;
        }
        // If list is still somehow null after init, re-initialize (shouldn't happen)
        if (playerCharacters == null)
        {
            playerCharacters = new List<PlayerCharacter>();
            Debug.LogWarning("PlayerCharacters list was null after initialization check, re-initializing.");
            // Re-running SetCharactersListAsync might be needed if initialization failed partially
            // For now, just return the empty list. Consider more robust recovery if needed.
            // await SetCharactersListAsync();
        }
        return playerCharacters;
    }
    public List<PlayerCharacter> GetCharacters()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Synchronous GetCharacters called before PlayerManager is initialized! Returning potentially empty/incomplete list.");
            // Cannot easily await here. Return current state.
            return playerCharacters ?? new List<PlayerCharacter>(); // Return empty list if null
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

    public async void OnCreateCharacter(string characterName, int charRace, int charGender, int charFace)
    {
        if (string.IsNullOrEmpty(familyName) || string.IsNullOrEmpty(characterName))
        {
            Debug.LogError("Character or Family Name cannot be empty");
            // TODO: Show UI popup
            return;
        }

        if (accountID <= 0)
        {
            Debug.LogError("Cannot create character: Invalid AccountID.");
            return;
        }

        Debug.Log($"Attempting to create character: {characterName}");
        // Use await with the async version
        bool created = await CharactersManager.Instance.CreateNewCharacterAsync(accountID, familyName, characterName, null, 1, charRace, charGender, charFace);

        if (created)
        {
            Debug.Log("Character creation successful, reloading character list...");
            // Reload the list asynchronously
            await SetCharactersListAsync();
            // Setup UI for the newly selected character (SetCharactersList should select the first one)
            if (selectedPlayerCharacter != null && uiManager != null)
            {
                uiManager.SetupUI(selectedPlayerCharacter);
            }
        }
        else
        {
            Debug.LogError($"Failed to create character: {characterName}");
            // TODO: Show UI feedback for failure
        }
    }

    public async Task SetSelectedCharacterAsync() // Changed to async
    {
        if (selectedPlayerCharacter == null || accountID <= 0)
        {
            Debug.LogWarning("Cannot set selected character - no character selected or invalid account ID.");
            return;
        }
        Debug.Log($"Setting last played character for Account {accountID} to CharID {selectedPlayerCharacter.GetCharacterID()}");
        // Use the async helper version from AccountManager
        bool success = await AccountManager.Instance.SetAccountLastPlayedCharacterAsync(accountID, selectedPlayerCharacter.GetCharacterID());
        if (!success)
        {
            Debug.LogError("Failed to update last played character in database.");
        }
    }
    public void SetSelectedCharacter()
    {
        if (selectedPlayerCharacter == null || accountID <= 0) { return; }
        // Call the blocking version from AccountManager
        bool success = AccountManager.Instance.SetAccountLastPlayedCharacter(accountID, selectedPlayerCharacter.GetCharacterID());
        if (!success) { Debug.LogError("Failed to update last played character (sync)."); }
    }
    public async Task<PlayerCharacter> GetSelectedPlayerCharacterAsync() // Make async
    {
        if (!isInitialized && initializationTask != null && !initializationTask.IsCompleted)
        {
            Debug.LogWarning("GetSelectedPlayerCharacterAsync called before initialization complete. Waiting...");
            await initializationTask;
        }
        if (selectedPlayerCharacter == null && isInitialized && playerCharacters != null && playerCharacters.Count > 0)
        {
            // If initialization finished but selection is null (e.g., no characters found initially),
            // try setting the list again or selecting the first.
            Debug.LogWarning("SelectedPlayerCharacter is null after init, attempting to select first.");
            // Optionally re-run list loading if needed: await SetCharactersListAsync();
            if (playerCharacters.Count > 0)
            {
                selectedPlayerCharacter = playerCharacters[0];
                // SetSelectedCharacter(); // Defer DB update
                selectedPlayerCharacter.ActivateModel(true);
                if (playerArmature != null)
                {
                    selectedPlayerCharacter.transform.SetParent(playerArmature.transform, false);
                }
                else { Debug.LogError("PlayerArmature reference missing!"); }
            }
        }
        return selectedPlayerCharacter;
    }
    public PlayerCharacter GetSelectedPlayerCharacter()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Synchronous GetSelectedPlayerCharacter called before PlayerManager is initialized! Returning potentially null character.");
            // Cannot easily await. Return current state.
        }
        // Original logic had a call to SetCharactersList here - this caused the hang.
        // We now rely on initialization in Start to populate the list.
        return selectedPlayerCharacter;
    }
    private void ClearPlayerListExceptSelected()
    {
        if (playerCharacters == null) playerCharacters = new List<PlayerCharacter>();

        // Use a temporary list to avoid issues while iterating and modifying
        List<PlayerCharacter> toRemove = new List<PlayerCharacter>();
        foreach (PlayerCharacter character in playerCharacters)
        {
            if (character == null) continue; // Skip null entries if any
            if (selectedPlayerCharacter == null || character.GetInstanceID() != selectedPlayerCharacter.GetInstanceID())
            {
                toRemove.Add(character);
            }
        }

        foreach (PlayerCharacter characterToRemove in toRemove)
        {
            playerCharacters.Remove(characterToRemove);
            if (characterToRemove.gameObject != null)
            {
                Destroy(characterToRemove.gameObject);
                Debug.Log($"Destroyed non-selected character object: {characterToRemove.GetCharacterName()}");
            }

        }
        // Ensure the selected character is definitely in the list if it exists
        if (selectedPlayerCharacter != null && !playerCharacters.Contains(selectedPlayerCharacter))
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
        // TODO: Check if this scene requires the player (e.g., not main menu)
        SetWaypoint(); // Call SetWaypoint when a relevant game scene is loaded
        if (uiManager != null)
        {
            // uiManager.StartHUD(); // Consider if StartHUD needs specific timing or data
            // If UIManager needs the selected character, ensure it's ready first
            if (isInitialized && selectedPlayerCharacter != null)
            {
                uiManager.SetupUI(selectedPlayerCharacter); // Re-setup UI on scene load if needed
                Debug.Log("UIManager re-setup on scene load.");
            }
            else if (!isInitialized)
            {
                Debug.LogWarning("Scene loaded but PlayerManager not yet initialized.");
                // UI setup will happen when initialization completes
            }
        }
        else { Debug.LogError("UIManager reference missing in PlayerManager!"); }

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
