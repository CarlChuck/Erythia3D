using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using Unity.Cinemachine;

public class PlayerManager : NetworkBehaviour
{
    #region Helper Classes
    private CharacterDataHandler characterHandler;
    private InventoryDataHandler inventoryHandler;
    private ZoneCoordinator zoneCoordinator;
    internal NetworkRequestManager requestManager;
    #endregion

    #region References
    [Header("Player Account Info")]
    [SerializeField] private string accountName = "";
    [SerializeField] private string email = "";
    private ulong steamID = 0;
    private int accountID;
    [SerializeField] private string familyName = "";
    [SerializeField] private string language = "en";
    [SerializeField] private string ipAddress = "0.0.0.0";

    [Header("Character Management")]
    [SerializeField] private CharacterModelManager characterModelManager; // Handles character models
    [SerializeField] private GameObject charListParent;
    [SerializeField] private List<PlayerStatBlock> playerCharacters;
    [SerializeField] private PlayerStatBlock selectedPlayerCharacter;
    [SerializeField] private GameObject characterPrefab;

    [Header("Multiplayer Prefabs")]
    [SerializeField] private GameObject networkedPlayerPrefab; // Contains NetworkObject, NetworkedPlayer, controllers
    
    [Header("Camera and UI (Persistent)")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject playerFollowCam;
    [SerializeField] private UIManager uiManager;
    
    [Header("Current References")]
    [SerializeField] private NetworkedPlayer currentNetworkedPlayer; // Currently controlled networked player
    [SerializeField] private List<NetworkedPlayer> allNetworkedPlayers = new List<NetworkedPlayer>(); // All networked players
    
    [Header("Legacy References")]
    [SerializeField] private PlayerController playerController; // Legacy
    [SerializeField] private GameObject playerControllerPrefab; // Legacy
    [SerializeField] private GameObject localPlayerControllerPrefab; // Legacy

    [Header("Inventory and Workbenches")]
    [SerializeField] private Transform workbenchParent;
    [SerializeField] private Inventory homeInventory;
    [SerializeField] private WorkBench workBenchPrefab;
    private List<WorkBench> ownedWorkbenches = new List<WorkBench>();

    #endregion

    #region State Management
    private bool isInitialized = false;
    private Task initializationTask;

    // Network response tracking - managed by NetworkRequestManager
    internal bool loginResultReceived = false;
    internal LoginResult currentLoginResult;
    
    internal bool characterListReceived = false;
    internal CharacterListResult currentCharacterListResult;
    
    internal bool accountInventoryReceived = false;
    internal AccountInventoryResult currentAccountInventoryResult;

    internal bool characterInventoryReceived = false;
    internal CharacterInventoryResult currentCharacterInventoryResult;
    
    internal bool workbenchListReceived = false;
    internal WorkbenchListResult currentWorkbenchListResult;

    internal bool waypointResultReceived = false;
    internal WaypointResult currentWaypointResult;

    internal bool playerZoneInfoResultReceived = false;
    internal PlayerZoneInfoResult currentPlayerZoneInfoResult;

    internal bool serverZoneLoadResultReceived = false;
    internal ServerZoneLoadResult currentServerZoneLoadResult;
    #endregion

    #region Helper Access Properties
    internal ulong SteamID { get => steamID; set => steamID = value; }
    internal int AccountID { get => accountID; set => accountID = value; }
    internal string AccountName { get => accountName; set => accountName = value; }
    internal string Email => email;
    internal string IPAddress => ipAddress;
    internal string Language => language;
    internal string FamilyName { get => familyName; set => familyName = value; }
    
    internal GameObject CharListParent => charListParent;
    internal List<PlayerStatBlock> PlayerCharacters => playerCharacters;
    internal PlayerStatBlock SelectedPlayerCharacter { get => selectedPlayerCharacter; set => selectedPlayerCharacter = value; }
    internal GameObject CharacterPrefab => characterPrefab;
    internal GameObject PlayerControllerPrefab => playerControllerPrefab;
    internal UIManager UIManager => uiManager;
    
    internal Inventory HomeInventory => homeInventory;
    internal WorkBench WorkBenchPrefab => workBenchPrefab;
    internal Transform WorkbenchParent => workbenchParent;
    internal List<WorkBench> OwnedWorkbenches => ownedWorkbenches;
    #endregion

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

        if (Instance == null)
        {
            Instance = this;
        }

        // Initialize helper classes
        InitializeHelpers();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void InitializeHelpers()
    {
        characterHandler = new CharacterDataHandler(this);
        inventoryHandler = new InventoryDataHandler(this);
        zoneCoordinator = new ZoneCoordinator(this);
        requestManager = new NetworkRequestManager(this);
    }
    #endregion

    #region Initialization
    private void Start()
    {
        Debug.Log($"PlayerManager.Start: IsServer={IsServer}, IsClient={IsClient}, IsHost={IsHost}, IsOwner={IsOwner}");
        
        if (playerCharacters == null)
        {
            playerCharacters = new List<PlayerStatBlock>();
        }
    }
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
        isInitialized = false;
        bool loginSuccess = await characterHandler.LoginAsync();
        if (!loginSuccess)
        {
            Debug.LogError("Login failed. PlayerManager initialization halted.");
            return;
        }

        await characterHandler.LoadCharactersAsync();
        await SetupInitialCharacterUI();
        await inventoryHandler.LoadAllInventoriesAsync();

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

    #region Save To Database
    public async Task SaveCharacter(PlayerStatBlock characterToSave)
    {

    }
    public async Task SaveItem(Item itemToSave) 
    { 
        
    }
    public async Task SaveResourceItem(ResourceItem resourceItemToSave)
    {

    }
    public async Task SaveSubComponent(SubComponent subComponentToSave)
    {

    }
    public async Task SaveCharacterEquipment(PlayerStatBlock characterToSave)
    {

    }
    public async Task SaveCharacterInventory(PlayerStatBlock characterToSave)
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

    #endregion

    #region Setters
    public async void OnCreateCharacter(string characterName, int charRace, int charGender, int charFace)
    {
        await characterHandler.CreateCharacterAsync(characterName, charRace, charGender, charFace);
    }
    public async Task SetupAndSpawnSelectedCharacterAsync()
    {
        if (selectedPlayerCharacter == null)
        {
            Debug.LogError("PlayerManager: Cannot set selected character - no character selected");
            return;
        }
        
        // First, sync character data to server if we're the client
        if (IsClient && !IsServer)
        {
            Debug.Log("PlayerManager (Client): Syncing character data to server before spawning...");
            PlayerCharacterData characterData = ConvertToPlayerCharacterData(selectedPlayerCharacter);
            //SyncCharacterDataToServerRpc(characterData);
            
            // Brief delay to allow server to process the character data
            await Task.Delay(500);
        }
        
        // Then, get zone info and load the zone
        await zoneCoordinator.SetupSelectedCharacterAsync(selectedPlayerCharacter);
        
        // After zone is loaded, spawn the multiplayer character with waypoints available
        await SpawnNetworkedPlayerAsync(selectedPlayerCharacter);
    }    
    private async Task SpawnNetworkedPlayerAsync(PlayerStatBlock playerStatBlock)
    {
        int characterID = playerStatBlock.GetCharacterID();
        Debug.Log($"SpawnNetworkedPlayerAsync: Requesting server to spawn player (CharacterID: {characterID}). The server will determine the spawn position.");

        // We no longer need to calculate spawn position on the client.
        // The server will be authoritative for the spawn location.
        SpawnNetworkedPlayerServerRpc(characterID, playerStatBlock.GetSpecies(), playerStatBlock.GetGender());

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
            Debug.Log($"SpawnNetworkedPlayerAsync: Successfully spawned and registered networked player (ID: {currentNetworkedPlayer.OwnerClientId}).");
            
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
            Debug.LogWarning("Synchronous GetSelectedPlayerCharacter called before PlayerManager is initialized! Returning potentially null character.");
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
    public List<PlayerStatBlock> GetCharacters()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Synchronous GetCharacters called before PlayerManager is initialized! Returning potentially empty/incomplete list.");
            // Cannot easily await here. Return current state.
            return playerCharacters ?? new List<PlayerStatBlock>(); // Return empty list if null
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
            Debug.Log($"PlayerManager: Camera Follow: {cinemachineCamera.Follow?.name}, LookAt: {cinemachineCamera.LookAt?.name}");
            
            // Additional validation
            if (Vector3.Distance(target.position, Vector3.zero) < 0.1f)
            {
                Debug.LogWarning($"PlayerManager: Camera target '{target.name}' is at or very close to origin (0,0,0) - this might cause camera issues");
            }
        }
        else
        {
            Debug.LogError($"PlayerManager: PlayerFollowCam '{playerFollowCam.name}' does not have a CinemachineCamera component!");
            
            // Try to find CinemachineCamera in children
            var childCinemachine = playerFollowCam.GetComponentInChildren<CinemachineCamera>();
            if (childCinemachine != null)
            {
                Debug.Log($"PlayerManager: Found CinemachineCamera in child: '{childCinemachine.name}' - using that instead");
                childCinemachine.Follow = target;
                childCinemachine.LookAt = target;
                Debug.Log($"PlayerManager: Child camera target set to '{target.name}'");
            }
            else
            {
                Debug.LogError("PlayerManager: No CinemachineCamera component found in PlayerFollowCam or its children!");
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
            Debug.Log($"PlayerManager: Switched control to {targetPlayer.name}, camera following PlayerCameraRoot: {cameraTargetTransform.name}");
        }
        else
        {
            // Fallback to movement transform if PlayerCameraRoot not found
            Transform movementTransform = currentNetworkedPlayer.GetMovementTransform();
            SetCameraTarget(movementTransform);
            Debug.LogWarning($"PlayerManager: PlayerCameraRoot not found for {targetPlayer.name}, using movement transform: {movementTransform.name}");
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
    #endregion

    #region Helpers
    public override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe to avoid memory leaks
        base.OnDestroy();
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
    #endregion

    #region Login Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestLoginServerRpc(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ServerRpcParams serverRpcParams = default)
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
    public void HandleLoginResult(LoginResult result)
    {
        currentLoginResult = result;
        loginResultReceived = true;
        Debug.Log($"Client: Received login result. Success: {result.Success}");
    }
    #endregion

    #region Character List Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestCharacterListServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received character list request, calling ServerManager for accountID={accountID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessCharacterListRequest...");
                ServerManager.Instance.ProcessCharacterListRequest(this, accountID, serverRpcParams.Receive.SenderClientId);
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
        Debug.Log($"PlayerManager: Received character list result. Success: {result.Success}. IsServer: {IsServer}, IsClient: {IsClient}");
        
        // For clients, process character data through CharacterDataHandler
        // Server will receive character data via direct RPC from client
        if (result.Success && characterHandler != null && (!IsServer || IsClient))
        {
            Debug.Log($"PlayerManager (Client): Processing character list result through CharacterDataHandler...");
            _ = characterHandler.ProcessCharacterListResult(result);
        }
        else if (!result.Success)
        {
            Debug.LogError($"PlayerManager: Character list result failed: {result.ErrorMessage}");
        }
        else if (IsServer && !IsClient)
        {
            Debug.Log($"PlayerManager (Server): Skipping character list processing - will receive character data via RPC from client");
        }
    }
    #endregion

    #region Inventory Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestAccountInventoryServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
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
    internal void RequestCharacterInventoryServerRpc(int characterID, ServerRpcParams serverRpcParams = default)
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
    internal void RequestWorkbenchListServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
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

    #region Waypoint Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestWaypointServerRpc(WaypointRequest request, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessWaypointRequest...");
                ServerManager.Instance.ProcessWaypointRequest(request.CharacterID, request.ZoneName, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessWaypointRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process waypoint request.");
                
                // Send error response back to client
                WaypointResult errorResult = new WaypointResult
                {
                    Success = false,
                    ErrorMessage = "Server error: ServerManager not available",
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = request.ZoneName
                };
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceiveWaypointResultClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestWaypointServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveWaypointResultClientRpc(WaypointResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the waypoint result
        HandleWaypointResult(result);
    }
    public void HandleWaypointResult(WaypointResult result)
    {
        currentWaypointResult = result;
        waypointResultReceived = true;
    }
    #endregion

    #region Player Zone Info Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestPlayerZoneInfoServerRpc(int characterID, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received player zone info request for character {characterID}");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessPlayerZoneInfoRequest...");
                ServerManager.Instance.ProcessPlayerZoneInfoRequest(characterID, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessPlayerZoneInfoRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process player zone info request.");
                
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
                
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                
                ReceivePlayerZoneInfoClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestPlayerZoneInfoServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceivePlayerZoneInfoClientRpc(PlayerZoneInfoResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the player zone info result
        Debug.Log($"PlayerManager (Client): Received player zone info result from server. Success: {result.Success}");
        HandlePlayerZoneInfoResult(result);
    }
    public void HandlePlayerZoneInfoResult(PlayerZoneInfoResult result)
    {
        currentPlayerZoneInfoResult = result;
        playerZoneInfoResultReceived = true;
    }
    #endregion

    #region Server Zone Load Communication Bridge RPCs
    [ServerRpc(RequireOwnership = false)]
    internal void RequestServerLoadZoneServerRpc(string zoneName, ServerRpcParams serverRpcParams = default)
    {
        // This runs on the server - act as a bridge to ServerManager
        if (IsServer)
        {
            Debug.Log($"PlayerManager (Server): Received server zone load request for zone '{zoneName}'");
            
            if (ServerManager.Instance != null)
            {
                // Call regular method on ServerManager (not RPC, since we're already on server)
                Debug.Log($"PlayerManager (Server): Calling ServerManager.ProcessServerLoadZoneRequest...");
                ServerManager.Instance.ProcessServerLoadZoneRequest(zoneName, serverRpcParams.Receive.SenderClientId);
                Debug.Log($"PlayerManager (Server): ServerManager.ProcessServerLoadZoneRequest called successfully");
            }
            else
            {
                Debug.LogError("PlayerManager (Server): ServerManager.Instance is null! Cannot process server zone load request.");
                
                // Send error response back to client
                ServerZoneLoadResult errorResult = new ServerZoneLoadResult
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
                
                ReceiveServerLoadZoneResultClientRpc(errorResult, clientRpcParams);
            }
        }
        else
        {
            Debug.LogError("PlayerManager: RequestServerLoadZoneServerRpc called on client! This should only run on server.");
        }
    }

    [ClientRpc]
    public void ReceiveServerLoadZoneResultClientRpc(ServerZoneLoadResult result, ClientRpcParams clientRpcParams = default)
    {
        // This runs on the client - handle the server zone load result
        Debug.Log($"PlayerManager (Client): Received server zone load result from server. Success: {result.Success}");
        HandleServerLoadZoneResult(result);
    }
    public void HandleServerLoadZoneResult(ServerZoneLoadResult result)
    {
        currentServerZoneLoadResult = result;
        serverZoneLoadResultReceived = true;
    }
    #endregion

    #region Networked Player Spawning RPCs
    [ServerRpc(RequireOwnership = false)]
    private void SpawnNetworkedPlayerServerRpc(int characterID, int characterRace, int characterGender, ServerRpcParams serverRpcParams = default)
    {
        var clientId = serverRpcParams.Receive.SenderClientId;
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
            Debug.LogError("SpawnNetworkedPlayerServerRpc: ServerManager.Instance is null! Cannot determine spawn position. Defaulting to origin.");
        }
        // -----------------------------------------

        // Instantiate the networked player prefab
        GameObject playerObject = Instantiate(networkedPlayerPrefab, spawnPosition, Quaternion.identity);
        if (playerObject == null)
        {
            Debug.LogError($"SpawnNetworkedPlayerServerRpc: Failed to instantiate networkedPlayerPrefab for client {clientId}.");
            return;
        }
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Instantiated player prefab for client {clientId} at {spawnPosition}.");

        // Get the NetworkObject component
        var networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"SpawnNetworkedPlayerServerRpc: networkedPlayerPrefab does not have a NetworkObject component. Cannot spawn for client {clientId}.");
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
            Debug.Log($"SpawnNetworkedPlayerServerRpc: Set character visuals for race {characterRace}, gender {characterGender}.");
        }
        else
        {
            Debug.LogError($"SpawnNetworkedPlayerServerRpc: Could not find NetworkedPlayer component on the spawned object for client {clientId}.");
        }

        // Notify the owning client that their player has been spawned
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };
        NotifyNetworkedPlayerSpawnedClientRpc(networkObject.NetworkObjectId, spawnPosition, clientRpcParams);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Sent NotifyNetworkedPlayerSpawnedClientRpc to client {clientId}.");
    }
    
    [ClientRpc]
    private void NotifyNetworkedPlayerSpawnedClientRpc(ulong networkObjectId, Vector3 spawnPosition, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"NotifyNetworkedPlayerSpawnedClientRpc: Received notification to find NetworkObject with ID {networkObjectId}.");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            if (networkObject != null)
            {
                currentNetworkedPlayer = networkObject.GetComponent<NetworkedPlayer>();
                if (currentNetworkedPlayer != null)
                {
                    Debug.Log($"NotifyNetworkedPlayerSpawnedClientRpc: Successfully found and assigned NetworkedPlayer (ID: {currentNetworkedPlayer.OwnerClientId}).");
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
                    Debug.LogError($"NotifyNetworkedPlayerSpawnedClientRpc: Found NetworkObject but it doesn't have a NetworkedPlayer component.");
                }
            }
            else
            {
                Debug.LogError($"NotifyNetworkedPlayerSpawnedClientRpc: NetworkObject with ID {networkObjectId} is null after retrieval.");
            }
        }
        else
        {
            Debug.LogError($"NotifyNetworkedPlayerSpawnedClientRpc: Could not find spawned NetworkObject with ID {networkObjectId}.");
        }
    }
    #endregion

    #region Character Data Sync RPCs
    [ServerRpc(RequireOwnership = false)]
    private void SyncCharacterDataToServerRpc(PlayerCharacterData characterData, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer)
        {
            Debug.LogError("PlayerManager: SyncCharacterDataToServerRpc called on non-server!");
            return;
        }

        try
        {
            Debug.Log($"PlayerManager (Server): Received character data sync for character ID: {characterData.CharacterID}");
            
            // Create PlayerStatBlock on server with the received data
            CreateServerPlayerStatBlock(characterData);
            
            Debug.Log($"PlayerManager (Server): Successfully synced character data for character: {characterData.CharacterName}");
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
                characterData.CharacterName,           // character name
                characterData.CharacterID,             // character ID
                "",                                    // title (empty for now)
                1,                                     // zone ID (default to 1)
                characterData.Race,                    // race
                characterData.Face,                    // face
                characterData.Gender,                  // gender
                characterData.CombatExp,               // combat XP
                characterData.CraftingExp,             // crafting XP
                characterData.ArcaneExp,               // arcane XP
                characterData.SpiritExp,               // spirit XP
                characterData.VeilExp,                 // veil XP
                characterData.BaseStrength,            // species strength
                characterData.BaseDexterity,           // species dexterity
                characterData.BaseConstitution,        // species constitution
                characterData.BaseIntelligence,        // species intelligence
                characterData.BaseSpirit               // species spirit
            );

            // Add to characters list and set as selected
            playerCharacters.Add(newPlayerChar);
            selectedPlayerCharacter = newPlayerChar;

            Debug.Log($"PlayerManager (Server): Created PlayerStatBlock for character {characterData.CharacterName} (ID: {characterData.CharacterID})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerManager (Server): Error creating server PlayerStatBlock: {ex.Message}");
        }
    }
    #endregion
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
                
                // Base stats from species template
                BaseStrength = playerStatBlock.species?.strength ?? 10,
                BaseDexterity = playerStatBlock.species?.dexterity ?? 10,
                BaseConstitution = playerStatBlock.species?.constitution ?? 10,
                BaseIntelligence = playerStatBlock.species?.intelligence ?? 10,
                BaseSpirit = playerStatBlock.species?.spirit ?? 10
            };

            Debug.Log($"PlayerManager: Converted PlayerStatBlock '{playerStatBlock.GetCharacterName()}' to network data");
            return characterData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"PlayerManager: Error converting PlayerStatBlock to PlayerCharacterData: {ex.Message}");
            return new PlayerCharacterData();
        }
    }
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