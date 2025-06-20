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

    [Header("Master Server Configuration")]
    [SerializeField] private ushort masterServerPort = 8888;
    [SerializeField] private int maxConnectionsPerArea = 50;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Load Balancing")]
    [SerializeField] private float loadBalanceThreshold = 0.8f;
    [SerializeField] private int healthCheckInterval = 10;

    // Network components
    private NetworkManager masterNetworkManager;
    private float lastHealthCheck;

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

    #region Player Management
    private void OnClientConnectedToMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} connected to master server");
    }
    private void OnClientDisconnectedFromMaster(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected from master server");
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


    #region Waypoint and Area Communication
    private void LoadSceneToClient(string sceneName)
    {

    }
    public async Task ProcessWaypointRequest(int characterID, string waypointName, ulong senderClientId)
    {
    }
    public async Task ProcessPlayerZoneInfoRequest(int characterID, ulong senderClientId)
    {
    }
    public Vector3 GetWaypointByZoneID(int zoneID)
    {

        return Vector3.zero;
    }
    public Vector3 GetSpawnPositionForCharacter(int characterID)
    {

        return Vector3.zero;
    }
    #endregion


    #region Helper Methods
    private int GetIntValue(Dictionary<string, object> dict, string key, int defaultValue)
    {
        if (dict.TryGetValue(key, out object value) && value != DBNull.Value)
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }
    private string GetStringValue(Dictionary<string, object> dict, string key, string defaultValue)
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
    private WorkbenchData[] ConvertToWorkbenchData(List<Dictionary<string, object>> dictionaries)
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

#region Additional Data Structures

[System.Serializable]
public struct AreaWaypoint
{
    public string waypointName;
    public Vector3 position;
    public int destinationAreaId;
    public string destinationWaypointName;
    public WaypointType waypointType;
    public WaypointRequirements requirements;
    public bool isActive;
    
    [Tooltip("Description shown to players")]
    public string description;
    
    [Tooltip("Minimum player level required")]
    public int minLevel;
    
    [Tooltip("Maximum simultaneous transfers allowed")]
    public int maxConcurrentTransfers;
}

[System.Serializable]
public enum WaypointType
{
    Portal,
    Teleporter,
    ZoneBoundary,
    Transport,
    QuestGated
}

[System.Serializable]
public struct WaypointRequirements: INetworkSerializable
{
    public bool requiresFlag;
    public int requiredFlagId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref requiresFlag);
        serializer.SerializeValue(ref requiredFlagId);
    }
}

[System.Serializable]
public class AreaServerTemplate
{
    public int areaId;
    public string sceneName;
    public ushort startingPort;
    public int maxPlayers = 50;
    public AreaWaypoint[] waypoints;
    public bool autoStartOnLaunch = true;
    
[Tooltip("Executable path for standalone server builds")]
    public string serverExecutablePath;
    [Tooltip("Additional command line arguments")]
    public string additionalArgs = "";
}

[System.Serializable]
public struct AreaInfo : INetworkSerializable
{
    public int areaId;
    public string sceneName;
    public int currentPlayers;
    public int maxPlayers;
    public string address;
    public ushort port;
    public float loadPercentage
    {
        get
        {
            return maxPlayers > 0 ? (float)currentPlayers / maxPlayers : 0f; 
            
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref areaId);
        serializer.SerializeValue(ref sceneName);
        serializer.SerializeValue(ref currentPlayers);
        serializer.SerializeValue(ref maxPlayers);
        serializer.SerializeValue(ref address);
        serializer.SerializeValue(ref port);
    }
}

[System.Serializable]
public struct PlayerTransferData
{
    public ulong clientId;
    public int characterId;
    public Vector3 currentPosition;
    public Vector3 targetPosition;
    public string sourceWaypointName;
    public string targetWaypointName;
    public DateTime transferInitiated;
    public TransferState state;
}

[System.Serializable]
public enum TransferState
{
    Pending,
    ValidatingRequirements,
    SavingPlayerState,
    InitiatingTransfer,
    TransferInProgress,
    LoadingTargetArea,
    TransferComplete,
    TransferFailed
}

[System.Serializable]
public struct AreaConnectivity
{
    public int areaId;
    public List<int> connectedAreaIds;
    public Dictionary<string, AreaWaypoint> outgoingWaypoints;
    public Dictionary<string, Vector3> incomingSpawnPoints;
    public bool allowsDirectTransfers;
    public float transferCooldownSeconds;
}

[System.Serializable]
public struct WaypointValidationResult
{
    public bool isValid;
    public string errorMessage;
    public bool requirementsMet;
    public List<string> missingRequirements;
    public bool destinationAvailable;
    public int estimatedTransferTime;
}

[System.Serializable]
public struct WaypointInfo : INetworkSerializable
{
    public string name;
    public string description;
    public Vector3 position;
    public int destinationAreaId;
    public WaypointType waypointType;
    public bool isActive;
    public WaypointRequirements requirements;
    public int estimatedTransferTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref name);        
        serializer.SerializeValue(ref description);       
        serializer.SerializeValue(ref position);       
        serializer.SerializeValue(ref destinationAreaId);       
        serializer.SerializeValue(ref waypointType);       
        serializer.SerializeValue(ref isActive);       
        serializer.SerializeValue(ref requirements);       
        serializer.SerializeValue(ref estimatedTransferTime);
    }
}
#endregion