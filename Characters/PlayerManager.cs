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
    private string language = "en";
    private string ipAddress = "0.0.0.0";
    [SerializeField] private CharacterModelManager characterModelManager;
    [SerializeField] private ZoneManager currentZone;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private UIManager uiManager;

    private bool isInitialized = false; 
    private Task initializationTask; 

    #region Singleton
    public static PlayerManager Instance { get; private set; }
    private void Awake()
    {        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); 
            return;
        }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded; 
        //TODO Steam Set ID
    }
    #endregion

    #region Initialization
    private async void Start()
    {
        if (playerCharacters == null)
        {
            playerCharacters = new List<PlayerCharacter>();
        }
        initializationTask = InitializePlayerManagerAsync();
        await initializationTask; 
    }
    private async Task InitializePlayerManagerAsync()
    {
        Debug.Log("PlayerManager Initialization Started...");
        isInitialized = false;

        // 1. Perform Login Asynchronously
        Debug.Log("Performing login...");
        bool loginSuccess = await LoginAsync();
        if (!loginSuccess)
        {
            Debug.LogError("Login failed. PlayerManager initialization halted.");
            // Handle login failure (e.g., show error message, return to main menu)
            return; // Stop initialization
        }
        Debug.Log($"Login successful. AccountID: {accountID}, AccountName: {accountName}");

        // 2. Load Character List Asynchronously
        Debug.Log("Loading character list...");
        await SetCharactersListAsync();
        Debug.Log($"Character list loaded. Found {playerCharacters.Count} characters.");

        // 3. Final setup (e.g., initial UI setup)
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
                MenuManager.Instance.SetCharCreationButton(false); 
            }
            else
            {
                MenuManager.Instance.SetCharCreationButton(true); 
            }
        }
        OnCharactersLoaded();
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
                // Use the ASYNC version and await it, including ipAddress and language
                bool created = await AccountManager.Instance.CreateNewAccountAsync(accountName, AccountManager.Instance.GenerateRandomPassword(), email, steamID, ipAddress, language);
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
    public async Task SetCharactersListAsync()
    {
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

            Camera cameraToSet = mainCamera.GetComponent<Camera>();
            newCharacter.SetUpCharacter(newCharacterName, newCharacterID, title, zoneID, race, face, gender, combatxp, craftingxp, arcaneexp, spiritxp, veilexp, cameraToSet);
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

        // Load inventory for all characters
        await LoadAllCharactersInventoryAsync();
    }
    private async Task LoadAllCharactersInventoryAsync()
    {
        if (playerCharacters == null || playerCharacters.Count == 0)
        {
            Debug.LogWarning("No characters to load inventory for.");
            return;
        }

        foreach (var character in playerCharacters)
        {
            await LoadCharacterInventoryAsync(character);
        }
    }
    private async void OnCharactersLoaded()
    {
        await LoadAllCharactersInventoryAsync();
    }
    private async Task LoadCharacterInventoryAsync(PlayerCharacter character)
    {
        if (character == null)
        {
            Debug.LogError("Cannot load inventory for null character.");
            return;
        }

        int charId = character.GetCharacterID();
        Inventory inventory = character.GetInventory();
        EquipmentProfile equipment = character.GetEquipmentProfile();

        if (charId <= 0)
        {
            Debug.LogError($"Invalid character ID: {charId} for character {character.GetCharacterName()}");
            return;
        }
        if (inventory == null)
        {
            Debug.LogError($"Inventory component not found for character {character.GetCharacterName()} (ID: {charId}).");
            return;
        }
        if (equipment == null)
        { 
            Debug.LogError($"EquipmentProfile component not found for character {character.GetCharacterName()} (ID: {charId}).");
            return;
        }
        if (ItemManager.Instance == null)
        {
            Debug.LogError("ItemManager instance is not available.");
            return;
        }
         if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager instance is not available.");
            return;
        }
        // Assuming SubComponentManager exists
        // if (SubComponentManager.Instance == null)
        // {
        //     Debug.LogError("SubComponentManager instance is not available.");
        //     return;
        // }

        Debug.Log($"Starting inventory load for character: {character.GetCharacterName()} (ID: {charId})");

        // Clear existing inventory and equipment before loading
        inventory.ClearInventory();
        equipment.ClearEquipmentProfile(); // Assuming an UnequipAll method exists

        try
        {
            // --- 1. Load Inventory Items (Equipment and Bag Items) ---
            Debug.Log($"Loading InventoryItems for CharID: {charId}...");
            List<Dictionary<string, object>> inventoryItemsData = await InventoryManager.Instance.GetCharacterInventoryItemsAsync(charId);
            Debug.Log($"Found {inventoryItemsData.Count} InventoryItems entries.");

            foreach (var itemData in inventoryItemsData)
            {
                if (!itemData.TryGetValue("ItemID", out object itemIdObj) || itemIdObj == DBNull.Value ||
                    !itemData.TryGetValue("SlotID", out object slotIdObj) || slotIdObj == DBNull.Value)
                {
                    Debug.LogWarning($"Skipping inventory item entry due to missing ItemID or SlotID for CharID: {charId}");
                    continue;
                }

                int itemId = Convert.ToInt32(itemIdObj);
                int slotId = Convert.ToInt32(slotIdObj);

                Item itemInstance = ItemManager.Instance.GetItemInstanceById(itemId); // Assumes this creates/returns the specific instance
                if (itemInstance == null)
                {
                    Debug.LogWarning($"Item with ID {itemId} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (slotId > 0)
                { 
                    // Equip Item
                    ItemType slotType = MapSlotIdToItemType(slotId);
                    if (slotType != ItemType.Other)
                    {
                        int slotIndex = GetSlotIndexForType(slotId);
                        EquipmentSlot targetSlot = equipment.GetSlotForItemType(slotType, slotIndex);
                        if (targetSlot != null)
                        {
                            Debug.Log($"Equipping item {itemInstance.ItemName} (ID: {itemId}) to SlotID: {slotId} (Type: {slotType}, Index: {slotIndex})");
                            equipment.EquipItemToSlot(itemInstance, targetSlot);
                        }
                        else
                        {
                            Debug.LogWarning($"Could not find EquipmentSlot for SlotID: {slotId} (Type: {slotType}, Index: {slotIndex}). Cannot equip {itemInstance.ItemName}.");
                            // Optionally add to inventory bag as fallback?
                            // inventory.AddItem(itemInstance);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid SlotID {slotId} found in InventoryItems table for ItemID {itemId}. Cannot equip.");
                        // Optionally add to inventory bag as fallback?
                        // inventory.AddItem(itemInstance);
                    }
                }
                else // slotId == 0 (or other designated bag slot identifier)
                {
                    // Add to Inventory Bag
                    Debug.Log($"Adding item {itemInstance.ItemName} (ID: {itemId}) to inventory bag.");
                    if (!inventory.AddItem(itemInstance))
                    {
                        Debug.LogWarning($"Failed to add item {itemInstance.ItemName} (ID: {itemId}) to inventory bag for character {charId}. Bag might be full or item invalid.");
                    }
                }
            }

            // --- 2. Load Inventory Resource Items (Specific Instances in Bag Slots) ---
            // IMPORTANT: This assumes ResourceItemID links to a uniquely defined instance somewhere (e.g., another table or ResourceManager knows about specific instances)
            Debug.Log($"Loading InventoryResourceItems for CharID: {charId}...");
            List<Dictionary<string, object>> inventoryResourceItemsData = await InventoryManager.Instance.GetCharacterInventoryResourceItemsAsync(charId);
            Debug.Log($"Found {inventoryResourceItemsData.Count} InventoryResourceItems entries.");

            foreach (var resourceItemData in inventoryResourceItemsData)
            {
                 if (!resourceItemData.TryGetValue("ResourceItemID", out object resourceItemIdObj) || resourceItemIdObj == DBNull.Value ||
                    !resourceItemData.TryGetValue("SlotID", out object slotIdObj) || slotIdObj == DBNull.Value)
                {
                    Debug.LogWarning($"Skipping inventory resource item entry due to missing ResourceItemID or SlotID for CharID: {charId}");
                    continue;
                }

                int resourceItemId = Convert.ToInt32(resourceItemIdObj);
                int slotId = Convert.ToInt32(slotIdObj); // Currently assuming this is always 0 (bag slot)

                // ASSUMPTION: GetResourceItemInstanceById returns a fully populated ResourceItem
                ResourceItem resourceItemInstance = InventoryManager.Instance.GetResourceItemById(resourceItemId);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"ResourceItem instance with ID {resourceItemId} not found via ResourceManager. Cannot load.");
                    continue;
                }

                if (slotId == 0) // Assuming resources only go into the bag for now
                {
                    Debug.Log($"Adding resource item {resourceItemInstance.Resource?.ResourceName ?? "Unknown"} (Instance ID: {resourceItemId}) to inventory bag.");
                    if (!inventory.AddResourceItem(resourceItemInstance))
                    {
                        Debug.LogWarning($"Failed to add ResourceItem instance (ID: {resourceItemId}) to inventory bag for character {charId}.");
                    }
                }
                else
                {
                    Debug.LogWarning($"InventoryResourceItem instance (ID: {resourceItemId}) found with SlotID {slotId}. Equipping resources not currently supported. Adding to bag instead.");
                    // Fallback to adding to bag
                     if (!inventory.AddResourceItem(resourceItemInstance))
                    {
                        Debug.LogWarning($"Failed to add ResourceItem instance (ID: {resourceItemId}) to inventory bag (fallback) for character {charId}.");
                    }
                }
            }

            // --- 3. Load Inventory SubComponents (Specific Instances in Bag Slots) ---
             // IMPORTANT: This assumes SubComponentID links to a uniquely defined instance
            Debug.Log($"Loading InventorySubComponents for CharID: {charId}...");
            List<Dictionary<string, object>> inventorySubComponentsData = await InventoryManager.Instance.GetCharacterInventorySubComponentsAsync(charId);
            Debug.Log($"Found {inventorySubComponentsData.Count} InventorySubComponents entries.");

            foreach (var subCompData in inventorySubComponentsData)
            {
                if (!subCompData.TryGetValue("SubComponentID", out object subCompIdObj) || subCompIdObj == DBNull.Value ||
                    !subCompData.TryGetValue("SlotID", out object slotIdObj) || slotIdObj == DBNull.Value)
                {
                    Debug.LogWarning($"Skipping inventory subcomponent entry due to missing SubComponentID or SlotID for CharID: {charId}");
                    continue;
                }
                
                int subComponentId = Convert.ToInt32(subCompIdObj);
                int slotId = Convert.ToInt32(slotIdObj); // Currently assuming this is always 0 (bag slot)

                 // ASSUMPTION: GetSubComponentInstanceById returns a fully populated SubComponent
                 // Replace 'SubComponentManager' with your actual manager if different
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subComponentId);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"SubComponent instance with ID {subComponentId} not found via SubComponentManager. Cannot load.");
                    continue;
                }

                if (slotId == 0) // Assuming subcomponents only go into the bag
                {
                    Debug.Log($"Adding subcomponent {subComponentInstance.Name ?? "Unknown"} (Instance ID: {subComponentId}) to inventory bag.");
                    if (!inventory.AddSubComponent(subComponentInstance))
                    {
                         Debug.LogWarning($"Failed to add SubComponent instance (ID: {subComponentId}) to inventory bag for character {charId}.");
                    }
                }
                 else
                {
                    Debug.LogWarning($"InventorySubComponent instance (ID: {subComponentId}) found with SlotID {slotId}. Equipping subcomponents not currently supported. Adding to bag instead.");
                    // Fallback to adding to bag
                     if (!inventory.AddSubComponent(subComponentInstance))
                    {
                        Debug.LogWarning($"Failed to add SubComponent instance (ID: {subComponentId}) to inventory bag (fallback) for character {charId}.");
                    }
                }
            }

             Debug.Log($"Finished inventory load for character: {character.GetCharacterName()} (ID: {charId})");

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during LoadCharacterInventoryAsync for character {charId}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

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

    #region Setters
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
    private void SetWaypoint()
    {
        if (currentZone != null)
        {
            Transform waypoint = currentZone.GetWaypoint();
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
    public void OnSetFamilyName(string newFamilyName)
    {
        if (GetCharacters().Count == 0)
        {
            familyName = newFamilyName;
        }
    }
    public void OnSetAccountName(string newAccountName)
    {
        accountName = newAccountName;
    }
    #endregion

    #region Getters
    public string GetFamilyName()
    {
        return familyName;
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
    public UIManager GetUIManager()
    {
        return uiManager;
    }
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
    #endregion

    #region Helpers
    private ItemType MapSlotIdToItemType(int slotId)
    {
        switch (slotId)
        {
            case 1: return ItemType.Helm;
            case 2: return ItemType.Cuirass;
            case 3: return ItemType.Greaves;
            case 4: return ItemType.Vambraces;
            case 5: return ItemType.Finger; // First finger slot
            case 6: return ItemType.Finger; // Second finger slot
            case 7: return ItemType.PrimaryHand;
            case 8: return ItemType.SecondaryHand;
            case 9: return ItemType.MiningTool;
            case 10: return ItemType.WoodTool;
            case 11: return ItemType.HarvestingTool;
            case 12: return ItemType.Hauberk;
            case 13: return ItemType.Trousers;
            case 14: return ItemType.Sleeves;
            case 15: return ItemType.Coif;
            case 16: return ItemType.Neck;
            case 17: return ItemType.Waist;
            case 18: return ItemType.Back;
            case 19: return ItemType.Boots;
            case 20: return ItemType.Ear; // First ear slot
            case 21: return ItemType.Ear; // Second ear slot
            default: return ItemType.Other;
        }
    }

    private int GetSlotIndexForType(int slotId)
    {
        switch (slotId)
        {
            case 5: return 0; // First finger slot
            case 6: return 1; // Second finger slot
            case 20: return 0; // First ear slot
            case 21: return 1; // Second ear slot
            default: return 0; // All other slots use index 0
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe to avoid memory leaks
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
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentZone = FindFirstObjectByType<ZoneManager>();
        SetWaypoint();

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
    #endregion
}
