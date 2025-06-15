using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Net;
using System.Net.Sockets;


/// <summary>
/// Manages a single game area server instance
/// Each area runs independently with its own NetworkManager
/// </summary>
public class AreaServerManager : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private ServerAreaConfig areaConfig;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Cross-Server Communication")]
    [SerializeField] private string masterServerAddress = "127.0.0.1";
    [SerializeField] private ushort masterServerPort = 8888;

    private NetworkManager areaNetworkManager;
    private MasterServerConnection masterConnection;
    private Dictionary<ulong, PlayerData> connectedPlayers = new();

    public ServerAreaConfig AreaConfig => areaConfig;
    public bool IsRunning => areaNetworkManager != null && areaNetworkManager.IsServer;
    public int ConnectedPlayersCount => connectedPlayers.Count;

    [System.Serializable]
    public class PlayerData
    {
        public ulong clientId;
        public string playerName;
        public Vector3 position;
        public DateTime joinTime;
    }

    void OnDestroy()
    {
        ShutdownAreaServer();
    }

    /// <summary>
    /// This is now the main entry point for setting up the area server.
    /// It is called by the ServerManager that creates it.
    /// </summary>
    public void Initialize(ServerAreaConfig config, NetworkManager networkManager)
    {
        this.areaConfig = config;
        this.areaNetworkManager = networkManager;
        InitializeAreaServer();
    }

    private void InitializeAreaServer()
    {
        if (areaNetworkManager == null)
        {
            LogError("NetworkManager component was not provided during initialization!");
            return;
        }

        // Configure transport
        var transport = areaNetworkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.SetConnectionData(GetLocalIPAddress(), areaConfig.port);
            LogDebug($"Configured transport for {areaConfig.areaId} on port {areaConfig.port}");
        }

        // Set up network callbacks
        areaNetworkManager.OnClientConnectedCallback += OnClientConnected;
        areaNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // Connect to master server
        masterConnection = new MasterServerConnection(masterServerAddress, masterServerPort);
        masterConnection.OnPlayerTransferRequest += HandlePlayerTransferRequest;

        if (areaConfig.autoStart)
        {
            StartAreaServer();
        }

        LogDebug($"Area server {areaConfig.areaId} initialized");
    }

    /// <summary>
    /// Start the area server
    /// </summary>
    public bool StartAreaServer()
    {
        if (IsRunning)
        {
            LogWarning($"Area server {areaConfig.areaId} is already running");
            return true;
        }

        try
        {
            // Load the area scene ADDITIVELY, so it doesn't unload the Persistent scene
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(areaConfig.sceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

            // Start the network server
            bool started = areaNetworkManager.StartServer();

            if (started)
            {
                // Register with master server
                RegisterWithMasterServer();
                LogDebug($"Area server {areaConfig.areaId} started successfully on port {areaConfig.port}");
                return true;
            }
            else
            {
                LogError($"Failed to start area server {areaConfig.areaId}");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception starting area server {areaConfig.areaId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Shutdown the area server
    /// </summary>
    public void ShutdownAreaServer()
    {
        if (areaNetworkManager != null)
        {
            // Notify master server we're shutting down
            UnregisterFromMasterServer();

            // Disconnect all clients
            if (IsRunning)
            {
                areaNetworkManager.Shutdown();
            }

            // Clean up callbacks
            areaNetworkManager.OnClientConnectedCallback -= OnClientConnected;
            areaNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        masterConnection?.Disconnect();
        LogDebug($"Area server {areaConfig.areaId} shut down");
    }

    #region Network Events

    private void OnClientConnected(ulong clientId)
    {
        LogDebug($"Client {clientId} connected to area {areaConfig.areaId}");

        // Store player data
        var playerData = new PlayerData
        {
            clientId = clientId,
            position = areaConfig.spawnPosition,
            joinTime = DateTime.Now
        };
        connectedPlayers[clientId] = playerData;

        // Spawn player at designated spawn point
        SpawnPlayerAtPosition(clientId, areaConfig.spawnPosition);

        // Update master server with new player count
        UpdateMasterServerStatus();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected from area {areaConfig.areaId}");

        if (connectedPlayers.ContainsKey(clientId))
        {
            connectedPlayers.Remove(clientId);
        }

        // Update master server with new player count
        UpdateMasterServerStatus();
    }

    #endregion

    #region Player Management

    /// <summary>
    /// Spawn a player at a specific position in this area
    /// </summary>
    private void SpawnPlayerAtPosition(ulong clientId, Vector3 position)
    {
        // This would typically instantiate your player prefab
        // For now, just update the stored position
        if (connectedPlayers.ContainsKey(clientId))
        {
            connectedPlayers[clientId].position = position;
        }

        // Send welcome message to client
        SendWelcomeMessageToClient(clientId);
    }

    /// <summary>
    /// Transfer a player to another area server
    /// </summary>
    public void TransferPlayerToArea(ulong clientId, string targetAreaId)
    {
        if (!connectedPlayers.ContainsKey(clientId))
        {
            LogError($"Cannot transfer unknown client {clientId}");
            return;
        }

        var playerData = connectedPlayers[clientId];

        // Request transfer through master server
        var transferRequest = new PlayerTransferRequest
        {
            clientId = clientId,
            playerName = playerData.playerName,
            fromAreaId = areaConfig.areaId,
            toAreaId = targetAreaId,
            playerPosition = playerData.position
        };

        masterConnection.RequestPlayerTransfer(transferRequest);

        LogDebug($"Initiated transfer of client {clientId} from {areaConfig.areaId} to {targetAreaId}");
    }

    private void HandlePlayerTransferRequest(PlayerTransferRequest request)
    {
        if (request.toAreaId == areaConfig.areaId)
        {
            LogDebug($"Accepting transfer of player {request.playerName} from {request.fromAreaId}");
            // Prepare to accept the incoming player
            // This would typically involve reserving a slot and preparing spawn data
        }
    }

    #endregion

    #region Master Server Communication

    private void RegisterWithMasterServer()
    {
        var serverInfo = new AreaServerInfo
        {
            areaId = areaConfig.areaId,
            sceneName = areaConfig.sceneName,
            address = GetLocalIPAddress(),
            port = areaConfig.port,
            maxPlayers = areaConfig.maxPlayers,
            currentPlayers = ConnectedPlayersCount,
            isOnline = true
        };

        masterConnection.RegisterServer(serverInfo);
        LogDebug($"Registered area server {areaConfig.areaId} with master server");
    }

    private void UnregisterFromMasterServer()
    {
        masterConnection.UnregisterServer(areaConfig.areaId);
        LogDebug($"Unregistered area server {areaConfig.areaId} from master server");
    }

    private void UpdateMasterServerStatus()
    {
        var statusUpdate = new ServerStatusUpdate
        {
            areaId = areaConfig.areaId,
            currentPlayers = ConnectedPlayersCount,
            isOnline = IsRunning
        };

        masterConnection.UpdateServerStatus(statusUpdate);
    }

    #endregion

    #region Client Communication

    private void SendWelcomeMessageToClient(ulong clientId)
    {
        // Send area-specific information to the client
        var welcomeData = new AreaWelcomeData
        {
            areaId = areaConfig.areaId,
            sceneName = areaConfig.sceneName,
            spawnPosition = areaConfig.spawnPosition,
            serverMessage = $"Welcome to {areaConfig.areaId}!"
        };

        // This would be sent via ClientRpc
        LogDebug($"Sent welcome message to client {clientId} in area {areaConfig.areaId}");
    }

    #endregion

    #region Utility Methods

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    public List<PlayerData> GetConnectedPlayers()
    {
        return connectedPlayers.Values.ToList();
    }

    public bool CanAcceptNewPlayer()
    {
        return ConnectedPlayersCount < areaConfig.maxPlayers;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[AreaServer-{areaConfig.areaId}] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[AreaServer-{areaConfig.areaId}] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[AreaServer-{areaConfig.areaId}] {message}");
    }

    #endregion
}

#region Data Structures

[System.Serializable]
public class ServerAreaConfig
{
    public string areaId;
    public string sceneName;
    public ushort port;
    public int maxPlayers = 50;
    public Vector3 spawnPosition;
    public bool autoStart = true;
}

[System.Serializable]
public class AreaServerInfo
{
    public string areaId;
    public string sceneName;
    public string address;
    public ushort port;
    public int maxPlayers;
    public int currentPlayers;
    public bool isOnline;
    public DateTime lastUpdate;
}

[System.Serializable]
public class PlayerTransferRequest
{
    public ulong clientId;
    public string playerName;
    public string fromAreaId;
    public string toAreaId;
    public Vector3 playerPosition;
    public Dictionary<string, object> playerData;
}

[System.Serializable]
public class ServerStatusUpdate
{
    public string areaId;
    public int currentPlayers;
    public bool isOnline;
    public DateTime timestamp;
}

[System.Serializable]
public class AreaWelcomeData
{
    public string areaId;
    public string sceneName;
    public Vector3 spawnPosition;
    public string serverMessage;
}

#endregion

#region Master Server Connection (Simplified)

/// <summary>
/// Handles communication with the master server
/// In production, this would use proper networking protocols
/// </summary>
public class MasterServerConnection
{
    private string masterServerAddress;
    private ushort masterServerPort;
    private bool isConnected;

    public event Action<PlayerTransferRequest> OnPlayerTransferRequest;

    public MasterServerConnection(string address, ushort port)
    {
        masterServerAddress = address;
        masterServerPort = port;
        Connect();
    }

    private void Connect()
    {
        // In production, implement actual network connection to master server
        // For now, simulate connection
        isConnected = true;
        Debug.Log($"[MasterServerConnection] Connected to master server at {masterServerAddress}:{masterServerPort}");
    }

    public void Disconnect()
    {
        isConnected = false;
        Debug.Log("[MasterServerConnection] Disconnected from master server");
    }

    public void RegisterServer(AreaServerInfo serverInfo)
    {
        if (!isConnected) return;

        // Send registration request to master server
        Debug.Log($"[MasterServerConnection] Registering server: {serverInfo.areaId}");
    }

    public void UnregisterServer(string areaId)
    {
        if (!isConnected) return;

        // Send unregistration request to master server
        Debug.Log($"[MasterServerConnection] Unregistering server: {areaId}");
    }

    public void UpdateServerStatus(ServerStatusUpdate statusUpdate)
    {
        if (!isConnected) return;

        // Send status update to master server
        Debug.Log($"[MasterServerConnection] Updating status for: {statusUpdate.areaId}");
    }

    public void RequestPlayerTransfer(PlayerTransferRequest request)
    {
        if (!isConnected) return;

        // Send transfer request to master server
        Debug.Log($"[MasterServerConnection] Requesting player transfer: {request.clientId} -> {request.toAreaId}");
    }
}

#endregion