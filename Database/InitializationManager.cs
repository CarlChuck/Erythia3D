using UnityEngine;
using System.Threading.Tasks;

public class InitializationManager : MonoBehaviour
{
    #region Singleton
    public static InitializationManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[InitializationManager] Duplicate instance found. Destroying {gameObject.name}.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Optional: Keep the initialization manager across scene loads if needed
        // DontDestroyOnLoad(gameObject);
    }
    #endregion

    private bool isInitialized = false;
    async void Start()
    {

        Debug.Log("[InitializationManager] Starting system initialization sequence...");
        
        // --- DatabaseManager --- 
        DatabaseManager.Instance.StartInitialization();
        await DatabaseManager.Instance.InitializationTask;
        if (!DatabaseManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] DatabaseManager FAILED to initialize. Halting further initializations.");
            this.enabled = false; // Disable this manager to prevent further issues
            return;
        }

        // --- SteamManager --- 
        //TODO SteamManager

        // --- AccountManager --- 
        AccountManager.Instance.StartInitialization();
        await AccountManager.Instance.InitializationTask;
        if (!AccountManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] AccountManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }

        // --- ResourceManager --- 
        ResourceManager.Instance.StartInitialization();
        await ResourceManager.Instance.InitializationTask;
        if (!ResourceManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] ResourceManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }

        // --- ItemManager --- 
        ItemManager.Instance.StartInitialization();
        await ItemManager.Instance.InitializationTask;
        if (!ItemManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] ItemManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }

        // --- CharactersManager --- 
        CharactersManager.Instance.StartInitialization();
        await CharactersManager.Instance.InitializationTask;
        if (!CharactersManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] CharactersManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }

        // --- InventoryManager --- 
        InventoryManager.Instance.StartInitialization();
        await InventoryManager.Instance.InitializationTask;
        if (!InventoryManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] InventoryManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }

        // --- CraftingManager --- 
        CraftingManager.Instance.StartInitialization();
        await CraftingManager.Instance.InitializationTask;
        if (!CraftingManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] CraftingManager FAILED to initialize.");
            this.enabled = false;
            return;
        }

        Debug.Log("[InitializationManager] All systems initialized successfully!");
        isInitialized = true;
    }

    public bool GetIsInitialized()
    {
        return isInitialized;
    }
}
