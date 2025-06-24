using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Threading;
using System;
using Unity.Cinemachine;
using System.Linq;

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

    #region References
    private string currentEnvironmentScene = "";
    private int currentEnvironmentId = 0;
    
    [Header("Player Account Info")]
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
    [SerializeField] private NetworkedPlayer currentNetworkedPlayer; 
    [SerializeField] private List<NetworkedPlayer> allNetworkedPlayers = new(); 
    [SerializeField] private PlayerController playerController; 
    [SerializeField] private GameObject playerControllerPrefab; 
    [SerializeField] private GameObject localPlayerControllerPrefab; 

    [Header("Inventory and Workbenches")] 
    [SerializeField] private Transform workbenchParent;
    [SerializeField] private Inventory homeInventory;
    [SerializeField] private WorkBench workBenchPrefab;
    #endregion

    #region State Management
    private bool isInitialized;
    private Task initializationTask;
    
    // Initialisation completion tracking
    private TaskCompletionSource<bool> loginCompletionSource;
    private TaskCompletionSource<bool> charCreateCompletionSource;
    private TaskCompletionSource<bool> charListCompletionSource;
    private TaskCompletionSource<bool> charInventoryCompletionSource;
    private TaskCompletionSource<bool> accountInventoryCompletionSource;
    private TaskCompletionSource<bool> workbenchesCompletionSource;
    #endregion

    #region Chat Network Variables
    // Network variables for chat system integration
    private NetworkVariable<int> currentAreaId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> lastKnownPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Chat state
    public int CurrentAreaId => currentAreaId.Value;
    public Vector3 LastKnownPosition => lastKnownPosition.Value;
    #endregion

    #region Access Properties
    private ulong SteamID { get; set; }
    public int AccountID { get; private set; }
    public string AccountName { get; private set; }
    private string FamilyName { get; set; }
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
    public PlayerStatBlock SelectedPlayerCharacter
    {
        get
        {
            return selectedPlayerCharacter; 
            
        }
        private set
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
        Debug.Log($"PlayerManager.OnStartInitialization: AccountID={newAccountID}, SteamID={newSteamID}, AccountName='{newAccountName}'");
        AccountID = newAccountID;
        SteamID = newSteamID;
        AccountName = newAccountName;
        initializationTask = InitializePlayerManagerAsync();
        await initializationTask;
    }
    private async Task InitializePlayerManagerAsync()
    {
        isInitialized = false;
        await LoginAsync();
        await LoadCharactersAsync();
        await LoadAllInventoriesAsync();
        await SetupInitialCharacterUI();
        isInitialized = true;
        Debug.Log("PlayerManager Initialization Complete.");
    }
    private Task SetupInitialCharacterUI()
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

        return Task.CompletedTask;
    }
    #endregion

    #region Login
    private async Task LoginAsync()
    {
        if (!IsServer)
        {
            loginCompletionSource = new();
            LoginRpc(SteamID, AccountID);
            
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
        }
    }
    [Rpc(SendTo.Server)] private void LoginRpc(ulong steamID, int accountID)
    {
        Debug.Log($"PlayerManager.LoginRpc: Sending login RPC to server with SteamID={steamID}, AccountID={accountID}");
        ServerManager.HandleLogin(this, steamID, accountID);
    }
    [Rpc(SendTo.Owner)] public void ReceiveLoginRpc(LoginResult result)
    {
        Debug.Log($"PlayerManager.ReceiveLoginRpc: Received result - Success={result.Success}, AccountID={result.AccountID}, AccountName='{result.AccountName}', ErrorMessage='{result.ErrorMessage}'");
        AccountID = result.AccountID;
        AccountName = result.AccountName;
        SteamID = result.SteamID;
        // Complete the login task with the result
        loginCompletionSource?.SetResult(result.Success);
    }
    #endregion

    #region Character Creation
    public async Task OnCreateCharacter(string characterName, string familyName, int charRace, int charGender, int charFace)
    {
        if (!IsServer)
        {            
            if (string.IsNullOrEmpty(familyName) || string.IsNullOrEmpty(characterName))
            {
                Debug.LogError("CharacterDataHandler: Character or Family Name cannot be empty");
                return;
            }
            
            charCreateCompletionSource = new();
            CreateCharacterRpc(characterName, familyName, charRace, charGender, charFace);

            // Wait for login response with timeout (10 seconds)
            Task delayTask = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(charCreateCompletionSource.Task, delayTask);
            
            if (completedTask == delayTask)
            {
                Debug.LogError("PlayerManager: Character creation timeout after 10 seconds");
                throw new TimeoutException("Character creation request timed out");
            }
            
            // Check if creation was successful
            bool creationSuccess = await charCreateCompletionSource.Task;
            
            if (creationSuccess)
            {
                await LoadCharactersAsync();
                if (SelectedPlayerCharacter != null && UIManager != null && !IsServer)
                {
                    UIManager.SetupUI(SelectedPlayerCharacter);
                }
            }
            else
            {
                Debug.LogError($"PlayerManager: Failed to create character: {characterName}");
            }
        }
    }
    [Rpc(SendTo.Server)] private void CreateCharacterRpc(string characterName, string familyName, int charRace, int charGender, int charFace)
    {
        ServerManager.Instance.HandleCharacterCreation(this, familyName ,characterName, charRace, charGender, charFace);
    }
    [Rpc(SendTo.Owner)] public void ReceiveCharacterCreationResultRpc(bool success, string errorMessage)
    {
        Debug.Log($"PlayerManager: Character creation result - Success: {success}");
        if (!success)
        {
            Debug.LogError($"PlayerManager: Character creation failed: {errorMessage}");
        }
        charCreateCompletionSource?.SetResult(success);
    }
    public void OnSetFamilyName(string newFamilyName)
    {
        if (GetCharacters().Count == 0)
        {
            FamilyName = newFamilyName;
        }
    }
    #endregion

    #region Character List
    private async Task LoadCharactersAsync()
    {
        if (!IsServer)
        { 
            charListCompletionSource = new();
            RequestCharacterListRpc(AccountID);

            // Wait for login response with timeout (10 seconds)
            Task delayTask = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(charListCompletionSource.Task, delayTask);
            
            if (completedTask == delayTask)
            {
                Debug.LogError("PlayerManager: Character List Load timeout after 10 seconds");
                throw new TimeoutException("Character List Load request timed out");
            }
            
            // Check if creation was successful
            bool creationSuccess = await charListCompletionSource.Task;
            
            if (!creationSuccess)
            {
                Debug.LogError($"PlayerManager: Failed to load characterList");
            }
        }
    }
    [Rpc(SendTo.Server)] private void RequestCharacterListRpc(int accountID)
    {
        ServerManager.Instance.ProcessCharacterListRequest(this, accountID);
    }
    [Rpc(SendTo.Owner)] public void ReceiveCharacterListRpc(CharacterListResult result)
    {
        if (!result.Success)
        {
            Debug.LogError($"PlayerManager: Character list request failed: {result.ErrorMessage ?? "Unknown error"}");
            charListCompletionSource?.SetResult(false);
            return;
        }

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

                PlayerStatBlock newCharacter = Instantiate(CharacterPrefab, CharListParent.transform).GetComponent<PlayerStatBlock>();
                MapCharacterDataToPlayerStatBlock(newCharacter, characterData);
                PlayerCharacters.Add(newCharacter);

                //Sets the first character loaded as the selected character if none is selected
                if (SelectedPlayerCharacter == null)
                {
                    SelectedPlayerCharacter = newCharacter;
                }
            }
            EnsureSelectedCharacterInList();
            
            // Complete the task successfully
            charListCompletionSource?.SetResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"CharacterDataHandler: Exception during ProcessCharacterListResult: {ex.Message}\n{ex.StackTrace}");
            // Complete the task with failure on exception
            charListCompletionSource?.SetResult(false);
        }
    }
    #endregion
    
    #region Inventory
    private async Task LoadAllInventoriesAsync()
    {
        await LoadAllCharactersInventoryAsync();
        await LoadAccountInventoryAsync();
    }
    
    //Character Inventories
    private async Task LoadAllCharactersInventoryAsync()
    {        
        if (!IsServer)
        { 
            charInventoryCompletionSource = new();
            foreach (PlayerStatBlock character in PlayerCharacters)
            {
                RequestCharacterInventoryRpc(character.GetCharacterID());
            }

            // Wait for login response with timeout (10 seconds)
            Task delayTask = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(charInventoryCompletionSource.Task, delayTask);
            
            if (completedTask == delayTask)
            {
                Debug.LogError("PlayerManager: Character Inventory Load timeout after 10 seconds");
                throw new TimeoutException("Character Inventory Load request timed out");
            }
            
            // Check if creation was successful
            bool creationSuccess = await charInventoryCompletionSource.Task;
            
            if (!creationSuccess)
            {
                Debug.LogError($"PlayerManager: Failed to load characterInventory");
            }
        }
        if (PlayerCharacters == null || PlayerCharacters.Count == 0)
        {
            Debug.LogWarning("InventoryDataHandler: No characters to load inventory for.");
            return;
        }
    }
    [Rpc(SendTo.Server)] private void RequestCharacterInventoryRpc(int characterID)
    {
        ServerManager.Instance.ProcessCharacterInventoryRequest(this, characterID);
    }
    [Rpc(SendTo.Owner)] public void ReceiveCharacterInventoryRpc(CharacterInventoryResult result, int characterId)
    {        
        if (!result.Success)
        {
            Debug.LogError($"PlayerManager: CharacterInventory request failed: {result.ErrorMessage ?? "Unknown error"}");
            charInventoryCompletionSource?.SetResult(false);
            return;
        }
        PlayerStatBlock characterData = GetCharacterByID(characterId);
        
        try
        {            
            characterData.GetInventory().ClearInventory();
            characterData.GetEquipmentProfile().ClearEquipmentProfile();

            // Load Inventory Items and Equip them if applicable
            foreach (ItemData itemData in result.Items)
            {
                Item itemInstance = Instantiate(PrefabLibrary.Instance.GetItemPrefab(), characterData.GetInventory().transform);
                MapItemDataToItem(itemData, itemInstance);
                
                if (itemData.SlotID > 0)
                {
                    // Equip Item
                    EquipItem(itemData, itemInstance, characterData.GetEquipmentProfile(),characterData.GetInventory());
                }
                else
                {
                    // Add to Inventory Bag
                    if (!characterData.GetInventory().AddItem(itemInstance))
                    {
                        Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to inventory bag for character {characterId}.");
                    }
                }
            }

            // Load Inventory Resource Items
            foreach (ResourceItemData resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = Instantiate(PrefabLibrary.Instance.GetResourceItemPrefab(), characterData.GetInventory().transform);
                Resource resourceInstance = Instantiate(PrefabLibrary.Instance.GetResourcePrefab(), resourceItemInstance.transform);
                MapResourceItemDataToResourceItem(resourceItemData, resourceInstance, resourceItemInstance);

                if (!characterData.GetInventory().AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceSpawnID}) to inventory bag for character {characterId}.");
                }
            }

            // Load Inventory SubComponents
            foreach (SubComponentData subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = Instantiate(PrefabLibrary.Instance.GetSubComponentPrefab(), characterData.GetInventory().transform);
                MapSubComponentDataToSubComponent(subCompData,subComponentInstance);

                if (!characterData.GetInventory().AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to inventory bag for character {characterId}.");
                }
            }
            // Complete the task successfully
            charInventoryCompletionSource?.SetResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing character inventory result for character {characterId}: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    //Account Inventory and Workbenches
    private async Task LoadAccountInventoryAsync()
    {
        if (!IsServer)
        { 
            accountInventoryCompletionSource = new();
            RequestAccountInventoryRpc(AccountID);
            
            // Wait for login response with timeout (10 seconds)
            Task delayTask = Task.Delay(10000);
            Task completedTask = await Task.WhenAny(accountInventoryCompletionSource.Task, delayTask);
            
            if (completedTask == delayTask)
            {
                Debug.LogError("PlayerManager: AccountInventory Load timeout after 10 seconds");
                throw new TimeoutException("AccountInventory Load request timed out");
            }
            
            // Check if creation was successful
            bool creationSuccess = await accountInventoryCompletionSource.Task;
            
            if (!creationSuccess)
            {
                Debug.LogError($"PlayerManager: Failed to load AccountInventory");
            }
        }
    }
    [Rpc(SendTo.Server)] private void RequestAccountInventoryRpc(int accountID)
    {
        ServerManager.Instance.ProcessAccountInventoryRequest(this, accountID);
    }
    [Rpc(SendTo.Owner)] public void ReceiveAccountInventoryRpc(AccountInventoryResult result)
    {        
        if (!result.Success)
        {
            Debug.LogError($"PlayerManager: AccountInventory request failed: {result.ErrorMessage ?? "Unknown error"}");
            accountInventoryCompletionSource?.SetResult(false);
            return;
        }
        try
        {
            HomeInventory.ClearInventory();

            // Load Inventory Items
            foreach (ItemData itemData in result.Items)
            {
                Item itemInstance = Instantiate(PrefabLibrary.Instance.GetItemPrefab(), HomeInventory.transform);
                MapItemDataToItem(itemData, itemInstance);

                if (!HomeInventory.AddItem(itemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add item {itemInstance.ItemName} (ID: {itemData.ItemID}) to home inventory for account {AccountID}.");
                }
            }

            // Load Account Inventory Resource Items
            foreach (ResourceItemData resourceItemData in result.ResourceItems)
            {
                ResourceItem resourceItemInstance = Instantiate(PrefabLibrary.Instance.GetResourceItemPrefab(), HomeInventory.transform);
                Resource resourceInstance = Instantiate(PrefabLibrary.Instance.GetResourcePrefab(), resourceItemInstance.transform);
                MapResourceItemDataToResourceItem(resourceItemData, resourceInstance, resourceItemInstance);

                if (!HomeInventory.AddResourceItem(resourceItemInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add ResourceItem instance (ID: {resourceItemData.ResourceSpawnID}) to home inventory for account {AccountID}.");
                }
            }

            // Load Account Inventory SubComponents
            foreach (SubComponentData subCompData in result.SubComponents)
            {
                SubComponent subComponentInstance = Instantiate(PrefabLibrary.Instance.GetSubComponentPrefab(), HomeInventory.transform);
                MapSubComponentDataToSubComponent(subCompData,subComponentInstance);

                if (!HomeInventory.AddSubComponent(subComponentInstance))
                {
                    Debug.LogWarning($"InventoryDataHandler: Failed to add SubComponent instance (ID: {subCompData.SubComponentID}) to home inventory for account {AccountID}.");
                }
            }
            foreach (WorkbenchData workbenchData in result.Workbenches)
            {
                int workbenchType = workbenchData.WorkBenchType;

                WorkBench newWorkBenchInstance = Instantiate(WorkBenchPrefab, WorkbenchParent);
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
                        Debug.LogWarning($"PlayerManager: No template workbench found in WorkBenchManager for type {workbenchType}. Initializing with empty recipes.");
                        newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
                    }
                }
                else
                {
                    newWorkBenchInstance.InitializeRecipes(new List<Recipe>());
                    Debug.LogError("PlayerManager: WorkBenchManager is null. Cannot initialize workbench.");
                }
                OwnedWorkbenches.Add(newWorkBenchInstance);
            }
            // Complete the task successfully
            accountInventoryCompletionSource?.SetResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"InventoryDataHandler: Error processing account inventory result: {ex.Message}\n{ex.StackTrace}");
        }
    }
    #endregion

    #region Character Spawning
    public async Task SetupAndSpawnSelectedCharacterAsync()
    {
        if (selectedPlayerCharacter == null)
        {
            Debug.LogError("PlayerManager: Cannot start zone transition - no character selected");
            return;
        }
        //RequestAreaTransition(selectedPlayerCharacter.GetCharacterID(), selectedPlayerCharacter.GetSpecies(), selectedPlayerCharacter.GetGender());
    }
    private async Task SpawnNetworkedPlayerAsync(PlayerStatBlock playerStatBlock)
    {
        int characterID = playerStatBlock.GetCharacterID();
        Debug.Log($"SpawnNetworkedPlayerAsync: Requesting server to spawn player (CharacterID: {characterID}). The server will determine the spawn position.");

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
        Debug.Log($"SpawnNetworkedPlayerAsync: Successfully spawned and registered networked player (ID: {OwnerClientId}).");

        // Setup camera for the newly spawned player
        Transform cameraTarget = currentNetworkedPlayer.GetPlayerCameraRoot();
        SetCameraTarget(cameraTarget);

    } 
    [Rpc(SendTo.Server)] private void SpawnNetworkedPlayerRpc(int characterID, int characterRace, int characterGender, string spawnPointName = "")
    {
        if (currentEnvironmentId == 0)
        {
            Debug.LogError("SpawnNetworkedPlayerServerRpc: Cannot spawn player - no environment scene loaded.");
            return;
        }
        ulong clientId = OwnerClientId;
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Received request from client {clientId} to spawn character {characterID}.");

        // --- Server-Authoritative Spawn Position ---
        AreaConfiguration targetConfig = ServerManager.Instance.GetAreaConfiguration(currentEnvironmentId);
        Vector3 targetSpawnPosition = targetConfig.defaultSpawnPosition;
        Quaternion rotationValue = Quaternion.identity;
        if (spawnPointName != "")
        {
            foreach (AreaSpawnPoint areaSpawnPoint in targetConfig.spawnPoints)
            {
                if (areaSpawnPoint.spawnPointName == spawnPointName)
                {
                    targetSpawnPosition = areaSpawnPoint.position;
                    rotationValue = Quaternion.Euler(areaSpawnPoint.rotation);
                }
            }
        }
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Determined spawn position from ServerManager: {targetSpawnPosition}");

        // Instantiate the networked player prefab
        GameObject playerObject = Instantiate(networkedPlayerPrefab, targetSpawnPosition, rotationValue);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Instantiated player prefab for client {clientId} at {targetSpawnPosition}.");

        // Get the NetworkObject component
        NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();

        // Spawn the object and assign ownership
        networkObject.SpawnAsPlayerObject(clientId);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Spawning NetworkObject for client {clientId} with ownership.");

        // Get the NetworkedPlayer component and initialize it
        NetworkedPlayer networkedPlayer = playerObject.GetComponent<NetworkedPlayer>();
        networkedPlayer.SetCharacterVisuals(characterRace, characterGender);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Set character visuals for race {characterRace}, gender {characterGender}.");
        
        NotifyNetworkedPlayerSpawnedRpc(networkObject.NetworkObjectId, targetSpawnPosition);
        Debug.Log($"SpawnNetworkedPlayerServerRpc: Sent NotifyNetworkedPlayerSpawnedClientRpc to client {clientId}.");
    }
    [Rpc(SendTo.Owner)] private void NotifyNetworkedPlayerSpawnedRpc(ulong networkObjectId, Vector3 spawnPosition)
    {            
        Debug.Log($"NotifyNetworkedPlayerSpawnedClientRpc: Received notification to find NetworkObject with ID {networkObjectId}.");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            currentNetworkedPlayer = networkObject.GetComponent<NetworkedPlayer>();
            Debug.Log($"NotifyNetworkedPlayerSpawnedClientRpc: Successfully found and assigned NetworkedPlayer (ID: {currentNetworkedPlayer.OwnerClientId}).");

            // The async spawning method will handle setting the camera, but we can do it here as well
            // to ensure it's set as early as possible for the owner.
            Transform cameraTarget = currentNetworkedPlayer.GetPlayerCameraRoot();
            SetCameraTarget(cameraTarget);
        }
        else
        {
            Debug.LogError($"NotifyNetworkedPlayerSpawnedClientRpc: Could not find spawned NetworkObject with ID {networkObjectId}.");
        }
    }
    #endregion
    
    #region Player Area Management
    // Area Transition Methods
    public void RequestAreaTransition(int targetAreaId)
    {        
        if (IsServer)
        {
            return;
        }
        if (selectedPlayerCharacter != null)
        {
            RequestAreaTransitionRpc(targetAreaId);
        }
    }
    [Rpc(SendTo.Server)] private void RequestAreaTransitionRpc(int targetAreaId, string spawnPointName = "")
    {
        AreaConfiguration targetConfig = ServerManager.Instance.GetAreaConfiguration(targetAreaId);
        Vector3 targetSpawnPosition = targetConfig.defaultSpawnPosition;
        Vector3 rotationValue = Vector3.zero;

        // Assign client to new area on server
        ServerManager.Instance.AssignClientToArea(OwnerClientId, targetAreaId);

        if (spawnPointName != "")
        {
            foreach (AreaSpawnPoint areaSpawnPoint in targetConfig.spawnPoints)
            {
                if (areaSpawnPoint.spawnPointName == spawnPointName)
                {
                    targetSpawnPosition = areaSpawnPoint.position;
                    rotationValue = areaSpawnPoint.rotation;
                }
            }
        }
        // Load environment scene on the client
        LoadEnvironmentSceneRpc(targetConfig.environmentScene, targetConfig.areaId);
        
        //TODO Move player to targetSpawnPosition/RotationValue
    }
    [Rpc(SendTo.Owner)] private void LoadEnvironmentSceneRpc(string environmentScene, int areaId)
    {
        _ = LoadEnvironmentSceneOnClient(environmentScene, areaId);
    }
    
    // Transition Confirmation Methods
    private async Task LoadEnvironmentSceneOnClient(string environmentScene, int areaId)
    {
        try
        {
            Debug.Log($"Loading environment scene: {environmentScene}");
            
            // Unload current environment scene if exists
            if (currentEnvironmentId != 0)
            {
                await UnloadEnvironmentSceneOnClient(currentEnvironmentScene);
            }
            
            // Load new environment scene
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(environmentScene, LoadSceneMode.Additive);
            
            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }
            
            if (asyncOperation.isDone)
            {
                currentEnvironmentScene = environmentScene;
                currentEnvironmentId = areaId;
                Debug.Log($"✅ Successfully loaded environment scene: {environmentScene}");
                
                // Notify server of successful transition
                ConfirmAreaTransitionRpc(true, $"Successfully entered area");
            }
            else
            {
                Debug.LogError($"❌ Failed to load environment scene: {environmentScene}");
                ConfirmAreaTransitionRpc(false, $"Failed to load environment scene");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Exception loading environment scene {environmentScene}: {ex.Message}");
            ConfirmAreaTransitionRpc(false, $"Exception: {ex.Message}");
        }
    }
    private async Task UnloadEnvironmentSceneOnClient(string environmentScene)
    {
        try
        {
            Debug.Log($"Unloading environment scene: {environmentScene}");
            
            AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(environmentScene);
            
            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }
            
            if (asyncOperation.isDone)
            {
                Debug.Log($"✅ Successfully unloaded environment scene: {environmentScene}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Failed to unload environment scene: {environmentScene}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Exception unloading environment scene {environmentScene}: {ex.Message}");
        }
    }
    [Rpc(SendTo.Server)] private void ConfirmAreaTransitionRpc(bool success, string message)
    {
        Debug.Log($"Area transition confirmation from client {OwnerClientId}: {(success ? "Success" : "Failed")} - {message}");
    }
    #endregion

    #region Getters
    private PlayerStatBlock GetCharacterByID(int charId)
    {
        return PlayerCharacters.FirstOrDefault(character => character.GetCharacterID() == charId);
    }
    public UIManager GetUIManager()
    {
        return uiManager;
    }
    private List<PlayerStatBlock> GetCharacters()
    {
        if (isInitialized)
        {
            return playerCharacters;
        }
        Debug.LogWarning("Synchronous GetCharacters called before PlayerManager is initialized! Returning potentially empty/incomplete list.");
        return playerCharacters ?? new List<PlayerStatBlock>(); // Return empty list if null
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

        CinemachineCamera cinemachineCamera = playerFollowCam.GetComponent<CinemachineCamera>();
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
        if (allNetworkedPlayers.Contains(networkedPlayer))
        {
            return;
        }

        allNetworkedPlayers.Add(networkedPlayer);
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
    private static void MapCharacterDataToPlayerStatBlock(PlayerStatBlock newCharacter, CharacterData characterData)
    {
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
    }
    private static void MapItemDataToItem(ItemData itemData, Item item)
    {
        item.SetItem(
            itemData.ItemID,
            itemData.ItemTemplateID,
            itemData.ItemName,
            itemData.ItemType,
            itemData.Durability,
            itemData.MaxDurability,
            itemData.Damage,
            itemData.Speed,
            itemData.DamageType,
            itemData.SlotType,
            itemData.SlashResist,
            itemData.ThrustResist,
            itemData.CrushResist,
            itemData.HeatResist,
            itemData.ShockResist,
            itemData.ColdResist,
            itemData.MindResist,
            itemData.CorruptResist,
            itemData.Icon,
            itemData.Colour,
            itemData.Weight,
            itemData.Model,
            itemData.Stackable,
            itemData.StackSizeMax,
            itemData.Price
        );
    }
    private static void MapResourceItemDataToResourceItem(ResourceItemData resourceItemData, Resource resource, ResourceItem resourceItem)
    {
        resource.SetResource(
            resourceItemData.ResourceData.ResourceSpawnID,
            resourceItemData.ResourceData.ResourceName,
            resourceItemData.ResourceData.ResourceTemplateID,
            resourceItemData.ResourceData.Type,
            resourceItemData.ResourceData.SubType,
            resourceItemData.ResourceData.Order,
            resourceItemData.ResourceData.Family,
            resourceItemData.ResourceData.Quality,
            resourceItemData.ResourceData.Toughness,
            resourceItemData.ResourceData.Strength,
            resourceItemData.ResourceData.Density,
            resourceItemData.ResourceData.Aura,
            resourceItemData.ResourceData.Energy,
            resourceItemData.ResourceData.Protein,
            resourceItemData.ResourceData.Carbohydrate,
            resourceItemData.ResourceData.Flavour,
            resourceItemData.ResourceData.Weight,
            resourceItemData.ResourceData.Value,
            resourceItemData.ResourceData.StartDate,
            resourceItemData.ResourceData.EndDate
            );
        
        resourceItem.SetResourceItem(resource, resourceItemData.StackSizeMax,resourceItemData.CurrentStackSize);
    }
    private static void MapSubComponentDataToSubComponent(SubComponentData subComponentData, SubComponent subComponent)
    {
        subComponent.SetSubComponent(
            subComponentData.SubComponentID,
            subComponentData.Name,
            subComponentData.SubComponentTemplateID,
            subComponentData.ComponentType,
            subComponentData.Quality,
            subComponentData.Toughness,
            subComponentData.Strength,
            subComponentData.Density,
            subComponentData.Aura,
            subComponentData.Energy,
            subComponentData.Protein,
            subComponentData.Carbohydrate,
            subComponentData.Flavour
        );
    }
    private void EquipItem(ItemData itemData, Item itemInstance, EquipmentProfile equipment, Inventory inventory)
    {
        ItemType slotType = MapSlotIdToItemType(itemData.SlotID);
        if (slotType != ItemType.Other)
        {
            int slotIndex = GetSlotIndexForType(itemData.SlotID);
            EquipmentSlot targetSlot = equipment.GetSlotForItemType(slotType, slotIndex);
            if (targetSlot != null)
            {
                equipment.EquipItemToSlot(itemInstance, targetSlot);
                itemInstance.transform.SetParent(targetSlot.transform);
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
    private static ItemType MapSlotIdToItemType(int slotId)
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
    #endregion
    
    #region Chat RPCs
    /// <summary>
    /// Sends a chat message from the client to the server
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SendChatMessageRpc(ChatMessage message)
    {
        if (!IsServer) return;

        // Update player's position and area for chat filtering
        UpdateChatNetworkVariables();

        // Forward to ChatNetworkManager for processing
        if (ChatNetworkManager.Instance != null)
        {
            ChatNetworkManager.Instance.SendChatMessageServerRpc(message, OwnerClientId);
        }
        else
        {
            Debug.LogError("ChatNetworkManager instance not found!");
        }
    }

    /// <summary>
    /// Receives a chat message from the server
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void ReceiveChatMessageRpc(ChatMessage message)
    {
        if (!IsOwner) return;

        // Forward to ChatManager for UI display
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.ReceiveMessage(message);
        }
        else
        {
            Debug.LogError("ChatManager instance not found!");
        }
    }

    /// <summary>
    /// Handles chat channel join requests
    /// </summary>
    [Rpc(SendTo.Server)]
    public void JoinChatChannelRpc(ChatChannel channel)
    {
        if (!IsServer) return;

        if (ChatNetworkManager.Instance != null)
        {
            ChatNetworkManager.Instance.JoinChannelServerRpc(channel, OwnerClientId);
        }
    }

    /// <summary>
    /// Handles chat channel leave requests
    /// </summary>
    [Rpc(SendTo.Server)]
    public void LeaveChatChannelRpc(ChatChannel channel)
    {
        if (!IsServer) return;

        if (ChatNetworkManager.Instance != null)
        {
            ChatNetworkManager.Instance.LeaveChannelServerRpc(channel, OwnerClientId);
        }
    }

    /// <summary>
    /// Receives channel join notification from server
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void NotifyChannelJoinedRpc(ChatChannel channel)
    {
        if (!IsOwner) return;

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.OnChannelJoined(channel);
        }
    }

    /// <summary>
    /// Receives channel leave notification from server
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void NotifyChannelLeftRpc(ChatChannel channel)
    {
        if (!IsOwner) return;

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.OnChannelLeft(channel);
        }
    }

    /// <summary>
    /// Updates chat-related network variables on the server
    /// </summary>
    private void UpdateChatNetworkVariables()
    {
        if (!IsServer) return;

        // Update current area from ServerManager
        if (ServerManager.Instance != null)
        {
            int? clientArea = ServerManager.Instance.GetClientCurrentArea(OwnerClientId);
            if (clientArea.HasValue)
            {
                currentAreaId.Value = clientArea.Value;
            }
        }

        // Update position from current transform
        lastKnownPosition.Value = transform.position;
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
