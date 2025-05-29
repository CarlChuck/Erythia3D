using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System;

public class PersistentSceneManager : MonoBehaviour
{
    #region Constants
    private const string mainMenuSceneName = "MainMenu";
    private const string persistentSceneName = "Persistent";
    #endregion

    #region Private Fields
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float sceneLoadDelay = 0.5f; // Small delay to ensure network initialization
    [SerializeField] private float persistentSceneMonitoringInterval = 5f; // Check persistent scene every 5 seconds
    
    private static PersistentSceneManager instance;
    private bool hasInitialized = false;
    
    // Zone Management Fields
    private Dictionary<string, bool> loadedZones = new Dictionary<string, bool>();
    private Dictionary<string, Coroutine> activeZoneLoadOperations = new Dictionary<string, Coroutine>();
    private HashSet<string> availableZones = new HashSet<string>(); // Known zone scenes
    
    // Persistent Scene Protection
    private Coroutine persistentSceneMonitorCoroutine;
    private bool persistentSceneProtectionEnabled = true;
    #endregion

    #region Public Properties
    public static PersistentSceneManager Instance => instance;
    #endregion

    #region Events
    public static event Action<string, bool> OnZoneLoadStateChanged; // zoneName, isLoaded
    public static event Action<string> OnZoneLoadCompleted; // zoneName
    public static event Action<string> OnZoneUnloadCompleted; // zoneName
    public static event Action OnPersistentSceneProtectionTriggered; // Emergency protection activated
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern - ensure only one PersistentSceneManager exists
        if (instance == null)
        {
            instance = this;
            InitializeAvailableZones();
            
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Initialized and set to DontDestroyOnLoad");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Duplicate instance destroyed");
            }
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (instance == this && !hasInitialized)
        {
            StartCoroutine(InitializeSceneManagement());
        }
    }

    private void OnDestroy()
    {
        // Clean up monitoring coroutine
        if (persistentSceneMonitorCoroutine != null)
        {
            StopCoroutine(persistentSceneMonitorCoroutine);
            persistentSceneMonitorCoroutine = null;
        }
        
        // Clean up any active zone loading operations
        foreach (var operation in activeZoneLoadOperations.Values)
        {
            if (operation != null)
            {
                StopCoroutine(operation);
            }
        }
        activeZoneLoadOperations.Clear();
    }
    #endregion

    #region Initialization
    private void InitializeAvailableZones()
    {
        // Initialize known zone scenes - this could be expanded to load from configuration
        availableZones.Add("IthoriaSouth");
        // Future zones would be added here:
        // availableZones.Add("Aelystian");
        // availableZones.Add("Qadian");
        // etc.
        
        // CRITICAL: Ensure persistent scene is NEVER added to available zones
        if (availableZones.Contains(persistentSceneName))
        {
            availableZones.Remove(persistentSceneName);
            Debug.LogError($"PersistentSceneManager: CRITICAL ERROR - {persistentSceneName} was found in available zones! Removed for safety.");
        }
        
        // PROTECTION: Ensure main menu is NEVER added to available zones for zone operations
        if (availableZones.Contains(mainMenuSceneName))
        {
            availableZones.Remove(mainMenuSceneName);
            Debug.LogError($"PersistentSceneManager: PROTECTION - {mainMenuSceneName} was found in available zones! Removed for safety.");
        }
        
        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Initialized {availableZones.Count} available zones");
        }
    }

    private IEnumerator InitializeSceneManagement()
    {
        hasInitialized = true;
        
        if (debugMode)
        {
            Debug.Log("PersistentSceneManager: Starting scene management initialization");
        }

        // Wait a frame to ensure all systems are initialized
        yield return new WaitForEndOfFrame();
        
        // Small delay to ensure network components are ready
        yield return new WaitForSeconds(sceneLoadDelay);

        // Ensure Persistent scene is set as active and won't unload
        SetupPersistentScene();

        // Start persistent scene monitoring
        StartPersistentSceneMonitoring();

        // Determine role and load appropriate scenes
        DetermineRoleAndLoadScenes();
    }

    private void SetupPersistentScene()
    {
        Scene persistentScene = SceneManager.GetSceneByName(persistentSceneName);
        
        if (persistentScene.IsValid())
        {
            // Set Persistent as the active scene
            SceneManager.SetActiveScene(persistentScene);
            
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Set {persistentSceneName} as active scene");
            }
        }
        else
        {
            Debug.LogError("PersistentSceneManager: Persistent scene not found! Ensure this script is in the Persistent scene.");
        }
    }

    private void DetermineRoleAndLoadScenes()
    {
        bool isServer = IsRunningAsServer();
        bool isClient = IsRunningAsClient();

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Role detection - IsServer: {isServer}, IsClient: {isClient}");
        }

        if (isClient && !isServer) // Pure client
        {
            LoadMainMenuScene();
        }
        else if (isServer && !isClient) // Dedicated server
        {
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Running as dedicated server - Main menu not needed");
            }
        }
        else if (isServer && isClient) // Host
        {
            LoadMainMenuScene(); // Host also needs main menu for UI
        }
        else
        {
            // Neither server nor client yet - might be in editor or pre-network state
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Network role not determined yet, loading main menu as default");
            }
            LoadMainMenuScene();
        }
    }
    #endregion

    #region Persistent Scene Protection
    private void StartPersistentSceneMonitoring()
    {
        if (persistentSceneProtectionEnabled && persistentSceneMonitorCoroutine == null)
        {
            persistentSceneMonitorCoroutine = StartCoroutine(MonitorPersistentScene());
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Started persistent scene monitoring for protection");
            }
        }
    }

    private IEnumerator MonitorPersistentScene()
    {
        while (persistentSceneProtectionEnabled)
        {
            yield return new WaitForSeconds(persistentSceneMonitoringInterval);
            
            // Check if persistent scene still exists and is loaded
            Scene persistentScene = SceneManager.GetSceneByName(persistentSceneName);
            
            if (!persistentScene.IsValid() || !persistentScene.isLoaded)
            {
                Debug.LogError($"PersistentSceneManager: CRITICAL ERROR - {persistentSceneName} scene is missing or unloaded!");
                OnPersistentSceneProtectionTriggered?.Invoke();
                
                // This is a critical failure - the game is likely broken
                // Log extensively and attempt emergency recovery
                Debug.LogError("PersistentSceneManager: Game state is compromised. NetworkManager and all server components may be lost.");
                Debug.LogError("PersistentSceneManager: Attempting emergency shutdown to prevent data corruption.");
                
                // Emergency shutdown - better to restart than continue in broken state
                if (Application.isEditor)
                {
                    Debug.LogError("PersistentSceneManager: In editor - stopping play mode");
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #endif
                }
                else
                {
                    Debug.LogError("PersistentSceneManager: In build - shutting down application");
                    Application.Quit();
                }
                
                yield break; // Stop monitoring
            }
            
            // Ensure persistent scene remains the active scene
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != persistentSceneName)
            {
                Debug.LogWarning($"PersistentSceneManager: Active scene changed to '{activeScene.name}'. Restoring {persistentSceneName} as active.");
                SceneManager.SetActiveScene(persistentScene);
            }
        }
    }

    /// <summary>
    /// Emergency method to disable persistent scene protection (use with extreme caution)
    /// </summary>
    public void DisablePersistentSceneProtection()
    {
        Debug.LogWarning("PersistentSceneManager: Persistent scene protection DISABLED. This is extremely dangerous!");
        persistentSceneProtectionEnabled = false;
        
        if (persistentSceneMonitorCoroutine != null)
        {
            StopCoroutine(persistentSceneMonitorCoroutine);
            persistentSceneMonitorCoroutine = null;
        }
    }

    /// <summary>
    /// Re-enable persistent scene protection
    /// </summary>
    public void EnablePersistentSceneProtection()
    {
        if (!persistentSceneProtectionEnabled)
        {
            Debug.Log("PersistentSceneManager: Re-enabling persistent scene protection");
            persistentSceneProtectionEnabled = true;
            StartPersistentSceneMonitoring();
        }
    }

    /// <summary>
    /// Check if the persistent scene is properly loaded and protected
    /// </summary>
    public bool IsPersistentSceneHealthy()
    {
        Scene persistentScene = SceneManager.GetSceneByName(persistentSceneName);
        bool isHealthy = persistentScene.IsValid() && persistentScene.isLoaded;
        
        if (!isHealthy && debugMode)
        {
            Debug.LogError("PersistentSceneManager: Persistent scene health check FAILED!");
        }
        
        return isHealthy;
    }
    #endregion

    #region Scene Loading
    private void LoadMainMenuScene()
    {
        if (IsSceneLoaded(mainMenuSceneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: {mainMenuSceneName} already loaded");
            }
            return;
        }

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Loading {mainMenuSceneName} additively");
        }

        StartCoroutine(LoadSceneAsync(mainMenuSceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        
        while (!asyncLoad.isDone)
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Loading {sceneName}... Progress: {asyncLoad.progress * 100f:F1}%");
            }
            yield return null;
        }

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Successfully loaded {sceneName}");
        }

        // Optionally set the loaded scene as active for UI purposes
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid() && sceneName == mainMenuSceneName)
        {
            // Keep Persistent as active scene for network objects, but we could switch for UI if needed
            // SceneManager.SetActiveScene(loadedScene);
        }
    }
    #endregion

    #region Zone Management
    /// <summary>
    /// Load a zone scene additively for gameplay
    /// </summary>
    public void LoadZone(string zoneName, Action<bool> onComplete = null)
    {
        // PROTECTION: Check if someone is trying to load essential scenes as zones
        if (IsProtectedFromZoneOperations(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot load protected scene '{zoneName}' as a zone!");
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(zoneName))
        {
            Debug.LogError("PersistentSceneManager: Cannot load zone - zone name is null or empty");
            onComplete?.Invoke(false);
            return;
        }

        if (!availableZones.Contains(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: Unknown zone '{zoneName}'. Available zones: {string.Join(", ", availableZones)}");
            onComplete?.Invoke(false);
            return;
        }

        if (IsZoneLoaded(zoneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Zone '{zoneName}' is already loaded");
            }
            onComplete?.Invoke(true);
            return;
        }

        if (activeZoneLoadOperations.ContainsKey(zoneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Zone '{zoneName}' is already being loaded");
            }
            onComplete?.Invoke(false);
            return;
        }

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Starting load for zone '{zoneName}'");
        }

        Coroutine loadOperation = StartCoroutine(LoadZoneAsync(zoneName, onComplete));
        activeZoneLoadOperations[zoneName] = loadOperation;
    }

    /// <summary>
    /// Unload a zone scene to free memory
    /// </summary>
    public void UnloadZone(string zoneName, Action<bool> onComplete = null)
    {
        // PROTECTION: Multiple layers of protection for essential scenes
        if (IsEssentialScene(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot unload essential scene '{zoneName}'!");
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(zoneName))
        {
            Debug.LogError("PersistentSceneManager: Cannot unload zone - zone name is null or empty");
            onComplete?.Invoke(false);
            return;
        }

        if (!IsZoneLoaded(zoneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Zone '{zoneName}' is not loaded");
            }
            onComplete?.Invoke(true);
            return;
        }

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Starting unload for zone '{zoneName}'");
        }

        StartCoroutine(UnloadZoneAsync(zoneName, onComplete));
    }

    /// <summary>
    /// Transition from current zones to a new zone (unloads other zones)
    /// </summary>
    public void TransitionToZone(string targetZone, Action<bool> onComplete = null)
    {
        // PROTECTION: Ensure target zone is not a protected scene
        if (IsProtectedFromZoneOperations(targetZone))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot transition to protected scene '{targetZone}' as a zone!");
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(targetZone))
        {
            Debug.LogError("PersistentSceneManager: Cannot transition - target zone name is null or empty");
            onComplete?.Invoke(false);
            return;
        }

        if (debugMode)
        {
            Debug.Log($"PersistentSceneManager: Transitioning to zone '{targetZone}'");
        }

        StartCoroutine(TransitionToZoneAsync(targetZone, onComplete));
    }

    private IEnumerator LoadZoneAsync(string zoneName, Action<bool> onComplete)
    {
        AsyncOperation asyncLoad = null;
        bool hasError = false;
        string errorMessage = string.Empty;

        // Initialize load operation
        try
        {
            // Mark zone as loading
            loadedZones[zoneName] = false;
            OnZoneLoadStateChanged?.Invoke(zoneName, false);

            asyncLoad = SceneManager.LoadSceneAsync(zoneName, LoadSceneMode.Additive);
        }
        catch (Exception ex)
        {
            hasError = true;
            errorMessage = ex.Message;
        }

        if (hasError)
        {
            Debug.LogError($"PersistentSceneManager: Error starting load for zone '{zoneName}': {errorMessage}");
            
            // Clean up failed load state
            if (loadedZones.ContainsKey(zoneName))
            {
                loadedZones.Remove(zoneName);
            }
            
            // Remove from active operations
            if (activeZoneLoadOperations.ContainsKey(zoneName))
            {
                activeZoneLoadOperations.Remove(zoneName);
            }
            
            onComplete?.Invoke(false);
            yield break;
        }

        // Wait for load to complete
        while (!asyncLoad.isDone)
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Loading zone '{zoneName}'... Progress: {asyncLoad.progress * 100f:F1}%");
            }
            yield return null;
        }

        // Complete load operation
        try
        {
            // Mark zone as loaded
            loadedZones[zoneName] = true;
            OnZoneLoadStateChanged?.Invoke(zoneName, true);
            OnZoneLoadCompleted?.Invoke(zoneName);

            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Successfully loaded zone '{zoneName}'");
            }

            onComplete?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"PersistentSceneManager: Error completing load for zone '{zoneName}': {ex.Message}");
            
            // Clean up failed load state
            if (loadedZones.ContainsKey(zoneName))
            {
                loadedZones.Remove(zoneName);
            }
            
            onComplete?.Invoke(false);
        }
        finally
        {
            // Remove from active operations
            if (activeZoneLoadOperations.ContainsKey(zoneName))
            {
                activeZoneLoadOperations.Remove(zoneName);
            }
        }
    }

    private IEnumerator UnloadZoneAsync(string zoneName, Action<bool> onComplete)
    {
        // FINAL PROTECTION: Double-check before unloading
        if (IsEssentialScene(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: CRITICAL PROTECTION - Blocked attempt to unload essential scene '{zoneName}'!");
            onComplete?.Invoke(false);
            yield break;
        }

        AsyncOperation asyncUnload = null;
        bool hasError = false;
        string errorMessage = string.Empty;

        // Initialize unload operation
        try
        {
            asyncUnload = SceneManager.UnloadSceneAsync(zoneName);
        }
        catch (Exception ex)
        {
            hasError = true;
            errorMessage = ex.Message;
        }

        if (hasError)
        {
            Debug.LogError($"PersistentSceneManager: Error starting unload for zone '{zoneName}': {errorMessage}");
            onComplete?.Invoke(false);
            yield break;
        }

        // Wait for unload to complete
        while (!asyncUnload.isDone)
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Unloading zone '{zoneName}'... Progress: {asyncUnload.progress * 100f:F1}%");
            }
            yield return null;
        }

        // Complete unload operation
        try
        {
            // Mark zone as unloaded
            if (loadedZones.ContainsKey(zoneName))
            {
                loadedZones.Remove(zoneName);
            }
            
            OnZoneLoadStateChanged?.Invoke(zoneName, false);
            OnZoneUnloadCompleted?.Invoke(zoneName);

            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Successfully unloaded zone '{zoneName}'");
            }

            onComplete?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"PersistentSceneManager: Error completing unload for zone '{zoneName}': {ex.Message}");
            onComplete?.Invoke(false);
        }
    }

    private IEnumerator TransitionToZoneAsync(string targetZone, Action<bool> onComplete)
    {
        bool transitionSuccess = true;
        List<string> zonesToUnload = new List<string>();

        // Get list of currently loaded zones (excluding essential scenes)
        try
        {
            foreach (var kvp in loadedZones)
            {
                if (kvp.Value && kvp.Key != targetZone && availableZones.Contains(kvp.Key) && !IsEssentialScene(kvp.Key))
                {
                    zonesToUnload.Add(kvp.Key);
                }
            }
            
            // Also unload MainMenu if it's loaded and we're transitioning to a gameplay zone
            if (IsSceneLoaded(mainMenuSceneName) && targetZone != mainMenuSceneName)
            {
                zonesToUnload.Add(mainMenuSceneName);
                if (debugMode)
                {
                    Debug.Log($"PersistentSceneManager: Adding MainMenu to unload list for transition to '{targetZone}'");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"PersistentSceneManager: Error preparing zone transition to '{targetZone}': {ex.Message}");
            onComplete?.Invoke(false);
            yield break;
        }

        // Unload current zones
        foreach (string zoneToUnload in zonesToUnload)
        {
            bool unloadComplete = false;
            bool unloadSuccess = false;
            
            UnloadZone(zoneToUnload, (success) =>
            {
                unloadSuccess = success;
                unloadComplete = true;
            });

            // Wait for unload to complete
            while (!unloadComplete)
            {
                yield return null;
            }

            if (!unloadSuccess)
            {
                Debug.LogWarning($"PersistentSceneManager: Failed to unload zone '{zoneToUnload}' during transition");
                transitionSuccess = false;
            }
        }

        // Load target zone if not already loaded
        if (!IsZoneLoaded(targetZone))
        {
            bool loadComplete = false;
            bool loadSuccess = false;
            
            LoadZone(targetZone, (success) =>
            {
                loadSuccess = success;
                loadComplete = true;
            });

            // Wait for load to complete
            while (!loadComplete)
            {
                yield return null;
            }

            if (!loadSuccess)
            {
                Debug.LogError($"PersistentSceneManager: Failed to load target zone '{targetZone}' during transition");
                transitionSuccess = false;
            }
        }

        // Complete transition
        try
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Zone transition to '{targetZone}' {(transitionSuccess ? "succeeded" : "failed")}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"PersistentSceneManager: Error completing zone transition to '{targetZone}': {ex.Message}");
            transitionSuccess = false;
        }

        onComplete?.Invoke(transitionSuccess);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Manually trigger main menu loading (useful for editor testing)
    /// </summary>
    public void ForceLoadMainMenu()
    {
        if (debugMode)
        {
            Debug.Log("PersistentSceneManager: Force loading main menu");
        }
        LoadMainMenuScene();
    }

    /// <summary>
    /// Unload a scene while keeping Persistent scene active (PROTECTED)
    /// </summary>
    public void UnloadScene(string sceneName)
    {
        // PROTECTION: Never allow unloading of truly essential scenes (only Persistent)
        if (IsEssentialScene(sceneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot unload essential scene '{sceneName}'!");
            return;
        }

        if (IsSceneLoaded(sceneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Unloading scene {sceneName}");
            }
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }

    /// <summary>
    /// Load a scene additively while keeping Persistent scene
    /// </summary>
    public void LoadSceneAdditive(string sceneName)
    {
        if (!IsSceneLoaded(sceneName))
        {
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Loading scene {sceneName} additively");
            }
            StartCoroutine(LoadSceneAsync(sceneName));
        }
    }

    /// <summary>
    /// Check if a specific zone is currently loaded
    /// </summary>
    public bool IsZoneLoaded(string zoneName)
    {
        return loadedZones.ContainsKey(zoneName) && loadedZones[zoneName];
    }

    /// <summary>
    /// Get list of currently loaded zones
    /// </summary>
    public List<string> GetLoadedZones()
    {
        List<string> loaded = new List<string>();
        foreach (var kvp in loadedZones)
        {
            if (kvp.Value)
            {
                loaded.Add(kvp.Key);
            }
        }
        return loaded;
    }

    /// <summary>
    /// Get list of available zones that can be loaded
    /// </summary>
    public HashSet<string> GetAvailableZones()
    {
        return new HashSet<string>(availableZones);
    }

    /// <summary>
    /// Add a new zone to the available zones list (PROTECTED)
    /// </summary>
    public void RegisterZone(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName))
        {
            return;
        }

        // PROTECTION: Never allow protected scenes to be registered as zones
        if (IsProtectedFromZoneOperations(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot register protected scene '{zoneName}' as a zone!");
            return;
        }

        if (!availableZones.Contains(zoneName))
        {
            availableZones.Add(zoneName);
            if (debugMode)
            {
                Debug.Log($"PersistentSceneManager: Registered new zone '{zoneName}'");
            }
        }
    }

    /// <summary>
    /// Unload MainMenu scene when transitioning to gameplay (special case)
    /// </summary>
    public void UnloadMainMenuForGameplay()
    {
        if (IsSceneLoaded(mainMenuSceneName))
        {
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Unloading MainMenu for gameplay transition");
            }
            UnloadScene(mainMenuSceneName);
        }
    }

    /// <summary>
    /// Load MainMenu scene (for returning from gameplay)
    /// </summary>
    public void LoadMainMenuForReturn()
    {
        if (!IsSceneLoaded(mainMenuSceneName))
        {
            if (debugMode)
            {
                Debug.Log("PersistentSceneManager: Loading MainMenu for return from gameplay");
            }
            LoadMainMenuScene();
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Check if a scene is essential and should never be unloaded
    /// </summary>
    private bool IsEssentialScene(string sceneName)
    {
        // Only Persistent scene is truly essential and should never be unloaded
        return sceneName == persistentSceneName;
    }

    /// <summary>
    /// Check if a scene should be protected from zone operations (but can be unloaded during transitions)
    /// </summary>
    private bool IsProtectedFromZoneOperations(string sceneName)
    {
        // Both Persistent and MainMenu are protected from being treated as zones
        return sceneName == persistentSceneName || sceneName == mainMenuSceneName;
    }

    private bool IsRunningAsServer()
    {
        // Check if running in batch mode (typical for dedicated servers)
        if (Application.isBatchMode)
        {
            return true;
        }

        // Check command line arguments for server indicators
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLower() == "-server" || args[i].ToLower() == "-batchmode" || args[i].ToLower() == "-dedicated")
            {
                return true;
            }
        }

        // Check if NetworkManager is initialized and we're server
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsServer;
        }

        return false;
    }

    private bool IsRunningAsClient()
    {
        // Check if NetworkManager is initialized and we're client
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsClient;
        }

        // If no network manager yet, assume client (will be corrected later)
        return !Application.isBatchMode;
    }

    public bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
    #endregion

    #region Unity Editor Support
    #if UNITY_EDITOR
    [ContextMenu("Debug: Force Load Main Menu")]
    private void DebugForceLoadMainMenu()
    {
        ForceLoadMainMenu();
    }

    [ContextMenu("Debug: Print Loaded Scenes")]
    private void DebugPrintLoadedScenes()
    {
        Debug.Log("=== Currently Loaded Scenes ===");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Debug.Log($"Scene {i}: {scene.name} (Active: {scene == SceneManager.GetActiveScene()})");
        }
    }

    [ContextMenu("Debug: Print Network State")]
    private void DebugPrintNetworkState()
    {
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"Network State - IsServer: {NetworkManager.Singleton.IsServer}, IsClient: {NetworkManager.Singleton.IsClient}, IsHost: {NetworkManager.Singleton.IsHost}");
        }
        else
        {
            Debug.Log("NetworkManager.Singleton is null");
        }
    }

    [ContextMenu("Debug: Print Zone States")]
    private void DebugPrintZoneStates()
    {
        Debug.Log("=== Zone States ===");
        Debug.Log($"Available Zones: {string.Join(", ", availableZones)}");
        foreach (var kvp in loadedZones)
        {
            Debug.Log($"Zone '{kvp.Key}': {(kvp.Value ? "Loaded" : "Unloaded")}");
        }
        Debug.Log($"Active Load Operations: {activeZoneLoadOperations.Count}");
    }

    [ContextMenu("Debug: Check Persistent Scene Health")]
    private void DebugCheckPersistentSceneHealth()
    {
        bool isHealthy = IsPersistentSceneHealthy();
        Debug.Log($"Persistent Scene Health: {(isHealthy ? "HEALTHY" : "CRITICAL - UNHEALTHY!")}");
        Debug.Log($"Protection Enabled: {persistentSceneProtectionEnabled}");
        Debug.Log($"Monitoring Active: {persistentSceneMonitorCoroutine != null}");
    }

    [ContextMenu("Debug: Load IthoriaSouth")]
    private void DebugLoadIthoriaSouth()
    {
        LoadZone("IthoriaSouth", (success) =>
        {
            Debug.Log($"IthoriaSouth load result: {success}");
        });
    }

    [ContextMenu("Debug: Unload All Zones")]
    private void DebugUnloadAllZones()
    {
        var loadedZonesList = GetLoadedZones();
        foreach (string zone in loadedZonesList)
        {
            UnloadZone(zone);
        }
    }

    [ContextMenu("Debug: Test Protection (Try Unload Persistent)")]
    private void DebugTestProtection()
    {
        Debug.Log("Testing protection by attempting to unload Persistent scene...");
        UnloadZone(persistentSceneName, (success) =>
        {
            Debug.Log($"Protection test result: {(success ? "FAILED - PROTECTION BROKEN!" : "PASSED - Protection working")}");
        });
    }

    [ContextMenu("Debug: Unload MainMenu")]
    private void DebugUnloadMainMenu()
    {
        UnloadMainMenuForGameplay();
    }

    [ContextMenu("Debug: Load MainMenu")]
    private void DebugLoadMainMenu()
    {
        LoadMainMenuForReturn();
    }

    [ContextMenu("Debug: Test MainMenu Unload Protection")]
    private void DebugTestMainMenuProtection()
    {
        Debug.Log("Testing MainMenu unload via regular UnloadScene...");
        UnloadScene(mainMenuSceneName);
        Debug.Log("MainMenu unload test complete - check if MainMenu was unloaded");
    }
    #endif
    #endregion
} 