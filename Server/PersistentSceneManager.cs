using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class PersistentSceneManager : NetworkBehaviour
{
    #region Constants
    private const string mainMenuSceneName = "MainMenu";
    private const string persistentSceneName = "Persistent";
    #endregion

    #region Private Fields    
    public static PersistentSceneManager Instance;
    private bool hasInitialized = false;
    [SerializeField] private float sceneLoadDelay = 0.5f; // Small delay to ensure network initialization
    private bool isServer = false;
    private bool isClient = false;

    // Zone Management Fields
    private Dictionary<string, bool> loadedZones = new Dictionary<string, bool>();
    private Dictionary<string, Coroutine> activeZoneLoadOperations = new Dictionary<string, Coroutine>();
    private HashSet<string> availableZones = new HashSet<string>(); // Known zone scenes    
    #endregion

    #region Events
    public static event Action<string, bool> OnZoneLoadStateChanged; // zoneName, isLoaded
    public static event Action<string> OnZoneLoadCompleted; // zoneName
    public static event Action<string> OnZoneUnloadCompleted; // zoneName
    //public static event Action OnPersistentSceneProtectionTriggered; // Emergency protection activated
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAvailableZones();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        if (Instance == this && !hasInitialized)
        {
            StartCoroutine(InitializeSceneManagement());
        }
    }
    #endregion

    #region Initialization
    private void InitializeAvailableZones()
    {
        availableZones.Add("IthoriaSouth");
        // availableZones.Add("Aelystian");
        // availableZones.Add("Qadian");
        // etc.
    }
    private IEnumerator InitializeSceneManagement()
    {
        hasInitialized = true;

        // Wait a frame to ensure all systems are initialized
        yield return new WaitForEndOfFrame();
        
        // Small delay to ensure network components are ready
        yield return new WaitForSeconds(sceneLoadDelay);

        // Ensure Persistent scene is set as active and won't unload
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(persistentSceneName));

        // Determine role and load appropriate scenes
        isServer = IsRunningAsServer();
        isClient = IsRunningAsClient();
        LoadScene(mainMenuSceneName);
    }
    #endregion

    #region Scene Loading
    private void LoadScene(string sceneName)
    {
        if (IsSceneLoaded(sceneName))
        {
            return;
        }
        StartCoroutine(LoadSceneAsync(sceneName));
    }
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
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
    public void LoadZone(string zoneName, Action<bool> onComplete = null)
    {
        if (!availableZones.Contains(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: Unknown zone '{zoneName}'. Available zones: {string.Join(", ", availableZones)}");
            onComplete?.Invoke(false);
            return;
        }

        if (IsZoneLoaded(zoneName))
        {
            onComplete?.Invoke(true);
            return;
        }

        if (activeZoneLoadOperations.ContainsKey(zoneName))
        {
            onComplete?.Invoke(false);
            return;
        }

        Coroutine loadOperation = StartCoroutine(LoadZoneAsync(zoneName, onComplete));
        activeZoneLoadOperations[zoneName] = loadOperation;
    }
    public void UnloadZone(string zoneName, Action<bool> onComplete = null)
    {
        if (IsEssentialScene(zoneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot unload essential scene '{zoneName}'!");
            onComplete?.Invoke(false);
            return;
        }

        if (!IsZoneLoaded(zoneName))
        {
            onComplete?.Invoke(true);
            return;
        }

        StartCoroutine(UnloadZoneAsync(zoneName, onComplete));
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
            yield return null;
        }

        // Complete load operation
        try
        {
            // Mark zone as loaded
            loadedZones[zoneName] = true;
            OnZoneLoadStateChanged?.Invoke(zoneName, true);
            OnZoneLoadCompleted?.Invoke(zoneName);

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

            onComplete?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"PersistentSceneManager: Error completing unload for zone '{zoneName}': {ex.Message}");
            onComplete?.Invoke(false);
        }
    }
    #endregion

    #region Public Methods
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
            SceneManager.UnloadSceneAsync(sceneName);
        }
    }
    public void LoadSceneAdditive(string sceneName)
    {
        if (!IsSceneLoaded(sceneName))
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }
    }
    public bool IsZoneLoaded(string zoneName)
    {
        return loadedZones.ContainsKey(zoneName) && loadedZones[zoneName];
    }
    #endregion

    #region Helper Methods
    private bool IsEssentialScene(string sceneName)
    {
        // Only Persistent scene is truly essential and should never be unloaded
        return sceneName == persistentSceneName;
    }
    private bool IsRunningAsServer()
    {
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsServer;
        }
        else
        {
            Debug.LogWarning("PersistentSceneManager: NetworkManager is null");
            return false;
        }
    }
    private bool IsRunningAsClient()
    {
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsClient;
        }
        else
        {
            Debug.LogWarning("PersistentSceneManager: NetworkManager is null");
            return false;
        }
    }
    public bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
    #endregion
} 