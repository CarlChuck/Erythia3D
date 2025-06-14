using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

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
    private Dictionary<string, AreaServerInfo> registeredServers = new();
    private Dictionary<ulong, string> playerToAreaMapping = new();
    private Dictionary<string, Queue<PlayerTransferRequest>> pendingTransfers = new();
    private Dictionary<string, System.Diagnostics.Process> externalServerProcesses = new();

    // Network components
    private NetworkManager masterNetworkManager;
    private float lastHealthCheck = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); 
        InitializeMasterServer();
    }
    public override void OnNetworkDespawn()
    {
        ShutdownMasterServer();
        base.OnNetworkDespawn();
    }


    #region Master Server Initialization

    void InitializeMasterServer()
    {
        // Set up master server networking
        masterNetworkManager = GetComponent<NetworkManager>();
        if (masterNetworkManager == null)
        {
            masterNetworkManager = gameObject.AddComponent<NetworkManager>();
        }

        // Configure transport
        var transport = masterNetworkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData("0.0.0.0", masterServerPort);
        }

        // Set up callbacks
        masterNetworkManager.OnClientConnectedCallback += OnClientConnectedToMaster;
        masterNetworkManager.OnClientDisconnectCallback += OnClientDisconnectedFromMaster;

        // Start master server
        bool started = masterNetworkManager.StartServer();
        if (started)
        {
            LogDebug($"Master server started on port {masterServerPort}");

            // Launch area servers if configured to auto-start
            LaunchAreaServers();
        }
        else
        {
            LogError("Failed to start master server");
        }
    }

    void ShutdownMasterServer()
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

    void LaunchAreaServers()
    {
        foreach (var template in areaServerTemplates)
        {
            if (template.autoStartOnLaunch)
            {
                LaunchAreaServer(template);
            }
        }
    }

    /// <summary>
    /// Launch an area server (either in-process or as separate executable)
    /// </summary>
    public bool LaunchAreaServer(AreaServerTemplate template)
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
                // Launch in current process
                return LaunchInProcessAreaServer(template);
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to launch area server {template.areaId}: {ex.Message}");
            return false;
        }
    }

    bool LaunchExternalAreaServer(AreaServerTemplate template)
    {
        var args = $"--area=\"{template.areaId}\" --scene=\"{template.sceneName}\" --port={template.startingPort} --master=\"127.0.0.1:{masterServerPort}\"";
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
                    LogDebug($"[{template.areaId}] {e.Data}");
            };
            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    LogError($"[{template.areaId}] {e.Data}");
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            LogDebug($"Launched external area server: {template.areaId} (PID: {process.Id})");
            return true;
        }

        return false;
    }

    bool LaunchInProcessAreaServer(AreaServerTemplate template)
    {
        // Create a new GameObject for the area server
        var areaServerGO = new GameObject($"AreaServer_{template.areaId}");
        areaServerGO.transform.parent = transform;

        // Add NetworkManager for this area
        var areaNetworkManager = areaServerGO.AddComponent<NetworkManager>();
        var areaTransport = areaServerGO.AddComponent<UnityTransport>();

        // Add area server manager
        var areaManager = areaServerGO.AddComponent<AreaServerManager>();

        // Configure the area server
        var config = new ServerAreaConfig
        {
            areaId = template.areaId,
            sceneName = template.sceneName,
            port = template.startingPort,
            maxPlayers = template.maxPlayers,
            spawnPosition = template.spawnPosition,
            autoStart = true
        };

        // Use reflection to set the config (since it's serialized)
        var configField = typeof(AreaServerManager).GetField("areaConfig",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        configField?.SetValue(areaManager, config);

        LogDebug($"Launched in-process area server: {template.areaId}");
        return true;
    }

    /// <summary>
    /// Restart a failed area server
    /// </summary>
    public bool RestartAreaServer(string areaId)
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

    /// <summary>
    /// Register an area server with the master server
    /// Called by area servers when they start up
    /// </summary>
    public void RegisterAreaServer(AreaServerInfo serverInfo)
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

    /// <summary>
    /// Unregister an area server
    /// </summary>
    public void UnregisterAreaServer(string areaId)
    {
        if (registeredServers.ContainsKey(areaId))
        {
            var serverInfo = registeredServers[areaId];
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

    /// <summary>
    /// Update area server status
    /// </summary>
    public void UpdateAreaServerStatus(ServerStatusUpdate statusUpdate)
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

    void HandleServerDisconnection(string areaId)
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

    void OnClientConnectedToMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} connected to master server");

        // Send available areas to client
        SendAvailableAreasToClient(clientId);
    }

    void OnClientDisconnectedFromMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected from master server");

        // Clean up player mapping
        if (playerToAreaMapping.ContainsKey(clientId))
        {
            playerToAreaMapping.Remove(clientId);
        }
    }

    /// <summary>
    /// Handle player request to join a specific area
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestJoinAreaServerRpc(string areaId, ServerRpcParams rpcParams = default)
    {
        var clientId = rpcParams.Receive.SenderClientId;

        if (!registeredServers.ContainsKey(areaId))
        {
            LogError($"Area {areaId} not found for client {clientId}");
            SendJoinAreaResponseClientRpc(false, "", 0, "Area not found",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        var serverInfo = registeredServers[areaId];

        // Check if server is online
        if (!serverInfo.isOnline)
        {
            LogWarning($"Area {areaId} is offline for client {clientId}");
            SendJoinAreaResponseClientRpc(false, "", 0, "Area is offline",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        // Check if server can accept new players
        if (serverInfo.currentPlayers >= serverInfo.maxPlayers)
        {
            LogWarning($"Area {areaId} is full for client {clientId}");

            // Try to find an alternative server
            var alternativeServer = FindBestServerForPlayer(clientId);
            if (alternativeServer != null)
            {
                SendJoinAreaResponseClientRpc(true, alternativeServer.address, alternativeServer.port,
                    $"Redirected to {alternativeServer.areaId}",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                playerToAreaMapping[clientId] = alternativeServer.areaId;
                return;
            }

            SendJoinAreaResponseClientRpc(false, "", 0, "All areas are full",
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
            return;
        }

        // Grant access to area server
        playerToAreaMapping[clientId] = areaId;

        LogDebug($"Granted client {clientId} access to area {areaId}");
        SendJoinAreaResponseClientRpc(true, serverInfo.address, serverInfo.port, "Success",
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }

    /// <summary>
    /// Find the best available server for a player
    /// </summary>
    AreaServerInfo FindBestServerForPlayer(ulong clientId)
    {
        return registeredServers.Values
            .Where(server => server.isOnline && server.currentPlayers < server.maxPlayers)
            .OrderBy(server => server.currentPlayers) // Prefer less crowded servers
            .FirstOrDefault();
    }

    /// <summary>
    /// Handle player transfer between areas
    /// </summary>
    public void RequestPlayerTransfer(PlayerTransferRequest transferRequest)
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

    void NotifyAreaServerOfIncomingTransfer(PlayerTransferRequest request)
    {
        // In a real implementation, this would send a message to the target area server
        // For now, we'll assume area servers poll for pending transfers
        LogDebug($"Notified area server {request.toAreaId} of incoming transfer for client {request.clientId}");
    }
    #endregion

    #region Client Communication

    void SendAvailableAreasToClient(ulong clientId)
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

        SendAvailableAreasClientRpc(availableAreas,
            new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
    }

    void BroadcastServerListUpdate()
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

        SendAvailableAreasClientRpc(availableAreas);
    }

    [ClientRpc]
    void SendAvailableAreasClientRpc(AreaInfo[] areas, ClientRpcParams rpcParams = default)
    {
        // Client receives list of available areas
        LogDebug($"Sent {areas.Length} available areas to client(s)");
    }

    [ClientRpc]
    void SendJoinAreaResponseClientRpc(bool success, string serverAddress, ushort serverPort, string message, ClientRpcParams rpcParams = default)
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
    void Update()
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

    void CheckServerHealth()
    {
        var currentTime = DateTime.Now;
        var serversToRemove = new List<string>();
        var serversToRestart = new List<string>();

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

    void ProcessPendingTransfers()
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

    void PerformLoadBalancing()
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

    /// <summary>
    /// Get server statistics for monitoring
    /// </summary>
    public Dictionary<string, object> GetServerStatistics()
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

    /// <summary>
    /// Get detailed server information for admin interface
    /// </summary>
    public List<AreaServerInfo> GetDetailedServerInfo()
    {
        return registeredServers.Values.OrderBy(s => s.areaId).ToList();
    }

    #endregion

    #region Login Communication RPCs
    [ClientRpc]
    private void ReceiveLoginResultClientRpc(LoginResult result, ClientRpcParams clientRpcParams)
    {
        // This RPC is now sent directly from the ServerManager to the client.
        // The client-side code that manages the connection to the master server
        // should handle this response. For example, it could find the local PlayerManager
        // and pass the result to it.
        // This is a placeholder for the client-side logic.
        Debug.Log($"Client received login result: Success={result.Success}, Name={result.AccountName}, Msg={result.ErrorMessage}");
        
        // Example of how a client-side handler might pass this along:
        // if (PlayerManager.LocalInstance != null)
        // {
        //     PlayerManager.LocalInstance.HandleLoginResult(result);
        // }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void RequestLoginServerRpc(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ServerRpcParams serverRpcParams = default)
    {
        //Debug.Log($"ServerManager: RequestLoginServerRpc ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={serverRpcParams.Receive.SenderClientId}");
        // Using an anonymous function to call the async task so we don't have to make the RPC async
        _ = ProcessLoginRequest(steamID, accountID, accountName, email, ipAddress, language, serverRpcParams.Receive.SenderClientId);
    }

    // Regular method for server-to-server communication (called by PlayerManager on server)
    public async Task ProcessLoginRequest(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessLoginRequest ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={senderClientId}");        
        LoginResult result;
        try
        {
            result = await ProcessLogin(steamID, accountID, accountName, email, ipAddress, language);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during login request: {ex.Message}\n{ex.StackTrace}");
            result = new LoginResult
            {
                Success = false,
                ErrorMessage = $"Server error during login: {ex.Message}",
                AccountName = "" // Initialize to avoid null during serialization
            };
        }

        // Send the response directly to the client who requested it.
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderClientId }
            }
        };
        ReceiveLoginResultClientRpc(result, clientRpcParams);
        
        Debug.Log($"ServerManager: ProcessLoginRequest completed");
    }
    #endregion

    #region Character Loading Communication
    public async void ProcessCharacterListRequest(PlayerManager pManager, int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessCharacterListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");        
        try
        {
            CharacterListResult result = await ProcessCharacterList(accountID);
            
            bool responseSet = false;
            if (pManager.IsServer && pManager.OwnerClientId == senderClientId)
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] 
                        { 
                            senderClientId 
                        }
                    }
                };
                pManager.ReceiveCharacterListClientRpc(result, clientRpcParams);
                responseSet = true;
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send character list response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during character list request: {ex.Message}\n{ex.StackTrace}");
            CharacterListResult errorResult = new CharacterListResult
            {
                Success = false,
                ErrorMessage = $"Server error during character list request: {ex.Message}",
                Characters = new CharacterData[0] // Empty array instead of null
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");

            if (pManager.IsServer && pManager.OwnerClientId == senderClientId)
            {
                Debug.Log($"ServerManager: Sending character list error response via PlayerManager...");
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { senderClientId }
                    }
                };
                pManager.ReceiveCharacterListClientRpc(errorResult, clientRpcParams);
            }
            
        }
        
        Debug.Log($"ServerManager: ProcessCharacterListRequest completed");
    }
    #endregion

    #region Inventory Loading Communication
    [ClientRpc]
    private void ReceiveAccountInventoryClientRpc(AccountInventoryResult result, ClientRpcParams clientRpcParams)
    {
        // Placeholder for client-side handling.
        Debug.Log($"Client received account inventory result: Success={result.Success}, Items: {result.Items.Length}");
    }

    [ClientRpc]
    private void ReceiveCharacterInventoryClientRpc(CharacterInventoryResult result, ClientRpcParams clientRpcParams)
    {
        // Placeholder for client-side handling.
        Debug.Log($"Client received character inventory result: Success={result.Success}, Items: {result.Items.Length}");
    }

    [ClientRpc]
    private void ReceiveWorkbenchListClientRpc(WorkbenchListResult result, ClientRpcParams clientRpcParams)
    {
        // Placeholder for client-side handling.
        Debug.Log($"Client received workbench list result: Success={result.Success}, Workbenches: {result.Workbenches.Length}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestAccountInventoryServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        _ = ProcessAccountInventoryRequest(accountID, serverRpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCharacterInventoryServerRpc(int characterID, ServerRpcParams serverRpcParams = default)
    {
        _ = ProcessCharacterInventoryRequest(characterID, serverRpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestWorkbenchListServerRpc(int accountID, ServerRpcParams serverRpcParams = default)
    {
        _ = ProcessWorkbenchListRequest(accountID, serverRpcParams.Receive.SenderClientId);
    }
    public async Task ProcessAccountInventoryRequest(int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessAccountInventoryRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
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
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0],
                Workbenches = new WorkbenchData[0]
            };
        }
        
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderClientId }
            }
        };
        ReceiveAccountInventoryClientRpc(result, clientRpcParams);
        
        Debug.Log($"ServerManager: ProcessAccountInventoryRequest completed");
    }
    public async Task ProcessCharacterInventoryRequest(int characterID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessCharacterInventoryRequest ENTRY - characterID={characterID}, senderClientId={senderClientId}");        
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
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0]
            };
        }
        
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderClientId }
            }
        };
        ReceiveCharacterInventoryClientRpc(result, clientRpcParams);
        
        Debug.Log($"ServerManager: ProcessCharacterInventoryRequest completed");
    }
    public async Task ProcessWorkbenchListRequest(int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessWorkbenchListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        WorkbenchListResult result;
        try
        {
            result = await ProcessWorkbenchList(accountID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during workbench list request: {ex.Message}\n{ex.StackTrace}");
            result = new WorkbenchListResult
            {
                Success = false,
                ErrorMessage = $"Server error during workbench list request: {ex.Message}",
                Workbenches = new WorkbenchData[0]
            };
        }
        
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderClientId }
            }
        };
        ReceiveWorkbenchListClientRpc(result, clientRpcParams);
        
        Debug.Log($"ServerManager: ProcessWorkbenchListRequest completed");
    }
    #endregion

    #region Waypoint and Zone Communication
    public async Task ProcessWaypointRequest(int characterID, string zoneName, ulong senderClientId)
    {

    }
    public async Task ProcessPlayerZoneInfoRequest(int characterID, ulong senderClientId)
    {

    }
    #endregion


    private async Task<CharacterListResult> ProcessCharacterList(int accountID)
    {
        //Debug.Log($"ServerManager: ProcessCharacterList ENTRY - accountID={accountID}");        
        if (!IsServer)
        {
            Debug.LogError("ProcessCharacterList called on client! This should only run on server.");
            return new CharacterListResult { Success = false, ErrorMessage = "Server-side method called on client", Characters = new CharacterData[0] };
        }
        if (CharactersManager.Instance == null)
        {
            Debug.LogError("ServerManager: CharactersManager.Instance is null!");
            return new CharacterListResult { Success = false, ErrorMessage = "CharactersManager not available", Characters = new CharacterData[0] };
        }
        
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
                Characters = new CharacterData[0] 
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


    #region Server-Side Login Logic
    private async Task<LoginResult> ProcessLogin(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language)
    {
        //Debug.Log($"ServerManager: ProcessLogin ENTRY - steamID={steamID}, accountID={accountID}");        
        if (!IsServer)
        {
            Debug.LogError("ProcessLogin called on client! This should only run on server.");
            return new LoginResult { Success = false, ErrorMessage = "Server-side method called on client", AccountName = "" };
        }
        if (AccountManager.Instance == null)
        {
            Debug.LogError("ServerManager: AccountManager.Instance is null!");
            return new LoginResult { Success = false, ErrorMessage = "AccountManager not available", AccountName = "" };
        }
        
        LoginMethod loginMethod = AccountManager.Instance.DetermineLoginMethod(steamID, accountID);
        
        switch (loginMethod)
        {
            case LoginMethod.SteamID:
                Debug.Log($"ServerManager: Processing Steam ID login for SteamID: {steamID}");
                return await HandleSteamIDLogin(steamID, accountName, email, ipAddress, language);
                
            case LoginMethod.AccountID:
                Debug.Log($"ServerManager: Processing Account ID login for AccountID: {accountID}");
                return await HandleAccountIDLogin(accountID);
                
            case LoginMethod.None:
            default:
                Debug.LogError("ServerManager: No valid login method available. Both SteamID and AccountID are 0.");
                return new LoginResult { Success = false, ErrorMessage = "No valid login method available. Both SteamID and AccountID are 0.", AccountName = "" };
        }
    }
    private async Task<LoginResult> HandleSteamIDLogin(ulong steamID, string accountName, string email, string ipAddress, string language)
    {
        Debug.Log($"ServerManager: HandleSteamIDLogin ENTRY - steamID={steamID}");
        Debug.Log($"ServerManager: Attempting login with SteamID: {steamID}");
        
        Debug.Log($"ServerManager: Calling AccountManager.GetAccountBySteamIDAsync...");
        Dictionary<string, object> account = await AccountManager.Instance.GetAccountBySteamIDAsync(steamID);

        if (account != null)
        {
            Debug.Log($"ServerManager: Account found via SteamID.");
        }
        else
        {
            Debug.Log($"ServerManager: No account found for SteamID {steamID}. Attempting to create...");
            bool created = await AccountManager.Instance.CreateNewAccountAsync(accountName, AccountManager.Instance.GenerateRandomPassword(), email, steamID, language, ipAddress);
            if (created)
            {
                Debug.Log($"ServerManager: Created new account for Steam user: {accountName}. Fetching account info...");
                account = await AccountManager.Instance.GetAccountByUsernameAsync(accountName);
                if (account == null)
                {
                    Debug.LogError("ServerManager: Failed to fetch newly created account info!");
                    return new LoginResult { Success = false, ErrorMessage = "Failed to fetch newly created account info!", AccountName = "" };
                }
            }
            else
            {
                Debug.LogError($"ServerManager: Failed to create new account for Steam user: {accountName}");
                return new LoginResult { Success = false, ErrorMessage = $"Failed to create new account for Steam user: {accountName}", AccountName = "" };
            }
        }

        Debug.Log($"ServerManager: Extracting account info to LoginResult...");
        return ExtractAccountInfoToLoginResult(account);
    }
    private async Task<LoginResult> HandleAccountIDLogin(int accountID)
    {
        Debug.Log($"ServerManager: HandleAccountIDLogin ENTRY - accountID={accountID}");
        Debug.Log($"ServerManager: Attempting login with AccountID: {accountID}");
        
        Debug.Log($"ServerManager: Calling AccountManager.GetAccountByAccountIDAsync...");
        Dictionary<string, object> account = await AccountManager.Instance.GetAccountByAccountIDAsync(accountID);

        if (account != null)
        {
            Debug.Log($"ServerManager: Account found via AccountID.");
        }
        else
        {
            Debug.LogError($"ServerManager: No account found for AccountID {accountID}. Cannot create account without SteamID.");
            return new LoginResult { Success = false, ErrorMessage = $"No account found for AccountID {accountID}. Cannot create account without SteamID.", AccountName = "" };
        }

        Debug.Log($"ServerManager: Extracting account info to LoginResult...");
        return ExtractAccountInfoToLoginResult(account);
    }
    private LoginResult ExtractAccountInfoToLoginResult(Dictionary<string, object> account)
    {
        Debug.Log($"ServerManager: ExtractAccountInfoToLoginResult ENTRY");
        try
        {
            LoginResult result = new LoginResult 
            { 
                Success = true,
                ErrorMessage = "" // Initialize to empty string to avoid null during serialization
            };

            if (account.TryGetValue("AccountID", out object idObj) && idObj != DBNull.Value)
            {
                result.AccountID = Convert.ToInt32(idObj);
                Debug.Log($"ServerManager: Extracted AccountID: {result.AccountID}");
            }
            else
            {
                Debug.LogError("ServerManager: Could not retrieve AccountID from account data.");
                return new LoginResult { Success = false, ErrorMessage = "Could not retrieve AccountID from account data.", AccountName = "" };
            }

            if (account.TryGetValue("Username", out object nameObj) && nameObj != DBNull.Value)
            {
                result.AccountName = nameObj.ToString();
                Debug.Log($"ServerManager: Extracted AccountName: {result.AccountName}");
            }
            else
            {
                Debug.LogError("ServerManager: Could not retrieve Username from account data.");
                return new LoginResult { Success = false, ErrorMessage = "Could not retrieve Username from account data.", AccountName = "" };
            }

            if (account.TryGetValue("SteamID", out object steamIdObj) && steamIdObj != DBNull.Value)
            {
                result.SteamID = Convert.ToUInt64(steamIdObj);
                Debug.Log($"ServerManager: Extracted SteamID: {result.SteamID}");
            }

            Debug.Log($"ServerManager: Successfully extracted all account info. Returning success result.");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ExtractAccountInfoToLoginResult: {ex.Message}\n{ex.StackTrace}");
            return new LoginResult { Success = false, ErrorMessage = $"Server error extracting account info: {ex.Message}", AccountName = "" };
        }
    }
    #endregion

    #region Server-Side Inventory Logic
    private async Task<AccountInventoryResult> ProcessAccountInventory(int accountID)
    {
        Debug.Log($"ServerManager: ProcessAccountInventory ENTRY - accountID={accountID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessAccountInventory called on client! This should only run on server.");
            return new AccountInventoryResult { Success = false, ErrorMessage = "Server-side method called on client", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0], Workbenches = new WorkbenchData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance methods...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new AccountInventoryResult { Success = false, ErrorMessage = "InventoryManager not available", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0], Workbenches = new WorkbenchData[0] };
        }
        
        try
        {
            // Load all account inventory data
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetAccountInventoryItemsAsync(accountID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetAccountInventoryResourceItemsAsync(accountID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetAccountInventorySubComponentsAsync(accountID);
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);
            
            Debug.Log($"ServerManager: Retrieved {itemDictionaries.Count} items, {resourceItemDictionaries.Count} resource items, {subComponentDictionaries.Count} subcomponents, {workbenchDictionaries.Count} workbenches");
            
            // Convert to network structs
            InventoryItemData[] items = ConvertToInventoryItemData(itemDictionaries);
            InventoryResourceItemData[] resourceItems = ConvertToInventoryResourceItemData(resourceItemDictionaries);
            InventorySubComponentData[] subComponents = ConvertToInventorySubComponentData(subComponentDictionaries);
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);
            
            Debug.Log($"ServerManager: Successfully processed account inventory. Returning {items.Length} items, {resourceItems.Length} resource items, {subComponents.Length} subcomponents, {workbenches.Length} workbenches.");
            return new AccountInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = items,
                ResourceItems = resourceItems,
                SubComponents = subComponents,
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
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0],
                Workbenches = new WorkbenchData[0]
            };
        }
    }
    private async Task<CharacterInventoryResult> ProcessCharacterInventory(int characterID)
    {
        Debug.Log($"ServerManager: ProcessCharacterInventory ENTRY - characterID={characterID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessCharacterInventory called on client! This should only run on server.");
            return new CharacterInventoryResult { Success = false, ErrorMessage = "Server-side method called on client", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance methods for character...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new CharacterInventoryResult { Success = false, ErrorMessage = "InventoryManager not available", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0] };
        }
        
        try
        {
            // Load all character inventory data
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetCharacterInventoryItemsAsync(characterID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetCharacterInventoryResourceItemsAsync(characterID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetCharacterInventorySubComponentsAsync(characterID);
            
            Debug.Log($"ServerManager: Retrieved {itemDictionaries.Count} items, {resourceItemDictionaries.Count} resource items, {subComponentDictionaries.Count} subcomponents for character {characterID}");
            
            // Convert to network structs
            InventoryItemData[] items = ConvertToInventoryItemData(itemDictionaries);
            InventoryResourceItemData[] resourceItems = ConvertToInventoryResourceItemData(resourceItemDictionaries);
            InventorySubComponentData[] subComponents = ConvertToInventorySubComponentData(subComponentDictionaries);
            
            Debug.Log($"ServerManager: Successfully processed character inventory. Returning {items.Length} items, {resourceItems.Length} resource items, {subComponents.Length} subcomponents.");
            return new CharacterInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = items,
                ResourceItems = resourceItems,
                SubComponents = subComponents
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterInventory: {ex.Message}\n{ex.StackTrace}");
            return new CharacterInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading character inventory: {ex.Message}", 
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0]
            };
        }
    }
    private async Task<WorkbenchListResult> ProcessWorkbenchList(int accountID)
    {
        Debug.Log($"ServerManager: ProcessWorkbenchList ENTRY - accountID={accountID}");

        if (!IsServer)
        {
            Debug.LogError("ProcessWorkbenchList called on client! This should only run on server.");
            return new WorkbenchListResult { Success = false, ErrorMessage = "Server-side method called on client", Workbenches = new WorkbenchData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new WorkbenchListResult { Success = false, ErrorMessage = "InventoryManager not available", Workbenches = new WorkbenchData[0] };
        }

        try
        {
            // Load workbench data
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);

            Debug.Log($"ServerManager: Retrieved {workbenchDictionaries.Count} workbench records from database");

            // Convert to network structs
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);

            Debug.Log($"ServerManager: Successfully processed workbench list. Returning {workbenches.Length} workbenches.");
            return new WorkbenchListResult
            {
                Success = true,
                ErrorMessage = "",
                Workbenches = workbenches
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessWorkbenchList: {ex.Message}\n{ex.StackTrace}");
            return new WorkbenchListResult
            {
                Success = false,
                ErrorMessage = $"Server error loading workbenches: {ex.Message}",
                Workbenches = new WorkbenchData[0]
            };
        }
    }
    #endregion

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
    private InventoryItemData[] ConvertToInventoryItemData(List<Dictionary<string, object>> dictionaries)
    {
        InventoryItemData[] items = new InventoryItemData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventoryItemData
            {
                ItemID = GetIntValue(dict, "ItemID", 0),
                SlotID = GetIntValue(dict, "SlotID", 0)
            };
        }
        return items;
    }
    private InventoryResourceItemData[] ConvertToInventoryResourceItemData(List<Dictionary<string, object>> dictionaries)
    {
        InventoryResourceItemData[] items = new InventoryResourceItemData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventoryResourceItemData
            {
                ResourceItemID = GetIntValue(dict, "ResourceItemID", 0),
                Quantity = GetIntValue(dict, "Quantity", 1)
            };
        }
        return items;
    }
    private InventorySubComponentData[] ConvertToInventorySubComponentData(List<Dictionary<string, object>> dictionaries)
    {
        InventorySubComponentData[] items = new InventorySubComponentData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventorySubComponentData
            {
                SubComponentID = GetIntValue(dict, "SubComponentID", 0)
            };
        }
        return items;
    }
    private WorkbenchData[] ConvertToWorkbenchData(List<Dictionary<string, object>> dictionaries)
    {
        WorkbenchData[] items = new WorkbenchData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new WorkbenchData
            {
                WorkBenchType = GetIntValue(dict, "WorkBenchType", 1)
            };
        }
        return items;
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

    #region Utility Methods

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[MasterServer] {message}");
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
    public string areaId;
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
    public string areaId;
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