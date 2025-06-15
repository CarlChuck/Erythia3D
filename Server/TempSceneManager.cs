using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System;

public class TempSceneManager : NetworkBehaviour
{
    //*
    #region Constants
    private const string mainMenuSceneName = "MainMenu";
    private const string persistentSceneName = "Persistent";
    #endregion

    #region Private Fields    
    public static TempSceneManager Instance;
    private bool hasInitialized = false;
    [SerializeField] private float sceneLoadDelay = 0.5f; // Small delay to ensure network initialization
    private bool isServer = false;
    private bool isClient = false;

    // Zone Management
    private HashSet<string> availableZones = new HashSet<string>(); // Known zone scenes 
    private Dictionary<string, HashSet<ulong>> sceneClients = new Dictionary<string, HashSet<ulong>>();
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
        // availableZones.Add("IthoriaNorth");
        // availableZones.Add("GreenSideMountainsSouth");
        // availableZones.Add("GreenSideMountainsNorth");
        // availableZones.Add("TheCrucible");
        // availableZones.Add("ForestOfRhythia");
        // availableZones.Add("ForestOfTheodylos");
        // availableZones.Add("TheodylosEstuary");
        // availableZones.Add("");
        // availableZones.Add("");
        // availableZones.Add("");
        // availableZones.Add("");
        // etc.
    }
    private IEnumerator InitializeSceneManagement()
    {
        hasInitialized = true;

        // Wait a frame to ensure all systems are initialized and a small delay to ensure network components are read
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(sceneLoadDelay);

        // Ensure Persistent scene is set as active
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(persistentSceneName));

        // Determine role and load appropriate scenes
        isServer = IsRunningAsServer();
        isClient = IsRunningAsClient();
        if (isServer && isClient)
        {
            //This should never happen, but it is the one check to make sure no one is running as a host
            Debug.LogError("PersistentSceneManager: Running as both server and client");
        }

        if (isServer)
        {
            Debug.Log("Server detected. Pre-loading all available zones...");
            foreach (var zoneName in availableZones)
            {
                // Server loads all possible gameplay scenes at startup
                StartCoroutine(LoadSceneAsync(zoneName));
            }
        }

        // The MainMenu is a local, non-networked scene loaded additively.
        //LoadNetworkedSceneForClient(mainMenuSceneName, isNetworked: false);
    }
    #endregion

    #region Scene Management Local Scenes
    public void LoadScene(string sceneName)
    {
        if (!isServer)
        {
            Debug.LogError("LoadScene can only be called on the server.");
            return;
        }
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }    
    public void UnloadScene(Scene scene)
    {
        if (IsEssentialScene(scene.name))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot unload essential scene '{scene.name}'!");
            return;
        }
        SceneManager.UnloadSceneAsync(scene);
    }
    #endregion

    #region Scene Management Networked Scenes
    public void LoadNetworkedSceneForClient(string sceneName, ulong clientId)
    {
        if (!isServer)
        {
            Debug.LogError("LoadNetworkedSceneForClient can only be called on the server.");
            return;
        }

        // Initialize tracking for this scene if needed
        if (!sceneClients.ContainsKey(sceneName))
        {
            sceneClients[sceneName] = new HashSet<ulong>();
        }

        // Check if client is already in this scene
        if (sceneClients[sceneName].Contains(clientId))
        {
            Debug.LogWarning($"Client {clientId} is already in scene {sceneName}");
            return;
        }

        // Add client to tracking
        sceneClients[sceneName].Add(clientId);

        // Server already has the scene loaded, so just tell the client to load it.
        LoadSceneForClientRpc(sceneName, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        });
    }    
    public void UnloadNetworkedSceneForClient(string sceneName, ulong clientId)
    {
        if (IsEssentialScene(sceneName))
        {
            Debug.LogError($"PersistentSceneManager: REJECTED - Cannot unload essential scene '{sceneName}'!");
            return;
        }

        if (!isServer)
        {
            Debug.LogError("UnloadNetworkedSceneForClient can only be called on the server.");
            return;
        }

        // Check if we're tracking this scene and if the client is in it
        if (!sceneClients.ContainsKey(sceneName) || !sceneClients[sceneName].Contains(clientId))
        {
            Debug.LogWarning($"Client {clientId} is not in scene {sceneName} to unload.");
            return;
        }

        // Remove client from tracking
        sceneClients[sceneName].Remove(clientId);

        // Tell the specific client to unload the scene.
        UnloadSceneForClientRpc(sceneName, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        });
    }
    public void LoadNetworkedSceneForServer(string sceneName)
    {
        if (!IsServer)
        {
            Debug.LogError("LoadNetworkedSceneForServer can only be called on the server");
            return;
        }

        // Load the scene additively on server without synchronizing to clients
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }
    public void UnloadNetworkedSceneFromServer(string sceneName)
    {
        if (!IsServer)
        {
            Debug.LogError("UnloadNetworkedSceneFromServer can only be called on the server");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("UnloadNetworkedSceneFromServer: Scene name cannot be null or empty");
            return;
        }

        // First unload from all clients in the scene
        /*
        if (sceneClients.ContainsKey(sceneName))
        {
            var clientsInScene = sceneClients[sceneName].ToArray();
            foreach (var clientId in clientsInScene)
            {
                //UnloadNetworkedSceneFromClient(clientId, sceneName);
            }
        }*/

        // Then unload from server using NetworkManager
        NetworkManager.SceneManager.UnloadScene(SceneManager.GetSceneByName(sceneName));
    }
    #endregion

    #region Scene Management Coroutines

    /// Loads a scene locally using Unity's SceneManager (for UI, menus, etc.)
    private IEnumerator LoadSceneAsync(string sceneName, Action onComplete = null)
    {
        if (IsSceneLoaded(sceneName)) 
        {
            onComplete?.Invoke();
            yield break;
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
  
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }

    /// Loads a scene using NetworkManager for proper NetworkObject synchronization
    private void LoadNetworkedSceneAsync(string sceneName, Action onComplete = null)
    {
        if (!isServer)
        {
            Debug.LogError("LoadNetworkedSceneAsync can only be called on the server.");
            return;
        }

        if (IsSceneLoaded(sceneName))
        {
            onComplete?.Invoke();
            return;
        }

        var sceneEventProgress = NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        /*
        if (sceneEventProgress == SceneEventProgressStatus.Started)
        {
            sceneEventProgress.OnLoadComplete += (clientId, scene, loadSceneMode) => {
                onComplete?.Invoke();
            };
        }
        else
        {
            Debug.LogError($"Failed to start loading networked scene: {sceneName}, status: {sceneEventProgress}");
            onComplete?.Invoke();
        }*/
    }

    private IEnumerator UnloadSceneAsync(string sceneName, Action onComplete = null)
    {
        if (!IsSceneLoaded(sceneName))
        {
            onComplete?.Invoke();
            yield break;
        }

        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
        while (!asyncUnload.isDone)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }

    /// Unloads a networked scene using NetworkManager
    private void UnloadNetworkedSceneAsync(string sceneName, Action onComplete = null)
    {
        if (!isServer)
        {
            Debug.LogError("UnloadNetworkedSceneAsync can only be called on the server.");
            return;
        }

        if (!IsSceneLoaded(sceneName))
        {
            onComplete?.Invoke();
            return;
        }

        var scene = SceneManager.GetSceneByName(sceneName);
        var sceneEventProgress = NetworkManager.Singleton.SceneManager.UnloadScene(scene);
        /*
        if (sceneEventProgress == SceneEventProgressStatus.Started)
        {
            sceneEventProgress.OnUnloadComplete += (unloadedSceneName) => {
                onComplete?.Invoke();
            };
        }
        else
        {
            Debug.LogError($"Failed to start unloading networked scene: {sceneName}, status: {sceneEventProgress}");
            onComplete?.Invoke();
        }*/
    }
    #endregion

    #region Networked RPCs
    [ClientRpc]
    private void LoadSceneForClientRpc(string sceneName, ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    [ClientRpc]
    private void UnloadSceneForClientRpc(string sceneName, ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(UnloadSceneAsync(sceneName));
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
        return Application.isBatchMode;
    }
    private bool IsRunningAsClient()
    {
        if (NetworkManager.Singleton != null)
        {
            return NetworkManager.Singleton.IsClient;
        }
        return !Application.isBatchMode;
    }
    public bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
    #endregion    
    //*/
} 