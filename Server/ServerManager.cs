using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class ServerManager : NetworkBehaviour
{
    #region Singleton
    public static ServerManager Instance { get; private set; }    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple ServerManager instances detected. Destroying this one on GameObject: {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("Server Configuration")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Area Configuration")]
    [SerializeField] private List<AreaConfiguration> areaConfigurations = new();

    // Network components
    private NetworkManager masterNetworkManager;
    
    // Area management
    private readonly Dictionary<ulong, int> clientToAreaMapping = new();
    private readonly Dictionary<int, List<ulong>> areaToClientsMapping = new();
    private readonly Dictionary<int, bool> loadedNetworkScenes = new();

    #region Master Server Initialization and Shutdown
    public override void OnNetworkSpawn()
    {
        Debug.Log($"ServerManager.OnNetworkSpawn: IsServer={IsServer}, IsClient={IsClient}, IsOwner={IsOwner}, NetworkObjectId={NetworkObjectId}");
        base.OnNetworkSpawn(); 
        InitializeMasterServer();
    }
    public override void OnNetworkDespawn()
    {
        ShutdownMasterServer();
        base.OnNetworkDespawn();
    }
    private void InitializeMasterServer()
    {
        masterNetworkManager = NetworkManager.Singleton;
        LogDebug($"Master server initialization - NetworkManager.Singleton: {(masterNetworkManager != null ? masterNetworkManager.gameObject.name : "null")}");
        LogDebug($"Master server - IsServer: {masterNetworkManager?.IsServer}, IsClient: {masterNetworkManager?.IsClient}");
        if (masterNetworkManager == null || !masterNetworkManager.IsServer)
        {
            LogError("Master server initialization failed - NetworkManager.Singleton is null or not a server");
            return;
        }
        masterNetworkManager.OnClientConnectedCallback += OnClientConnectedToMaster;
        masterNetworkManager.OnClientDisconnectCallback += OnClientDisconnectedFromMaster;
        
        // Load all networked scenes on server startup
        LoadServerNetworkScenes();
    }
    private void ShutdownMasterServer()
    {
        if (masterNetworkManager != null && masterNetworkManager.IsServer)
        {
            masterNetworkManager.Shutdown();
        }
        LogDebug("Master server shut down");
    }
    #endregion

    #region Server Scene Management
    private async Task LoadServerNetworkScenes()
    {
        LogDebug("Loading networked scenes on server...");
        
        foreach (AreaConfiguration areaConfig in areaConfigurations)
        {
            if (areaConfig.autoLoadOnServerStart && !string.IsNullOrEmpty(areaConfig.networkedScene))
            {
                await LoadNetworkSceneOnServer(areaConfig);
            }
        }
        
        LogDebug($"Server scene loading completed. Loaded {loadedNetworkScenes.Count} networked scenes.");
    }
    private async Task<bool> LoadNetworkSceneOnServer(AreaConfiguration areaConfig)
    {
        try
        {
            LogDebug($"Loading networked scene for area '{areaConfig.areaId}': {areaConfig.networkedScene}");
            
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(areaConfig.networkedScene, LoadSceneMode.Additive);
            
            // Wait for scene to load
            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }
            
            if (asyncOperation.isDone)
            {
                loadedNetworkScenes[areaConfig.areaId] = true;
                
                // Initialize area client tracking
                if (!areaToClientsMapping.ContainsKey(areaConfig.areaId))
                {
                    areaToClientsMapping[areaConfig.areaId] = new List<ulong>();
                }
                
                LogDebug($"✅ Successfully loaded networked scene for area '{areaConfig.areaId}'");
                return true;
            }

            LogError($"❌ Failed to load networked scene for area '{areaConfig.areaId}': {areaConfig.networkedScene}");
            return false;
        }
        catch (System.Exception ex)
        {
            LogError($"❌ Exception loading networked scene for area '{areaConfig.areaId}': {ex.Message}");
            return false;
        }
    }
    public AreaConfiguration GetAreaConfiguration(int areaId)
    {
        return areaConfigurations.FirstOrDefault(config => config.areaId == areaId);
    }
    #endregion

    #region Player Management
    private void OnClientConnectedToMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} connected to master server");
    }
    private void OnClientDisconnectedFromMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected from master server");
        
        // Remove client from area tracking
        RemoveClientFromAllAreas(clientId);
    }
    public void AssignClientToArea(ulong clientId, int areaId)
    {
        // Remove from current area first
        RemoveClientFromAllAreas(clientId);
        
        // Add to new area
        if (areaToClientsMapping.ContainsKey(areaId))
        {
            areaToClientsMapping[areaId].Add(clientId);
            clientToAreaMapping[clientId] = areaId;
            
            LogDebug($"Assigned client {clientId} to area '{areaId}'");
        }
        else
        {
            LogError($"Cannot assign client {clientId} to unknown area '{areaId}'");
        }
    }
    public int? GetClientCurrentArea(ulong clientId)
    {
        return clientToAreaMapping.ContainsKey(clientId) ? clientToAreaMapping[clientId] : null;
    }
    private void RemoveClientFromAllAreas(ulong clientId)
    {
        if (!clientToAreaMapping.ContainsKey(clientId))
        {
            return;
        }

        int currentArea = clientToAreaMapping[clientId];
            
        if (areaToClientsMapping.ContainsKey(currentArea))
        {
            areaToClientsMapping[currentArea].Remove(clientId);
        }
            
        clientToAreaMapping.Remove(clientId);
        LogDebug($"Removed client {clientId} from area '{currentArea}'");
    }
    public List<ulong> GetClientsInArea(int areaId)
    {
        return areaToClientsMapping.ContainsKey(areaId) ? 
            new List<ulong>(areaToClientsMapping[areaId]) : 
            new List<ulong>();
    }
    #endregion

    #region Login Communication
    public static async Task HandleLogin(PlayerManager playerManager, ulong steamID, int accountID)
    {
        LoginResult result;
        
        if (steamID > 0)
        {
            // Handle Steam login - TODO implement
            result = new LoginResult { Success = false, ErrorMessage = "Steam login not implemented", AccountName = "" };
        }
        else if (accountID > 0)
        {
            Dictionary<string, object> account = await AccountManager.Instance.GetAccountByAccountIDAsync(accountID);
            if (account != null)
            {
                result = new LoginResult 
                { 
                    Success = true,
                    ErrorMessage = "",
                    AccountID = Convert.ToInt32(account["AccountID"]),
                    AccountName = account["Username"].ToString(),
                    SteamID = account.TryGetValue("SteamID", out object steamObj) && steamObj != DBNull.Value ? Convert.ToUInt64(steamObj) : 0
                };
            }
            else
            {
                Debug.LogError($"ServerManager: No account found for AccountID {accountID}");
                result = new LoginResult { Success = false, ErrorMessage = $"No account found for AccountID {accountID}", AccountName = "" };
            }
        }
        else
        {
            result = new LoginResult { Success = false, ErrorMessage = "No valid login method available. Both SteamID and AccountID are 0.", AccountName = "" };
        }
        playerManager.ReceiveLoginRpc(result);
    }
    #endregion

    #region Character Loading Communication
    public async Task HandleCharacterCreation(PlayerManager playerManager, string familyName, string charName, int charRace, int charGender, int charFace)
    {
        bool success = await CharactersManager.Instance.CreateNewCharacterAsync(playerManager.AccountID, familyName, charName, charRace, charGender, charFace);
        
        if (success)
        {
            Debug.Log($"ServerManager: Character '{charName}' created successfully");
            playerManager.ReceiveCharacterCreationResultRpc(true, "");
        }
        else
        {
            Debug.LogError($"ServerManager: Character creation failed for '{charName}'");
            playerManager.ReceiveCharacterCreationResultRpc(false, "Failed to create character");
        }
    }
    public async Task ProcessCharacterListRequest(PlayerManager pManager, int accountID)
    {      
        CharacterListResult result = await ProcessCharacterList(accountID);

        pManager.ReceiveCharacterListRpc(result);
    }
    private async Task<CharacterListResult> ProcessCharacterList(int accountID)
    {
        try
        {
            List<Dictionary<string, object>> characterDictionaries = await CharactersManager.Instance.GetCharactersByAccountIDAsync(accountID);
            CharacterData[] characters = new CharacterData[characterDictionaries.Count];
            
            for (int i = 0; i < characterDictionaries.Count; i++)
            {
                var charDict = characterDictionaries[i];
                characters[i] = ConvertDictionaryToCharacterData(charDict);
            }
            return new CharacterListResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Characters = characters 
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterList: {ex.Message}\n{ex.StackTrace}");
            return new CharacterListResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading characters: {ex.Message}", 
                Characters = Array.Empty<CharacterData>() 
            };
        }
    }
    private CharacterData ConvertDictionaryToCharacterData(Dictionary<string, object> charDict)
    {
        CharacterData charData = new CharacterData();
        
        // Helper method to safely convert values with defaults
        charData.CharID = GetIntValue(charDict, "CharID", 0);
        charData.AccountID = GetIntValue(charDict, "AccountID", 0);
        charData.FamilyName = GetStringValue(charDict, "FamilyName", "");
        charData.Name = GetStringValue(charDict, "Name", "");
        charData.Title = GetStringValue(charDict, "Title", "");
        charData.ZoneID = GetIntValue(charDict, "ZoneID", 1);
        charData.XLoc = GetIntValue(charDict, "XLoc", 0);
        charData.YLoc = GetIntValue(charDict, "YLoc", 0);
        charData.ZLoc = GetIntValue(charDict, "ZLoc", 0);
        charData.Race = GetIntValue(charDict, "Race", 1);
        charData.Gender = GetIntValue(charDict, "Gender", 1);
        charData.Face = GetIntValue(charDict, "Face", 1);
        charData.CombatExp = GetIntValue(charDict, "CombatExp", 0);
        charData.CraftingExp = GetIntValue(charDict, "CraftingExp", 0);
        charData.ArcaneExp = GetIntValue(charDict, "ArcaneExp", 0);
        charData.SpiritExp = GetIntValue(charDict, "SpiritExp", 0);
        charData.VeilExp = GetIntValue(charDict, "VeilExp", 0);
        
        // Populate species stats from CharactersManager
        SpeciesTemplate species = CharactersManager.Instance.GetSpeciesByID(charData.Race);
        charData.SpeciesStrength = species.strength;
        charData.SpeciesDexterity = species.dexterity;
        charData.SpeciesConstitution = species.constitution;
        charData.SpeciesIntelligence = species.intelligence;
        charData.SpeciesSpirit = species.spirit;
        
        return charData;
    }
    #endregion

    #region Inventory Loading Communication
    public async Task ProcessCharacterInventoryRequest(PlayerManager pManager, int characterID)
    {  
        CharacterInventoryResult result;
        try
        {
            result = await ProcessCharacterInventory(characterID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during character inventory request: {ex.Message}\n{ex.StackTrace}");
            result = new CharacterInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during character inventory request: {ex.Message}",
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>()
            };
        }
        pManager.ReceiveCharacterInventoryRpc(result, characterID);
    }
    public async Task ProcessAccountInventoryRequest(PlayerManager pManager, int accountID)
    {
        AccountInventoryResult result;
        try
        {
            result = await ProcessAccountInventory(accountID);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during account inventory request: {ex.Message}\n{ex.StackTrace}");
            result = new AccountInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during account inventory request: {ex.Message}",
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>(),
                Workbenches = Array.Empty<WorkbenchData>()
            };
        }
        pManager.ReceiveAccountInventoryRpc(result);
    }
    private async Task<CharacterInventoryResult> ProcessCharacterInventory(int characterID)
    {
        try
        {
            // Load all character inventory data (association tables)
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetCharacterInventoryItemsAsync(characterID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetCharacterInventoryResourceItemsAsync(characterID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetCharacterInventorySubComponentsAsync(characterID);

            // Retrieve actual items from ItemManager
            List<ItemData> itemDataList = new List<ItemData>();
            foreach (Dictionary<string, object> itemDict in itemDictionaries)
            {
                int itemID = GetIntValue(itemDict, "ItemID", 0);
                int slotID = GetIntValue(itemDict, "SlotID", 0);

                Item item = ItemManager.Instance.GetItemInstanceByID(itemID);
                if (item != null)
                {
                    ItemData itemData = ConvertToItemData(item, slotID);
                    itemDataList.Add(itemData);
                }
            }

            // Retrieve actual resources from ResourceManager
            List<ResourceItemData> resourceDataList = new List<ResourceItemData>();
            foreach (var resourceDict in resourceItemDictionaries)
            {
                int resourceID = GetIntValue(resourceDict, "ResourceID", 0);
                int quantity = GetIntValue(resourceDict, "Quantity", 1);
                
                Resource resource = ResourceManager.Instance.GetResourceById(resourceID);
                if (resource != null)
                {
                    ResourceItemData resourceItemData = ConvertToResourceData(resource, resourceID, quantity);
                    resourceDataList.Add(resourceItemData);
                }
            }

            // Retrieve actual subcomponents from ItemManager
            List<SubComponentData> subComponentDataList = new List<SubComponentData>();
            foreach (var subCompDict in subComponentDictionaries)
            {
                int subComponentID = GetIntValue(subCompDict, "SubComponentID", 0);
                
                SubComponent subComponent = ItemManager.Instance.GetSubComponentInstanceByID(subComponentID);
                if (subComponent != null)
                {
                    SubComponentData subComponentData = ConvertToSubComponentData(subComponent);
                    subComponentDataList.Add(subComponentData);
                }
            }
            
            return new CharacterInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = itemDataList.ToArray(),
                ResourceItems = resourceDataList.ToArray(),
                SubComponents = subComponentDataList.ToArray()
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterInventory: {ex.Message}\n{ex.StackTrace}");
            return new CharacterInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading character inventory: {ex.Message}", 
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>()
            };
        }
    }
    private async Task<AccountInventoryResult> ProcessAccountInventory(int accountID)
    {
        try
        {
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetAccountInventoryItemsAsync(accountID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetAccountInventoryResourceItemsAsync(accountID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetAccountInventorySubComponentsAsync(accountID);
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);
            
            // Retrieve actual items from ItemManager
            List<ItemData> itemDataList = new List<ItemData>();
            foreach (Dictionary<string, object> itemDict in itemDictionaries)
            {
                int itemID = GetIntValue(itemDict, "ItemID", 0);
                
                Item item = ItemManager.Instance.GetItemInstanceByID(itemID);
                if (item != null)
                {
                    ItemData itemData = ConvertToItemData(item, 0);
                    itemDataList.Add(itemData);
                }
            }

            // Retrieve actual resources from ResourceManager
            List<ResourceItemData> resourceDataList = new List<ResourceItemData>();
            foreach (var resourceDict in resourceItemDictionaries)
            {
                int resourceID = GetIntValue(resourceDict, "ResourceID", 0);
                int quantity = GetIntValue(resourceDict, "Quantity", 1);
                
                Resource resource = ResourceManager.Instance.GetResourceById(resourceID);
                if (resource != null)
                {
                    ResourceItemData resourceItemData = ConvertToResourceData(resource, resourceID, quantity);
                    resourceDataList.Add(resourceItemData);
                }
            }

            // Retrieve actual subcomponents from ItemManager
            List<SubComponentData> subComponentDataList = new List<SubComponentData>();
            foreach (var subCompDict in subComponentDictionaries)
            {
                int subComponentID = GetIntValue(subCompDict, "SubComponentID", 0);
                
                SubComponent subComponent = ItemManager.Instance.GetSubComponentInstanceByID(subComponentID);
                if (subComponent != null)
                {
                    SubComponentData subComponentData = ConvertToSubComponentData(subComponent);
                    subComponentDataList.Add(subComponentData);
                }
            }
            
            return new AccountInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = itemDataList.ToArray(),
                ResourceItems = resourceDataList.ToArray(),
                SubComponents = subComponentDataList.ToArray(),
                Workbenches = workbenches
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessAccountInventory: {ex.Message}\n{ex.StackTrace}");
            return new AccountInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading account inventory: {ex.Message}", 
                Items = Array.Empty<ItemData>(),
                ResourceItems = Array.Empty<ResourceItemData>(),
                SubComponents = Array.Empty<SubComponentData>(),
                Workbenches = Array.Empty<WorkbenchData>()
            };
        }
    }
    #endregion

    #region Helper Methods
    private static int GetIntValue(Dictionary<string, object> dict, string key, int defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }
    private static string GetStringValue(Dictionary<string, object> dict, string key, string defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
    private float GetFloatValue(Dictionary<string, object> dict, string key, float defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToSingle(value);
        }
        return defaultValue;
    }
    private bool GetBoolValue(Dictionary<string, object> dict, string key, bool defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToBoolean(value);
        }
        return defaultValue;
    }
    private DateTime GetDateTimeValue(Dictionary<string, object> dict, string key, DateTime defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToDateTime(value);
        }
        return defaultValue;
    }
    private static ItemData ConvertToItemData(Item item, int slotID)
    {
        ItemData itemData = new ItemData
        {
            ItemID = item.ItemID,
            ItemTemplateID = item.ItemTemplateID,
            ItemName = item.ItemName,
            ItemType = (int)item.Type,
            Durability = item.Durability,
            MaxDurability = item.MaxDurability,
            Damage = item.Damage,
            Speed = item.Speed,
            DamageType = (int)item.WeaponType,
            SlotType = (int)item.Slot,
            SlashResist = item.SlashResist,
            ThrustResist = item.ThrustResist,
            CrushResist = item.CrushResist,
            HeatResist = item.HeatResist,
            ShockResist = item.ShockResist,
            ColdResist = item.ColdResist,
            MindResist = item.MindResist,
            CorruptResist = item.CorruptResist,
            Icon = item.Icon,
            Colour = item.ColourHex,
            Weight = item.Weight,
            Model = item.Model,
            Stackable = item.IsStackable,
            StackSizeMax = item.StackSizeMax,
            Price = item.Price,
            SlotID = slotID
        };
        
        return itemData;
    }
    private static ResourceItemData ConvertToResourceData(Resource resource, int resourceID, int quantity)
    {
        ResourceData resourceData = new ResourceData
        {
            ResourceSpawnID = resourceID,
            ResourceName = resource.ResourceName,
            ResourceTemplateID = resource.ResourceTemplateID,
            Type = (int)resource.Type,
            SubType = (int)resource.SubType,
            Order = (int)resource.Order,
            Family = (int)resource.Family,
            Quality = resource.Quality,
            Toughness = resource.Toughness,
            Strength = resource.Strength,
            Density = resource.Density,
            Aura = resource.Aura,
            Energy = resource.Energy,
            Protein = resource.Protein,
            Carbohydrate = resource.Carbohydrate,
            Flavour = resource.Flavour,
            Weight = resource.Weight,
            Value = resource.Value,
            StartDate = resource.StartDate,
            EndDate = resource.EndDate
        };

        ResourceItemData resourceItemData = new ResourceItemData
        {
            ResourceSpawnID = resourceID,
            CurrentStackSize = quantity,
            StackSizeMax = 1000, // Default or get from template
            Weight = (float)(quantity * resource.Weight) / 100,
            ResourceData = resourceData
        };
        return resourceItemData;
    }
    private static SubComponentData ConvertToSubComponentData(SubComponent subComponent)
    {
        SubComponentData subComponentData = new SubComponentData
        {
            SubComponentID = subComponent.SubComponentID,
            Name = subComponent.Name,
            SubComponentTemplateID = subComponent.SubComponentTemplateID,
            ComponentType = subComponent.ComponentType,
            Quality = subComponent.Quality,
            Toughness = subComponent.Toughness,
            Strength = subComponent.Strength,
            Density = subComponent.Density,
            Aura = subComponent.Aura,
            Energy = subComponent.Energy,
            Protein = subComponent.Protein,
            Carbohydrate = subComponent.Carbohydrate,
            Flavour = subComponent.Flavour
        };
        return subComponentData;
    }
    private static WorkbenchData[] ConvertToWorkbenchData(List<Dictionary<string, object>> dictionaries)
    {
        WorkbenchData[] items = new WorkbenchData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            Dictionary<string, object> dict = dictionaries[i];
            items[i] = new WorkbenchData
            {
                WorkBenchType = GetIntValue(dict, "WorkBenchType", 1)
            };
        }
        return items;
    }
    #endregion

    #region Utility Methods
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MasterServer] {message}");
        }
    }
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MasterServer] {message}");
    }
    private void LogError(string message)
    {
        Debug.LogError($"[MasterServer] {message}");
    }
    #endregion
}

#region Area Management Data Structures

[System.Serializable]
public class AreaConfiguration
{
    [Header("Area Identification")]
    public int areaId;
    public string areaName;
    
    [Header("Scene Configuration")]
    [Tooltip("Environment scene with terrain, buildings, graphics (loaded on clients)")]
    public string environmentScene;
    
    [Tooltip("Networked scene with NetworkObjects, NPCs, interactive items (server-only)")]
    public string networkedScene;
    
    [Header("Spawn Configuration")]
    public Vector3 defaultSpawnPosition = Vector3.zero;
    public List<AreaSpawnPoint> spawnPoints = new();
    
    [Header("Area Settings")]
    public int maxPlayers = 50;
    public bool autoLoadOnServerStart = true;
}

[System.Serializable]
public struct AreaSpawnPoint
{
    public string spawnPointName;
    public Vector3 position;
    public Vector3 rotation;
    public bool isDefault;
}

[System.Serializable]
public struct AreaTransitionInfo
{
    public int fromAreaId;
    public int toAreaId;
    public string waypointName;
    public Vector3 targetPosition;
}
#endregion
