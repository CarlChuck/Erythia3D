using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServerManager : NetworkBehaviour
{
    #region Singleton
    public static ServerManager Instance { get; private set; }    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple ServerManager instances detected. Destroying this one on GameObject: {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("Master Server Configuration")]
    [SerializeField] private ushort masterServerPort = 8888;
    [SerializeField] private int maxConnectionsPerArea = 50;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Area Server Management")]
    [SerializeField] private List<AreaServerTemplate> areaServerTemplates = new();

    [Header("Load Balancing")]
    [SerializeField] private float loadBalanceThreshold = 0.8f;
    [SerializeField] private int healthCheckInterval = 10;

    // Runtime data
    private readonly Dictionary<int, AreaServerInfo> registeredServers = new();
    private readonly Dictionary<ulong, int> playerToAreaMapping = new();
    private readonly Dictionary<int, Queue<PlayerTransferRequest>> pendingTransfers = new();
    private readonly Dictionary<int, System.Diagnostics.Process> externalServerProcesses = new();

    // Network components
    private NetworkManager masterNetworkManager;
    private float lastHealthCheck;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"ServerManager.OnNetworkSpawn: IsServer={IsServer}, IsClient={IsClient}, IsOwner={IsOwner}, NetworkObjectId={NetworkObjectId}");
        base.OnNetworkSpawn(); 
        InitializeMasterServer();
    }
    public override void OnNetworkDespawn()
    {
        ShutdownMasterServer();
        base.OnNetworkDespawn();
    }

    #region Master Server Initialization
    private void InitializeMasterServer()
    {
        masterNetworkManager = NetworkManager.Singleton;
        LogDebug($"Master server initialization - NetworkManager.Singleton: {(masterNetworkManager != null ? masterNetworkManager.gameObject.name : "null")}");
        LogDebug($"Master server - IsServer: {masterNetworkManager?.IsServer}, IsClient: {masterNetworkManager?.IsClient}");
        
        if (masterNetworkManager == null)
        {
            return;
        }
        // Only initialize server-side functionality if this is running on the server
        if (IsServer)
        {
            masterNetworkManager.OnClientConnectedCallback += OnClientConnectedToMaster;
            masterNetworkManager.OnClientDisconnectCallback += OnClientDisconnectedFromMaster;

            if (masterNetworkManager.IsServer)
            {
                LogDebug($"Master server is running on port {masterNetworkManager.GetComponent<UnityTransport>().ConnectionData.Port}");
                LogDebug($"ServerManager NetworkObject spawned - NetworkObjectId: {NetworkObjectId}, IsSpawned: {IsSpawned}");
                LaunchAreaServers();
            }
            else
            {
                LogError("Master server was not started before ServerManager was initialized!");
            }
        }
        else
        {
            LogDebug($"ServerManager spawned on client - NetworkObjectId: {NetworkObjectId}. Ready to receive RPCs.");
        }
    }
    private void ShutdownMasterServer()
    {
        // Shutdown all external server processes
        foreach (var process in externalServerProcesses.Values)
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds
                }
                catch (Exception ex)
                {
                    LogError($"Error shutting down external server process: {ex.Message}");
                }
            }
        }
        externalServerProcesses.Clear();

        if (masterNetworkManager != null && masterNetworkManager.IsServer)
        {
            masterNetworkManager.Shutdown();
        }

        LogDebug("Master server shut down");
    }
    #endregion

    #region Area Server Management
    private void LaunchAreaServers()
    {
        LogDebug($"Attempting to launch {areaServerTemplates.Count} area servers...");
        
        foreach (AreaServerTemplate template in areaServerTemplates)
        {
            // Validate template configuration
            if (template.areaId == 0)
            {
                LogError($"Area server template has empty areaId. Skipping.");
                continue;
            }
            
            if (string.IsNullOrEmpty(template.serverExecutablePath))
            {
                LogWarning($"Area server '{template.areaId}' has no serverExecutablePath configured. Skipping launch.");
                LogWarning($"To enable area server '{template.areaId}', configure serverExecutablePath in the AreaServerTemplate.");
                continue;
            }
            
            if (template.autoStartOnLaunch)
            {
                LogDebug($"Auto-launching area server: {template.areaId}");
                bool success = LaunchAreaServer(template);
                if (success)
                {
                    LogDebug($"Successfully initiated launch of area server: {template.areaId}");
                }
                else
                {
                    LogError($"Failed to launch area server: {template.areaId}");
                }
            }
            else
            {
                LogDebug($"Area server '{template.areaId}' has autoStartOnLaunch disabled. Skipping.");
            }
        }
        
        LogDebug("Area server launch process completed.");
    }
    private bool LaunchAreaServer(AreaServerTemplate template)
    {
        try
        {
            if (!string.IsNullOrEmpty(template.serverExecutablePath))
            {
                // Launch as separate process
                return LaunchExternalAreaServer(template);
            }
            else
            {
                // IN-PROCESS LAUNCHING DISABLED: Prevents NetworkManager.Singleton conflicts
                LogError($"Area server {template.areaId} has no serverExecutablePath configured. In-process launching is disabled to prevent NetworkManager conflicts.");
                LogError($"Please configure serverExecutablePath for area server template: {template.areaId}");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to launch area server {template.areaId}: {ex.Message}");
            return false;
        }
    }
    private bool LaunchExternalAreaServer(AreaServerTemplate template)
    {
        var args = $"--area={template.areaId} --scene=\"{template.sceneName}\" --port={template.startingPort} --master=\"127.0.0.1:{masterServerPort}\"";
        if (!string.IsNullOrEmpty(template.additionalArgs))
        {
            args += " " + template.additionalArgs;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = template.serverExecutablePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            externalServerProcesses[template.areaId] = process;

            // Set up output monitoring
            process.OutputDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    LogDebug($"[Area{template.areaId}] {e.Data}");
            };
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    LogError($"[Area{template.areaId}] {e.Data}");
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            LogDebug($"Launched external area server: {template.areaId} (PID: {process.Id})");
            return true;
        }

        return false;
    }
    private bool RestartAreaServer(int areaId)
    {
        var template = areaServerTemplates.FirstOrDefault(t => t.areaId == areaId);
        if (template == null)
        {
            LogError($"No template found for area server {areaId}");
            return false;
        }

        // Clean up existing process if any
        if (externalServerProcesses.ContainsKey(areaId))
        {
            var process = externalServerProcesses[areaId];
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
            externalServerProcesses.Remove(areaId);
        }

        // Remove from registered servers
        UnregisterAreaServer(areaId);

        // Relaunch
        LogDebug($"Restarting area server: {areaId}");
        return LaunchAreaServer(template);
    }
    #endregion

    #region Server Registration
    private void RegisterAreaServer(AreaServerInfo serverInfo)
    {
        serverInfo.lastUpdate = DateTime.Now;
        registeredServers[serverInfo.areaId] = serverInfo;

        LogDebug($"Registered area server: {serverInfo.areaId} ({serverInfo.address}:{serverInfo.port})");

        // Initialize transfer queue for this server
        if (!pendingTransfers.ContainsKey(serverInfo.areaId))
        {
            pendingTransfers[serverInfo.areaId] = new Queue<PlayerTransferRequest>();
        }

        // Notify all connected clients about the new server
        BroadcastServerListUpdate();
    }
    private void UnregisterAreaServer(int areaId)
    {
        if (registeredServers.ContainsKey(areaId))
        {
            registeredServers.Remove(areaId);
            LogDebug($"Unregistered area server: {areaId}");

            // Handle players that were connected to this server
            HandleServerDisconnection(areaId);
        }

        // Clean up pending transfers
        if (pendingTransfers.ContainsKey(areaId))
        {
            pendingTransfers.Remove(areaId);
        }

        // Notify clients
        BroadcastServerListUpdate();
    }
    private void UpdateAreaServerStatus(ServerStatusUpdate statusUpdate)
    {
        if (registeredServers.ContainsKey(statusUpdate.areaId))
        {
            var serverInfo = registeredServers[statusUpdate.areaId];
            serverInfo.currentPlayers = statusUpdate.currentPlayers;
            serverInfo.isOnline = statusUpdate.isOnline;
            serverInfo.lastUpdate = DateTime.Now;

            LogDebug($"Updated status for {statusUpdate.areaId}: {statusUpdate.currentPlayers} players, Online: {statusUpdate.isOnline}");
        }
    }
    private void HandleServerDisconnection(int areaId)
    {
        // Find all players that were connected to this server
        var affectedPlayers = playerToAreaMapping
            .Where(kvp => kvp.Value == areaId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var playerId in affectedPlayers)
        {
            // Try to find an alternative server
            var alternativeServer = FindBestServerForPlayer(playerId);
            if (alternativeServer != null)
            {
                // Create a transfer request to move the player
                var transferRequest = new PlayerTransferRequest
                {
                    clientId = playerId,
                    fromAreaId = areaId,
                    toAreaId = alternativeServer.areaId,
                    playerPosition = Vector3.zero // Will be set to spawn position
                };

                RequestPlayerTransfer(transferRequest);
                LogDebug($"Relocating player {playerId} from failed server {areaId} to {alternativeServer.areaId}");
            }
            else
            {
                // No alternative server available, remove from mapping
                playerToAreaMapping.Remove(playerId);
                LogWarning($"No alternative server found for player {playerId} from failed server {areaId}");
            }
        }
    }
    #endregion

    #region Player Management
    private void OnClientConnectedToMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} connected to master server");
        SendAvailableAreasToClient(clientId);
    }
    private void OnClientDisconnectedFromMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected from master server");

        // Clean up player mapping
        if (playerToAreaMapping.ContainsKey(clientId))
        {
            playerToAreaMapping.Remove(clientId);
        }
    }

    private void RequestJoinArea(int areaId)
    {
        //TODO
    }
    private AreaServerInfo FindBestServerForPlayer(ulong clientId)
    {
        return registeredServers.Values
            .Where(server => server.isOnline && server.currentPlayers < server.maxPlayers)
            .OrderBy(server => server.currentPlayers) // Prefer less crowded servers
            .FirstOrDefault();
    }
    private void RequestPlayerTransfer(PlayerTransferRequest transferRequest)
    {
        LogDebug($"Processing transfer request: Client {transferRequest.clientId} from {transferRequest.fromAreaId} to {transferRequest.toAreaId}");

        // Validate target area exists and can accept players
        if (!registeredServers.ContainsKey(transferRequest.toAreaId))
        {
            LogError($"Transfer failed: Target area {transferRequest.toAreaId} not found");
            return;
        }

        var targetServer = registeredServers[transferRequest.toAreaId];
        if (!targetServer.isOnline)
        {
            LogError($"Transfer failed: Target area {transferRequest.toAreaId} is offline");
            return;
        }

        if (targetServer.currentPlayers >= targetServer.maxPlayers)
        {
            LogWarning($"Transfer failed: Target area {transferRequest.toAreaId} is full");
            return;
        }

        // Add to pending transfers queue
        pendingTransfers[transferRequest.toAreaId].Enqueue(transferRequest);

        // Update player mapping
        playerToAreaMapping[transferRequest.clientId] = transferRequest.toAreaId;

        // Notify target server about incoming transfer
        NotifyAreaServerOfIncomingTransfer(transferRequest);

        LogDebug($"Transfer queued: Client {transferRequest.clientId} -> {transferRequest.toAreaId}");
    }
    private void NotifyAreaServerOfIncomingTransfer(PlayerTransferRequest request)
    {
        // In a real implementation, this would send a message to the target area server
        // For now, we'll assume area servers poll for pending transfers
        LogDebug($"Notified area server {request.toAreaId} of incoming transfer for client {request.clientId}");
    }
    #endregion

    #region Client Communication
    private void SendAvailableAreasToClient(ulong clientId)
    {
        var availableAreas = registeredServers.Values
            .Where(server => server.isOnline && server.currentPlayers < server.maxPlayers)
            .Select(server => new AreaInfo
            {
                areaId = server.areaId,
                sceneName = server.sceneName,
                currentPlayers = server.currentPlayers,
                maxPlayers = server.maxPlayers,
                address = server.address,
                port = server.port
            })
            .ToArray();

        SendAvailableAreas(availableAreas);
    }
    private void BroadcastServerListUpdate()
    {
        var availableAreas = registeredServers.Values
            .Where(server => server.isOnline)
            .Select(server => new AreaInfo
            {
                areaId = server.areaId,
                sceneName = server.sceneName,
                currentPlayers = server.currentPlayers,
                maxPlayers = server.maxPlayers,
                address = server.address,
                port = server.port
            })
            .ToArray();

        SendAvailableAreas(availableAreas);
    }
    private void LoadSceneToClient(string sceneName)
    {
        // This executes on the targeted client.
        // Ensure this scene is included in the Build Settings.
        SceneManager.LoadScene(sceneName);
    }
    private void SendAvailableAreas(AreaInfo[] areas)
    {
        // Client receives list of available areas
        LogDebug($"Sent {areas.Length} available areas to client(s)");
    }
    private void SendJoinAreaResponse(bool success, string serverAddress, ushort serverPort, string message, ClientRpcParams rpcParams = default)
    {
        if (success)
        {
            LogDebug($"Client can join area at {serverAddress}:{serverPort} - {message}");
            // Client should now connect to the specific area server
        }
        else
        {
            LogWarning($"Client join request failed: {message}");
        }
    }
    #endregion

    #region Monitoring and Load Balancing
    private  void Update()
    {
        // Periodically check server health and handle load balancing
        if (Time.time - lastHealthCheck >= healthCheckInterval)
        {
            lastHealthCheck = Time.time;
            CheckServerHealth();
            ProcessPendingTransfers();
            PerformLoadBalancing();
        }
    }
    private void CheckServerHealth()
    {
        var currentTime = DateTime.Now;
        var serversToRemove = new List<int>();
        var serversToRestart = new List<int>();

        foreach (var kvp in registeredServers)
        {
            var serverInfo = kvp.Value;
            var timeSinceLastUpdate = currentTime - serverInfo.lastUpdate;

            // Mark servers as offline if they haven't updated in 30 seconds
            if (timeSinceLastUpdate.TotalSeconds > 30)
            {
                if (serverInfo.isOnline)
                {
                    LogWarning($"Server {serverInfo.areaId} appears to be offline (last update: {timeSinceLastUpdate.TotalSeconds:F1}s ago)");
                    serverInfo.isOnline = false;

                    // Try to restart external server processes
                    if (externalServerProcesses.ContainsKey(serverInfo.areaId))
                    {
                        var process = externalServerProcesses[serverInfo.areaId];
                        if (process != null && process.HasExited)
                        {
                            serversToRestart.Add(serverInfo.areaId);
                        }
                    }
                }

                // Remove completely offline servers after 60 seconds
                if (timeSinceLastUpdate.TotalSeconds > 60)
                {
                    serversToRemove.Add(kvp.Key);
                }
            }
        }

        // Restart failed servers
        foreach (var areaId in serversToRestart)
        {
            LogDebug($"Attempting to restart failed server: {areaId}");
            RestartAreaServer(areaId);
        }

        // Clean up dead servers
        foreach (var areaId in serversToRemove)
        {
            LogWarning($"Removing dead server: {areaId}");
            UnregisterAreaServer(areaId);
        }
    }
    private void ProcessPendingTransfers()
    {
        foreach (var kvp in pendingTransfers)
        {
            var areaId = kvp.Key;
            var transferQueue = kvp.Value;

            // Process up to 5 transfers per area per update to avoid overwhelming
            int processed = 0;
            while (transferQueue.Count > 0 && processed < 5)
            {
                var transfer = transferQueue.Dequeue();
                // In a real implementation, complete the transfer process here
                LogDebug($"Processed transfer for client {transfer.clientId} to {areaId}");
                processed++;
            }
        }
    }
    private void PerformLoadBalancing()
    {
        // Find overloaded servers
        var overloadedServers = registeredServers.Values
            .Where(server => server.isOnline &&
                           (float)server.currentPlayers / server.maxPlayers > loadBalanceThreshold)
            .ToList();

        if (overloadedServers.Count == 0) return;

        // Find underutilized servers
        var underutilizedServers = registeredServers.Values
            .Where(server => server.isOnline &&
                           (float)server.currentPlayers / server.maxPlayers < 0.5f)
            .OrderBy(server => server.currentPlayers)
            .ToList();

        foreach (var overloadedServer in overloadedServers)
        {
            var targetServer = underutilizedServers.FirstOrDefault();
            if (targetServer != null)
            {
                LogDebug($"Load balancing: Server {overloadedServer.areaId} is overloaded ({overloadedServer.currentPlayers}/{overloadedServer.maxPlayers}), considering redistribution");
                // In a real implementation, you might suggest player transfers here
                // For now, just log the recommendation
            }
        }
    }
    private Dictionary<string, object> GetServerStatistics()
    {
        var onlineServers = registeredServers.Values.Where(s => s.isOnline).ToList();
        var totalPlayers = onlineServers.Sum(s => s.currentPlayers);
        var totalCapacity = onlineServers.Sum(s => s.maxPlayers);

        var stats = new Dictionary<string, object>
        {
            ["totalServers"] = registeredServers.Count,
            ["onlineServers"] = onlineServers.Count,
            ["totalPlayers"] = totalPlayers,
            ["totalCapacity"] = totalCapacity,
            ["averageLoad"] = totalCapacity > 0 ? (float)totalPlayers / totalCapacity : 0f,
            ["serverDetails"] = registeredServers.Values.Select(s => new {
                areaId = s.areaId,
                isOnline = s.isOnline,
                players = s.currentPlayers,
                maxPlayers = s.maxPlayers,
                load = s.maxPlayers > 0 ? (float)s.currentPlayers / s.maxPlayers : 0f,
                lastUpdate = s.lastUpdate
            }).ToList()
        };

        return stats;
    }
    private List<AreaServerInfo> GetDetailedServerInfo()
    {
        return registeredServers.Values.OrderBy(s => s.areaId).ToList();
    }
    #endregion

    #region Login Communication
    public static async Task HandleLogin(PlayerManager playerManager, ulong steamID, int accountID)
    {
        LoginResult result;
        
        if (steamID > 0)
        {
            // Handle Steam login - TODO implement
            result = new LoginResult { Success = false, ErrorMessage = "Steam login not implemented", AccountName = "" };
        }
        else if (accountID > 0)
        {
            Dictionary<string, object> account = await AccountManager.Instance.GetAccountByAccountIDAsync(accountID);
            if (account != null)
            {
                result = new LoginResult 
                { 
                    Success = true,
                    ErrorMessage = "",
                    AccountID = Convert.ToInt32(account["AccountID"]),
                    AccountName = account["Username"].ToString(),
                    SteamID = account.TryGetValue("SteamID", out object steamObj) && steamObj != DBNull.Value ? Convert.ToUInt64(steamObj) : 0
                };
                Debug.Log($"ServerManager: Account found for AccountID {accountID}: {result.AccountName}");
            }
            else
            {
                Debug.LogError($"ServerManager: No account found for AccountID {accountID}");
                result = new LoginResult { Success = false, ErrorMessage = $"No account found for AccountID {accountID}", AccountName = "" };
            }
        }
        else
        {
            result = new LoginResult { Success = false, ErrorMessage = "No valid login method available. Both SteamID and AccountID are 0.", AccountName = "" };
        }
        
        playerManager.ReceiveLoginRpc(result);
    }
    #endregion

    #region Character Loading Communication
    public async Task HandleCharacterCreation(PlayerManager playerManager, string familyName, string charName, int charRace, int charGender, int charFace)
    {
        bool success = await CharactersManager.Instance.CreateNewCharacterAsync(playerManager.AccountID, familyName, charName, charRace, charGender, charFace);
        
        if (success)
        {
            Debug.Log($"ServerManager: Character '{charName}' created successfully");
            playerManager.ReceiveCharacterCreationResultRpc(true, "");
        }
        else
        {
            Debug.LogError($"ServerManager: Character creation failed for '{charName}'");
            playerManager.ReceiveCharacterCreationResultRpc(false, "Failed to create character");
        }
    }
    public async Task ProcessCharacterListRequest(PlayerManager pManager, int accountID)
    {      
        CharacterListResult result = await ProcessCharacterList(accountID);

        pManager.ReceiveCharacterListRpc(result);
    }
    private async Task<CharacterListResult> ProcessCharacterList(int accountID)
    {
        try
        {
            List<Dictionary<string, object>> characterDictionaries = await CharactersManager.Instance.GetCharactersByAccountIDAsync(accountID);
            CharacterData[] characters = new CharacterData[characterDictionaries.Count];
            
            for (int i = 0; i < characterDictionaries.Count; i++)
            {
                var charDict = characterDictionaries[i];
                characters[i] = ConvertDictionaryToCharacterData(charDict);
            }
            return new CharacterListResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Characters = characters 
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterList: {ex.Message}\n{ex.StackTrace}");
            return new CharacterListResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading characters: {ex.Message}", 
                Characters = Array.Empty<CharacterData>() 
            };
        }
    }
    private CharacterData ConvertDictionaryToCharacterData(Dictionary<string, object> charDict)
    {
        CharacterData charData = new CharacterData();
        
        // Helper method to safely convert values with defaults
        charData.CharID = GetIntValue(charDict, "CharID", 0);
        charData.AccountID = GetIntValue(charDict, "AccountID", 0);
        charData.FamilyName = GetStringValue(charDict, "FamilyName", "");
        charData.Name = GetStringValue(charDict, "Name", "");
        charData.Title = GetStringValue(charDict, "Title", "");
        charData.ZoneID = GetIntValue(charDict, "ZoneID", 1);
        charData.XLoc = GetIntValue(charDict, "XLoc", 0);
        charData.YLoc = GetIntValue(charDict, "YLoc", 0);
        charData.ZLoc = GetIntValue(charDict, "ZLoc", 0);
        charData.Race = GetIntValue(charDict, "Race", 1);
        charData.Gender = GetIntValue(charDict, "Gender", 1);
        charData.Face = GetIntValue(charDict, "Face", 1);
        charData.CombatExp = GetIntValue(charDict, "CombatExp", 0);
        charData.CraftingExp = GetIntValue(charDict, "CraftingExp", 0);
        charData.ArcaneExp = GetIntValue(charDict, "ArcaneExp", 0);
        charData.SpiritExp = GetIntValue(charDict, "SpiritExp", 0);
        charData.VeilExp = GetIntValue(charDict, "VeilExp", 0);
        
        // Populate species stats from CharactersManager
        SpeciesTemplate species = CharactersManager.Instance.GetSpeciesByID(charData.Race);
        charData.SpeciesStrength = species.strength;
        charData.SpeciesDexterity = species.dexterity;
        charData.SpeciesConstitution = species.constitution;
        charData.SpeciesIntelligence = species.intelligence;
        charData.SpeciesSpirit = species.spirit;
        
        return charData;
    }
    #endregion

    #region Inventory Loading Communication
    public async Task ProcessCharacterInventoryRequest(PlayerManager pManager, int characterID)
    {  
        CharacterInventoryResult result;
        try
        {
            result = await ProcessCharacterInventory(characterID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during character inventory request: {ex.Message}\n{ex.StackTrace}");
            result = new CharacterInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during character inventory request: {ex.Message}",
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>()
            };
        }
        pManager.ReceiveCharacterInventoryRpc(result, characterID);
    }
    public async Task ProcessAccountInventoryRequest(PlayerManager pManager, int accountID)
    {
        AccountInventoryResult result;
        try
        {
            result = await ProcessAccountInventory(accountID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during account inventory request: {ex.Message}\n{ex.StackTrace}");
            result = new AccountInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during account inventory request: {ex.Message}",
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>(),
                Workbenches = Array.Empty<WorkbenchData>()
            };
        }
        pManager.ReceiveAccountInventoryRpc(result);
    }
    private async Task<CharacterInventoryResult> ProcessCharacterInventory(int characterID)
    {
        try
        {
            // Load all character inventory data (association tables)
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetCharacterInventoryItemsAsync(characterID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetCharacterInventoryResourceItemsAsync(characterID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetCharacterInventorySubComponentsAsync(characterID);

            // Retrieve actual items from ItemManager
            List<ItemData> itemDataList = new List<ItemData>();
            foreach (Dictionary<string, object> itemDict in itemDictionaries)
            {
                int itemID = GetIntValue(itemDict, "ItemID", 0);
                int slotID = GetIntValue(itemDict, "SlotID", 0);

                Item item = ItemManager.Instance.GetItemInstanceByID(itemID);
                if (item != null)
                {
                    ItemData itemData = ConvertToItemData(item, slotID);
                    itemDataList.Add(itemData);
                }
            }

            // Retrieve actual resources from ResourceManager
            List<ResourceItemData> resourceDataList = new List<ResourceItemData>();
            foreach (var resourceDict in resourceItemDictionaries)
            {
                int resourceID = GetIntValue(resourceDict, "ResourceID", 0);
                int quantity = GetIntValue(resourceDict, "Quantity", 1);
                
                Resource resource = ResourceManager.Instance.GetResourceById(resourceID);
                if (resource != null)
                {
                    ResourceItemData resourceItemData = ConvertToResourceData(resource, resourceID, quantity);
                    resourceDataList.Add(resourceItemData);
                }
            }

            // Retrieve actual subcomponents from ItemManager
            List<SubComponentData> subComponentDataList = new List<SubComponentData>();
            foreach (var subCompDict in subComponentDictionaries)
            {
                int subComponentID = GetIntValue(subCompDict, "SubComponentID", 0);
                
                SubComponent subComponent = ItemManager.Instance.GetSubComponentInstanceByID(subComponentID);
                if (subComponent != null)
                {
                    SubComponentData subComponentData = ConvertToSubComponentData(subComponent);
                    subComponentDataList.Add(subComponentData);
                }
            }
            
            return new CharacterInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = itemDataList.ToArray(),
                ResourceItems = resourceDataList.ToArray(),
                SubComponents = subComponentDataList.ToArray()
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterInventory: {ex.Message}\n{ex.StackTrace}");
            return new CharacterInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading character inventory: {ex.Message}", 
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>()
            };
        }
    }
    private async Task<AccountInventoryResult> ProcessAccountInventory(int accountID)
    {
        try
        {
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetAccountInventoryItemsAsync(accountID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetAccountInventoryResourceItemsAsync(accountID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetAccountInventorySubComponentsAsync(accountID);
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);
            
            // Retrieve actual items from ItemManager
            List<ItemData> itemDataList = new List<ItemData>();
            foreach (Dictionary<string, object> itemDict in itemDictionaries)
            {
                int itemID = GetIntValue(itemDict, "ItemID", 0);
                
                Item item = ItemManager.Instance.GetItemInstanceByID(itemID);
                if (item != null)
                {
                    ItemData itemData = ConvertToItemData(item, 0);
                    itemDataList.Add(itemData);
                }
            }

            // Retrieve actual resources from ResourceManager
            List<ResourceItemData> resourceDataList = new List<ResourceItemData>();
            foreach (var resourceDict in resourceItemDictionaries)
            {
                int resourceID = GetIntValue(resourceDict, "ResourceID", 0);
                int quantity = GetIntValue(resourceDict, "Quantity", 1);
                
                Resource resource = ResourceManager.Instance.GetResourceById(resourceID);
                if (resource != null)
                {
                    ResourceItemData resourceItemData = ConvertToResourceData(resource, resourceID, quantity);
                    resourceDataList.Add(resourceItemData);
                }
            }

            // Retrieve actual subcomponents from ItemManager
            List<SubComponentData> subComponentDataList = new List<SubComponentData>();
            foreach (var subCompDict in subComponentDictionaries)
            {
                int subComponentID = GetIntValue(subCompDict, "SubComponentID", 0);
                
                SubComponent subComponent = ItemManager.Instance.GetSubComponentInstanceByID(subComponentID);
                if (subComponent != null)
                {
                    SubComponentData subComponentData = ConvertToSubComponentData(subComponent);
                    subComponentDataList.Add(subComponentData);
                }
            }
            
            return new AccountInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = itemDataList.ToArray(),
                ResourceItems = resourceDataList.ToArray(),
                SubComponents = subComponentDataList.ToArray(),
                Workbenches = workbenches
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessAccountInventory: {ex.Message}\n{ex.StackTrace}");
            return new AccountInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading account inventory: {ex.Message}", 
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>(),
                Workbenches = Array.Empty<WorkbenchData>()
            };
        }
    }
    #endregion

    #region Waypoint and Zone Communication
    public async Task ProcessWaypointRequest(int characterID, string zoneName, ulong senderClientId)
    {

    }
    public async Task ProcessPlayerZoneInfoRequest(int characterID, ulong senderClientId)
    {

    }
    private async Task<WaypointResult> ProcessWaypoint(int characterID, string zoneName)
    {
        Debug.Log($"ServerManager: ProcessWaypoint ENTRY - characterID={characterID}, zoneName={zoneName}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessWaypoint called on client! This should only run on server.");
            return new WaypointResult 
            { 
                Success = false, 
                ErrorMessage = "Server-side method called on client", 
                WaypointPosition = Vector3.zero,
                HasWaypoint = false,
                ZoneName = zoneName
            };
        }

        try
        {
            // Debug: Check if WorldManager is available
            if (WorldManager.Instance == null)
            {
                Debug.LogError("ServerManager: WorldManager.Instance is null!");
                return new WaypointResult 
                { 
                    Success = false, 
                    ErrorMessage = "WorldManager not available", 
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = zoneName
                };
            }

            Debug.Log($"ServerManager: WorldManager found, requesting waypoint for zone '{zoneName}'...");

            // Debug: Check active ZoneManagers
            var activeZoneManagers = WorldManager.Instance.GetAllZoneManagers();
            Debug.Log($"ServerManager: WorldManager reports {activeZoneManagers.Count} active ZoneManagers:");
            foreach (var kvp in activeZoneManagers)
            {
                Debug.Log($"ServerManager: - Zone '{kvp.Key}': {(kvp.Value != null ? "Valid ZoneManager" : "NULL ZoneManager")}");
                if (kvp.Value != null)
                {
                    Debug.Log($"ServerManager: - Zone '{kvp.Key}' has MarketWaypoint: {kvp.Value.HasMarketWaypoint()}");
                }
            }

            // Get MarketWaypoint position from WorldManager
            Vector3? waypointPosition = WorldManager.Instance.GetMarketWaypointPosition(zoneName);
            
            if (waypointPosition.HasValue)
            {
                Debug.Log($"ServerManager: Successfully retrieved waypoint position {waypointPosition.Value} for zone '{zoneName}'");
                return new WaypointResult 
                { 
                    Success = true, 
                    ErrorMessage = "", 
                    WaypointPosition = waypointPosition.Value,
                    HasWaypoint = true,
                    ZoneName = zoneName
                };
            }
            else
            {
                Debug.LogWarning($"ServerManager: No MarketWaypoint found for zone '{zoneName}'");
                return new WaypointResult 
                { 
                    Success = true, 
                    ErrorMessage = $"No MarketWaypoint found in zone '{zoneName}'", 
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = zoneName
                };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessWaypoint: {ex.Message}\n{ex.StackTrace}");
            return new WaypointResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error processing waypoint: {ex.Message}", 
                WaypointPosition = Vector3.zero,
                HasWaypoint = false,
                ZoneName = zoneName
            };
        }
    }
    public Vector3 GetWaypointByZoneID(int zoneID)
    {
        foreach (AreaServerTemplate template in areaServerTemplates)
        {
            if (zoneID == template.areaId)
            {
                return template.spawnPosition;
            }
        }
        Debug.LogError($"ServerManager: GetWaypointByZoneID: No waypoint found for zoneID={zoneID}");
        return Vector3.zero;
    }
    public Vector3 GetSpawnPositionForCharacter(int characterID)
    {
        // In a full implementation, you would look up the character's zone
        // and find the appropriate ZoneManager.
        // For now, we'll assume there is one active ZoneManager.
        ZoneManager zoneManager = FindObjectOfType<ZoneManager>();
        if (zoneManager != null && zoneManager.HasMarketWaypoint())
        {
            return zoneManager.GetMarketWaypoint().position;
        }

        Debug.LogWarning($"ServerManager: Could not find a ZoneManager with a MarketWaypoint. Defaulting to spawn position (0, 10, 0).");
        return new Vector3(0, 10, 0); // Return a default spawn point if no waypoint is found
    }
    #endregion

    #region Helper Methods
    private int GetIntValue(Dictionary<string, object> dict, string key, int defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }
    private string GetStringValue(Dictionary<string, object> dict, string key, string defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
    private float GetFloatValue(Dictionary<string, object> dict, string key, float defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToSingle(value);
        }
        return defaultValue;
    }
    private bool GetBoolValue(Dictionary<string, object> dict, string key, bool defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToBoolean(value);
        }
        return defaultValue;
    }
    private DateTime GetDateTimeValue(Dictionary<string, object> dict, string key, DateTime defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToDateTime(value);
        }
        return defaultValue;
    }

    private static ItemData ConvertToItemData(Item item, int slotID)
    {
        ItemData itemData = new ItemData
        {
            ItemID = item.ItemID,
            ItemTemplateID = item.ItemTemplateID,
            ItemName = item.ItemName,
            ItemType = (int)item.Type,
            Durability = item.Durability,
            MaxDurability = item.MaxDurability,
            Damage = item.Damage,
            Speed = item.Speed,
            DamageType = (int)item.WeaponType,
            SlotType = (int)item.Slot,
            SlashResist = item.SlashResist,
            ThrustResist = item.ThrustResist,
            CrushResist = item.CrushResist,
            HeatResist = item.HeatResist,
            ShockResist = item.ShockResist,
            ColdResist = item.ColdResist,
            MindResist = item.MindResist,
            CorruptResist = item.CorruptResist,
            Icon = item.Icon,
            Colour = item.ColourHex,
            Weight = item.Weight,
            Model = item.Model,
            Stackable = item.IsStackable,
            StackSizeMax = item.StackSizeMax,
            Price = item.Price,
            SlotID = slotID
        };
        
        return itemData;
    }
    private static ResourceItemData ConvertToResourceData(Resource resource, int resourceID, int quantity)
    {
        ResourceData resourceData = new ResourceData
        {
            ResourceSpawnID = resourceID,
            ResourceName = resource.ResourceName,
            ResourceTemplateID = resource.ResourceTemplateID,
            Type = (int)resource.Type,
            SubType = (int)resource.SubType,
            Order = (int)resource.Order,
            Family = (int)resource.Family,
            Quality = resource.Quality,
            Toughness = resource.Toughness,
            Strength = resource.Strength,
            Density = resource.Density,
            Aura = resource.Aura,
            Energy = resource.Energy,
            Protein = resource.Protein,
            Carbohydrate = resource.Carbohydrate,
            Flavour = resource.Flavour,
            Weight = resource.Weight,
            Value = resource.Value,
            StartDate = resource.StartDate,
            EndDate = resource.EndDate
        };

        ResourceItemData resourceItemData = new ResourceItemData
        {
            ResourceSpawnID = resourceID,
            CurrentStackSize = quantity,
            StackSizeMax = 1000, // Default or get from template
            Weight = (float)(quantity * resource.Weight) / 100,
            ResourceData = resourceData
        };
        return resourceItemData;
    }
    private static SubComponentData ConvertToSubComponentData(SubComponent subComponent)
    {
        SubComponentData subComponentData = new SubComponentData
        {
            SubComponentID = subComponent.SubComponentID,
            Name = subComponent.Name,
            SubComponentTemplateID = subComponent.SubComponentTemplateID,
            ComponentType = subComponent.ComponentType,
            Quality = subComponent.Quality,
            Toughness = subComponent.Toughness,
            Strength = subComponent.Strength,
            Density = subComponent.Density,
            Aura = subComponent.Aura,
            Energy = subComponent.Energy,
            Protein = subComponent.Protein,
            Carbohydrate = subComponent.Carbohydrate,
            Flavour = subComponent.Flavour
        };
        return subComponentData;
    }
    private WorkbenchData[] ConvertToWorkbenchData(List<Dictionary<string, object>> dictionaries)
    {
        WorkbenchData[] items = new WorkbenchData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            Dictionary<string, object> dict = dictionaries[i];
            items[i] = new WorkbenchData
            {
                WorkBenchType = GetIntValue(dict, "WorkBenchType", 1)
            };
        }
        return items;
    }
    #endregion

    #region Utility Methods
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MasterServer] {message}");
        }
    }
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MasterServer] {message}");
    }
    private void LogError(string message)
    {
        Debug.LogError($"[MasterServer] {message}");
    }
    #endregion
}

#region Additional Data Structures
/// Player zone information result struct for server-client communication
[System.Serializable]
public struct PlayerZoneInfoResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public PlayerZoneInfo ZoneInfo;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref ZoneInfo);
    }
}

/// Server zone loading result struct for server-client communication
[System.Serializable]
public struct ServerZoneLoadResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
    }
}

[System.Serializable]
public class AreaServerTemplate
{
    public int areaId;
    public string sceneName;
    public ushort startingPort;
    public int maxPlayers = 50;
    public Vector3 spawnPosition;
    public bool autoStartOnLaunch = true;
    [Tooltip("Executable path for standalone server builds")]
    public string serverExecutablePath;
    [Tooltip("Additional command line arguments")]
    public string additionalArgs = "";
}

[System.Serializable]
public struct AreaInfo : INetworkSerializable
{
    public int areaId;
    public string sceneName;
    public int currentPlayers;
    public int maxPlayers;
    public string address;
    public ushort port;
    public float loadPercentage => maxPlayers > 0 ? (float)currentPlayers / maxPlayers : 0f;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref areaId);
        serializer.SerializeValue(ref sceneName);
        serializer.SerializeValue(ref currentPlayers);
        serializer.SerializeValue(ref maxPlayers);
        serializer.SerializeValue(ref address);
        serializer.SerializeValue(ref port);
    }
}
#endregion