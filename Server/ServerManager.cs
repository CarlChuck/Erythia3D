using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

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
    
    // Area connectivity data
    private readonly Dictionary<int, AreaConnectivity> areaConnectivity = new();
    private readonly Dictionary<string, Dictionary<string, AreaWaypoint>> globalWaypointMap = new();
    private readonly ConcurrentDictionary<ulong, PlayerTransferData> activeTransfers = new();

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
                InitializeAreaConnectivity();
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
        foreach (AreaServerTemplate template in areaServerTemplates)
        {
            if (template.autoStartOnLaunch && !string.IsNullOrEmpty(template.serverExecutablePath))
            {
                LogDebug($"Auto-launching area server: {template.areaId}");
                bool success = LaunchExternalAreaServer(template);
                if (!success)
                {
                    LogError($"Failed to launch area server: {template.areaId}");
                }
            }
            else
            {
                LogError($"Area server '{template.areaId}' has autoStartOnLaunch disabled. Skipping.");
            }
        }
        
        LogError("Area server launch process completed.");
    }
    private bool LaunchExternalAreaServer(AreaServerTemplate template)
    {
        string args = $"--area={template.areaId} --scene=\"{template.sceneName}\" --port={template.startingPort} --master=\"127.0.0.1:{masterServerPort}\"";
        if (!string.IsNullOrEmpty(template.additionalArgs))
        {
            args += " " + template.additionalArgs;
        }

        ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = template.serverExecutablePath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        Process process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            return false;
        }

        externalServerProcesses[template.areaId] = process;

        // Set up output monitoring
        process.OutputDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LogDebug($"[Area{template.areaId}] {e.Data}");
            }
        };
        process.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LogError($"[Area{template.areaId}] {e.Data}");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        LogDebug($"Launched external area server: {template.areaId} (PID: {process.Id})");
        return true;

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
        return LaunchExternalAreaServer(template);
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

        // Update connectivity when area comes online
        UpdateAreaConnectivityStatus(serverInfo.areaId, true);
        
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

        // Update connectivity when area goes offline
        UpdateAreaConnectivityStatus(areaId, false);

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
    public async Task<bool> InitiateWaypointTransfer(ulong clientId, int characterId, int sourceAreaId, string waypointName, Vector3 currentPosition)
    {
        // Validate waypoint access
        var validation = ValidatePlayerWaypointAccess(characterId, sourceAreaId, waypointName);
        if (!validation.isValid)
        {
            LogWarning($"Transfer denied for character {characterId}: {validation.errorMessage}");
            return false;
        }
        
        // Find the waypoint
        var sourceTemplate = areaServerTemplates.FirstOrDefault(t => t.areaId == sourceAreaId);
        var waypoint = sourceTemplate?.waypoints.FirstOrDefault(w => w.waypointName == waypointName);
        
        if (waypoint?.waypointName == null)
        {
            LogError($"Waypoint '{waypointName}' not found in area {sourceAreaId}");
            return false;
        }
        
        // Create transfer data
        var transferData = new PlayerTransferData
        {
            clientId = clientId,
            characterId = characterId,
            currentPosition = currentPosition,
            targetPosition = GetDestinationSpawnPosition(waypoint.Value),
            sourceWaypointName = waypointName,
            targetWaypointName = waypoint.Value.destinationWaypointName,
            transferInitiated = DateTime.Now,
            state = TransferState.Pending
        };
        
        // Store active transfer
        activeTransfers[clientId] = transferData;
        
        // Begin transfer process
        return await ProcessPlayerTransfer(transferData);
    }
    private async Task<bool> ProcessPlayerTransfer(PlayerTransferData transferData)
    {
        try
        {
            // Update transfer state
            transferData.state = TransferState.ValidatingRequirements;
            activeTransfers[transferData.clientId] = transferData;
            
            // Save player state before transfer
            transferData.state = TransferState.SavingPlayerState;
            activeTransfers[transferData.clientId] = transferData;
            
            bool stateSaved = await SavePlayerStateForTransfer(transferData.characterId);
            if (!stateSaved)
            {
                transferData.state = TransferState.TransferFailed;
                activeTransfers[transferData.clientId] = transferData;
                LogError($"Failed to save player state for character {transferData.characterId}");
                return false;
            }
            
            // Initiate the actual transfer
            transferData.state = TransferState.InitiatingTransfer;
            activeTransfers[transferData.clientId] = transferData;
            
            var waypoint = GetWaypointByName(transferData.sourceWaypointName);
            if (waypoint == null)
            {
                transferData.state = TransferState.TransferFailed;
                activeTransfers[transferData.clientId] = transferData;
                return false;
            }
            
            // Create traditional transfer request for the existing system
            var transferRequest = new PlayerTransferRequest
            {
                clientId = transferData.clientId,
                fromAreaId = playerToAreaMapping.ContainsKey(transferData.clientId) ? playerToAreaMapping[transferData.clientId] : 0,
                toAreaId = waypoint.Value.destinationAreaId,
                playerPosition = transferData.targetPosition
            };
            
            transferData.state = TransferState.TransferInProgress;
            activeTransfers[transferData.clientId] = transferData;
            
            RequestPlayerTransfer(transferRequest);
            
            // The transfer will be completed when the area server confirms the player has loaded
            LogDebug($"Transfer initiated for character {transferData.characterId} via waypoint '{transferData.sourceWaypointName}'");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Transfer process failed for character {transferData.characterId}: {ex.Message}");
            transferData.state = TransferState.TransferFailed;
            activeTransfers[transferData.clientId] = transferData;
            return false;
        }
    }
    private async Task<bool> SavePlayerStateForTransfer(int characterId)
    {
        try
        {
            // TODO: Implement player state saving logic
            // This would involve saving character position, inventory, status effects, etc.
            // For now, return true as a placeholder
            await Task.Delay(100); // Simulate database operation
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to save player state: {ex.Message}");
            return false;
        }
    }
    private Vector3 GetDestinationSpawnPosition(AreaWaypoint waypoint)
    {
        if (waypoint.destinationAreaId == 0)
        {
            return waypoint.position;
        }
        
        var targetTemplate = areaServerTemplates.FirstOrDefault(t => t.areaId == waypoint.destinationAreaId);
        if (targetTemplate != null && !string.IsNullOrEmpty(waypoint.destinationWaypointName))
        {
            var targetWaypoint = targetTemplate.waypoints.FirstOrDefault(w => w.waypointName == waypoint.destinationWaypointName);
            if (targetWaypoint.waypointName != null)
            {
                return targetWaypoint.position;
            }
        }
        
        // Default spawn position if no specific destination waypoint
        return Vector3.zero;
    }
    private AreaWaypoint? GetWaypointByName(string waypointName)
    {
        foreach (var template in areaServerTemplates)
        {
            var waypoint = template.waypoints.FirstOrDefault(w => w.waypointName == waypointName);
            if (waypoint.waypointName != null)
            {
                return waypoint;
            }
        }
        return null;
    }
    public void CompletePlayerTransfer(ulong clientId, bool success)
    {
        if (activeTransfers.ContainsKey(clientId))
        {
            PlayerTransferData transferData = activeTransfers[clientId];
            transferData.state = success ? TransferState.TransferComplete : TransferState.TransferFailed;
            
            if (success)
            {
                LogDebug($"Transfer completed successfully for character {transferData.characterId}");
                // Clean up the transfer data after a delay
                _ = CleanupTransferDataAsync(clientId);
            }
            else
            {
                LogError($"Transfer failed for character {transferData.characterId}");
                activeTransfers[clientId] = transferData;
            }
        }
    }
    public PlayerTransferData? GetActiveTransfer(ulong clientId)
    {
        return activeTransfers.ContainsKey(clientId) ? activeTransfers[clientId] : null;
    }
    public Dictionary<ulong, PlayerTransferData> GetAllActiveTransfers()
    {
        return new Dictionary<ulong, PlayerTransferData>(activeTransfers);
    }
    private async Task CleanupTransferDataAsync(ulong clientId)
    {
        try
        {
            await Task.Delay(5000);
            activeTransfers.TryRemove(clientId, out _);
            LogDebug($"Cleaned up transfer data for client {clientId}");
        }
        catch (Exception ex)
        {
            LogError($"Error cleaning up transfer data for client {clientId}: {ex.Message}");
        }
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

    #region Waypoint Management and Validation
    private void InitializeAreaConnectivity()
    {
        foreach (var template in areaServerTemplates)
        {
            BuildAreaConnectivity(template);
        }
        ValidateGlobalWaypointNetwork();
    }
    
    private void BuildAreaConnectivity(AreaServerTemplate template)
    {
        var connectivity = new AreaConnectivity
        {
            areaId = template.areaId,
            connectedAreaIds = new List<int>(),
            outgoingWaypoints = new Dictionary<string, AreaWaypoint>(),
            incomingSpawnPoints = new Dictionary<string, Vector3>(),
            allowsDirectTransfers = true,
            transferCooldownSeconds = 5.0f
        };
        
        // Build outgoing waypoint map
        foreach (var waypoint in template.waypoints)
        {
            connectivity.outgoingWaypoints[waypoint.waypointName] = waypoint;
            
            // Track connected areas
            if (waypoint.destinationAreaId != 0 && !connectivity.connectedAreaIds.Contains(waypoint.destinationAreaId))
            {
                connectivity.connectedAreaIds.Add(waypoint.destinationAreaId);
            }
            
            // Add to global waypoint map
            string globalKey = $"{template.areaId}:{waypoint.waypointName}";
            if (!globalWaypointMap.ContainsKey(globalKey))
            {
                globalWaypointMap[globalKey] = new Dictionary<string, AreaWaypoint>();
            }
            globalWaypointMap[globalKey][waypoint.waypointName] = waypoint;
        }
        
        areaConnectivity[template.areaId] = connectivity;
    }
    
    private void ValidateGlobalWaypointNetwork()
    {
        List<string> validationErrors = new List<string>();
        
        foreach (var template in areaServerTemplates)
        {
            foreach (var waypoint in template.waypoints)
            {
                var validation = ValidateWaypoint(template.areaId, waypoint);
                if (!validation.isValid)
                {
                    validationErrors.Add($"Area {template.areaId} waypoint '{waypoint.waypointName}': {validation.errorMessage}");
                }
            }
        }
        
        if (validationErrors.Count > 0)
        {
            LogWarning($"Waypoint validation found {validationErrors.Count} issues:");
            foreach (var error in validationErrors)
            {
                LogWarning($"  - {error}");
            }
        }
        else
        {
            LogDebug("All waypoints validated successfully");
        }
    }
    
    private WaypointValidationResult ValidateWaypoint(int sourceAreaId, AreaWaypoint waypoint)
    {
        var result = new WaypointValidationResult
        {
            isValid = true,
            errorMessage = "",
            requirementsMet = true,
            missingRequirements = new List<string>(),
            destinationAvailable = true,
            estimatedTransferTime = 3
        };
        
        // Validate destination exists
        if (waypoint.destinationAreaId != 0)
        {
            var targetTemplate = areaServerTemplates.FirstOrDefault(t => t.areaId == waypoint.destinationAreaId);
            if (targetTemplate == null)
            {
                result.isValid = false;
                result.destinationAvailable = false;
                result.errorMessage = $"Destination area {waypoint.destinationAreaId} not found";
                return result;
            }
            
            // Validate destination waypoint exists
            if (!string.IsNullOrEmpty(waypoint.destinationWaypointName))
            {
                var targetWaypoint = targetTemplate.waypoints.FirstOrDefault(w => w.waypointName == waypoint.destinationWaypointName);
                if (targetWaypoint.waypointName == null)
                {
                    result.isValid = false;
                    result.destinationAvailable = false;
                    result.errorMessage = $"Destination waypoint '{waypoint.destinationWaypointName}' not found in area {waypoint.destinationAreaId}";
                    return result;
                }
            }
        }
        
        // Validate waypoint requirements structure
        if (waypoint.requirements.requiresFlag && waypoint.requirements.requiredFlagId <= 0)
        {
            result.isValid = false;
            result.errorMessage = "Waypoint requires quest but no valid quest ID specified";
        }
        
        return result;
    }
    
    public WaypointValidationResult ValidatePlayerWaypointAccess(int characterId, int sourceAreaId, string waypointName)
    {
        var result = new WaypointValidationResult
        {
            isValid = false,
            requirementsMet = false,
            missingRequirements = new List<string>(),
            destinationAvailable = false
        };
        
        // Find the waypoint
        var sourceTemplate = areaServerTemplates.FirstOrDefault(t => t.areaId == sourceAreaId);
        if (sourceTemplate == null)
        {
            result.errorMessage = "Source area not found";
            return result;
        }
        
        var waypoint = sourceTemplate.waypoints.FirstOrDefault(w => w.waypointName == waypointName);
        if (waypoint.waypointName == null)
        {
            result.errorMessage = "Waypoint not found";
            return result;
        }
        
        if (!waypoint.isActive)
        {
            result.errorMessage = "Waypoint is currently inactive";
            return result;
        }
        
        // Check if destination area server is online
        if (waypoint.destinationAreaId != 0)
        {
            if (!registeredServers.ContainsKey(waypoint.destinationAreaId) || 
                !registeredServers[waypoint.destinationAreaId].isOnline)
            {
                result.errorMessage = "Destination area is currently offline";
                return result;
            }
            
            // Check if destination has capacity
            var destServer = registeredServers[waypoint.destinationAreaId];
            if (destServer.currentPlayers >= destServer.maxPlayers)
            {
                result.errorMessage = "Destination area is full";
                return result;
            }
            
            result.destinationAvailable = true;
        }
        
        // TODO: Validate player-specific requirements (quest completion, items, level, etc.)
        // This would require character data lookup
        result.requirementsMet = true; // Placeholder
        
        result.isValid = result.destinationAvailable && result.requirementsMet;
        result.estimatedTransferTime = CalculateTransferTime(waypoint);
        
        return result;
    }
    
    private int CalculateTransferTime(AreaWaypoint waypoint)
    {
        return waypoint.waypointType switch
        {
            WaypointType.Portal => 1,
            WaypointType.Teleporter => 2,
            WaypointType.ZoneBoundary => 3,
            WaypointType.Transport => 5,
            WaypointType.QuestGated => 3,
            _ => 3
        };
    }
    
    public AreaWaypoint[] GetAvailableWaypoints(int areaId)
    {
        var template = areaServerTemplates.FirstOrDefault(t => t.areaId == areaId);
        if (template == null) return Array.Empty<AreaWaypoint>();
        
        return template.waypoints.Where(w => w.isActive).ToArray();
    }
    
    public AreaWaypoint[] GetWaypointsToArea(int sourceAreaId, int targetAreaId)
    {
        var template = areaServerTemplates.FirstOrDefault(t => t.areaId == sourceAreaId);
        if (template == null) return Array.Empty<AreaWaypoint>();
        
        return template.waypoints
            .Where(w => w.destinationAreaId == targetAreaId && w.isActive)
            .ToArray();
    }
    
    private void UpdateAreaConnectivityStatus(int areaId, bool isOnline)
    {
        // Update waypoint availability based on server status
        foreach (var template in areaServerTemplates)
        {
            foreach (var waypoint in template.waypoints)
            {
                if (waypoint.destinationAreaId == areaId)
                {
                    // Find this waypoint in our connectivity map and update its availability
                    if (areaConnectivity.ContainsKey(template.areaId))
                    {
                        var connectivity = areaConnectivity[template.areaId];
                        if (connectivity.outgoingWaypoints.ContainsKey(waypoint.waypointName))
                        {
                            var updatedWaypoint = connectivity.outgoingWaypoints[waypoint.waypointName];
                            updatedWaypoint.isActive = isOnline && waypoint.isActive;
                            connectivity.outgoingWaypoints[waypoint.waypointName] = updatedWaypoint;
                            
                            LogDebug($"Updated waypoint '{waypoint.waypointName}' to area {areaId}: {(isOnline ? "Available" : "Unavailable")}");
                        }
                    }
                }
            }
        }
        
        // Refresh global waypoint map
        RefreshGlobalWaypointAvailability();
    }
    
    private void RefreshGlobalWaypointAvailability()
    {
        foreach (var template in areaServerTemplates)
        {
            foreach (var waypoint in template.waypoints)
            {
                bool isDestinationOnline = waypoint.destinationAreaId == 0 || 
                    (registeredServers.ContainsKey(waypoint.destinationAreaId) && 
                     registeredServers[waypoint.destinationAreaId].isOnline);
                
                string globalKey = $"{template.areaId}:{waypoint.waypointName}";
                if (globalWaypointMap.ContainsKey(globalKey))
                {
                    var updatedWaypoint = waypoint;
                    updatedWaypoint.isActive = waypoint.isActive && isDestinationOnline;
                    globalWaypointMap[globalKey][waypoint.waypointName] = updatedWaypoint;
                }
            }
        }
    }
    
    public Dictionary<int, List<string>> GetAreaConnectivityMap()
    {
        var connectivityMap = new Dictionary<int, List<string>>();
        
        foreach (var kvp in areaConnectivity)
        {
            var areaId = kvp.Key;
            var connectivity = kvp.Value;
            
            connectivityMap[areaId] = new List<string>();
            
            foreach (var waypoint in connectivity.outgoingWaypoints.Values)
            {
                if (waypoint.isActive && waypoint.destinationAreaId != 0)
                {
                    string connectionInfo = $"→ Area {waypoint.destinationAreaId} via '{waypoint.waypointName}'";
                    if (!string.IsNullOrEmpty(waypoint.destinationWaypointName))
                    {
                        connectionInfo += $" to '{waypoint.destinationWaypointName}'";
                    }
                    connectivityMap[areaId].Add(connectionInfo);
                }
            }
        }
        
        return connectivityMap;
    }
    #endregion

    #region Waypoint and Zone Communication
    public async Task ProcessWaypointRequest(int characterID, string waypointName, ulong senderClientId)
    {
        try
        {
            // Find the character's current area
            int currentAreaId = playerToAreaMapping.ContainsKey(senderClientId) ? playerToAreaMapping[senderClientId] : 1;
            
            // Initiate waypoint transfer
            bool success = await InitiateWaypointTransfer(senderClientId, characterID, currentAreaId, waypointName, Vector3.zero);
            
            if (success)
            {
                LogDebug($"Waypoint transfer initiated for character {characterID} via '{waypointName}'");
            }
            else
            {
                LogWarning($"Waypoint transfer failed for character {characterID} via '{waypointName}'");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing waypoint request: {ex.Message}");
        }
    }
    
    public async Task ProcessPlayerZoneInfoRequest(int characterID, ulong senderClientId)
    {
        try
        {
            int currentAreaId = playerToAreaMapping.ContainsKey(senderClientId) ? playerToAreaMapping[senderClientId] : 1;
            
            // Get available waypoints for the current area
            var availableWaypoints = GetAvailableWaypoints(currentAreaId);
            
            // TODO: Send waypoint information to client via RPC
            LogDebug($"Sending {availableWaypoints.Length} available waypoints to character {characterID}");
        }
        catch (Exception ex)
        {
            LogError($"Error processing zone info request: {ex.Message}");
        }
    }
    
    public Vector3 GetWaypointByZoneID(int zoneID)
    {
        var template = areaServerTemplates.FirstOrDefault(t => t.areaId == zoneID);
        if (template?.waypoints != null && template.waypoints.Length > 0)
        {
            // Return the first active waypoint position as default spawn
            var defaultWaypoint = template.waypoints.FirstOrDefault(w => w.isActive);
            if (defaultWaypoint.waypointName != null)
            {
                return defaultWaypoint.position;
            }
        }
        return Vector3.zero;
    }
    
    public Vector3 GetSpawnPositionForCharacter(int characterID)
    {
        // TODO: Get character's last known position from database
        // For now, return default spawn position
        return GetWaypointByZoneID(1); // Default to area 1
    }
    
    public async Task<WaypointInfo[]> GetWaypointInfoForArea(int areaId)
    {
        var waypoints = GetAvailableWaypoints(areaId);
        var waypointInfos = new List<WaypointInfo>();
        
        foreach (var waypoint in waypoints)
        {
            var info = new WaypointInfo
            {
                name = waypoint.waypointName,
                description = waypoint.description,
                position = waypoint.position,
                destinationAreaId = waypoint.destinationAreaId,
                waypointType = waypoint.waypointType,
                isActive = waypoint.isActive,
                requirements = waypoint.requirements,
                estimatedTransferTime = CalculateTransferTime(waypoint)
            };
            
            waypointInfos.Add(info);
        }
        
        return waypointInfos.ToArray();
    }
    #endregion

    #region Area-to-Area Communication
    public void HandleWaypointTransferRequest(PlayerManager playerManager, int characterId, string waypointName, Vector3 currentPosition)
    {
        ulong clientId = playerManager.OwnerClientId;
        _ = ProcessWaypointTransferRequest(playerManager, characterId, waypointName, currentPosition, clientId);
    }
    
    public void HandleZoneInfoRequest(PlayerManager playerManager, int characterId)
    {
        ulong clientId = playerManager.OwnerClientId;
        _ = ProcessZoneInfoRequest(playerManager, characterId, clientId);
    }
    
    public void HandleTransferConfirmation(PlayerManager playerManager, bool success, string message)
    {
        ulong clientId = playerManager.OwnerClientId;
        CompletePlayerTransfer(clientId, success);
        LogDebug($"Transfer confirmation for client {clientId}: {(success ? "Success" : "Failed")} - {message}");
    }
    
    private async Task ProcessWaypointTransferRequest(PlayerManager playerManager, int characterId, string waypointName, Vector3 currentPosition, ulong clientId)
    {
        try
        {
            // Find the character's current area
            int currentAreaId = playerToAreaMapping.ContainsKey(clientId) ? playerToAreaMapping[clientId] : 1;
            
            // Initiate waypoint transfer
            bool success = await InitiateWaypointTransfer(clientId, characterId, currentAreaId, waypointName, currentPosition);
            
            if (success)
            {
                LogDebug($"Waypoint transfer initiated for character {characterId} via '{waypointName}'");
                playerManager.ReceiveWaypointTransferResultRpc(true, $"Transfer to '{waypointName}' initiated successfully", "");
            }
            else
            {
                LogWarning($"Waypoint transfer failed for character {characterId} via '{waypointName}'");
                playerManager.ReceiveWaypointTransferResultRpc(false, "Transfer failed", "Unable to initiate transfer");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing waypoint request: {ex.Message}");
            playerManager.ReceiveWaypointTransferResultRpc(false, "Transfer failed", ex.Message);
        }
    }
    
    private async Task ProcessZoneInfoRequest(PlayerManager playerManager, int characterId, ulong clientId)
    {
        try
        {
            int currentAreaId = playerToAreaMapping.ContainsKey(clientId) ? playerToAreaMapping[clientId] : 1;
            
            // Get waypoint information for the current area
            var waypointInfo = await GetWaypointInfoForArea(currentAreaId);
            
            // Send waypoint information to client
            playerManager.ReceivePlayerAreaInfoRpc(waypointInfo, currentAreaId);
            
            LogDebug($"Sent {waypointInfo.Length} available waypoints to character {characterId}");
        }
        catch (Exception ex)
        {
            LogError($"Error processing zone info request: {ex.Message}");
            playerManager.ReceivePlayerAreaInfoRpc(Array.Empty<WaypointInfo>(), 0);
        }
    }
    
    public void NotifyAreaServerDirectly(int areaId, string messageType, object data)
    {
        // This would be used for direct area-to-area server communication
        // In a production environment, this might use HTTP requests, TCP sockets, or message queues
        LogDebug($"Direct notification to area {areaId}: {messageType}");
        
        // For now, we'll use the existing transfer queue system
        // In a real implementation, you might use:
        // - REST API calls between servers
        // - Message queue systems (RabbitMQ, Redis)
        // - Direct TCP/UDP communication
        // - Shared database with polling
    }
    
    public async Task<bool> ValidateInterAreaConnection(int fromAreaId, int toAreaId)
    {
        // Check if both areas are online
        bool fromAreaOnline = registeredServers.ContainsKey(fromAreaId) && registeredServers[fromAreaId].isOnline;
        bool toAreaOnline = registeredServers.ContainsKey(toAreaId) && registeredServers[toAreaId].isOnline;
        
        if (!fromAreaOnline || !toAreaOnline)
        {
            return false;
        }
        
        // Check if there's a valid waypoint connection
        var waypoints = GetWaypointsToArea(fromAreaId, toAreaId);
        return waypoints.Length > 0;
    }
    
    public async Task<Dictionary<string, object>> GetAreaServerStatistics()
    {
        var stats = GetServerStatistics();
        
        // Add connectivity information
        stats["connectivityMap"] = GetAreaConnectivityMap();
        stats["activeTransfers"] = activeTransfers.Count;
        stats["waypointNetworkSize"] = globalWaypointMap.Count;
        
        // Add per-area waypoint counts
        var waypointCounts = new Dictionary<int, int>();
        foreach (var template in areaServerTemplates)
        {
            waypointCounts[template.areaId] = template.waypoints.Length;
        }
        stats["waypointCounts"] = waypointCounts;
        
        return stats;
    }
    
    public void BroadcastAreaConnectivityUpdate()
    {
        var connectivityInfo = GetAreaConnectivityMap();
        LogDebug($"Broadcasting connectivity update to all clients: {connectivityInfo.Count} areas");
        
        // TODO: Implement actual broadcast to clients
        // This would send the connectivity map to all connected clients
        // so they can update their UI with available travel options
    }
    
    public async Task<bool> TestWaypointConnection(int sourceAreaId, string waypointName)
    {
        try
        {
            var validation = ValidateWaypoint(sourceAreaId, 
                areaServerTemplates.FirstOrDefault(t => t.areaId == sourceAreaId)?
                .waypoints.FirstOrDefault(w => w.waypointName == waypointName) ?? default);
            
            return validation.isValid && validation.destinationAvailable;
        }
        catch (Exception ex)
        {
            LogError($"Error testing waypoint connection: {ex.Message}");
            return false;
        }
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

[System.Serializable]
public struct AreaWaypoint
{
    public string waypointName;
    public Vector3 position;
    public int destinationAreaId;
    public string destinationWaypointName;
    public WaypointType waypointType;
    public WaypointRequirements requirements;
    public bool isActive;
    
    [Tooltip("Description shown to players")]
    public string description;
    
    [Tooltip("Minimum player level required")]
    public int minLevel;
    
    [Tooltip("Maximum simultaneous transfers allowed")]
    public int maxConcurrentTransfers;
}

[System.Serializable]
public enum WaypointType
{
    Portal,
    Teleporter,
    ZoneBoundary,
    Transport,
    QuestGated
}

[System.Serializable]
public struct WaypointRequirements: INetworkSerializable
{
    public bool requiresFlag;
    public int requiredFlagId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref requiresFlag);
        serializer.SerializeValue(ref requiredFlagId);
    }
}

[System.Serializable]
public class AreaServerTemplate
{
    public int areaId;
    public string sceneName;
    public ushort startingPort;
    public int maxPlayers = 50;
    public AreaWaypoint[] waypoints;
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
    public float loadPercentage
    {
        get
        {
            return maxPlayers > 0 ? (float)currentPlayers / maxPlayers : 0f; 
            
        }
    }

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

[System.Serializable]
public struct PlayerTransferData
{
    public ulong clientId;
    public int characterId;
    public Vector3 currentPosition;
    public Vector3 targetPosition;
    public string sourceWaypointName;
    public string targetWaypointName;
    public DateTime transferInitiated;
    public TransferState state;
}

[System.Serializable]
public enum TransferState
{
    Pending,
    ValidatingRequirements,
    SavingPlayerState,
    InitiatingTransfer,
    TransferInProgress,
    LoadingTargetArea,
    TransferComplete,
    TransferFailed
}

[System.Serializable]
public struct AreaConnectivity
{
    public int areaId;
    public List<int> connectedAreaIds;
    public Dictionary<string, AreaWaypoint> outgoingWaypoints;
    public Dictionary<string, Vector3> incomingSpawnPoints;
    public bool allowsDirectTransfers;
    public float transferCooldownSeconds;
}

[System.Serializable]
public struct WaypointValidationResult
{
    public bool isValid;
    public string errorMessage;
    public bool requirementsMet;
    public List<string> missingRequirements;
    public bool destinationAvailable;
    public int estimatedTransferTime;
}

[System.Serializable]
public struct WaypointInfo : INetworkSerializable
{
    public string name;
    public string description;
    public Vector3 position;
    public int destinationAreaId;
    public WaypointType waypointType;
    public bool isActive;
    public WaypointRequirements requirements;
    public int estimatedTransferTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref name);        
        serializer.SerializeValue(ref description);       
        serializer.SerializeValue(ref position);       
        serializer.SerializeValue(ref destinationAreaId);       
        serializer.SerializeValue(ref waypointType);       
        serializer.SerializeValue(ref isActive);       
        serializer.SerializeValue(ref requirements);       
        serializer.SerializeValue(ref estimatedTransferTime);
    }
}
#endregion