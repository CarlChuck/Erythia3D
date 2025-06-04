using Unity.Netcode;
using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    [Header("Server Prefabs")]
    [SerializeField] private GameObject serverManagerPrefab;
    [SerializeField] private NetworkManager networkManager;

    [Header("Settings")]
    [SerializeField] private bool autoStartServer = true;

    private void Start()
    {
        
        // Only initialize server if this is actually running on the server
        if (autoStartServer && ShouldRunServer())
        {
            Debug.Log("ServerBootstrap: Conditions met, initializing server...");
            InitializeServer();
        }
        else if (!ShouldRunServer())
        {
            Debug.Log("ServerBootstrap: ShouldRunServer returned false, skipping server initialization");
            // Don't disable on clients in editor for debugging purposes
            #if !UNITY_EDITOR
            enabled = false;
            #endif
        }
        else
        {
            Debug.Log("ServerBootstrap: autoStartServer is false, waiting for manual start");
        }
    }

    public void InitializeServer()
    {
        // Additional safety check - only proceed if we should run server
        if (!ShouldRunServer() && !Application.isBatchMode)
        {
            Debug.LogWarning("ServerBootstrap: InitializeServer called but this instance should not run server");
            return;
        }

        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
            Debug.Log($"ServerBootstrap: NetworkManager.Singleton = {networkManager}");
        }

        if (networkManager == null)
        {
            Debug.LogError("ServerBootstrap: NetworkManager not found!");
            return;
        }

        Debug.Log($"ServerBootstrap: NetworkManager.IsListening = {networkManager.IsListening}");

        // Start the server first
        if (!networkManager.IsListening)
        {
            Debug.Log("ServerBootstrap: Starting server...");
            bool serverStarted = networkManager.StartServer();
            if (!serverStarted)
            {
                Debug.LogError("ServerBootstrap: Failed to start server!");
                return;
            }
            Debug.Log("ServerBootstrap: Server started successfully");
        }
        else
        {
            Debug.Log("ServerBootstrap: NetworkManager already listening");
        }

        // Wait for server to be ready, then spawn ServerManager
        Debug.Log($"ServerBootstrap: NetworkManager.IsServer = {networkManager.IsServer}");
        if (networkManager.IsServer)
        {
            Debug.Log("ServerBootstrap: Server is ready, spawning ServerManager...");
            SpawnServerManager();
        }
        else
        {
            Debug.Log("ServerBootstrap: Server not ready yet, subscribing to OnServerStarted event");
            // Subscribe to server started event if not ready yet
            networkManager.OnServerStarted += OnServerStarted;
        }
    }

    private bool ShouldRunServer()
    {
        Debug.Log("ServerBootstrap: Checking if should run server...");
        
        // Run server if in batch mode (dedicated server)
        if (Application.isBatchMode)
        {
            Debug.Log("ServerBootstrap: Running in batch mode - should run server");
            return true;
        }
            
        // Run server if explicitly set via command line
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-server" || args[i] == "--server")
            {
                Debug.Log($"ServerBootstrap: Found server command line arg: {args[i]} - should run server");
                return true;
            }
        }
        
        // In editor, always allow server to run for testing
        #if UNITY_EDITOR
        Debug.Log("ServerBootstrap: Running in editor - should run server");
        return true;
        #else
        // For client builds without server flags
        Debug.Log("ServerBootstrap: No server conditions met - should not run server");
        return false;
        #endif
    }

    private void OnServerStarted()
    {
        networkManager.OnServerStarted -= OnServerStarted;
        SpawnServerManager();
    }

    private void SpawnServerManager()
    {        
        if (serverManagerPrefab == null)
        {
            Debug.LogError("ServerBootstrap: ServerManager prefab is not assigned!");
            return;
        }

        if (ServerManager.Instance != null)
        {
            Debug.LogWarning("ServerBootstrap: ServerManager already exists, skipping spawn");
            return;
        }

        Debug.Log("ServerBootstrap: Instantiating ServerManager prefab...");
        // Instantiate ServerManager
        GameObject serverManagerInstance = Instantiate(serverManagerPrefab);
        
        // Get NetworkObject component
        NetworkObject networkObject = serverManagerInstance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("ServerBootstrap: ServerManager prefab missing NetworkObject component!");
            Destroy(serverManagerInstance);
            return;
        }

        Debug.Log("ServerBootstrap: Setting up server-only visibility...");
        // Configure the NetworkObject for server-only visibility before spawning
        networkObject.CheckObjectVisibility = (clientId) =>
        {
            // Only visible to the server (return false for all clients)
            bool isVisible = clientId == NetworkManager.ServerClientId;
            Debug.Log($"ServerBootstrap: CheckObjectVisibility for client {clientId}: {isVisible}");
            return isVisible;
        };

        Debug.Log("ServerBootstrap: Spawning NetworkObject...");
        // Spawn the NetworkObject with server ownership
        networkObject.SpawnWithOwnership(NetworkManager.ServerClientId, false); // false = don't destroy with owner
        
        Debug.Log("ServerBootstrap: ServerManager spawned successfully as server-only NetworkObject");
    }

    private void OnDestroy()
    {
        // Clean up event subscription
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    #if UNITY_EDITOR
    [ContextMenu("Force Start Server")]
    public void ForceStartServer()
    {
        Debug.Log("ServerBootstrap: ForceStartServer called from context menu");
        InitializeServer();
    }

    [ContextMenu("Debug Server Status")]
    public void DebugServerStatus()
    {
        Debug.Log($"ServerBootstrap Debug Status:");
        Debug.Log($"  - autoStartServer: {autoStartServer}");
        Debug.Log($"  - ShouldRunServer(): {ShouldRunServer()}");
        Debug.Log($"  - NetworkManager: {networkManager}");
        Debug.Log($"  - NetworkManager.IsServer: {(networkManager != null ? networkManager.IsServer : "N/A")}");
        Debug.Log($"  - NetworkManager.IsListening: {(networkManager != null ? networkManager.IsListening : "N/A")}");
        Debug.Log($"  - ServerManager.Instance: {ServerManager.Instance}");
        Debug.Log($"  - serverManagerPrefab assigned: {serverManagerPrefab != null}");
    }
    #endif
} 