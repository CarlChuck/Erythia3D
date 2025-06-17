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

    private void InitializeServer()
    {

        // Start the server first
        if (!networkManager.IsListening)
        {
            bool serverStarted = networkManager.StartServer();
            if (!serverStarted)
            {
                Debug.LogError("ServerBootstrap: Failed to start server!");
                return;
            }
        }

        if (networkManager.IsServer)
        {
            SpawnServerManager();
        }
        else
        {
            Debug.Log("ServerBootstrap: Server not ready yet, subscribing to OnServerStarted event");
            networkManager.OnServerStarted += OnServerStarted;
        }
    }

    private bool ShouldRunServer()
    {
        if (Application.isBatchMode)
        {
            Debug.Log("ServerBootstrap: Running in batch mode - should run server");
            return true;
        }
            
        // Run server if explicitly set via command line
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string t in args)
        {
            if (t != "-server" && t != "--server")
            {
                continue;
            }

            Debug.Log($"ServerBootstrap: Found server command line arg: {t} - should run server");
            return true;
        }
        
        // In editor, always allow server to run for testing
        #if UNITY_EDITOR
        //Debug.Log("ServerBootstrap: Running in editor - should run server");
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
        if (ServerManager.Instance != null)
        {
            Debug.LogWarning("ServerBootstrap: ServerManager already exists, skipping spawn");
            return;
        }

        // Instantiate ServerManager
        GameObject serverManagerInstance = Instantiate(serverManagerPrefab);
        
        // Get NetworkObject component
        NetworkObject networkObject = serverManagerInstance.GetComponent<NetworkObject>();

        // Configure the NetworkObject to have no visibility to clients.
        networkObject.CheckObjectVisibility = (clientId) => false;

        // Spawn the NetworkObject with server ownership
        networkObject.SpawnWithOwnership(NetworkManager.ServerClientId);
        
        Debug.Log($"ServerBootstrap: ServerManager spawned with NetworkObjectId {networkObject.NetworkObjectId}");
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