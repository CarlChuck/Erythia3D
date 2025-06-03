using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

[Serializable]
public struct ZoneConfiguration
{
    public int ZoneID;
    public string SceneName;
    public ResourceSubType ResourceType;
    public string DisplayName;
    public bool IsDefault; // Mark the default/starting zone
}

[Serializable]
public struct PlayerZoneInfo : INetworkSerializable
{
    public int CharacterID;
    public int ZoneID;
    public string ZoneName;
    public Vector3? SpawnPosition; // Null if MarketWaypoint should be used
    public bool RequiresMarketWaypoint;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CharacterID);
        serializer.SerializeValue(ref ZoneID);
        serializer.SerializeValue(ref ZoneName);
        
        // Handle nullable Vector3
        bool hasSpawnPosition = SpawnPosition.HasValue;
        serializer.SerializeValue(ref hasSpawnPosition);
        if (hasSpawnPosition)
        {
            Vector3 position = SpawnPosition.Value;
            serializer.SerializeValue(ref position);
            SpawnPosition = position;
        }
        else
        {
            SpawnPosition = null;
        }
        
        serializer.SerializeValue(ref RequiresMarketWaypoint);
    }
}


public class WorldManager : MonoBehaviour
{
    #region Constants
    private const float zoneManagerSpawnDelay = 0.5f; // Small delay to ensure zone is fully loaded
    private const int defaultZoneID = 1; // Default starting zone
    #endregion

    #region Private Fields
    [SerializeField] private GameObject zoneManagerPrefab;
    [SerializeField] private bool debugMode = true;
    [SerializeField] private Transform zoneManagerParent; // Optional parent for organization
    
    // Zone Configuration System
    [SerializeField] private ZoneConfiguration[] zoneConfigurations = new ZoneConfiguration[]
    {
        new ZoneConfiguration
        {
            ZoneID = 1,
            SceneName = "IthoriaSouth",
            ResourceType = ResourceSubType.Ithoria,
            DisplayName = "Ithoria South",
            IsDefault = true
        }
        // Future zones can be added here:
        // new ZoneConfiguration { ZoneID = 2, SceneName = "Aelystian", ResourceType = ResourceSubType.Aelystian, DisplayName = "Aelystian Plains", IsDefault = false }
    };
    
    // Active ZoneManager tracking
    private Dictionary<string, ZoneManager> activeZoneManagers = new Dictionary<string, ZoneManager>();
    private Dictionary<string, NetworkObject> zoneManagerNetworkObjects = new Dictionary<string, NetworkObject>();
    
    private static WorldManager instance;
    private Dictionary<int, ZoneConfiguration> zoneIdToConfig = new Dictionary<int, ZoneConfiguration>();
    private Dictionary<string, ZoneConfiguration> sceneNameToConfig = new Dictionary<string, ZoneConfiguration>();
    private bool hasInitialized = false;
    #endregion

    #region Public Properties
    public static WorldManager Instance => instance;
    public int DefaultZoneID => defaultZoneID;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            InitializeZoneMapping();
            
            if (debugMode)
            {
                Debug.Log("WorldManager: Initialized singleton instance");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.Log("WorldManager: Duplicate instance destroyed");
            }
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (instance == this && !hasInitialized)
        {
            InitializeWorldManager();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            // Unsubscribe from events
            if (PersistentSceneManager.Instance != null)
            {
                PersistentSceneManager.OnZoneLoadCompleted -= OnZoneLoadCompleted;
                PersistentSceneManager.OnZoneUnloadCompleted -= OnZoneUnloadCompleted;
            }
            
            instance = null;
        }
        
        // Clean up any active ZoneManagers
        CleanupAllZoneManagers();
    }
    #endregion

    #region Initialization
    private void InitializeZoneMapping()
    {
        zoneIdToConfig.Clear();
        sceneNameToConfig.Clear();
        
        foreach (ZoneConfiguration config in zoneConfigurations)
        {
            // Map ZoneID to configuration
            if (!zoneIdToConfig.ContainsKey(config.ZoneID))
            {
                zoneIdToConfig[config.ZoneID] = config;
            }
            else
            {
                Debug.LogError($"WorldManager: Duplicate ZoneID {config.ZoneID} found in configuration!");
            }
            
            // Map scene name to configuration
            if (!sceneNameToConfig.ContainsKey(config.SceneName))
            {
                sceneNameToConfig[config.SceneName] = config;
            }
            else
            {
                Debug.LogError($"WorldManager: Duplicate scene name {config.SceneName} found in configuration!");
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"WorldManager: Initialized zone mapping with {zoneConfigurations.Length} zones");
        }
    }

    private void InitializeWorldManager()
    {
        hasInitialized = true;
        
        if (debugMode)
        {
            Debug.Log("WorldManager: Starting initialization");
        }

        // Validate prefab assignment
        if (zoneManagerPrefab == null)
        {
            Debug.LogError("WorldManager: ZoneManager prefab is not assigned!");
            return;
        }

        // Validate prefab has required components
        if (zoneManagerPrefab.GetComponent<ZoneManager>() == null)
        {
            Debug.LogError("WorldManager: ZoneManager prefab is missing ZoneManager component!");
            return;
        }

        if (zoneManagerPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError("WorldManager: ZoneManager prefab is missing NetworkObject component!");
            return;
        }

        // Subscribe to PersistentSceneManager events
        if (PersistentSceneManager.Instance != null)
        {
            PersistentSceneManager.OnZoneLoadCompleted += OnZoneLoadCompleted;
            PersistentSceneManager.OnZoneUnloadCompleted += OnZoneUnloadCompleted;
            
            if (debugMode)
            {
                Debug.Log("WorldManager: Subscribed to PersistentSceneManager events");
            }
        }
        else
        {
            Debug.LogError("WorldManager: PersistentSceneManager.Instance not found!");
        }

        if (debugMode)
        {
            Debug.Log("WorldManager: Initialization complete");
        }
    }
    #endregion

    #region Zone Event Handlers
    private void OnZoneLoadCompleted(string zoneName)
    {
        if (!IsServer())
        {
            // Only server should spawn ZoneManagers
            if (debugMode)
            {
                Debug.Log($"WorldManager: Ignoring zone load '{zoneName}' - not running as server");
            }
            return;
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' loaded, spawning ZoneManager...");
        }

        // Small delay to ensure zone scene is fully loaded
        StartCoroutine(DelayedZoneManagerSpawn(zoneName));
    }

    private void OnZoneUnloadCompleted(string zoneName)
    {
        if (!IsServer())
        {
            return;
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' unloaded, cleaning up ZoneManager...");
        }

        CleanupZoneManager(zoneName);
    }
    #endregion

    #region ZoneManager Lifecycle
    private System.Collections.IEnumerator DelayedZoneManagerSpawn(string zoneName)
    {
        yield return new WaitForSeconds(zoneManagerSpawnDelay);
        SpawnZoneManager(zoneName);
    }

    private void SpawnZoneManager(string zoneName)
    {
        try
        {
            Debug.Log($"WorldManager: SpawnZoneManager ENTRY for zone '{zoneName}'");

            // Check if ZoneManager already exists for this zone
            if (activeZoneManagers.ContainsKey(zoneName))
            {
                Debug.LogWarning($"WorldManager: ZoneManager already exists for zone '{zoneName}'");
                return;
            }

            // Get zone configuration
            ZoneConfiguration? configNullable = GetZoneConfiguration(zoneName);
            if (!configNullable.HasValue)
            {
                Debug.LogError($"WorldManager: No configuration found for zone '{zoneName}'. Cannot spawn ZoneManager.");
                return;
            }
            
            ZoneConfiguration config = configNullable.Value;
            Debug.Log($"WorldManager: Found zone configuration for '{zoneName}' with ResourceType '{config.ResourceType}'");

            // Get ServerManager reference
            ServerManager serverManager = ServerManager.Instance;
            if (serverManager == null)
            {
                Debug.LogError($"WorldManager: ServerManager.Instance is null. Cannot spawn ZoneManager for zone '{zoneName}'.");
                return;
            }
            Debug.Log($"WorldManager: ServerManager found, proceeding with ZoneManager spawn");


            // Instantiate ZoneManager prefab
            Debug.Log($"WorldManager: Instantiating ZoneManager prefab for zone '{zoneName}'");
            GameObject zoneManagerInstance = Instantiate(zoneManagerPrefab);
            zoneManagerInstance.name = $"ZoneManager_{zoneName}";
            Debug.Log($"WorldManager: ZoneManager GameObject created with name '{zoneManagerInstance.name}'");

            // Get components
            ZoneManager zoneManager = zoneManagerInstance.GetComponent<ZoneManager>();
            NetworkObject networkObject = zoneManagerInstance.GetComponent<NetworkObject>();

            if (zoneManager == null || networkObject == null)
            {
                Debug.LogError($"WorldManager: Failed to get required components from ZoneManager instance for zone '{zoneName}' - ZoneManager: {zoneManager != null}, NetworkObject: {networkObject != null}");
                Destroy(zoneManagerInstance);
                return;
            }
            Debug.Log($"WorldManager: Got ZoneManager and NetworkObject components successfully");

            // Configure server-only visibility
            networkObject.CheckObjectVisibility = (clientId) => false; // Server-only, invisible to all clients
            Debug.Log($"WorldManager: Configured server-only visibility for ZoneManager");

            // Spawn as NetworkObject
            Debug.Log($"WorldManager: Spawning ZoneManager as NetworkObject...");
            networkObject.Spawn();
            Debug.Log($"WorldManager: ZoneManager NetworkObject spawned successfully");

            // Initialize ZoneManager with zone-specific data
            Debug.Log($"WorldManager: Initializing ZoneManager for zone '{zoneName}' with ResourceType '{config.ResourceType}'");
            zoneManager.InitializeForZone(zoneName, config.ResourceType, serverManager);
            Debug.Log($"WorldManager: ZoneManager initialization completed");

            // Track the spawned ZoneManager
            activeZoneManagers[zoneName] = zoneManager;
            zoneManagerNetworkObjects[zoneName] = networkObject;

            Debug.Log($"WorldManager: Successfully spawned and initialized ZoneManager for zone '{zoneName}'. Total active ZoneManagers: {activeZoneManagers.Count}");

            // Notify ServerManager that ZoneManager is ready (if needed for future integration)
            NotifyServerManagerZoneReady(zoneName, zoneManager);
        }
        catch (Exception ex)
        {
            Debug.LogError($"WorldManager: Exception while spawning ZoneManager for zone '{zoneName}': {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CleanupZoneManager(string zoneName)
    {
        try
        {
            // Remove from tracking
            if (activeZoneManagers.ContainsKey(zoneName))
            {
                ZoneManager zoneManager = activeZoneManagers[zoneName];
                activeZoneManagers.Remove(zoneName);

                if (debugMode)
                {
                    Debug.Log($"WorldManager: Removed ZoneManager for zone '{zoneName}' from active tracking");
                }
            }

            // Despawn NetworkObject
            if (zoneManagerNetworkObjects.ContainsKey(zoneName))
            {
                NetworkObject networkObject = zoneManagerNetworkObjects[zoneName];
                zoneManagerNetworkObjects.Remove(zoneName);

                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                    
                    if (debugMode)
                    {
                        Debug.Log($"WorldManager: Despawned ZoneManager NetworkObject for zone '{zoneName}'");
                    }
                }

                // Destroy the GameObject
                if (networkObject != null && networkObject.gameObject != null)
                {
                    Destroy(networkObject.gameObject);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"WorldManager: Exception while cleaning up ZoneManager for zone '{zoneName}': {ex.Message}");
        }
    }

    private void CleanupAllZoneManagers()
    {
        if (debugMode)
        {
            Debug.Log($"WorldManager: Cleaning up all {activeZoneManagers.Count} active ZoneManagers");
        }

        // Create a copy of the keys to avoid modification during iteration
        List<string> zoneNames = new List<string>(activeZoneManagers.Keys);
        
        foreach (string zoneName in zoneNames)
        {
            CleanupZoneManager(zoneName);
        }
    }
    #endregion

    #region Configuration Management
    /// <summary>
    /// Get zone configuration by ZoneID
    /// </summary>
    public ZoneConfiguration? GetZoneConfiguration(int zoneID)
    {
        if (zoneIdToConfig.TryGetValue(zoneID, out ZoneConfiguration config))
        {
            return config;
        }
        return null;
    }

    /// <summary>
    /// Get zone configuration by scene name
    /// </summary>
    public ZoneConfiguration? GetZoneConfiguration(string sceneName)
    {
        if (sceneNameToConfig.TryGetValue(sceneName, out ZoneConfiguration config))
        {
            return config;
        }
        return null;
    }

    /// <summary>
    /// Get the default zone configuration
    /// </summary>
    public ZoneConfiguration GetDefaultZoneConfiguration()
    {
        foreach (ZoneConfiguration config in zoneConfigurations)
        {
            if (config.IsDefault)
            {
                return config;
            }
        }
        
        // Fallback to first zone if no default marked
        if (zoneConfigurations.Length > 0)
        {
            Debug.LogWarning("WorldManager: No default zone marked, using first zone as default");
            return zoneConfigurations[0];
        }
        
        Debug.LogError("WorldManager: No zones configured!");
        return default;
    }

    /// <summary>
    /// Check if a ZoneID is valid
    /// </summary>
    public bool IsValidZoneID(int zoneID)
    {
        return zoneIdToConfig.ContainsKey(zoneID);
    }

    /// <summary>
    /// Get all available zone IDs
    /// </summary>
    public int[] GetAllZoneIDs()
    {
        int[] zoneIDs = new int[zoneIdToConfig.Count];
        zoneIdToConfig.Keys.CopyTo(zoneIDs, 0);
        return zoneIDs;
    }

    /// <summary>
    /// Get scene name from ZoneID with fallback to default
    /// </summary>
    public string GetSceneNameForZone(int zoneID)
    {
        ZoneConfiguration? config = GetZoneConfiguration(zoneID);
        if (config.HasValue)
        {
            return config.Value.SceneName;
        }
        
        // Fallback to default zone
        Debug.LogWarning($"WorldManager: Invalid ZoneID {zoneID}, falling back to default zone");
        ZoneConfiguration defaultConfig = GetDefaultZoneConfiguration();
        return defaultConfig.SceneName;
    }
    #endregion

    #region Player Zone Loading API
    /// <summary>
    /// Get MarketWaypoint position from ZoneManager (server-side only)
    /// </summary>
    public Vector3? GetMarketWaypointPosition(string zoneName)
    {
        if (!IsServer())
        {
            Debug.LogError("WorldManager: GetMarketWaypointPosition can only be called on server!");
            return null;
        }

        if (debugMode)
        {
            Debug.Log($"WorldManager: Looking for ZoneManager for zone '{zoneName}'. Active ZoneManagers: {activeZoneManagers.Count}");
            foreach (var kvp in activeZoneManagers)
            {
                Debug.Log($"WorldManager: Available ZoneManager: '{kvp.Key}' -> {(kvp.Value != null ? "Valid" : "NULL")}");
            }
        }

        if (activeZoneManagers.TryGetValue(zoneName, out ZoneManager zoneManager))
        {
            if (zoneManager != null)
            {
                if (debugMode)
                {
                    Debug.Log($"WorldManager: Found ZoneManager for '{zoneName}', checking for MarketWaypoint...");
                }

                if (zoneManager.HasMarketWaypoint())
                {
                    Transform waypoint = zoneManager.GetMarketWaypoint();
                    Vector3 position = waypoint.position;
                    
                    if (debugMode)
                    {
                        Debug.Log($"WorldManager: MarketWaypoint position for zone '{zoneName}': {position}");
                    }
                    
                    return position;
                }
                else
                {
                    Debug.LogWarning($"WorldManager: ZoneManager for '{zoneName}' has no MarketWaypoint");
                    return null;
                }
            }
            else
            {
                Debug.LogWarning($"WorldManager: ZoneManager for '{zoneName}' is null!");
                return null;
            }
        }
        else
        {
            Debug.LogWarning($"WorldManager: No ZoneManager found for zone '{zoneName}'");
            return null;
        }
    }
    #endregion

    #region Integration Methods
    private void NotifyServerManagerZoneReady(string zoneName, ZoneManager zoneManager)
    {
        // This method can be expanded for future ServerManager integration
        // For now, it's a placeholder for communication between WorldManager and ServerManager
        
        if (debugMode)
        {
            Debug.Log($"WorldManager: Zone '{zoneName}' is ready for server operations");
        }

        // Future: ServerManager.OnZoneManagerReady(zoneName, zoneManager);
    }

    /// <summary>
    /// Get active ZoneManager for a specific zone (for ServerManager access)
    /// </summary>
    public ZoneManager GetZoneManager(string zoneName)
    {
        return activeZoneManagers.ContainsKey(zoneName) ? activeZoneManagers[zoneName] : null;
    }

    /// <summary>
    /// Get all active ZoneManagers
    /// </summary>
    public Dictionary<string, ZoneManager> GetAllZoneManagers()
    {
        return new Dictionary<string, ZoneManager>(activeZoneManagers);
    }
    #endregion

    #region Helper Methods
    private bool IsServer()
    {
        // Check if we're running as server
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsServer;
        }

        // Fallback: check if running in batch mode (dedicated server)
        return Application.isBatchMode;
    }
    #endregion

    #region Unity Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Debug: Print Active ZoneManagers")]
    private void DebugPrintActiveZoneManagers()
    {
        Debug.Log($"=== Active ZoneManagers ({activeZoneManagers.Count}) ===");
        foreach (var kvp in activeZoneManagers)
        {
            ZoneManager zm = kvp.Value;
            if (zm != null)
            {
                ZoneStatus status = zm.GetZoneStatus();
                Debug.Log($"Zone '{kvp.Key}': Initialized={status.IsInitialized}, Nodes={status.ActiveNodeCount}, Resources={status.RegionResourceCount}");
            }
            else
            {
                Debug.Log($"Zone '{kvp.Key}': NULL ZoneManager");
            }
        }
    }

    [ContextMenu("Debug: Print Zone Configurations")]
    private void DebugPrintZoneConfigurations()
    {
        Debug.Log($"=== Zone Configurations ({zoneConfigurations.Length}) ===");
        for (int i = 0; i < zoneConfigurations.Length; i++)
        {
            ZoneConfiguration config = zoneConfigurations[i];
            Debug.Log($"Config {i}: Zone='{config.SceneName}', ResourceType='{config.ResourceType}'");
        }
    }

    [ContextMenu("Debug: Force Spawn ZoneManager for IthoriaSouth")]
    private void DebugForceSpawnIthoriaSouth()
    {
        if (Application.isPlaying && IsServer())
        {
            SpawnZoneManager("IthoriaSouth");
        }
        else
        {
            Debug.LogWarning("WorldManager: Debug spawn only available during play mode on server");
        }
    }

    [ContextMenu("Debug: Cleanup All ZoneManagers")]
    private void DebugCleanupAllZoneManagers()
    {
        if (Application.isPlaying)
        {
            CleanupAllZoneManagers();
        }
        else
        {
            Debug.LogWarning("WorldManager: Debug cleanup only available during play mode");
        }
    }
    #endif
    #endregion
}
