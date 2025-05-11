using UnityEngine;
using System.Threading.Tasks;

public class InitializationManager : MonoBehaviour
{
    async void Start()
    {

        Debug.Log("[InitializationManager] Starting system initialization sequence...");
        
        // --- DatabaseManager --- 
        Debug.Log("[InitializationManager] Initializing DatabaseManager...");
        DatabaseManager.Instance.StartInitialization();
        await DatabaseManager.Instance.InitializationTask;
        if (!DatabaseManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] DatabaseManager FAILED to initialize. Halting further initializations.");
            this.enabled = false; // Disable this manager to prevent further issues
            return;
        }
        Debug.Log("[InitializationManager] DatabaseManager initialized successfully.");

        // --- SteamManager --- 
        //TODO SteamManager

        // --- AccountManager --- 
        Debug.Log("[InitializationManager] Initializing AccountManager...");
        AccountManager.Instance.StartInitialization();
        await AccountManager.Instance.InitializationTask;
        if (!AccountManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] AccountManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] AccountManager initialized successfully.");

        // --- ResourceManager --- 
        Debug.Log("[InitializationManager] Initializing ResourceManager...");
        ResourceManager.Instance.StartInitialization();
        await ResourceManager.Instance.InitializationTask;
        if (!ResourceManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] ResourceManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] ResourceManager initialized successfully.");

        // --- ItemManager --- 
        Debug.Log("[InitializationManager] Initializing ItemManager...");
        ItemManager.Instance.StartInitialization();
        await ItemManager.Instance.InitializationTask;
        if (!ItemManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] ItemManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] ItemManager initialized successfully.");

        // --- CharactersManager --- 
        Debug.Log("[InitializationManager] Initializing CharactersManager...");
        CharactersManager.Instance.StartInitialization();
        await CharactersManager.Instance.InitializationTask;
        if (!CharactersManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] CharactersManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] CharactersManager initialized successfully.");

        // --- InventoryManager --- 
        Debug.Log("[InitializationManager] Initializing InventoryManager...");
        InventoryManager.Instance.StartInitialization();
        await InventoryManager.Instance.InitializationTask;
        if (!InventoryManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] InventoryManager FAILED to initialize. Halting further initializations.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] InventoryManager initialized successfully.");

        // --- CraftingManager --- 
        Debug.Log("[InitializationManager] Initializing CraftingManager...");
        CraftingManager.Instance.StartInitialization();
        await CraftingManager.Instance.InitializationTask;
        if (!CraftingManager.Instance.GetIsInitialized())
        {
            Debug.LogError("[InitializationManager] CraftingManager FAILED to initialize.");
            this.enabled = false;
            return;
        }
        Debug.Log("[InitializationManager] CraftingManager initialized successfully.");

        Debug.Log("[InitializationManager] All systems initialized successfully!");
    }
}
