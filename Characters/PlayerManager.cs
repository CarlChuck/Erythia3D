using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Linq;
using UnityEditor.Profiling.Memory.Experimental;

public class PlayerManager : NetworkBehaviour
{
    [Header("Player Account Info")]
    [SerializeField] private string accountName = "";
    [SerializeField] private string email = ""; //No Idea if this is being used yet
    private ulong steamID = 0; //TODO
    private int accountID;
    [SerializeField] private string familyName = "";
    [SerializeField] private string language = "en";
    [SerializeField] private string ipAddress = "0.0.0.0";

    [Header("Player Character Info")]
    [SerializeField] private GameObject charListParent;
    [SerializeField] private GameObject playerFollowCam;
    [SerializeField] private GameObject controlledCharacter;
    [SerializeField] private List<PlayerCharacter> playerCharacters;
    [SerializeField] private PlayerCharacter selectedPlayerCharacter;
    [SerializeField] private CharacterModelManager characterModelManager;
    [SerializeField] private ZoneManager currentZone;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private GameObject playerArmaturePrefab;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private WorkBench workBenchPrefab; // New prefab reference

    private List<WorkBench> ownedWorkbenches = new List<WorkBench>(); // New list for owned workbenches
    [SerializeField] private Transform workbenchParent;
    [SerializeField] private Inventory homeInventory;

    private bool isInitialized = false; 
    private Task initializationTask; 

    // Fields for handling server response
    private bool loginResultReceived = false;
    private LoginResult currentLoginResult;
    
    // Fields for handling character list response
    private bool characterListReceived = false;
    private CharacterListResult currentCharacterListResult;
    
    // Fields for handling inventory responses
    private bool accountInventoryReceived = false;
    private AccountInventoryResult currentAccountInventoryResult;
    private bool characterInventoryReceived = false;
    private CharacterInventoryResult currentCharacterInventoryResult;
    
    // Fields for handling workbench responses
    private bool workbenchListReceived = false;
    private WorkbenchListResult currentWorkbenchListResult;

    #region Singleton
    public static PlayerManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple PlayerManager instances owned by client. Destroying this one on GameObject: {gameObject.name}");
            Destroy(gameObject); 
            return;
        }
        Debug.Log($"Setting PlayerManager instance for owner: {gameObject.name}");

        if (Instance == null)
        {
            Instance = this;
        }

        SceneManager.sceneLoaded += OnSceneLoaded; 

        //TODO Steam Set ID
    }
    #endregion

    #region Initialization
    private void Start()
    {
        if (playerCharacters == null)
        {
            playerCharacters = new List<PlayerCharacter>();
        }
    }

    //This is to be called when starting PlayerManager
    public async void OnStartInitialization(int newAccountID = 0, ulong newSteamID = 0, string newAccountName = "")
    {
        accountID = newAccountID;
        steamID = newSteamID;
        accountName = newAccountName;
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
        await LoadAllCharactersInventoryAsync();
        await LoadAccountInventoryAsync();
    }
    private async Task<bool> LoginAsync()
    {
        try
        {
            Debug.Log("Client: Requesting login from server...");
            
            // Wait for server response with a timeout
            loginResultReceived = false;
            currentLoginResult = default;
            
            // Send RPC to server-side PlayerManager which will call ServerManager
            RequestLoginServerRpc(steamID, accountID, accountName, email, ipAddress, language);
            
            // Wait for response with timeout
            float timeout = 10f; // 10 seconds timeout
            float elapsed = 0f;
            while (!loginResultReceived && elapsed < timeout)
            {
                await Task.Delay(100); // Wait 100ms
                elapsed += 0.1f;
            }
            
            if (!loginResultReceived)
            {
                Debug.LogError("Login request timed out.");
                return false;
            }
            
            if (currentLoginResult.Success)
            {
                return ProcessLoginResult(currentLoginResult);
            }
            else
            {
                Debug.LogError($"Login failed: {currentLoginResult.ErrorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during LoginAsync: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private bool ProcessLoginResult(LoginResult result)
    {
        try
        {
            // Update local variables with server response
            accountID = result.AccountID;
            accountName = result.AccountName;
            steamID = result.SteamID;
            
            Debug.Log($"Login successful. AccountID: {accountID}, AccountName: {accountName}, SteamID: {steamID}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during ProcessLoginResult: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private async Task<bool> LoginViaSteamIDAsync()
    {
        // This method is no longer needed - LoginAsync handles both cases
        return await LoginAsync();
    }

    private async Task<bool> LoginViaAccountIDAsync()
    {
        // This method is no longer needed - LoginAsync handles both cases
        return await LoginAsync();
    }

    public void HandleLoginResult(LoginResult result)
    {
        currentLoginResult = result;
        loginResultReceived = true;
        Debug.Log($"Client: Received login result. Success: {result.Success}");
    }

    public async Task SetCharactersListAsync()
    {
        if (accountID <= 0)
        {
            Debug.LogError("Cannot set character list: Invalid AccountID.");
            return;
        }

        Debug.Log("Client: Requesting character list from server...");
        
        // Wait for server response with a timeout
        characterListReceived = false;
        currentCharacterListResult = default;
        
        // Send RPC to server-side PlayerManager which will call ServerManager
        RequestCharacterListServerRpc(accountID);
        
        // Wait for response with timeout
        float timeout = 10f; // 10 seconds timeout
        float elapsed = 0f;
        while (!characterListReceived && elapsed < timeout)
        {
            await Task.Delay(100); // Wait 100ms
            elapsed += 0.1f;
        }
        
        if (!characterListReceived)
        {
            Debug.LogError("Character list request timed out.");
            return;
        }
        
        if (currentCharacterListResult.Success)
        {
            await ProcessCharacterListResult(currentCharacterListResult);
        }
        else
        {
            Debug.LogError($"Character list request failed: {currentCharacterListResult.ErrorMessage}");
        }
    }

    private async Task ProcessCharacterListResult(CharacterListResult result)
    {
        try
        {
            ClearPlayerListExceptSelected();

            Debug.Log($"Processing {result.Characters.Length} character data entries from server.");

            foreach (CharacterData characterData in result.Characters)
            {
                // --- Check if character already loaded (avoid duplicates) ---
                bool alreadyExists = false;
                if (selectedPlayerCharacter != null && selectedPlayerCharacter.GetCharacterID() == characterData.CharID)
                {
                    Debug.Log($"Character ID {characterData.CharID} is already the selected character.");
                    if (!playerCharacters.Contains(selectedPlayerCharacter))
                    {
                        playerCharacters.Add(selectedPlayerCharacter);
                    }
                    alreadyExists = true;
                }
                else if (playerCharacters.Any(pc => pc.GetCharacterID() == characterData.CharID))
                {
                    Debug.Log($"Character ID {characterData.CharID} already exists in the loaded list.");
                    alreadyExists = true;
                }

                if (alreadyExists)
                {
                    continue; // Skip to next character in the list
                }

                // Load FamilyName ONLY if not already set (e.g., from Account data)
                if (string.IsNullOrEmpty(familyName) && !string.IsNullOrEmpty(characterData.FamilyName))
                {
                    familyName = characterData.FamilyName;
                }

                // --- Instantiate and Setup Character ---
                if (characterPrefab == null || charListParent == null || characterModelManager == null)
                {
                    Debug.LogError("Missing prefabs or parent references for character instantiation!");
                    continue;
                }

                Debug.Log($"Instantiating new character object for ID: {characterData.CharID}, Name: {characterData.Name}");
                PlayerCharacter newCharacter = Instantiate(characterPrefab, charListParent.transform).GetComponent<PlayerCharacter>();
                if (newCharacter == null)
                {
                    Debug.LogError("Failed to get PlayerCharacter component from prefab!");
                    continue;
                }

                GameObject charModelInstance = characterModelManager.GetCharacterModel(characterData.Race, characterData.Gender);
                if (charModelInstance != null)
                {
                    GameObject charGameObject = Instantiate(charModelInstance, newCharacter.transform);
                    newCharacter.AddModel(charGameObject);
                }
                else
                {
                    Debug.LogWarning($"Could not find character model for Race:{characterData.Race} Gender:{characterData.Gender}");
                }

                Camera cameraToSet = mainCamera.GetComponent<Camera>();
                newCharacter.SetUpCharacter(
                    characterData.Name, 
                    characterData.CharID, 
                    characterData.Title, 
                    characterData.ZoneID, 
                    characterData.Race, 
                    characterData.Face, 
                    characterData.Gender, 
                    characterData.CombatExp, 
                    characterData.CraftingExp, 
                    characterData.ArcaneExp, 
                    characterData.SpiritExp, 
                    characterData.VeilExp, 
                    cameraToSet, 
                    characterData.XLoc, 
                    characterData.YLoc, 
                    characterData.ZLoc,
                    characterData.SpeciesStrength,
                    characterData.SpeciesDexterity,
                    characterData.SpeciesConstitution,
                    characterData.SpeciesIntelligence,
                    characterData.SpeciesSpirit
                );
                playerCharacters.Add(newCharacter);

                // Set as selected if none currently selected
                if (selectedPlayerCharacter == null)
                {
                    Debug.Log($"Setting first loaded character as selected: {characterData.Name}");
                    selectedPlayerCharacter = newCharacter;
                    selectedPlayerCharacter.ActivateModel(true);
                    InstantiateCharacter();
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
                selectedPlayerCharacter.ActivateModel(true);
                InstantiateCharacter();
            }

            Debug.Log($"Finished processing character list. Final count: {playerCharacters.Count}. Selected: {selectedPlayerCharacter?.GetCharacterName() ?? "None"}");

            // Load inventory for all characters
            await LoadAllCharactersInventoryAsync();

            // Load owned workbenches
            await LoadOwnedWorkbenchesAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during ProcessCharacterListResult: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task LoadOwnedWorkbenchesAsync()
    {
        Debug.Log("Client: Requesting workbench list from server...");
        
        // Wait for server response with a timeout
        workbenchListReceived = false;
        currentWorkbenchListResult = default;
        
        // Send RPC to server-side PlayerManager which will call ServerManager
        RequestWorkbenchListServerRpc(accountID);
        
        // Wait for response with timeout
        float timeout = 10f; // 10 seconds timeout
        float elapsed = 0f;
        while (!workbenchListReceived && elapsed < timeout)
        {
            await Task.Delay(100); // Wait 100ms
            elapsed += 0.1f;
        }
        
        if (!workbenchListReceived)
        {
            Debug.LogError("Workbench list request timed out.");
            return;
        }
        
        if (currentWorkbenchListResult.Success)
        {
            await ProcessOwnedWorkbenches(currentWorkbenchListResult.Workbenches);
        }
        else
        {
            Debug.LogError($"Workbench list request failed: {currentWorkbenchListResult.ErrorMessage}");
        }
    }
    #endregion

    public void InstantiateCharacter()
    {
        if (playerArmaturePrefab != null)
        {
            controlledCharacter = Instantiate(playerArmaturePrefab, transform); // Instantiate armature
        }
        else 
        { 
            Debug.LogError("PlayerArmature reference missing!"); 
        }
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

    #region Save To Database
    public async Task SaveCharacter()
    {
        if (selectedPlayerCharacter == null)
        {
            Debug.LogWarning("Cannot save character - no character selected.");
            return;
        }
        Debug.Log($"Saving character {selectedPlayerCharacter.GetCharacterName()} (ID: {selectedPlayerCharacter.GetCharacterID()})");
        // Use the async version from CharactersManager
        Transform transform = selectedPlayerCharacter.transform;

        bool success = await CharactersManager.Instance.UpdateCharacterAsync(selectedPlayerCharacter.GetCharacterID(), selectedPlayerCharacter.GetTitle(), 
            (int)(transform.position.x * 100), (int)(transform.position.y * 100), (int)(transform.position.z * 100), selectedPlayerCharacter.GetCombatExp(), 
            selectedPlayerCharacter.GetCraftingExp(), selectedPlayerCharacter.GetArcaneExp(), selectedPlayerCharacter.GetSpiritExp(), selectedPlayerCharacter.GetVeilExp());
        if (!success)
        {
            Debug.LogError("Failed to save character data.");
        }
    }
    public async Task SaveItem(Item item) 
    { 
        
    }
    public async Task SaveResourceItem(ResourceItem resourceItem)
    {

    }
    public async Task SaveSubComponent(SubComponent subComponent)
    {

    }
    public async Task SaveCharacterEquipment(PlayerCharacter character)
    {

    }
    public async Task SaveCharacterInventory(PlayerCharacter character)
    {

    }
    public async Task SaveAccountInventory()
    {

    }
    public async Task SaveWorkbench()
    {

    }
    #endregion

    #region Load From Database
    public async Task LoadCharacter()
    {

    }
    public async Task<Item> LoadItem()
    {
        Item item = null;

        return item;
    }
    public async Task<ResourceItem> LoadResourceItem()
    {
        ResourceItem resourceItem = null;

        return resourceItem;
    }
    public async Task<SubComponent> LoadSubComponent()
    {
        SubComponent subComponent = null;

        return subComponent;
    }
    public async Task LoadCharacterEquipment()
    {

    }
    public async Task LoadCharacterInventory()
    {

    }
    public async Task LoadAccountInventory()
    {

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
    private async Task LoadCharacterInventoryAsync(PlayerCharacter character)
    {
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

        Debug.Log($"Client: Requesting character inventory from server for character: {character.GetCharacterName()} (ID: {charId})");
        
        // Wait for server response with a timeout
        characterInventoryReceived = false;
        currentCharacterInventoryResult = default;
        
        // Send RPC to server-side PlayerManager which will call ServerManager
        RequestCharacterInventoryServerRpc(charId);
        
        // Wait for response with timeout
        float timeout = 10f; // 10 seconds timeout
        float elapsed = 0f;
        while (!characterInventoryReceived && elapsed < timeout)
        {
            await Task.Delay(100); // Wait 100ms
            elapsed += 0.1f;
        }
        
        if (!characterInventoryReceived)
        {
            Debug.LogError($"Character inventory request timed out for character {character.GetCharacterName()} (ID: {charId}).");
            return;
        }
        
        if (currentCharacterInventoryResult.Success)
        {
            await ProcessCharacterInventoryResult(currentCharacterInventoryResult, character, inventory, equipment);
        }
        else
        {
            Debug.LogError($"Character inventory request failed for character {character.GetCharacterName()}: {currentCharacterInventoryResult.ErrorMessage}");
        }
    }

    private async Task ProcessCharacterInventoryResult(CharacterInventoryResult result, PlayerCharacter character, Inventory inventory, EquipmentProfile equipment)
    {
        int charId = character.GetCharacterID();
        
        try
        {
            Debug.Log($"Processing character inventory result for character: {character.GetCharacterName()} (ID: {charId})");
            
            inventory.ClearInventory();
            equipment.ClearEquipmentProfile();

            // --- 1. Load Inventory Items (Equipment and Bag Items) ---
            Debug.Log($"Processing {result.Items.Length} inventory items for CharID: {charId}...");
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"Item with ID {itemData.ItemID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (itemData.SlotID > 0)
                {
                    // Equip Item
                    ItemType slotType = MapSlotIdToItemType(itemData.SlotID);
                    if (slotType != ItemType.Other)
                    {
                        int slotIndex = GetSlotIndexForType(itemData.SlotID);
                        EquipmentSlot targetSlot = equipment.GetSlotForItemType(slotType, slotIndex);
                        if (targetSlot != null)
                        {
                            Debug.Log($"Equipping item {itemInstance.ItemName} (ID: {itemData.ItemID}) to SlotID: {itemData.SlotID} (Type: {slotType}, Index: {slotIndex})");
                            equipment.EquipItemToSlot(itemInstance, targetSlot);
                        }
                        else
                        {
                            Debug.LogWarning($"Could not find EquipmentSlot for SlotID: {itemData.SlotID} (Type: {slotType}, Index: {slotIndex}). Cannot equip {itemInstance.ItemName}.");
                            inventory.AddItem(itemInstance); // Put in bag as fallback
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid SlotID {itemData.SlotID} found for ItemID {itemData.ItemID}. Cannot equip.");
                        inventory.AddItem(itemInstance); // Put in bag as fallback
                    }
                }
                else
                {
                    // Add to Inventory Bag
                    Debug.Log($"Adding item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag.");
                    if (!inventory.AddItem(itemInstance))
                    {
                        Debug.LogWarning($"Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag for character {charId}.");
                    }
                }
            }

            // --- 2. Load Inventory Resource Items ---
            Debug.Log($"Processing {result.ResourceItems.Length} resource items for CharID: {charId}...");
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                Debug.Log($"Adding resource item {resourceItemInstance.Resource?.ResourceName ?? "Unknown"} (Instance ID: {resourceItemData.ResourceItemID}) to inventory bag.");
                if (!inventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to inventory bag for character {charId}.");
                }
            }

            // --- 3. Load Inventory SubComponents ---
            Debug.Log($"Processing {result.SubComponents.Length} subcomponents for CharID: {charId}...");
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                Debug.Log($"Adding subcomponent {subComponentInstance.Name ?? "Unknown"} (Instance ID: {subCompData.SubComponentID}) to inventory bag.");
                if (!inventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to inventory bag for character {charId}.");
                }
            }

            Debug.Log($"Finished processing inventory for character: {character.GetCharacterName()} (ID: {charId})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing character inventory result for character {charId}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    private async Task LoadAccountInventoryAsync()
    {
        Debug.Log("Client: Requesting account inventory from server...");
        
        // Wait for server response with a timeout
        accountInventoryReceived = false;
        currentAccountInventoryResult = default;
        
        // Send RPC to server-side PlayerManager which will call ServerManager
        RequestAccountInventoryServerRpc(accountID);
        
        // Wait for response with timeout
        float timeout = 10f; // 10 seconds timeout
        float elapsed = 0f;
        while (!accountInventoryReceived && elapsed < timeout)
        {
            await Task.Delay(100); // Wait 100ms
            elapsed += 0.1f;
        }
        
        if (!accountInventoryReceived)
        {
            Debug.LogError("Account inventory request timed out.");
            return;
        }
        
        if (currentAccountInventoryResult.Success)
        {
            await ProcessAccountInventoryResult(currentAccountInventoryResult);
        }
        else
        {
            Debug.LogError($"Account inventory request failed: {currentAccountInventoryResult.ErrorMessage}");
        }
    }

    private async Task ProcessAccountInventoryResult(AccountInventoryResult result)
    {
        try
        {
            homeInventory.ClearInventory();

            // --- 1. Load Inventory Items ---
            Debug.Log($"Processing {result.Items.Length} account inventory items from server.");
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"Item with ID {itemData.ItemID} not found via ItemManager for account inventory. Cannot load.");
                    continue;
                }

                Debug.Log($"Adding item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory.");
                if (!homeInventory.AddItem(itemInstance))
                {
                    Debug.LogWarning($"Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory for account {accountID}.");
                }
            }

            // --- 2. Load Account Inventory Resource Items ---
            Debug.Log($"Processing {result.ResourceItems.Length} account resource items from server.");
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                Debug.Log($"Adding resource item {resourceItemInstance.Resource?.ResourceName ?? "Unknown"} (Instance ID: {resourceItemData.ResourceItemID}) to home inventory.");
                if (!homeInventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to home inventory for account {accountID}.");
                }
            }

            // --- 3. Load Account Inventory SubComponents ---
            Debug.Log($"Processing {result.SubComponents.Length} account subcomponents from server.");
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                Debug.Log($"Adding subcomponent {subComponentInstance.Name ?? "Unknown"} (Instance ID: {subCompData.SubComponentID}) to home inventory.");
                if (!homeInventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to home inventory for account {accountID}.");
                }
            }

            // --- 4. Load Owned Workbenches ---
            await ProcessOwnedWorkbenches(result.Workbenches);

            Debug.Log($"Finished loading account inventory for AccountID: {accountID}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing account inventory result: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private async Task ProcessOwnedWorkbenches(WorkbenchData[] workbenches)
    {
        Debug.Log($"Processing {workbenches.Length} owned workbenches from server.");
        ownedWorkbenches.Clear();

        foreach (var workbenchData in workbenches)
        {
            int workbenchType = workbenchData.WorkBenchType;

            WorkBench newWorkBenchInstance = Instantiate(workBenchPrefab, workbenchParent);
            newWorkBenchInstance.SetWorkbenchType(workbenchType);

            if (WorkBenchManager.Instance != null)
            {
                WorkBench templateWorkBench = WorkBenchManager.Instance.GetWorkbenchByType(workbenchType);
                if (templateWorkBench != null)
                {
                    newWorkBenchInstance.InitializeRecipes(templateWorkBench.Recipes);
                    Debug.Log($"Initialized workbench type {workbenchType} with {templateWorkBench.Recipes.Count} recipes from WorkBenchManager.");
                }
                else
                {
                    Debug.LogWarning($"No template workbench found in WorkBenchManager for type {workbenchType}. Initializing with empty recipes.");
                    newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
                }
            }
            else
            {
                newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
            }

            ownedWorkbenches.Add(newWorkBenchInstance);
        }
        Debug.Log($"Finished processing {ownedWorkbenches.Count} owned workbenches.");
    }
    #endregion

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
            if (waypoint != null && controlledCharacter != null)
            {
                controlledCharacter.transform.position = waypoint.position;
                controlledCharacter.transform.rotation = waypoint.rotation;
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
                InstantiateCharacter();
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
    public List<WorkBench> GetOwnedWorkbenches()
    {
        return ownedWorkbenches;
    }
    public Inventory GetHomeInventory()
    {
        return homeInventory;
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
    public void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe to avoid memory leaks
    }
    private void ClearPlayerListExceptSelected()
    {
        if (playerCharacters == null) 
        { 
            playerCharacters = new List<PlayerCharacter>(); 
        }

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
        if (controlledCharacter != null)
        {
            controlledCharacter.SetActive(isActive);
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
            uiManager.StartHUD();
            Debug.Log("UIManager re-setup on scene load.");
        }
        else if (!isInitialized)
        {
            Debug.LogWarning("Scene loaded but PlayerManager not yet initialized for owner.");
            // UI setup will happen when initialization completes
        }
    }
    private ResourceItem GetResourceItemById(int resourceItemId)
    {
        ResourceItem resourceItem = null; //TODO
        return resourceItem;
    }
    #endregion

    #region Login Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    private void RequestLoginServerRpc(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received login request, calling ServerManager for steamID={steamID}, accountID={accountID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessLoginRequest...");
                ServerManager.Instance.ProcessLoginRequest(steamID, accountID, accountName, email, ipAddress, language, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessLoginRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process login request.");
                
                // Send error response back to client
                LoginResult errorResult = new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available"
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveLoginResultClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestLoginServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveLoginResultClientRpc(LoginResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the login result
        Debug.Log($"PlayerManager (Client): Received login result from server. Success: {result.Success}");
        HandleLoginResult(result);
    }
    #endregion

    #region Character List Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    private void RequestCharacterListServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received character list request, calling ServerManager for accountID={accountID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessCharacterListRequest...");
                ServerManager.Instance.ProcessCharacterListRequest(accountID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessCharacterListRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process character list request.");
                
                // Send error response back to client
                CharacterListResult errorResult = new CharacterListResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Characters = new CharacterData[0]
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveCharacterListClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestCharacterListServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveCharacterListClientRpc(CharacterListResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the character list result
        Debug.Log($"PlayerManager (Client): Received character list result from server. Success: {result.Success}, CharacterCount: {result.Characters?.Length ?? 0}");
        HandleCharacterListResult(result);
    }

    public void HandleCharacterListResult(CharacterListResult result)
    {
        currentCharacterListResult = result;
        characterListReceived = true;
        Debug.Log($"Client: Received character list result. Success: {result.Success}");
    }
    #endregion

    #region Inventory Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    private void RequestAccountInventoryServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received account inventory request, calling ServerManager for accountID={accountID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessAccountInventoryRequest...");
                ServerManager.Instance.ProcessAccountInventoryRequest(accountID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessAccountInventoryRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process account inventory request.");
                
                // Send error response back to client
                AccountInventoryResult errorResult = new AccountInventoryResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Items = new InventoryItemData[0],
                    ResourceItems = new InventoryResourceItemData[0],
                    SubComponents = new InventorySubComponentData[0],
                    Workbenches = new WorkbenchData[0]
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveAccountInventoryClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestAccountInventoryServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveAccountInventoryClientRpc(AccountInventoryResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the account inventory result
        Debug.Log($"PlayerManager (Client): Received account inventory result from server. Success: {result.Success}, ItemCount: {result.Items?.Length ?? 0}");
        HandleAccountInventoryResult(result);
    }

    public void HandleAccountInventoryResult(AccountInventoryResult result)
    {
        currentAccountInventoryResult = result;
        accountInventoryReceived = true;
        Debug.Log($"Client: Received account inventory result. Success: {result.Success}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCharacterInventoryServerRpc(int characterID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received character inventory request, calling ServerManager for characterID={characterID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessCharacterInventoryRequest...");
                ServerManager.Instance.ProcessCharacterInventoryRequest(characterID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessCharacterInventoryRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process character inventory request.");
                
                // Send error response back to client
                CharacterInventoryResult errorResult = new CharacterInventoryResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Items = new InventoryItemData[0],
                    ResourceItems = new InventoryResourceItemData[0],
                    SubComponents = new InventorySubComponentData[0]
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveCharacterInventoryClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestCharacterInventoryServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveCharacterInventoryClientRpc(CharacterInventoryResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the character inventory result
        Debug.Log($"PlayerManager (Client): Received character inventory result from server. Success: {result.Success}, ItemCount: {result.Items?.Length ?? 0}");
        HandleCharacterInventoryResult(result);
    }

    public void HandleCharacterInventoryResult(CharacterInventoryResult result)
    {
        currentCharacterInventoryResult = result;
        characterInventoryReceived = true;
        Debug.Log($"Client: Received character inventory result. Success: {result.Success}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestWorkbenchListServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received workbench list request, calling ServerManager for accountID={accountID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessWorkbenchListRequest...");
                ServerManager.Instance.ProcessWorkbenchListRequest(accountID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessWorkbenchListRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process workbench list request.");
                
                // Send error response back to client
                WorkbenchListResult errorResult = new WorkbenchListResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Workbenches = new WorkbenchData[0]
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveWorkbenchListClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestWorkbenchListServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveWorkbenchListClientRpc(WorkbenchListResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the workbench list result
        Debug.Log($"PlayerManager (Client): Received workbench list result from server. Success: {result.Success}, WorkbenchCount: {result.Workbenches?.Length ?? 0}");
        HandleWorkbenchListResult(result);
    }

    public void HandleWorkbenchListResult(WorkbenchListResult result)
    {
        currentWorkbenchListResult = result;
        workbenchListReceived = true;
        Debug.Log($"Client: Received workbench list result. Success: {result.Success}");
    }
    #endregion
}