using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap script for standalone Area Server builds
/// Handles command-line arguments and initializes the area server
/// </summary>
public class AreaServerBootstrap : MonoBehaviour
{
    [Header("Area Server Configuration")]
    [SerializeField] private GameObject areaServerPrefab; // AreaServer prefab from Assets/Prefabs/
    [SerializeField] private bool enableDebugLogs = true;

    private void Start()
    {
        // Parse command line arguments
        var args = ParseCommandLineArgs();
        
        if (args == null)
        {
            LogError("Failed to parse command line arguments. Area server will not start.");
            return;
        }

        LogDebug($"Starting Area Server: {args.areaId} on port {args.port}");
        
        // Initialize the area server
        InitializeAreaServer(args);
    }

    private void InitializeAreaServer(AreaServerArgs args)
    {
        // Instantiate the AreaServer prefab
        var areaServerGO = Instantiate(areaServerPrefab);
        var areaManager = areaServerGO.GetComponent<AreaServerManager>();
        
        if (areaManager == null)
        {
            LogError("AreaServerManager component not found on prefab!");
            return;
        }

        // Find the NetworkManager (should be a child of the AreaServer prefab)
        var networkManager = areaServerGO.GetComponentInChildren<NetworkManager>();
        if (networkManager == null)
        {
            LogError("NetworkManager component not found in AreaServer prefab!");
            return;
        }

        // Configure the server
        var config = new ServerAreaConfig
        {
            areaId = args.areaId,
            sceneName = args.sceneName,
            port = args.port,
            maxPlayers = 50, // Default, could be passed as arg
            spawnPosition = Vector3.zero, // Default, could be configured per scene
            autoStart = true
        };

        // Initialize the area manager
        areaManager.Initialize(config, networkManager);
        
        LogDebug($"Area Server {args.areaId} initialized successfully");
    }

    private AreaServerArgs ParseCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        var result = new AreaServerArgs();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--area":
                    if (i + 1 < args.Length)
                        result.areaId = args[i + 1].Trim('"');
                    break;
                case "--scene":
                    if (i + 1 < args.Length)
                        result.sceneName = args[i + 1].Trim('"');
                    break;
                case "--port":
                    if (i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort port))
                        result.port = port;
                    break;
                case "--master":
                    if (i + 1 < args.Length)
                        result.masterAddress = args[i + 1].Trim('"');
                    break;
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(result.areaId) || 
            string.IsNullOrEmpty(result.sceneName) || 
            result.port == 0)
        {
            LogError($"Missing required arguments. Got: area={result.areaId}, scene={result.sceneName}, port={result.port}");
            return null;
        }

        return result;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[AreaServerBootstrap] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[AreaServerBootstrap] {message}");
    }

    private class AreaServerArgs
    {
        public string areaId;
        public string sceneName;
        public ushort port;
        public string masterAddress = "127.0.0.1:8888";
    }
}