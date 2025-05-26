using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;

public class PersistentSceneManager : MonoBehaviour
{
    #region Constants
    private const string mainMenuSceneName = "MainMenu";
    private const string persistentSceneName = "Persistent";
    #endregion

    #region Private Fields
    [SerializeField] private bool debugMode = true;
    [SerializeField] private float sceneLoadDelay = 0.5f; // Small delay to ensure network initialization
    
    private static PersistentSceneManager instance;
    private bool hasInitialized = false;
    #endregion

    #region Public Properties
    public static PersistentSceneManager Instance => instance;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        // Singleton pattern - ensure only one PersistentSceneManager exists
        if (instance == null)
        {
            instance = this;
            
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
    #endregion

    #region Initialization
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
    /// Unload a scene while keeping Persistent scene active
    /// </summary>
    public void UnloadScene(string sceneName)
    {
        if (sceneName == persistentSceneName)
        {
            Debug.LogWarning("PersistentSceneManager: Cannot unload Persistent scene!");
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
    #endregion

    #region Helper Methods
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

    private bool IsSceneLoaded(string sceneName)
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
    #endif
    #endregion
} 