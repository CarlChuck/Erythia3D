using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Threading;
using System;
using Unity.Cinemachine;
using System.Linq;
using Object = UnityEngine.Object;

public class PlayerManager : NetworkBehaviour
{
    #region Local Instance Access
    private static readonly List<PlayerManager> SInstances = new();

    /// Gets the PlayerManager instance  owned by the local client.
    public static PlayerManager LocalInstance
    {
        get
        {
            PlayerManager instance = SInstances.FirstOrDefault(pm => pm.IsOwner);
            return instance;
        }
    }
    #endregion

    #region Helper Classes
    internal NetworkRequestManager requestManager;
    #endregion

    #region References

    [Header("Player Account Info")] [SerializeField]
    private string accountName = "";

    [SerializeField] private string email = "";
    [SerializeField] private string familyName = "";
    [SerializeField] private string language = "en";
    [SerializeField] private string ipAddress = "0.0.0.0";

    [Header("Character Management")] [SerializeField]
    private CharacterModelManager characterModelManager; // Handles character models

    [SerializeField] private GameObject charListParent;
    [SerializeField] private List<PlayerStatBlock> playerCharacters;
    [SerializeField] private PlayerStatBlock selectedPlayerCharacter;
    [SerializeField] private GameObject characterPrefab;

    [Header("Multiplayer Prefabs")] [SerializeField]
    private GameObject networkedPlayerPrefab; // Contains NetworkObject, NetworkedPlayer, controllers
    public GameObject NetworkedPlayerPrefab
    {
        get
        {
            return networkedPlayerPrefab;
            // Public accessor for ServerManager
        }
    }

    [Header("Camera and UI (Persistent)")] [SerializeField]
    private Camera mainCamera;

    [SerializeField] private GameObject playerFollowCam;
    [SerializeField] private UIManager uiManager;

    [Header("Current References")] [SerializeField]
    private NetworkedPlayer currentNetworkedPlayer; // Currently controlled networked player

    [SerializeField]
    private List<NetworkedPlayer> allNetworkedPlayers = new List<NetworkedPlayer>(); // All networked players

    [Header("Legacy References")] [SerializeField]
    private PlayerController playerController; // Legacy

    [SerializeField] private GameObject playerControllerPrefab; // Legacy
    [SerializeField] private GameObject localPlayerControllerPrefab; // Legacy

    [Header("Inventory and Workbenches")] [SerializeField]
    private Transform workbenchParent;

    [SerializeField] private Inventory homeInventory;
    [SerializeField] private WorkBench workBenchPrefab;
    #endregion

    #region State Management
    private bool isInitialized;
    private Task initializationTask;
    
    // Login completion tracking
    private TaskCompletionSource<bool> loginCompletionSource;

    internal bool characterListReceived;
    internal CharacterListResult currentCharacterListResult;

    internal bool accountInventoryReceived;
    internal AccountInventoryResult currentAccountInventoryResult;

    internal bool characterInventoryReceived;
    internal CharacterInventoryResult currentCharacterInventoryResult;

    internal bool workbenchListReceived;
    internal WorkbenchListResult currentWorkbenchListResult;

    internal bool waypointResultReceived;
    internal WaypointResult currentWaypointResult;

    internal bool playerZoneInfoResultReceived;
    internal PlayerZoneInfoResult currentPlayerZoneInfoResult;

    internal bool serverZoneLoadResultReceived;
    internal ServerZoneLoadResult currentServerZoneLoadResult;
    #endregion

    #region Helper Access Properties
    private ulong SteamID { get; set; }
    private int AccountID { get; set; }
    private string AccountName
    {
        get
        {
            return accountName; 
            
        }
        set
        {
            accountName = value; 
            
        }
    }
    private string Email
    {
        get { return email; }
    }
    private string IPAddress
    {
        get
        {
            return ipAddress; 
            
        }
    }
    private string Language
    {
        get
        {
            return language; 
            
        }
    }
    private string FamilyName
    {
        get
        {
            return familyName; 
            
        }
        set
        {
            familyName = value; 
            
        }
    }
    private GameObject CharListParent
    {
        get
        {
            return charListParent; 
            
        }
    }
    private List<PlayerStatBlock> PlayerCharacters
    {
        get
        {
            return playerCharacters; 
            
        }
    }
    private PlayerStatBlock SelectedPlayerCharacter
    {
        get
        {
            return selectedPlayerCharacter; 
            
        }
        set
        {
            selectedPlayerCharacter = value; 
            
        }
    }
    private GameObject CharacterPrefab
    {
        get
        {
            return characterPrefab; 
            
        }
    }
    private GameObject PlayerControllerPrefab
    {
        get
        {
            return playerControllerPrefab; 
            
        }
    }
    private UIManager UIManager
    {
        get
        {
            return uiManager; 
            
        }
    }
    private Inventory HomeInventory
    {
        get
        {
            return homeInventory; 
            
        }
    }
    private WorkBench WorkBenchPrefab
    {
        get
        {
            return workBenchPrefab; 
            
        }
    }
    private Transform WorkbenchParent
    {
        get
        {
            return workbenchParent; 
            
        }
    }
    private List<WorkBench> OwnedWorkbenches { get; } = new();
    #endregion

    #region Initialization
    private void Start()
    {
        Debug.Log($"PlayerManager.Start: IsServer={IsServer}, IsClient={IsClient}, IsHost={IsHost}, IsOwner={IsOwner}");
        playerCharacters ??= new();
    }
    public async Task OnStartInitialization(int newAccountID = 0, ulong newSteamID = 0, string newAccountName = "")
    {
        AccountID = newAccountID;
        SteamID = newSteamID;
        accountName = newAccountName;
        initializationTask = InitializePlayerManagerAsync();
        await initializationTask;
    }
    private async Task InitializePlayerManagerAsync()
    {
        isInitialized = false;
        await LoginAsync();
        await LoadCharactersAsync();
        await SetupInitialCharacterUI();
        await LoadAllInventoriesAsync();
        isInitialized = true;
        Debug.Log("PlayerManager Initialization Complete.");
    }
    private async Task SetupInitialCharacterUI()
    {
        if (selectedPlayerCharacter != null)
        {
            uiManager.SetupUI(selectedPlayerCharacter);
            Debug.Log($"UIManager setup for selected character: {selectedPlayerCharacter.GetCharacterName()}");
        }

        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.SetCharCreationButton(selectedPlayerCharacter == null);
        }
    }
    #endregion

    #region Login
    private async Task LoginAsync()
    {
        if (!IsServer)
        {
            loginCompletionSource = new();
            LoginRpc();
            
            // Wait for login response with timeout (10 seconds)
            Task delayTask = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(loginCompletionSource.Task, delayTask);
            
            if (completedTask == delayTask)
            {
                Debug.LogError("LoginAsync: Login timeout after 10 seconds");
                throw new TimeoutException("Login request timed out");
            }
            
            // Check if login was successful
            bool loginSuccess = await loginCompletionSource.Task;
            if (!loginSuccess)
            {
                Debug.LogError("LoginAsync: Login failed");
                throw new InvalidOperationException("Login failed");
            }
            
            Debug.Log("PlayerManager: Login completed successfully");
        }
    }
    
    [Rpc(SendTo.Server)]
    private void LoginRpc()
    {
        ServerManager.HandleLogin(this, SteamID, AccountID);
    }

    [Rpc(SendTo.Owner)]
    public void ReceiveLoginRpc(LoginResult result)
    {
        AccountID = result.AccountID;
        AccountName = result.AccountName;
        SteamID = result.SteamID;
        // Complete the login task with the result
        loginCompletionSource?.SetResult(result.Success);
    }
    #endregion

    #region Character Creation
    public async Task OnCreateCharacter(string characterName, int charRace, int charGender, int charFace)
    {
        int charStartingZone = 1; // Use the player's starting zone
        charStartingZone = GetStartingZoneByRace(charRace);
        if (string.IsNullOrEmpty(FamilyName) || string.IsNullOrEmpty(characterName))
        {
            Debug.LogError("CharacterDataHandler: Character or Family Name cannot be empty");
            return;
        }

        if (AccountID <= 0)
        {
            Debug.LogError("CharacterDataHandler: Cannot create character: Invalid AccountID.");
            return;
        }
        
        // Use CharactersManager directly for character creation
        bool created = await CharactersManager.Instance.CreateNewCharacterAsync(
            AccountID, 
            FamilyName, 
            characterName, 
            null,
            charStartingZone, 
            charRace, 
            charGender, 
            charFace
        );

        if (created)
        {
            await LoadCharactersAsync();
            
            // Only update UI on client side
            if (SelectedPlayerCharacter != null && UIManager != null && !IsServer)
            {
                UIManager.SetupUI(SelectedPlayerCharacter);
            }
        }
        else
        {
            Debug.LogError($"CharacterDataHandler: Failed to create character: {characterName}");
        }
    }
    public async Task CreateCharacterAsync(string characterName, int charRace, int charGender, int charFace)
    {
        
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

    #region Character List

    [Rpc(SendTo.Server)]
    internal void RequestCharacterListRpc(int accountID)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log(
                $"PlayerManager (Server): Received character list request, calling ServerManager for accountID={accountID}");

            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessCharacterListRequest...");
                //TODO fix this bs
                //ServerManager.Instance.ProcessCharacterListRequest(this, accountID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessCharacterListRequest called successfully");
            }
            else
            {
                Debug.LogError(
                    "PlayerManager (Server): ServerManager.Instance is null! Cannot process character list request.");

                // Send error response back to client
                CharacterListResult errorResult = new CharacterListResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Characters = Array.Empty<CharacterData>()
                };

                ReceiveCharacterListRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestCharacterListServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    public void ReceiveCharacterListRpc(CharacterListResult result)
    {
        // This runs on the client - handle the character list result
        Debug.Log($"PlayerManager (Client): Received character list result from server. Success: {result.Success}, CharacterCount: {result.Characters?.Length ?? 0}");
        HandleCharacterListResult(result);
    }
    private void HandleCharacterListResult(CharacterListResult result)
    {
        currentCharacterListResult = result;
        characterListReceived = true;
        Debug.Log(
            $"PlayerManager: Received character list result. Success: {result.Success}. IsServer: {IsServer}, IsClient: {IsClient}");

        switch (result.Success)
        {
            // For clients, process character data through CharacterDataHandler
            // Server will receive character data via direct RPC from client
            case true when (!IsServer || IsClient):
                Debug.Log($"PlayerManager (Client): Processing character list result through CharacterDataHandler...");
                _ = ProcessCharacterListResult(result);
                break;
            case false:
                Debug.LogError($"PlayerManager: Character list result failed: {result.ErrorMessage}");
                break;
            default:
            {
                if (IsServer && !IsClient)
                {
                    Debug.Log(
                        $"PlayerManager (Server): Skipping character list processing - will receive character data via RPC from client");
                }

                break;
            }
        }
    }
    private async Task LoadCharactersAsync()
    {

    }
    private async Task ProcessCharacterListResult(CharacterListResult result)
    {
        try
        {
            ClearPlayerListExceptSelected();

            foreach (CharacterData characterData in result.Characters)
            {
                // Check if character already loaded (avoid duplicates)
                if (CheckIfCharacterExists(characterData.CharID)) 
                { 
                    continue; 
                }

                // Load FamilyName ONLY if not already set
                if (string.IsNullOrEmpty(FamilyName) && !string.IsNullOrEmpty(characterData.FamilyName))
                {
                    FamilyName = characterData.FamilyName;
                }

                PlayerStatBlock newCharacter = InstantiateCharacter(characterData);
                if (newCharacter != null)
                {
                    PlayerCharacters.Add(newCharacter);

                    //Sets the first character loaded as the selected character if none is selected
                    if (SelectedPlayerCharacter == null)
                    {
                        SelectedPlayerCharacter = newCharacter;
                    }
                }
            }
            EnsureSelectedCharacterInList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during ProcessCharacterListResult: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Character Data

    [Rpc(SendTo.Server)]
    private void SyncCharacterDataToServerRpc(PlayerCharacterData characterData)
    {
        if (!IsServer)
        {
            Debug.LogError("PlayerManager: SyncCharacterDataToServerRpc called on non-server!");
            return;
        }

        try
        {
            Debug.Log(
                $"PlayerManager (Server): Received character data sync for character ID: {characterData.CharacterID}");

            // Create PlayerStatBlock on server with the received data
            CreateServerPlayerStatBlock(characterData);

            Debug.Log(
                $"PlayerManager (Server): Successfully synced character data for character: {characterData.CharacterName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerManager (Server): Error syncing character data: {ex.Message}");
        }
    }
    private void CreateServerPlayerStatBlock(PlayerCharacterData characterData)
    {
        if (!IsServer)
        {
            Debug.LogWarning("PlayerManager: CreateServerPlayerStatBlock called on client!");
            return;
        }

        try
        {
            // Clear existing characters on server
            if (playerCharacters == null)
            {
                playerCharacters = new List<PlayerStatBlock>();
            }
            else
            {
                // Clear existing character data but keep list
                foreach (var existingChar in playerCharacters)
                {
                    if (existingChar != null)
                    {
                        Destroy(existingChar.gameObject);
                    }
                }

                playerCharacters.Clear();
            }

            // Create new PlayerStatBlock from character data
            GameObject newCharObj = Instantiate(characterPrefab, charListParent.transform);
            PlayerStatBlock newPlayerChar = newCharObj.GetComponent<PlayerStatBlock>();

            if (newPlayerChar == null)
            {
                Debug.LogError("PlayerManager (Server): Character prefab missing PlayerStatBlock component!");
                Destroy(newCharObj);
                return;
            }

            // Use the existing SetUpCharacter method which handles all initialization
            newPlayerChar.SetUpCharacter(
                characterData.CharacterName, // character name
                characterData.CharacterID, // character ID
                "", // title (empty for now)
                1, // zone ID (default to 1)
                characterData.Race, // race
                characterData.Face, // face
                characterData.Gender, // gender
                characterData.CombatExp, // combat XP
                characterData.CraftingExp, // crafting XP
                characterData.ArcaneExp, // arcane XP
                characterData.SpiritExp, // spirit XP
                characterData.VeilExp, // veil XP
                characterData.BaseStrength, // species strength
                characterData.BaseDexterity, // species dexterity
                characterData.BaseConstitution, // species constitution
                characterData.BaseIntelligence, // species intelligence
                characterData.BaseSpirit // species spirit
            );

            // Add to characters list and set as selected
            playerCharacters.Add(newPlayerChar);
            selectedPlayerCharacter = newPlayerChar;

            Debug.Log(
                $"PlayerManager (Server): Created PlayerStatBlock for character {characterData.CharacterName} (ID: {characterData.CharacterID})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerManager (Server): Error creating server PlayerStatBlock: {ex.Message}");
        }
    }
   
    #endregion
    
    #region Character Spawning

    [Rpc(SendTo.Server)]
    private void SpawnNetworkedPlayerRpc(int characterID, int characterRace, int characterGender)
    {
        //TODO
        ulong clientId = 0;
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Received request from client {clientId} to spawn character {characterID}.");

        // --- Server-Authoritative Spawn Position ---
        Vector3 spawnPosition = Vector3.zero;
        if (ServerManager.Instance != null)
        {
            // This is a simplified call. In a real scenario, you might need an async method.
            spawnPosition = ServerManager.Instance.GetSpawnPositionForCharacter(characterID);
            Debug.Log($"SpawnNetworkedPlayerServerRpc: Determined spawn position from ServerManager: {spawnPosition}");
        }
        else
        {
            Debug.LogError(
                "SpawnNetworkedPlayerServerRpc: ServerManager.Instance is null! Cannot determine spawn position. Defaulting to origin.");
        }
        // -----------------------------------------

        // Instantiate the networked player prefab
        GameObject playerObject = Instantiate(networkedPlayerPrefab, spawnPosition, Quaternion.identity);
        if (playerObject == null)
        {
            Debug.LogError(
                $"SpawnNetworkedPlayerServerRpc: Failed to instantiate networkedPlayerPrefab for client {clientId}.");
            return;
        }

        Debug.Log(
            $"SpawnNetworkedPlayerServerRpc: Instantiated player prefab for client {clientId} at {spawnPosition}.");

        // Get the NetworkObject component
        var networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError(
                $"SpawnNetworkedPlayerServerRpc: networkedPlayerPrefab does not have a NetworkObject component. Cannot spawn for client {clientId}.");
            Destroy(playerObject);
            return;
        }

        // Spawn the object and assign ownership
        networkObject.SpawnAsPlayerObject(clientId);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Spawning NetworkObject for client {clientId} with ownership.");

        // Get the NetworkedPlayer component and initialize it
        var networkedPlayer = playerObject.GetComponent<NetworkedPlayer>();
        if (networkedPlayer != null)
        {
            // Set the character's identity via NetworkVariables.
            // This will trigger the model to spawn on all clients.
            networkedPlayer.SetCharacterVisuals(characterRace, characterGender);
            Debug.Log(
                $"SpawnNetworkedPlayerServerRpc: Set character visuals for race {characterRace}, gender {characterGender}.");
        }
        else
        {
            Debug.LogError(
                $"SpawnNetworkedPlayerServerRpc: Could not find NetworkedPlayer component on the spawned object for client {clientId}.");
        }
        
        NotifyNetworkedPlayerSpawnedRpc(networkObject.NetworkObjectId, spawnPosition);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Sent NotifyNetworkedPlayerSpawnedClientRpc to client {clientId}.");
    }

    [Rpc(SendTo.Owner)]
    private void NotifyNetworkedPlayerSpawnedRpc(ulong networkObjectId, Vector3 spawnPosition)
    {
        Debug.Log(
            $"NotifyNetworkedPlayerSpawnedClientRpc: Received notification to find NetworkObject with ID {networkObjectId}.");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId,
                out NetworkObject networkObject))
        {
            if (networkObject != null)
            {
                currentNetworkedPlayer = networkObject.GetComponent<NetworkedPlayer>();
                if (currentNetworkedPlayer != null)
                {
                    Debug.Log(
                        $"NotifyNetworkedPlayerSpawnedClientRpc: Successfully found and assigned NetworkedPlayer (ID: {currentNetworkedPlayer.OwnerClientId}).");
                    if (IsOwner)
                    {
                        // The async spawning method will handle setting the camera, but we can do it here as well
                        // to ensure it's set as early as possible for the owner.
                        Transform cameraTarget = currentNetworkedPlayer.GetPlayerCameraRoot();
                        if (cameraTarget != null)
                        {
                            SetCameraTarget(cameraTarget);
                        }
                        else
                        {
                            // Fallback to the movement transform if camera root isn't found
                            SetCameraTarget(currentNetworkedPlayer.GetMovementTransform());
                        }
                    }
                }
                else
                {
                    Debug.LogError(
                        $"NotifyNetworkedPlayerSpawnedClientRpc: Found NetworkObject but it doesn't have a NetworkedPlayer component.");
                }
            }
            else
            {
                Debug.LogError(
                    $"NotifyNetworkedPlayerSpawnedClientRpc: NetworkObject with ID {networkObjectId} is null after retrieval.");
            }
        }
        else
        {
            Debug.LogError(
                $"NotifyNetworkedPlayerSpawnedClientRpc: Could not find spawned NetworkObject with ID {networkObjectId}.");
        }
    }
    private async Task SpawnNetworkedPlayerAsync(PlayerStatBlock playerStatBlock)
    {
        int characterID = playerStatBlock.GetCharacterID();
        Debug.Log(
            $"SpawnNetworkedPlayerAsync: Requesting server to spawn player (CharacterID: {characterID}). The server will determine the spawn position.");

        // We no longer need to calculate spawn position on the client.
        // The server will be authoritative for the spawn location.
        SpawnNetworkedPlayerRpc(characterID, playerStatBlock.GetSpecies(), playerStatBlock.GetGender());

        // Wait for the networked player to be spawned and registered
        float timeout = 10f; // 10 seconds timeout
        float timer = 0f;
        while (currentNetworkedPlayer == null && timer < timeout)
        {
            await Task.Delay(100);
            timer += 0.1f;
        }

        if (currentNetworkedPlayer == null)
        {
            throw new System.Exception("Timeout waiting for networked player to spawn");
        }
        else
        {
            Debug.Log(
                $"SpawnNetworkedPlayerAsync: Successfully spawned and registered networked player (ID: {currentNetworkedPlayer.OwnerClientId}).");

            // Setup camera for the newly spawned player
            Transform cameraTarget = currentNetworkedPlayer.GetPlayerCameraRoot();
            if (cameraTarget != null)
            {
                SetCameraTarget(cameraTarget);
            }
            else
            {
                // Fallback to the movement transform if camera root isn't found
                SetCameraTarget(currentNetworkedPlayer.GetMovementTransform());
            }
        }
    } 
    public async Task SetupAndSpawnSelectedCharacterAsync()
    {
        if (selectedPlayerCharacter == null)
        {
            Debug.LogError("PlayerManager: Cannot start zone transition - no character selected");
            return;
        }

        // The client no longer handles scene transitions.
        // It simply tells the server it's ready and provides the necessary character data.
        Debug.Log($"PlayerManager: Requesting server to transition to zone for character {selectedPlayerCharacter.GetCharacterID()}.");
        RequestZoneTransitionRpc(selectedPlayerCharacter.GetCharacterID(), selectedPlayerCharacter.GetSpecies(), selectedPlayerCharacter.GetGender());

        // The old flow of calling ZoneCoordinator and SpawnNetworkedPlayerAsync from the client is now obsolete.
        // The server will handle all of it.
    }
    #endregion
    
    #region Inventory
    public async Task LoadAllInventoriesAsync()
    {
        await LoadAllCharactersInventoryAsync();
        await LoadAccountInventoryAsync();
        await LoadOwnedWorkbenchesAsync();
    }
    #region Character Inventory
    
    [Rpc(SendTo.Server)]
    internal void RequestCharacterInventoryRpc(int characterID)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log(
                $"PlayerManager (Server): Received character inventory request, calling ServerManager for characterID={characterID}");

            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessCharacterInventoryRequest...");
                //TODO fix this
                //ServerManager.Instance.ProcessCharacterInventoryRequest(characterID, serverRpcParams.Receive.SenderClientId);
                Debug.Log(
                    $"PlayerManager (Server): ServerManager.ProcessCharacterInventoryRequest called successfully");
            }
            else
            {
                Debug.LogError(
                    "PlayerManager (Server): ServerManager.Instance is null! Cannot process character inventory request.");

                // Send error response back to client
                CharacterInventoryResult errorResult = new CharacterInventoryResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    Items = new InventoryItemData[0],
                    ResourceItems = new InventoryResourceItemData[0],
                    SubComponents = new InventorySubComponentData[0]
                };

                ReceiveCharacterInventoryRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestCharacterInventoryServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceiveCharacterInventoryRpc(CharacterInventoryResult result)
    {
        // This runs on the client - handle the character inventory result
        Debug.Log(
            $"PlayerManager (Client): Received character inventory result from server. Success: {result.Success}, ItemCount: {result.Items?.Length ?? 0}");
        HandleCharacterInventoryResult(result);
    }
    private void HandleCharacterInventoryResult(CharacterInventoryResult result)
    {
        currentCharacterInventoryResult = result;
        characterInventoryReceived = true;
        Debug.Log($"Client: Received character inventory result. Success: {result.Success}");
    }
    private async Task LoadAllCharactersInventoryAsync()
    {
        if (PlayerCharacters == null || PlayerCharacters.Count == 0)
        {
            Debug.LogWarning("InventoryDataHandler: No characters to load inventory for.");
            return;
        }

        foreach (var character in PlayerCharacters)
        {
            await LoadCharacterInventoryAsync(character);
        }
    }
    private async Task LoadCharacterInventoryAsync(PlayerStatBlock character)
    {

    }
    private async Task ProcessCharacterInventoryResult(CharacterInventoryResult result, PlayerStatBlock character, Inventory inventory, EquipmentProfile equipment)
    {
        int charId = character.GetCharacterID();
        
        try
        {            
            inventory.ClearInventory();
            equipment.ClearEquipmentProfile();

            // Load Inventory Items and Equip them if applicable
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: Item with ID {itemData.ItemID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (itemData.SlotID > 0)
                {
                    // Equip Item
                    EquipItem(itemData, itemInstance, equipment, inventory);
                }
                else
                {
                    // Add to Inventory Bag
                    if (!inventory.AddItem(itemInstance))
                    {
                        Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag for character {charId}.");
                    }
                }
            }

            // Load Inventory Resource Items
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                if (!inventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to inventory bag for character {charId}.");
                }
            }

            // Load Inventory SubComponents
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (!inventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to inventory bag for character {charId}.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing character inventory result for character {charId}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion
    
    #region Account Inventory
    [Rpc(SendTo.Server)]
    internal void RequestAccountInventoryRpc(int accountID)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received account inventory request, calling ServerManager for accountID={accountID}");

            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessAccountInventoryRequest...");
                //TODO fix this bs
                //ServerManager.Instance.ProcessAccountInventoryRequest(accountID, serverRpcParams.Receive.SenderClientId);
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
                    Items = Array.Empty<InventoryItemData>(),
                    ResourceItems = Array.Empty<InventoryResourceItemData>(),
                    SubComponents = Array.Empty<InventorySubComponentData>(),
                    Workbenches = Array.Empty<WorkbenchData>()
                };
                ReceiveAccountInventoryRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestAccountInventoryServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceiveAccountInventoryRpc(AccountInventoryResult result)
    {
        // This runs on the client - handle the account inventory result
        Debug.Log(
            $"PlayerManager (Client): Received account inventory result from server. Success: {result.Success}, ItemCount: {result.Items?.Length ?? 0}");
        HandleAccountInventoryResult(result);
    }
    private void HandleAccountInventoryResult(AccountInventoryResult result)
    {
        currentAccountInventoryResult = result;
        accountInventoryReceived = true;
        Debug.Log($"Client: Received account inventory result. Success: {result.Success}");
    }
    private async Task LoadAccountInventoryAsync()
    {

    }
    private async Task ProcessAccountInventoryResult(AccountInventoryResult result)
    {
        try
        {
            HomeInventory.ClearInventory();

            // Load Inventory Items
            foreach (var itemData in result.Items)
            {
                Item itemInstance = ItemManager.Instance.GetItemInstanceByID(itemData.ItemID);
                if (itemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: Item with ID {itemData.ItemID} not found via ItemManager for account inventory. Cannot load.");
                    continue;
                }

                if (!HomeInventory.AddItem(itemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory for account {AccountID}.");
                }
            }

            // Load Account Inventory Resource Items
            foreach (var resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = GetResourceItemById(resourceItemData.ResourceItemID);
                if (resourceItemInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: ResourceItem instance with ID {resourceItemData.ResourceItemID} not found. Cannot load.");
                    continue;
                }

                if (!HomeInventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceItemID}) to home inventory for account {AccountID}.");
                }
            }

            // Load Account Inventory SubComponents
            foreach (var subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = ItemManager.Instance.GetSubComponentInstanceByID(subCompData.SubComponentID);
                if (subComponentInstance == null)
                {
                    Debug.LogWarning($"InventoryDataHandler: SubComponent instance with ID {subCompData.SubComponentID} not found via ItemManager. Cannot load.");
                    continue;
                }

                if (!HomeInventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to home inventory for account {AccountID}.");
                }
            }

            // Load Owned Workbenches from account inventory
            await ProcessOwnedWorkbenches(result.Workbenches);
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing account inventory result: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Workbench Management
    [Rpc(SendTo.Server)]
    internal void RequestWorkbenchListRpc(int accountID)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received workbench list request, calling ServerManager for accountID={accountID}");

            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessWorkbenchListRequest...");
                //TODO do this right
                //ServerManager.Instance.ProcessWorkbenchListRequest(accountID, serverRpcParams.Receive.SenderClientId);
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
                    Workbenches = Array.Empty<WorkbenchData>()
                };

                ReceiveWorkbenchListRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestWorkbenchListServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceiveWorkbenchListRpc(WorkbenchListResult result)
    {
        // This runs on the client - handle the workbench list result
        Debug.Log(
            $"PlayerManager (Client): Received workbench list result from server. Success: {result.Success}, WorkbenchCount: {result.Workbenches?.Length ?? 0}");
        HandleWorkbenchListResult(result);
    }
    private void HandleWorkbenchListResult(WorkbenchListResult result)
    {
        currentWorkbenchListResult = result;
        workbenchListReceived = true;
        Debug.Log($"Client: Received workbench list result. Success: {result.Success}");
    }
    private async Task LoadOwnedWorkbenchesAsync()
    {

    }
    private async Task ProcessOwnedWorkbenches(WorkbenchData[] workbenches)
    {
        OwnedWorkbenches.Clear();

        foreach (WorkbenchData workbenchData in workbenches)
        {
            int workbenchType = workbenchData.WorkBenchType;

            WorkBench newWorkBenchInstance = Object.Instantiate(WorkBenchPrefab, WorkbenchParent);
            newWorkBenchInstance.SetWorkbenchType(workbenchType);

            if (WorkBenchManager.Instance != null)
            {
                WorkBench templateWorkBench = WorkBenchManager.Instance.GetWorkbenchByType(workbenchType);
                if (templateWorkBench != null)
                {
                    newWorkBenchInstance.InitializeRecipes(templateWorkBench.Recipes);
                }
                else
                {
                    Debug.LogWarning($"InventoryDataHandler: No template workbench found in WorkBenchManager for type {workbenchType}. Initializing with empty recipes.");
                    newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
                }
            }
            else
            {
                newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
            }

            OwnedWorkbenches.Add(newWorkBenchInstance);
        }
    }

    #endregion
    private void EquipItem(InventoryItemData itemData, Item itemInstance, EquipmentProfile equipment, Inventory inventory)
    {
        ItemType slotType = MapSlotIdToItemType(itemData.SlotID);
        if (slotType != ItemType.Other)
        {
            int slotIndex = GetSlotIndexForType(itemData.SlotID);
            EquipmentSlot targetSlot = equipment.GetSlotForItemType(slotType, slotIndex);
            if (targetSlot != null)
            {
                equipment.EquipItemToSlot(itemInstance, targetSlot);
            }
            else
            {
                Debug.LogWarning($"InventoryDataHandler: Could not find EquipmentSlot for SlotID: {itemData.SlotID} (Type: {slotType}, Index: {slotIndex}). Cannot equip {itemInstance.ItemName}.");
                inventory.AddItem(itemInstance); // Put in bag as fallback
            }
        }
        else
        {
            Debug.LogWarning($"InventoryDataHandler: Invalid SlotID {itemData.SlotID} found for ItemID {itemData.ItemID}. Cannot equip.");
            inventory.AddItem(itemInstance); // Put in bag as fallback
        }
    }
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
    private ResourceItem GetResourceItemById(int resourceItemId)
    {
        // TODO: Implement proper ResourceItem lookup
        ResourceItem resourceItem = null;
        return resourceItem;
    }
    #endregion

    #region Player Zone Management

    [Rpc(SendTo.Server)]
    private void RequestZoneTransitionRpc(int characterId, int species, int gender)
    {
        /*
        if (ServerManager.Instance != null)
        {
            ServerManager.Instance.HandleClientZoneTransitionRequest(serverRpcParams.Receive.SenderClientId, characterId, race, gender);
        }
        else
        {
            Debug.LogError("ServerManager not found! Cannot handle zone transition request.");
        }*/
    }
    
    [Rpc(SendTo.Server)]
    internal void RequestWaypointRpc(WaypointRequest request)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessWaypointRequest...");
                //TODO do this right
                //ServerManager.Instance.ProcessWaypointRequest(request.CharacterID, request.ZoneName, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessWaypointRequest called successfully");
            }
            else
            {
                Debug.LogError(
                    "PlayerManager (Server): ServerManager.Instance is null! Cannot process waypoint request.");

                // Send error response back to client
                WaypointResult errorResult = new WaypointResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = request.ZoneName
                };

                ReceiveWaypointResultRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestWaypointServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceiveWaypointResultRpc(WaypointResult result)
    {
        // This runs on the client - handle the waypoint result
        HandleWaypointResult(result);
    }
    private void HandleWaypointResult(WaypointResult result)
    {
        currentWaypointResult = result;
        waypointResultReceived = true;
    }

    [Rpc(SendTo.Server)]
    internal void RequestPlayerZoneInfoRpc(int characterID)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received player zone info request for character {characterID}");

            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessPlayerZoneInfoRequest...");

                //TODO sort out how to handle this properly
                //ServerManager.Instance.ProcessPlayerZoneInfoRequest(characterID);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessPlayerZoneInfoRequest called successfully");
            }
            else
            {
                Debug.LogError(
                    "PlayerManager (Server): ServerManager.Instance is null! Cannot process player zone info request.");

                // Send error response back to client
                PlayerZoneInfoResult errorResult = new PlayerZoneInfoResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    ZoneInfo = new PlayerZoneInfo
                    {
                        CharacterID = characterID,
                        ZoneID = 1,
                        ZoneName = "IthoriaSouth",
                        SpawnPosition = null,
                        RequiresMarketWaypoint = true
                    }
                };

                ReceivePlayerZoneInfoRpc(errorResult);
            }
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestPlayerZoneInfoServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceivePlayerZoneInfoRpc(PlayerZoneInfoResult result)
    {
        // This runs on the client - handle the player zone info result
        Debug.Log($"PlayerManager (Client): Received player zone info result from server. Success: {result.Success}");
        HandlePlayerZoneInfoResult(result);
    }
    private void HandlePlayerZoneInfoResult(PlayerZoneInfoResult result)
    {
        currentPlayerZoneInfoResult = result;
        playerZoneInfoResultReceived = true;
    }

    [Rpc(SendTo.Server)]
    internal void RequestServerLoadZoneRpc(string zoneName)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received server zone load request for zone: {zoneName}");

            // For now, just send a success response as zone loading might be handled differently
            ServerZoneLoadResult result = new ServerZoneLoadResult
            {
                Success = true,
                ErrorMessage = ""
            };

            ReceiveServerLoadZoneResultRpc(result);
        }
        else
        {
            Debug.LogError(
                "PlayerManager: RequestServerLoadZoneServerRpc called on client! This should only run on server.");
        }
    }

    [Rpc(SendTo.Owner)]
    private void ReceiveServerLoadZoneResultRpc(ServerZoneLoadResult result)
    {
        // This runs on the client - handle the server zone load result
        Debug.Log($"PlayerManager (Client): Received server zone load result from server. Success: {result.Success}");
        HandleServerLoadZoneResult(result);
    }
    private void HandleServerLoadZoneResult(ServerZoneLoadResult result)
    {
        currentServerZoneLoadResult = result;
        serverZoneLoadResultReceived = true;
    }
    public async Task SetupSelectedCharacterAsync(PlayerStatBlock selectedCharacter)
    {
        try
        {
            int characterID = selectedCharacter.GetCharacterID();

            // Step 1: Get player zone information
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            
            if (string.IsNullOrEmpty(zoneInfo.ZoneName))
            {
                Debug.LogError($"ZoneCoordinator: Failed to determine zone for character {characterID}");
                return;
            }

            // Step 2: Load the appropriate zone scene
            await LoadCharacterZoneAsync(zoneInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error in SetupSelectedCharacterAsync: {ex.Message}");
        }
    }
    public async Task SetupSelectedCharacterLegacyAsync(PlayerStatBlock selectedCharacter)
    {
        try
        {
            int characterID = selectedCharacter.GetCharacterID();

            // Step 1: Get player zone information
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            
            if (string.IsNullOrEmpty(zoneInfo.ZoneName))
            {
                Debug.LogError($"ZoneCoordinator: Failed to determine zone for character {characterID}");
                return;
            }

            // Step 2: Load the appropriate zone scene
            await LoadCharacterZoneAsync(zoneInfo);

            // Step 3: Create and position PlayerController (legacy)
            await SetupPlayerControllerAsync(selectedCharacter, zoneInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error in SetupSelectedCharacterLegacyAsync: {ex.Message}");
        }
    }
    private async Task<PlayerZoneInfo> GetPlayerZoneInfoInternalAsync(int characterID)
    {
        return new PlayerZoneInfo();
    }
    private async Task LoadCharacterZoneAsync(PlayerZoneInfo zoneInfo)
    {
        try
        {
            // Step 1: Request server to load the zone first
            await RequestServerLoadZoneAsync(zoneInfo.ZoneName);

            // Step 2: Unload MainMenu when transitioning to gameplay zones
            await UnloadMainMenuIfNeeded(zoneInfo.ZoneName);

            // Step 3: Load zone on client side
            await LoadZoneOnClient(zoneInfo.ZoneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error loading zone '{zoneInfo.ZoneName}': {ex.Message}");
            throw;
        }
    }
    private async Task SetupPlayerControllerAsync(PlayerStatBlock selectedCharacter, PlayerZoneInfo zoneInfo)
    {
        try
        {
            // Step 1: Determine spawn position
            Vector3 spawnPosition = await DetermineSpawnPositionAsync(zoneInfo);

            // Step 2: Instantiate PlayerController
            if (PlayerControllerPrefab == null)
            {
                Debug.LogError("ZoneCoordinator: PlayerControllerPrefab is null! Cannot instantiate PlayerController.");
                return;
            }

            GameObject controllerGO = GameObject.Instantiate(PlayerControllerPrefab);
            PlayerController controller = controllerGO.GetComponent<PlayerController>();
            
            if (controller == null)
            {
                Debug.LogError("ZoneCoordinator: PlayerController component not found on instantiated prefab!");
                GameObject.Destroy(controllerGO);
                return;
            }

            // Step 3: Setup controller
            controller.SetCharacterPosition(spawnPosition);

            // TODO: Link controller to selected character
            // controller.SetPlayerStatBlock(selectedCharacter);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error setting up PlayerController: {ex.Message}");
        }
    }
    private async Task RequestServerLoadZoneAsync(string zoneName)
    {
        //TODO
    }
    private async Task UnloadMainMenuIfNeeded(string zoneName)
    {

    }
    private async Task LoadZoneOnClient(string zoneName)
    {
       
    }
    public async Task<PlayerZoneInfo> GetPlayerZoneInfoAsync(int characterID)
    {
        return await GetPlayerZoneInfoInternalAsync(characterID);
    }
    public async Task<Vector3> GetSpawnPositionAsync(int characterID)
    {
        try
        {
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            Vector3 spawnPosition = await DetermineSpawnPositionAsync(zoneInfo);
            
            return spawnPosition;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error getting spawn position for character {characterID}: {ex.Message}");
            return Vector3.zero;
        }
    }
    private async Task<Vector3> DetermineSpawnPositionAsync(PlayerZoneInfo zoneInfo)
    {
        try
        {
            Vector3 spawnPosition;

            if (zoneInfo.SpawnPosition.HasValue && !zoneInfo.RequiresMarketWaypoint)
            {
                // Use stored database position
                spawnPosition = zoneInfo.SpawnPosition.Value;
            }
            else
            {

                Vector3? waypointPosition = await GetMarketWaypointPositionWithRetryAsync(zoneInfo.ZoneName);
                
                if (waypointPosition.HasValue)
                {
                    spawnPosition = waypointPosition.Value;
                }
                else
                {
                    // Fallback to origin if no waypoint found
                    spawnPosition = Vector3.zero;
                    Debug.LogWarning($"ZoneCoordinator: No MarketWaypoint found for zone '{zoneInfo.ZoneName}', using origin (0,0,0)");
                }
            }

            // Check for problematic origin position
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogWarning($"ZoneCoordinator: WARNING - Character positioned at origin (0,0,0). This might cause 'hanging in space' if there's no ground at origin!");
            }

            return spawnPosition;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error determining spawn position: {ex.Message}");
            return Vector3.zero; // Safe fallback
        }
    }
    private async Task<Vector3?> GetMarketWaypointPositionWithRetryAsync(string zoneName)
    {
        int maxRetries = 3;
        int retryDelay = 1000; // 1 second between retries
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            
            Vector3? waypointPosition = await GetMarketWaypointPositionAsync(zoneName);
            
            if (waypointPosition.HasValue)
            {
                return waypointPosition;
            }
            
            // Wait before retrying (except on last attempt)
            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelay);
            }
        }
        
        Debug.LogWarning($"ZoneCoordinator: Failed to find MarketWaypoint for zone '{zoneName}' after {maxRetries} attempts");
        return null;
    }
    private async Task<Vector3?> GetMarketWaypointPositionAsync(string zoneName)
    {
        return null;
    }
    private PlayerZoneInfo GetFallbackZoneInfo(int characterID)
    {
        return new PlayerZoneInfo
        {
            CharacterID = characterID,
            ZoneID = 1,
            ZoneName = "IthoriaSouth",
            SpawnPosition = null,
            RequiresMarketWaypoint = true
        };
    }
    #endregion

    #region Getters
    public string GetFamilyName()
    {
        return familyName;
    }
    public async Task<PlayerStatBlock> GetSelectedPlayerCharacterAsync()
    {
        if (!isInitialized && initializationTask != null && !initializationTask.IsCompleted)
        {
            Debug.LogWarning("GetSelectedPlayerCharacterAsync called before initialization complete. Waiting...");
            await initializationTask;
        }

        return selectedPlayerCharacter;
    }
    public PlayerStatBlock GetSelectedPlayerCharacter()
    {
        if (!isInitialized)
        {
            Debug.LogWarning(
                "Synchronous GetSelectedPlayerCharacter called before PlayerManager is initialized! Returning potentially null character.");
        }

        return selectedPlayerCharacter;
    }
    public UIManager GetUIManager()
    {
        return uiManager;
    }
    private async Task<List<PlayerStatBlock>> GetCharactersAsync()
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
            playerCharacters = new List<PlayerStatBlock>();
            Debug.LogWarning("PlayerCharacters list was null after initialization check, re-initializing.");
            // Re-running SetCharactersListAsync might be needed if initialization failed partially
            // For now, just return the empty list. Consider more robust recovery if needed.
            // await SetCharactersListAsync();
        }

        return playerCharacters;
    }
    private List<PlayerStatBlock> GetCharacters()
    {
        if (!isInitialized)
        {
            Debug.LogWarning(
                "Synchronous GetCharacters called before PlayerManager is initialized! Returning potentially empty/incomplete list.");
            // Cannot easily await here. Return current state.
            return playerCharacters ?? new List<PlayerStatBlock>(); // Return empty list if null
        }

        return playerCharacters;
    }
    public List<WorkBench> GetOwnedWorkbenches()
    {
        return OwnedWorkbenches;
    }
    public Inventory GetHomeInventory()
    {
        return homeInventory;
    }
    public PlayerController GetControlledCharacter()
    {
        return playerController;
    }
    public Camera GetMainCamera()
    {
        return mainCamera;
    }
    public void SetCameraTarget(Transform target)
    {
        if (target == null)
        {
            Debug.LogError("PlayerManager: Cannot set camera target - target transform is null");
            return;
        }

        if (playerFollowCam == null)
        {
            Debug.LogError("PlayerManager: Cannot set camera target - playerFollowCam is null");
            return;
        }

        var cinemachineCamera = playerFollowCam.GetComponent<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            // Set both Follow and LookAt to the target
            cinemachineCamera.Follow = target;
            cinemachineCamera.LookAt = target;

            Debug.Log($"PlayerManager: Camera target set to '{target.name}' at position: {target.position}");
            Debug.Log(
                $"PlayerManager: Camera Follow: {cinemachineCamera.Follow?.name}, LookAt: {cinemachineCamera.LookAt?.name}");

            // Additional validation
            if (Vector3.Distance(target.position, Vector3.zero) < 0.1f)
            {
                Debug.LogWarning(
                    $"PlayerManager: Camera target '{target.name}' is at or very close to origin (0,0,0) - this might cause camera issues");
            }
        }
        else
        {
            Debug.LogError(
                $"PlayerManager: PlayerFollowCam '{playerFollowCam.name}' does not have a CinemachineCamera component!");

            // Try to find CinemachineCamera in children
            var childCinemachine = playerFollowCam.GetComponentInChildren<CinemachineCamera>();
            if (childCinemachine != null)
            {
                Debug.Log(
                    $"PlayerManager: Found CinemachineCamera in child: '{childCinemachine.name}' - using that instead");
                childCinemachine.Follow = target;
                childCinemachine.LookAt = target;
                Debug.Log($"PlayerManager: Child camera target set to '{target.name}'");
            }
            else
            {
                Debug.LogError(
                    "PlayerManager: No CinemachineCamera component found in PlayerFollowCam or its children!");
            }
        }
    }
    public NetworkedPlayer GetCurrentNetworkedPlayer()
    {
        return currentNetworkedPlayer;
    }
    public List<NetworkedPlayer> GetAllNetworkedPlayers()
    {
        return allNetworkedPlayers;
    }
    public void SwitchControlTo(NetworkedPlayer targetPlayer)
    {
        if (targetPlayer == null || !allNetworkedPlayers.Contains(targetPlayer))
        {
            Debug.LogWarning("PlayerManager: Cannot switch to invalid networked player");
            return;
        }

        // Deactivate current player control
        if (currentNetworkedPlayer != null)
        {
            currentNetworkedPlayer.SetPlayerControlled(false);
        }

        // Activate new player control
        currentNetworkedPlayer = targetPlayer;
        currentNetworkedPlayer.SetPlayerControlled(true);

        // Update camera target to follow the PlayerCameraRoot (proper camera anchor)
        Transform cameraTargetTransform = currentNetworkedPlayer.GetPlayerCameraRoot();
        if (cameraTargetTransform != null)
        {
            SetCameraTarget(cameraTargetTransform);
            Debug.Log(
                $"PlayerManager: Switched control to {targetPlayer.name}, camera following PlayerCameraRoot: {cameraTargetTransform.name}");
        }
        else
        {
            // Fallback to movement transform if PlayerCameraRoot not found
            Transform movementTransform = currentNetworkedPlayer.GetMovementTransform();
            SetCameraTarget(movementTransform);
            Debug.LogWarning(
                $"PlayerManager: PlayerCameraRoot not found for {targetPlayer.name}, using movement transform: {movementTransform.name}");
        }
    }
    public void RegisterNetworkedPlayer(NetworkedPlayer networkedPlayer)
    {
        if (!allNetworkedPlayers.Contains(networkedPlayer))
        {
            allNetworkedPlayers.Add(networkedPlayer);
            Debug.Log($"PlayerManager: Registered networked player {networkedPlayer.name}");
        }
    }
    public void UnregisterNetworkedPlayer(NetworkedPlayer networkedPlayer)
    {
        if (allNetworkedPlayers.Contains(networkedPlayer))
        {
            allNetworkedPlayers.Remove(networkedPlayer);

            // If this was the current player, clear the reference
            if (currentNetworkedPlayer == networkedPlayer)
            {
                currentNetworkedPlayer = null;
            }

            Debug.Log($"PlayerManager: Unregistered networked player {networkedPlayer.name}");
        }
    }
    public CharacterModelManager GetCharacterModelManager()
    {
        return characterModelManager;
    }
    private int GetStartingZoneByRace(int race)
    {
        int toReturn = 1; // Default starting zone
        if (SelectedPlayerCharacter != null)
        {
            switch (SelectedPlayerCharacter.GetSpecies())
            {
                case 1: // Aelystian
                    toReturn = 1; // IthoriaSouth
                    break;
                case 2: // Anurian
                    toReturn = 2; // ShiftingWastes
                    break;
                case 3: // Getaii
                    toReturn = 3; // PurrgishWoodlands
                    break;
                case 4: // Hivernian
                    toReturn = 4; // HiverniaForestNorth
                    break;
                case 5: // Kasmiran
                    toReturn = 5; // CanaGrasslands
                    break;
                case 6: // Meliviaen
                    toReturn = 6; // GreatForestSouth
                    break;
                case 7: // Qadian
                    toReturn = 7; // QadianDelta
                    break;
                case 8: // Tkyan
                    toReturn = 8; // TkyanDepths
                    break;
                case 9: // Valahoran
                    toReturn = 9; // ValahorSouth
                    break;
                default:
                    toReturn = 1; // IthoriaSouth - can be default for now
                    break;
            }
        }

        return toReturn;
    }
    #endregion
    
    #region Helpers
    public override void OnNetworkSpawn()
    {
        SInstances.Add(this);
        Debug.Log(
            $"PlayerManager.OnNetworkSpawn: Added to instances list. Total instances: {SInstances.Count}, IsOwner: {IsOwner}, IsServer: {IsServer}, IsClient: {IsClient}, NetworkObjectId: {NetworkObjectId}");

        if (IsOwner)
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
        }

        base.OnNetworkSpawn();
    }
    public override void OnNetworkDespawn()
    {
        SInstances.Remove(this);
        Debug.Log($"PlayerManager.OnNetworkDespawn: Removed from instances list. Remaining instances: {SInstances.Count}");
        base.OnNetworkDespawn();
    }
    public void PlayerManagerControlSetActive(bool isActive)
    {
        // Camera control for the new architecture
        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(isActive);
        }

        if (playerFollowCam != null)
        {
            playerFollowCam.SetActive(isActive);
        }

        // Legacy PlayerController support
        if (playerController != null)
        {
            playerController.SetLocalComponentsActive(isActive);
        }
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The new zone loading system handles character positioning during SetSelectedCharacterAsync
        // No need to search for ZoneManager here since it's server-only

        // Optional: Add any client-side scene setup logic here if needed
        // TODO: End load screen
    }
    private PlayerCharacterData ConvertToPlayerCharacterData(PlayerStatBlock playerStatBlock)
    {
        if (playerStatBlock == null)
        {
            Debug.LogError("PlayerManager: Cannot convert null PlayerStatBlock to PlayerCharacterData");
            return new PlayerCharacterData();
        }

        try
        {
            PlayerCharacterData characterData = new PlayerCharacterData
            {
                // Basic character info
                CharacterID = playerStatBlock.GetCharacterID(),
                CharacterName = playerStatBlock.GetCharacterName(),
                Race = playerStatBlock.GetSpecies(),
                Gender = playerStatBlock.GetGender(),
                Face = playerStatBlock.GetFace(),

                // Experience values
                CombatExp = playerStatBlock.GetCombatExp(),
                CraftingExp = playerStatBlock.GetCraftingExp(),
                ArcaneExp = playerStatBlock.GetArcaneExp(),
                SpiritExp = playerStatBlock.GetSpiritExp(),
                VeilExp = playerStatBlock.GetVeilExp(),

                // Base stats from SpeciesTemplate
                BaseStrength = playerStatBlock.species?.strength ?? 10,
                BaseDexterity = playerStatBlock.species?.dexterity ?? 10,
                BaseConstitution = playerStatBlock.species?.constitution ?? 10,
                BaseIntelligence = playerStatBlock.species?.intelligence ?? 10,
                BaseSpirit = playerStatBlock.species?.spirit ?? 10
            };

            Debug.Log(
                $"PlayerManager: Converted PlayerStatBlock '{playerStatBlock.GetCharacterName()}' to network data");
            return characterData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerManager: Error converting PlayerStatBlock to PlayerCharacterData: {ex.Message}");
            return new PlayerCharacterData();
        }
    }
    private bool CheckIfCharacterExists(int characterID)
    {
        if (SelectedPlayerCharacter != null && SelectedPlayerCharacter.GetCharacterID() == characterID)
        {
            if (!PlayerCharacters.Contains(SelectedPlayerCharacter))
            {
                PlayerCharacters.Add(SelectedPlayerCharacter);
            }
            return true;
        }
        else if (PlayerCharacters.Any(pc => pc.GetCharacterID() == characterID))
        {
            return true;
        }
        return false;
    }
    private void EnsureSelectedCharacterInList()
    {
        if (SelectedPlayerCharacter != null && !PlayerCharacters.Contains(SelectedPlayerCharacter))
        {
            //Debug.LogWarning("CharacterDataHandler: Selected character was not found in list during processing. Adding now.");
            PlayerCharacters.Add(SelectedPlayerCharacter);
        }
        else if (SelectedPlayerCharacter == null && PlayerCharacters.Count > 0)
        {
            //Debug.LogWarning("CharacterDataHandler: No character was selected during load, selecting first from list.");
            SelectedPlayerCharacter = PlayerCharacters[0];
        }
    }
    private void ClearPlayerListExceptSelected()
    {
        List<PlayerStatBlock> toRemove = new();
        foreach (PlayerStatBlock character in PlayerCharacters)
        {
            if (character == null)
            {
                continue;
            }

            if (SelectedPlayerCharacter == null || character.GetInstanceID() != SelectedPlayerCharacter.GetInstanceID())
            {
                toRemove.Add(character);
            }
        }

        foreach (PlayerStatBlock characterToRemove in toRemove)
        {
            PlayerCharacters.Remove(characterToRemove);
            if (characterToRemove.gameObject != null)
            {
                Destroy(characterToRemove.gameObject);
            }
        }
    }
    private PlayerStatBlock InstantiateCharacter(CharacterData characterData)
    {
        PlayerStatBlock newCharacter = GameObject.Instantiate(CharacterPrefab, CharListParent.transform).GetComponent<PlayerStatBlock>();
        
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
            characterData.SpeciesStrength,
            characterData.SpeciesDexterity,
            characterData.SpeciesConstitution,
            characterData.SpeciesIntelligence,
            characterData.SpeciesSpirit
        );

        return newCharacter;
    }
    #endregion
}

[System.Serializable]
public struct PlayerCharacterData : INetworkSerializable
{
    // Basic character info
    public int CharacterID;
    public string CharacterName;
    public int Race;
    public int Gender;
    public int Face;

    // Experience values
    public int CombatExp;
    public int CraftingExp;
    public int ArcaneExp;
    public int SpiritExp;
    public int VeilExp;

    // Base stats
    public int BaseStrength;
    public int BaseDexterity;
    public int BaseConstitution;
    public int BaseIntelligence;
    public int BaseSpirit;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CharacterID);
        serializer.SerializeValue(ref CharacterName);
        serializer.SerializeValue(ref Race);
        serializer.SerializeValue(ref Gender);
        serializer.SerializeValue(ref Face);
        serializer.SerializeValue(ref CombatExp);
        serializer.SerializeValue(ref CraftingExp);
        serializer.SerializeValue(ref ArcaneExp);
        serializer.SerializeValue(ref SpiritExp);
        serializer.SerializeValue(ref VeilExp);
        serializer.SerializeValue(ref BaseStrength);
        serializer.SerializeValue(ref BaseDexterity);
        serializer.SerializeValue(ref BaseConstitution);
        serializer.SerializeValue(ref BaseIntelligence);
        serializer.SerializeValue(ref BaseSpirit);
    }
}

[System.Serializable]
public struct WaypointRequest : INetworkSerializable
{
    public int CharacterID;
    public string ZoneName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CharacterID);
        serializer.SerializeValue(ref ZoneName);
    }
}

[System.Serializable]
public struct WaypointResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public Vector3 WaypointPosition;
    public bool HasWaypoint;
    public string ZoneName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref WaypointPosition);
        serializer.SerializeValue(ref HasWaypoint);
        serializer.SerializeValue(ref ZoneName);
    }
}