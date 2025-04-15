//using Steamworks;
using UnityEngine;

public class SteamManager : MonoBehaviour
{
    //Steam app needs to be initialised for £100 before we sort this
    /*
    public static SteamManager Instance { get; private set; }
    private bool m_bInitialized = false;




    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // Initialize the Steam API
        try
        {
            // Attempt to initialize Steam API
            m_bInitialized = SteamAPI.Init();
            if (!m_bInitialized)
            {
                Debug.LogError("SteamAPI initialization failed!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Steam initialization error: {e.Message}");
        }
    }

    void Update()
    {
        // Steam requires periodic callback dispatching
        if (m_bInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        // Shutdown the Steam API when the application quits
        if (m_bInitialized)
        {
            SteamAPI.Shutdown();
        }
    }

    public ulong GetPlayerSteamID()
    {
        // Check if Steam is initialized before getting Steam ID
        if (m_bInitialized)
        {
            return SteamUser.GetSteamID().m_SteamID;
        }
        else
        {
            Debug.LogError("Steam is not initialized.");
            return 0;
        }
    }
    */
}