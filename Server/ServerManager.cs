using Unity.Netcode;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
    }

    #region Login Communication RPCs
    [ServerRpc(RequireOwnership = false)]
    public void RequestLoginServerRpc(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ServerRpcParams serverRpcParams = default)
    {
        //Debug.Log($"ServerManager: RequestLoginServerRpc ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={serverRpcParams.Receive.SenderClientId}");
        ProcessLoginRequest(steamID, accountID, accountName, email, ipAddress, language, serverRpcParams.Receive.SenderClientId);
    }

    // Regular method for server-to-server communication (called by PlayerManager on server)
    public async void ProcessLoginRequest(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessLoginRequest ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={senderClientId}");        
        try
        {
            LoginResult result = await ProcessLogin(steamID, accountID, accountName, email, ipAddress, language);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                // Find the PlayerManager that belongs to the client who sent the request
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveLoginResultClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during login request: {ex.Message}\n{ex.StackTrace}");
            LoginResult errorResult = new LoginResult
            {
                Success = false,
                ErrorMessage = $"Server error during login: {ex.Message}",
                AccountName = "" // Initialize to avoid null during serialization
            };
            
            // Send error response via PlayerManager as well
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveLoginResultClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessLoginRequest completed");
    }
    #endregion

    #region Character Loading Communication
    public async void ProcessCharacterListRequest(PlayerManager pManager, int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessCharacterListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");        
        try
        {
            CharacterListResult result = await ProcessCharacterList(accountID);
            
            bool responseSet = false;
            if (pManager.IsServer && pManager.OwnerClientId == senderClientId)
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] 
                        { 
                            senderClientId 
                        }
                    }
                };
                pManager.ReceiveCharacterListClientRpc(result, clientRpcParams);
                responseSet = true;
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send character list response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during character list request: {ex.Message}\n{ex.StackTrace}");
            CharacterListResult errorResult = new CharacterListResult
            {
                Success = false,
                ErrorMessage = $"Server error during character list request: {ex.Message}",
                Characters = new CharacterData[0] // Empty array instead of null
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");

            if (pManager.IsServer && pManager.OwnerClientId == senderClientId)
            {
                Debug.Log($"ServerManager: Sending character list error response via PlayerManager...");
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { senderClientId }
                    }
                };
                pManager.ReceiveCharacterListClientRpc(errorResult, clientRpcParams);
            }
            
        }
        
        Debug.Log($"ServerManager: ProcessCharacterListRequest completed");
    }
    #endregion

    #region Inventory Loading Communication
    public async void ProcessAccountInventoryRequest(int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessAccountInventoryRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            AccountInventoryResult result = await ProcessAccountInventory(accountID);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveAccountInventoryClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send account inventory response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during account inventory request: {ex.Message}\n{ex.StackTrace}");
            AccountInventoryResult errorResult = new AccountInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during account inventory request: {ex.Message}",
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0],
                Workbenches = new WorkbenchData[0]
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send account inventory error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending account inventory error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveAccountInventoryClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessAccountInventoryRequest completed");
    }

    public async void ProcessCharacterInventoryRequest(int characterID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessCharacterInventoryRequest ENTRY - characterID={characterID}, senderClientId={senderClientId}");        
        try
        {
            CharacterInventoryResult result = await ProcessCharacterInventory(characterID);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveCharacterInventoryClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send character inventory response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during character inventory request: {ex.Message}\n{ex.StackTrace}");
            CharacterInventoryResult errorResult = new CharacterInventoryResult
            {
                Success = false,
                ErrorMessage = $"Server error during character inventory request: {ex.Message}",
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0]
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send character inventory error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending character inventory error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveCharacterInventoryClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessCharacterInventoryRequest completed");
    }

    public async void ProcessWorkbenchListRequest(int accountID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessWorkbenchListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            WorkbenchListResult result = await ProcessWorkbenchList(accountID);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveWorkbenchListClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send workbench list response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during workbench list request: {ex.Message}\n{ex.StackTrace}");
            WorkbenchListResult errorResult = new WorkbenchListResult
            {
                Success = false,
                ErrorMessage = $"Server error during workbench list request: {ex.Message}",
                Workbenches = new WorkbenchData[0]
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send workbench list error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending workbench list error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveWorkbenchListClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessWorkbenchListRequest completed");
    }
    #endregion

    #region Waypoint and Zone Communication
    public async void ProcessWaypointRequest(int characterID, string zoneName, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessWaypointRequest ENTRY - characterID={characterID}, zoneName={zoneName}, senderClientId={senderClientId}");
        
        try
        {
            WaypointResult result = await ProcessWaypoint(characterID, zoneName);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveWaypointResultClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send waypoint response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during waypoint request: {ex.Message}\n{ex.StackTrace}");
            WaypointResult errorResult = new WaypointResult
            {
                Success = false,
                ErrorMessage = $"Server error during waypoint request: {ex.Message}",
                WaypointPosition = Vector3.zero,
                HasWaypoint = false,
                ZoneName = zoneName
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending waypoint error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveWaypointResultClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessWaypointRequest completed");
    }
    public async void ProcessPlayerZoneInfoRequest(int characterID, ulong senderClientId)
    {
        //Debug.Log($"ServerManager: ProcessPlayerZoneInfoRequest ENTRY - characterID={characterID}, senderClientId={senderClientId}");        
        try
        {
            PlayerZoneInfoResult result = await ProcessPlayerZoneInfo(characterID);
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceivePlayerZoneInfoClientRpc(result, clientRpcParams);
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send player zone info response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during player zone info request: {ex.Message}\n{ex.StackTrace}");
            PlayerZoneInfoResult errorResult = new PlayerZoneInfoResult
            {
                Success = false,
                ErrorMessage = $"Server error during player zone info request: {ex.Message}",
                ZoneInfo = new PlayerZoneInfo
                {
                    CharacterID = characterID,
                    ZoneID = 1,
                    ZoneName = "IthoriaSouth",
                    SpawnPosition = null,
                    RequiresMarketWaypoint = true
                }
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending player zone info error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceivePlayerZoneInfoClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessPlayerZoneInfoRequest completed");
    }
    #endregion

    #region Server-Side Character Logic
    private async Task<CharacterListResult> ProcessCharacterList(int accountID)
    {
        //Debug.Log($"ServerManager: ProcessCharacterList ENTRY - accountID={accountID}");        
        if (!IsServer)
        {
            Debug.LogError("ProcessCharacterList called on client! This should only run on server.");
            return new CharacterListResult { Success = false, ErrorMessage = "Server-side method called on client", Characters = new CharacterData[0] };
        }
        if (CharactersManager.Instance == null)
        {
            Debug.LogError("ServerManager: CharactersManager.Instance is null!");
            return new CharacterListResult { Success = false, ErrorMessage = "CharactersManager not available", Characters = new CharacterData[0] };
        }
        
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
                Characters = new CharacterData[0] 
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


    #region Server-Side Login Logic
    private async Task<LoginResult> ProcessLogin(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language)
    {
        //Debug.Log($"ServerManager: ProcessLogin ENTRY - steamID={steamID}, accountID={accountID}");        
        if (!IsServer)
        {
            Debug.LogError("ProcessLogin called on client! This should only run on server.");
            return new LoginResult { Success = false, ErrorMessage = "Server-side method called on client", AccountName = "" };
        }
        if (AccountManager.Instance == null)
        {
            Debug.LogError("ServerManager: AccountManager.Instance is null!");
            return new LoginResult { Success = false, ErrorMessage = "AccountManager not available", AccountName = "" };
        }
        
        LoginMethod loginMethod = AccountManager.Instance.DetermineLoginMethod(steamID, accountID);
        
        switch (loginMethod)
        {
            case LoginMethod.SteamID:
                Debug.Log($"ServerManager: Processing Steam ID login for SteamID: {steamID}");
                return await HandleSteamIDLogin(steamID, accountName, email, ipAddress, language);
                
            case LoginMethod.AccountID:
                Debug.Log($"ServerManager: Processing Account ID login for AccountID: {accountID}");
                return await HandleAccountIDLogin(accountID);
                
            case LoginMethod.None:
            default:
                Debug.LogError("ServerManager: No valid login method available. Both SteamID and AccountID are 0.");
                return new LoginResult { Success = false, ErrorMessage = "No valid login method available. Both SteamID and AccountID are 0.", AccountName = "" };
        }
    }

    private async Task<LoginResult> HandleSteamIDLogin(ulong steamID, string accountName, string email, string ipAddress, string language)
    {
        Debug.Log($"ServerManager: HandleSteamIDLogin ENTRY - steamID={steamID}");
        Debug.Log($"ServerManager: Attempting login with SteamID: {steamID}");
        
        Debug.Log($"ServerManager: Calling AccountManager.GetAccountBySteamIDAsync...");
        Dictionary<string, object> account = await AccountManager.Instance.GetAccountBySteamIDAsync(steamID);

        if (account != null)
        {
            Debug.Log($"ServerManager: Account found via SteamID.");
        }
        else
        {
            Debug.Log($"ServerManager: No account found for SteamID {steamID}. Attempting to create...");
            bool created = await AccountManager.Instance.CreateNewAccountAsync(accountName, AccountManager.Instance.GenerateRandomPassword(), email, steamID, language, ipAddress);
            if (created)
            {
                Debug.Log($"ServerManager: Created new account for Steam user: {accountName}. Fetching account info...");
                account = await AccountManager.Instance.GetAccountByUsernameAsync(accountName);
                if (account == null)
                {
                    Debug.LogError("ServerManager: Failed to fetch newly created account info!");
                    return new LoginResult { Success = false, ErrorMessage = "Failed to fetch newly created account info!", AccountName = "" };
                }
            }
            else
            {
                Debug.LogError($"ServerManager: Failed to create new account for Steam user: {accountName}");
                return new LoginResult { Success = false, ErrorMessage = $"Failed to create new account for Steam user: {accountName}", AccountName = "" };
            }
        }

        Debug.Log($"ServerManager: Extracting account info to LoginResult...");
        return ExtractAccountInfoToLoginResult(account);
    }

    private async Task<LoginResult> HandleAccountIDLogin(int accountID)
    {
        Debug.Log($"ServerManager: HandleAccountIDLogin ENTRY - accountID={accountID}");
        Debug.Log($"ServerManager: Attempting login with AccountID: {accountID}");
        
        Debug.Log($"ServerManager: Calling AccountManager.GetAccountByAccountIDAsync...");
        Dictionary<string, object> account = await AccountManager.Instance.GetAccountByAccountIDAsync(accountID);

        if (account != null)
        {
            Debug.Log($"ServerManager: Account found via AccountID.");
        }
        else
        {
            Debug.LogError($"ServerManager: No account found for AccountID {accountID}. Cannot create account without SteamID.");
            return new LoginResult { Success = false, ErrorMessage = $"No account found for AccountID {accountID}. Cannot create account without SteamID.", AccountName = "" };
        }

        Debug.Log($"ServerManager: Extracting account info to LoginResult...");
        return ExtractAccountInfoToLoginResult(account);
    }

    private LoginResult ExtractAccountInfoToLoginResult(Dictionary<string, object> account)
    {
        Debug.Log($"ServerManager: ExtractAccountInfoToLoginResult ENTRY");
        try
        {
            LoginResult result = new LoginResult 
            { 
                Success = true,
                ErrorMessage = "" // Initialize to empty string to avoid null during serialization
            };

            if (account.TryGetValue("AccountID", out object idObj) && idObj != DBNull.Value)
            {
                result.AccountID = Convert.ToInt32(idObj);
                Debug.Log($"ServerManager: Extracted AccountID: {result.AccountID}");
            }
            else
            {
                Debug.LogError("ServerManager: Could not retrieve AccountID from account data.");
                return new LoginResult { Success = false, ErrorMessage = "Could not retrieve AccountID from account data.", AccountName = "" };
            }

            if (account.TryGetValue("Username", out object nameObj) && nameObj != DBNull.Value)
            {
                result.AccountName = nameObj.ToString();
                Debug.Log($"ServerManager: Extracted AccountName: {result.AccountName}");
            }
            else
            {
                Debug.LogError("ServerManager: Could not retrieve Username from account data.");
                return new LoginResult { Success = false, ErrorMessage = "Could not retrieve Username from account data.", AccountName = "" };
            }

            if (account.TryGetValue("SteamID", out object steamIdObj) && steamIdObj != DBNull.Value)
            {
                result.SteamID = Convert.ToUInt64(steamIdObj);
                Debug.Log($"ServerManager: Extracted SteamID: {result.SteamID}");
            }

            Debug.Log($"ServerManager: Successfully extracted all account info. Returning success result.");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ExtractAccountInfoToLoginResult: {ex.Message}\n{ex.StackTrace}");
            return new LoginResult { Success = false, ErrorMessage = $"Server error extracting account info: {ex.Message}", AccountName = "" };
        }
    }
    #endregion

    #region Server-Side Inventory Logic
    private async Task<AccountInventoryResult> ProcessAccountInventory(int accountID)
    {
        Debug.Log($"ServerManager: ProcessAccountInventory ENTRY - accountID={accountID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessAccountInventory called on client! This should only run on server.");
            return new AccountInventoryResult { Success = false, ErrorMessage = "Server-side method called on client", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0], Workbenches = new WorkbenchData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance methods...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new AccountInventoryResult { Success = false, ErrorMessage = "InventoryManager not available", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0], Workbenches = new WorkbenchData[0] };
        }
        
        try
        {
            // Load all account inventory data
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetAccountInventoryItemsAsync(accountID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetAccountInventoryResourceItemsAsync(accountID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetAccountInventorySubComponentsAsync(accountID);
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);
            
            Debug.Log($"ServerManager: Retrieved {itemDictionaries.Count} items, {resourceItemDictionaries.Count} resource items, {subComponentDictionaries.Count} subcomponents, {workbenchDictionaries.Count} workbenches");
            
            // Convert to network structs
            InventoryItemData[] items = ConvertToInventoryItemData(itemDictionaries);
            InventoryResourceItemData[] resourceItems = ConvertToInventoryResourceItemData(resourceItemDictionaries);
            InventorySubComponentData[] subComponents = ConvertToInventorySubComponentData(subComponentDictionaries);
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);
            
            Debug.Log($"ServerManager: Successfully processed account inventory. Returning {items.Length} items, {resourceItems.Length} resource items, {subComponents.Length} subcomponents, {workbenches.Length} workbenches.");
            return new AccountInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = items,
                ResourceItems = resourceItems,
                SubComponents = subComponents,
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
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0],
                Workbenches = new WorkbenchData[0]
            };
        }
    }

    private async Task<CharacterInventoryResult> ProcessCharacterInventory(int characterID)
    {
        Debug.Log($"ServerManager: ProcessCharacterInventory ENTRY - characterID={characterID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessCharacterInventory called on client! This should only run on server.");
            return new CharacterInventoryResult { Success = false, ErrorMessage = "Server-side method called on client", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance methods for character...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new CharacterInventoryResult { Success = false, ErrorMessage = "InventoryManager not available", Items = new InventoryItemData[0], ResourceItems = new InventoryResourceItemData[0], SubComponents = new InventorySubComponentData[0] };
        }
        
        try
        {
            // Load all character inventory data
            List<Dictionary<string, object>> itemDictionaries = await InventoryManager.Instance.GetCharacterInventoryItemsAsync(characterID);
            List<Dictionary<string, object>> resourceItemDictionaries = await InventoryManager.Instance.GetCharacterInventoryResourceItemsAsync(characterID);
            List<Dictionary<string, object>> subComponentDictionaries = await InventoryManager.Instance.GetCharacterInventorySubComponentsAsync(characterID);
            
            Debug.Log($"ServerManager: Retrieved {itemDictionaries.Count} items, {resourceItemDictionaries.Count} resource items, {subComponentDictionaries.Count} subcomponents for character {characterID}");
            
            // Convert to network structs
            InventoryItemData[] items = ConvertToInventoryItemData(itemDictionaries);
            InventoryResourceItemData[] resourceItems = ConvertToInventoryResourceItemData(resourceItemDictionaries);
            InventorySubComponentData[] subComponents = ConvertToInventorySubComponentData(subComponentDictionaries);
            
            Debug.Log($"ServerManager: Successfully processed character inventory. Returning {items.Length} items, {resourceItems.Length} resource items, {subComponents.Length} subcomponents.");
            return new CharacterInventoryResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Items = items,
                ResourceItems = resourceItems,
                SubComponents = subComponents
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessCharacterInventory: {ex.Message}\n{ex.StackTrace}");
            return new CharacterInventoryResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading character inventory: {ex.Message}", 
                Items = new InventoryItemData[0],
                ResourceItems = new InventoryResourceItemData[0],
                SubComponents = new InventorySubComponentData[0]
            };
        }
    }

    // Helper conversion methods
    private InventoryItemData[] ConvertToInventoryItemData(List<Dictionary<string, object>> dictionaries)
    {
        InventoryItemData[] items = new InventoryItemData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventoryItemData
            {
                ItemID = GetIntValue(dict, "ItemID", 0),
                SlotID = GetIntValue(dict, "SlotID", 0)
            };
        }
        return items;
    }

    private InventoryResourceItemData[] ConvertToInventoryResourceItemData(List<Dictionary<string, object>> dictionaries)
    {
        InventoryResourceItemData[] items = new InventoryResourceItemData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventoryResourceItemData
            {
                ResourceItemID = GetIntValue(dict, "ResourceItemID", 0),
                Quantity = GetIntValue(dict, "Quantity", 1)
            };
        }
        return items;
    }

    private InventorySubComponentData[] ConvertToInventorySubComponentData(List<Dictionary<string, object>> dictionaries)
    {
        InventorySubComponentData[] items = new InventorySubComponentData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new InventorySubComponentData
            {
                SubComponentID = GetIntValue(dict, "SubComponentID", 0)
            };
        }
        return items;
    }

    private WorkbenchData[] ConvertToWorkbenchData(List<Dictionary<string, object>> dictionaries)
    {
        WorkbenchData[] items = new WorkbenchData[dictionaries.Count];
        for (int i = 0; i < dictionaries.Count; i++)
        {
            var dict = dictionaries[i];
            items[i] = new WorkbenchData
            {
                WorkBenchType = GetIntValue(dict, "WorkBenchType", 1)
            };
        }
        return items;
    }
    #endregion

    #region Server-Side Workbench Logic
    private async Task<WorkbenchListResult> ProcessWorkbenchList(int accountID)
    {
        Debug.Log($"ServerManager: ProcessWorkbenchList ENTRY - accountID={accountID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessWorkbenchList called on client! This should only run on server.");
            return new WorkbenchListResult { Success = false, ErrorMessage = "Server-side method called on client", Workbenches = new WorkbenchData[0] };
        }

        Debug.Log($"ServerManager: Calling InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync...");
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("ServerManager: InventoryManager.Instance is null!");
            return new WorkbenchListResult { Success = false, ErrorMessage = "InventoryManager not available", Workbenches = new WorkbenchData[0] };
        }
        
        try
        {
            // Load workbench data
            List<Dictionary<string, object>> workbenchDictionaries = await InventoryManager.Instance.GetAccountOwnedWorkbenchesAsync(accountID);
            
            Debug.Log($"ServerManager: Retrieved {workbenchDictionaries.Count} workbench records from database");
            
            // Convert to network structs
            WorkbenchData[] workbenches = ConvertToWorkbenchData(workbenchDictionaries);
            
            Debug.Log($"ServerManager: Successfully processed workbench list. Returning {workbenches.Length} workbenches.");
            return new WorkbenchListResult 
            { 
                Success = true, 
                ErrorMessage = "", 
                Workbenches = workbenches 
            };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessWorkbenchList: {ex.Message}\n{ex.StackTrace}");
            return new WorkbenchListResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error loading workbenches: {ex.Message}", 
                Workbenches = new WorkbenchData[0] 
            };
        }
    }
    #endregion

    #region Business Logic
    private async Task<WaypointResult> ProcessWaypoint(int characterID, string zoneName)
    {
        Debug.Log($"ServerManager: ProcessWaypoint ENTRY - characterID={characterID}, zoneName={zoneName}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessWaypoint called on client! This should only run on server.");
            return new WaypointResult 
            { 
                Success = false, 
                ErrorMessage = "Server-side method called on client", 
                WaypointPosition = Vector3.zero,
                HasWaypoint = false,
                ZoneName = zoneName
            };
        }

        try
        {
            // Debug: Check if WorldManager is available
            if (WorldManager.Instance == null)
            {
                Debug.LogError("ServerManager: WorldManager.Instance is null!");
                return new WaypointResult 
                { 
                    Success = false, 
                    ErrorMessage = "WorldManager not available", 
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = zoneName
                };
            }

            Debug.Log($"ServerManager: WorldManager found, requesting waypoint for zone '{zoneName}'...");

            // Debug: Check active ZoneManagers
            var activeZoneManagers = WorldManager.Instance.GetAllZoneManagers();
            Debug.Log($"ServerManager: WorldManager reports {activeZoneManagers.Count} active ZoneManagers:");
            foreach (var kvp in activeZoneManagers)
            {
                Debug.Log($"ServerManager: - Zone '{kvp.Key}': {(kvp.Value != null ? "Valid ZoneManager" : "NULL ZoneManager")}");
                if (kvp.Value != null)
                {
                    Debug.Log($"ServerManager: - Zone '{kvp.Key}' has MarketWaypoint: {kvp.Value.HasMarketWaypoint()}");
                }
            }

            // Get MarketWaypoint position from WorldManager
            Vector3? waypointPosition = WorldManager.Instance.GetMarketWaypointPosition(zoneName);
            
            if (waypointPosition.HasValue)
            {
                Debug.Log($"ServerManager: Successfully retrieved waypoint position {waypointPosition.Value} for zone '{zoneName}'");
                return new WaypointResult 
                { 
                    Success = true, 
                    ErrorMessage = "", 
                    WaypointPosition = waypointPosition.Value,
                    HasWaypoint = true,
                    ZoneName = zoneName
                };
            }
            else
            {
                Debug.LogWarning($"ServerManager: No MarketWaypoint found for zone '{zoneName}'");
                return new WaypointResult 
                { 
                    Success = true, 
                    ErrorMessage = $"No MarketWaypoint found in zone '{zoneName}'", 
                    WaypointPosition = Vector3.zero,
                    HasWaypoint = false,
                    ZoneName = zoneName
                };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessWaypoint: {ex.Message}\n{ex.StackTrace}");
            return new WaypointResult 
            { 
                Success = false, 
                ErrorMessage = $"Server error processing waypoint: {ex.Message}", 
                WaypointPosition = Vector3.zero,
                HasWaypoint = false,
                ZoneName = zoneName
            };
        }
    }
    
    // Add other server-specific functionality here as needed
    // Future: Character creation, inventory sync, etc.
    #endregion

    #region Player Zone Information Logic
    private async Task<PlayerZoneInfoResult> ProcessPlayerZoneInfo(int characterID)
    {
        Debug.Log($"ServerManager: ProcessPlayerZoneInfo ENTRY - characterID={characterID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessPlayerZoneInfo called on client! This should only run on server.");
            return new PlayerZoneInfoResult { Success = false, ErrorMessage = "Server-side method called on client", ZoneInfo = new PlayerZoneInfo { CharacterID = characterID, ZoneID = 1, ZoneName = "IthoriaSouth", SpawnPosition = null, RequiresMarketWaypoint = true } };
        }

        try
        {
            // Step 1: Get character's zone from database
            string zoneName = await GetCharacterZoneScene(characterID);
            
            // Step 2: Get character's spawn position from database
            Vector3? spawnPosition = await GetCharacterSpawnPosition(characterID, zoneName);
            
            // Step 3: Get zone configuration
            ZoneConfiguration? config = null;
            if (WorldManager.Instance != null)
            {
                config = WorldManager.Instance.GetZoneConfiguration(zoneName);
            }
            int zoneID = config?.ZoneID ?? 1; // Default to zone 1

            PlayerZoneInfo zoneInfo = new PlayerZoneInfo
            {
                CharacterID = characterID,
                ZoneID = zoneID,
                ZoneName = zoneName,
                SpawnPosition = spawnPosition,
                RequiresMarketWaypoint = !spawnPosition.HasValue
            };

            Debug.Log($"ServerManager: Successfully retrieved player zone information for character {characterID} - Zone: {zoneName}, RequiresWaypoint: {zoneInfo.RequiresMarketWaypoint}");
            return new PlayerZoneInfoResult { Success = true, ErrorMessage = "", ZoneInfo = zoneInfo };
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessPlayerZoneInfo: {ex.Message}\n{ex.StackTrace}");
            return new PlayerZoneInfoResult { Success = false, ErrorMessage = $"Server error retrieving player zone information: {ex.Message}", ZoneInfo = new PlayerZoneInfo { CharacterID = characterID, ZoneID = 1, ZoneName = "IthoriaSouth", SpawnPosition = null, RequiresMarketWaypoint = true } };
        }
    }

    private async Task<string> GetCharacterZoneScene(int characterID)
    {
        if (CharactersManager.Instance == null)
        {
            Debug.LogError("ServerManager: CharactersManager.Instance is null!");
            return WorldManager.Instance?.GetDefaultZoneConfiguration().SceneName ?? "IthoriaSouth";
        }

        try
        {
            // Get character's stored location from database
            Dictionary<string, object> locationData = await CharactersManager.Instance.GetCharacterLocationAsync(characterID);
            
            if (locationData != null && locationData.ContainsKey("ZoneID"))
            {
                int storedZoneID = Convert.ToInt32(locationData["ZoneID"]);
                
                // Get scene name from WorldManager configuration
                string sceneName = "IthoriaSouth"; // Default
                if (WorldManager.Instance != null)
                {
                    sceneName = WorldManager.Instance.GetSceneNameForZone(storedZoneID);
                }
                
                Debug.Log($"ServerManager: Character {characterID} should load zone '{sceneName}' (ZoneID: {storedZoneID})");
                return sceneName;
            }
            else
            {
                // No location data found, use default
                Debug.LogWarning($"ServerManager: No location data found for character {characterID}, using default zone");
                return WorldManager.Instance?.GetDefaultZoneConfiguration().SceneName ?? "IthoriaSouth";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Error getting character zone for CharID {characterID}: {ex.Message}");
            return WorldManager.Instance?.GetDefaultZoneConfiguration().SceneName ?? "IthoriaSouth";
        }
    }

    private async Task<Vector3?> GetCharacterSpawnPosition(int characterID, string zoneName)
    {
        if (CharactersManager.Instance == null)
        {
            Debug.LogError("ServerManager: CharactersManager.Instance is null!");
            return null;
        }

        try
        {
            // Get character's stored location from database
            Dictionary<string, object> locationData = await CharactersManager.Instance.GetCharacterLocationAsync(characterID);
            
            if (locationData != null)
            {
                int xLoc = Convert.ToInt32(locationData["XLoc"]);
                int yLoc = Convert.ToInt32(locationData["YLoc"]);
                int zLoc = Convert.ToInt32(locationData["ZLoc"]);
                
                // Check if position is default (0,0,0) - requires MarketWaypoint fallback
                if (CharactersManager.Instance.IsDefaultLocation(xLoc, yLoc, zLoc))
                {
                    Debug.Log($"ServerManager: Character {characterID} has default position (0,0,0), will use MarketWaypoint");
                    return null; // Null indicates MarketWaypoint should be used
                }
                else
                {
                    Vector3 storedPosition = new Vector3(xLoc, yLoc, zLoc);
                    Debug.Log($"ServerManager: Character {characterID} should spawn at stored position {storedPosition}");
                    return storedPosition;
                }
            }
            else
            {
                // No location data found, use MarketWaypoint
                Debug.LogWarning($"ServerManager: No location data found for character {characterID}, will use MarketWaypoint");
                return null; // Null indicates MarketWaypoint should be used
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Error getting character spawn position for CharID {characterID}: {ex.Message}");
            return null; // Fallback to MarketWaypoint on error
        }
    }
    #endregion

    #region Server Zone Loading Communication
    public async void ProcessServerLoadZoneRequest(string zoneName, ulong senderClientId)
    {
        Debug.Log($"ServerManager: ProcessServerLoadZoneRequest ENTRY - zoneName={zoneName}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessServerLoadZone...");
            ServerZoneLoadResult result = await ProcessServerLoadZone(zoneName);
            
            Debug.Log($"ServerManager: ProcessServerLoadZone completed. Success={result.Success}");
            
            // Find the PlayerManager that belongs to the client who sent the request
            Debug.Log($"ServerManager: Finding PlayerManager to send server zone load response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending server zone load response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveServerLoadZoneResultClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Server zone load response sent via PlayerManager successfully");
                    responseSet = true;
                    break;
                }
            }
            
            if (!responseSet)
            {
                Debug.LogError($"ServerManager: Could not find PlayerManager for client {senderClientId} to send server zone load response!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during server zone load request: {ex.Message}\n{ex.StackTrace}");
            ServerZoneLoadResult errorResult = new ServerZoneLoadResult
            {
                Success = false,
                ErrorMessage = $"Server error during zone load request: {ex.Message}"
            };
            
            // Send error response via PlayerManager
            Debug.Log($"ServerManager: Finding PlayerManager to send error response to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending server zone load error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveServerLoadZoneResultClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessServerLoadZoneRequest completed");
    }
    #endregion

    #region Server Zone Loading Logic
    private async Task<ServerZoneLoadResult> ProcessServerLoadZone(string zoneName)
    {
        Debug.Log($"ServerManager: ProcessServerLoadZone ENTRY - zoneName={zoneName}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessServerLoadZone called on client! This should only run on server.");
            return new ServerZoneLoadResult { Success = false, ErrorMessage = "Server-side method called on client" };
        }

        try
        {
            // Check if PersistentSceneManager is available
            if (PersistentSceneManager.Instance == null)
            {
                Debug.LogError("ServerManager: PersistentSceneManager.Instance is null!");
                return new ServerZoneLoadResult { Success = false, ErrorMessage = "PersistentSceneManager not available on server" };
            }

            // Check if zone is already loaded on server
            if (PersistentSceneManager.Instance.IsZoneLoaded(zoneName))
            {
                Debug.Log($"ServerManager: Zone '{zoneName}' is already loaded on server");
                return new ServerZoneLoadResult { Success = true, ErrorMessage = "" };
            }

            Debug.Log($"ServerManager: Loading zone '{zoneName}' on server...");

            // Use TaskCompletionSource to wait for server zone loading
            var loadCompletionSource = new TaskCompletionSource<bool>();
            
            PersistentSceneManager.Instance.LoadZone(zoneName, (success) =>
            {
                Debug.Log($"ServerManager: Server zone load callback - Zone: {zoneName}, Success: {success}");
                loadCompletionSource.SetResult(success);
            });

            // Wait for zone loading to complete with timeout
            bool loadSuccess = await loadCompletionSource.Task;

            if (loadSuccess)
            {
                Debug.Log($"ServerManager: Successfully loaded zone '{zoneName}' on server");
                
                // Additional delay to ensure WorldManager has time to spawn ZoneManager
                await Task.Delay(1000);
                
                Debug.Log($"ServerManager: Zone '{zoneName}' fully initialized on server");
                return new ServerZoneLoadResult { Success = true, ErrorMessage = "" };
            }
            else
            {
                Debug.LogError($"ServerManager: Failed to load zone '{zoneName}' on server");
                return new ServerZoneLoadResult { Success = false, ErrorMessage = $"Failed to load zone '{zoneName}' on server" };
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ServerManager: Exception during ProcessServerLoadZone: {ex.Message}\n{ex.StackTrace}");
            return new ServerZoneLoadResult { Success = false, ErrorMessage = $"Server error loading zone: {ex.Message}" };
        }
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
    #endregion

    #endregion
}

/// <summary>
/// Player zone information result structure for server-client communication
/// </summary>
[System.Serializable]
public struct PlayerZoneInfoResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public PlayerZoneInfo ZoneInfo;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref ZoneInfo);
    }
}

/// <summary>
/// Server zone loading result structure for server-client communication
/// </summary>
[System.Serializable]
public struct ServerZoneLoadResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
    }
}

