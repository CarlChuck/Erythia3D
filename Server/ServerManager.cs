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
        
        if (IsServer)
        {
            Debug.Log("ServerManager: NetworkObject spawned on server, ready to handle RPCs");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        Debug.Log("ServerManager: NetworkObject despawned");
    }

    #region Login Communication RPCs
    [ServerRpc(RequireOwnership = false)]
    public void RequestLoginServerRpc(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ServerRpcParams serverRpcParams = default)
    {
        Debug.Log($"ServerManager: RequestLoginServerRpc ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={serverRpcParams.Receive.SenderClientId}");
        
        // Process the login directly
        ProcessLoginRequest(steamID, accountID, accountName, email, ipAddress, language, serverRpcParams.Receive.SenderClientId);
    }

    // Regular method for server-to-server communication (called by PlayerManager on server)
    public async void ProcessLoginRequest(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language, ulong senderClientId)
    {
        Debug.Log($"ServerManager: ProcessLoginRequest ENTRY - steamID={steamID}, accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessLogin directly (async/await)...");
            LoginResult result = await ProcessLogin(steamID, accountID, accountName, email, ipAddress, language);
            
            Debug.Log($"ServerManager: ProcessLogin completed. Success={result.Success}, ErrorMessage={result.ErrorMessage}");
            
            // Instead of sending ClientRpc directly, call back to PlayerManager on server
            Debug.Log($"ServerManager: Finding PlayerManager to send response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                // Find the PlayerManager that belongs to the client who sent the request
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveLoginResultClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Response sent via PlayerManager successfully");
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
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            
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
    public async void ProcessCharacterListRequest(int accountID, ulong senderClientId)
    {
        Debug.Log($"ServerManager: ProcessCharacterListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessCharacterList...");
            CharacterListResult result = await ProcessCharacterList(accountID);
            
            Debug.Log($"ServerManager: ProcessCharacterList completed. Success={result.Success}, CharacterCount={result.Characters?.Length ?? 0}");
            
            // Find the PlayerManager that belongs to the client who sent the request
            Debug.Log($"ServerManager: Finding PlayerManager to send character list response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending character list response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveCharacterListClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Character list response sent via PlayerManager successfully");
                    responseSet = true;
                    break;
                }
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
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            
            foreach (PlayerManager pm in playerManagers)
            {
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Sending character list error response via PlayerManager...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveCharacterListClientRpc(errorResult, clientRpcParams);
                    break;
                }
            }
        }
        
        Debug.Log($"ServerManager: ProcessCharacterListRequest completed");
    }
    #endregion

    #region Inventory Loading Communication
    public async void ProcessAccountInventoryRequest(int accountID, ulong senderClientId)
    {
        Debug.Log($"ServerManager: ProcessAccountInventoryRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessAccountInventory...");
            AccountInventoryResult result = await ProcessAccountInventory(accountID);
            
            Debug.Log($"ServerManager: ProcessAccountInventory completed. Success={result.Success}, ItemCount={result.Items?.Length ?? 0}");
            
            // Find the PlayerManager that belongs to the client who sent the request
            Debug.Log($"ServerManager: Finding PlayerManager to send account inventory response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending account inventory response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveAccountInventoryClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Account inventory response sent via PlayerManager successfully");
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
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            
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
        Debug.Log($"ServerManager: ProcessCharacterInventoryRequest ENTRY - characterID={characterID}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessCharacterInventory...");
            CharacterInventoryResult result = await ProcessCharacterInventory(characterID);
            
            Debug.Log($"ServerManager: ProcessCharacterInventory completed. Success={result.Success}, ItemCount={result.Items?.Length ?? 0}");
            
            // Find the PlayerManager that belongs to the client who sent the request
            Debug.Log($"ServerManager: Finding PlayerManager to send character inventory response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending character inventory response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveCharacterInventoryClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Character inventory response sent via PlayerManager successfully");
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
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            
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
        Debug.Log($"ServerManager: ProcessWorkbenchListRequest ENTRY - accountID={accountID}, senderClientId={senderClientId}");
        
        try
        {
            Debug.Log($"ServerManager: Calling ProcessWorkbenchList...");
            WorkbenchListResult result = await ProcessWorkbenchList(accountID);
            
            Debug.Log($"ServerManager: ProcessWorkbenchList completed. Success={result.Success}, WorkbenchCount={result.Workbenches?.Length ?? 0}");
            
            // Find the PlayerManager that belongs to the client who sent the request
            Debug.Log($"ServerManager: Finding PlayerManager to send workbench list response back to client {senderClientId}...");
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            Debug.Log($"ServerManager: Found {playerManagers.Length} PlayerManager instances");
            
            bool responseSet = false;
            foreach (PlayerManager pm in playerManagers)
            {
                Debug.Log($"ServerManager: Checking PlayerManager - IsServer={pm.IsServer}, OwnerClientId={pm.OwnerClientId}");
                if (pm.IsServer && pm.OwnerClientId == senderClientId)
                {
                    Debug.Log($"ServerManager: Found target PlayerManager for client {senderClientId}, sending workbench list response...");
                    ClientRpcParams clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { senderClientId }
                        }
                    };
                    pm.ReceiveWorkbenchListClientRpc(result, clientRpcParams);
                    Debug.Log($"ServerManager: Workbench list response sent via PlayerManager successfully");
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
            PlayerManager[] playerManagers = FindObjectsOfType<PlayerManager>();
            
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

    #region Server-Side Character Logic
    private async Task<CharacterListResult> ProcessCharacterList(int accountID)
    {
        Debug.Log($"ServerManager: ProcessCharacterList ENTRY - accountID={accountID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessCharacterList called on client! This should only run on server.");
            return new CharacterListResult { Success = false, ErrorMessage = "Server-side method called on client", Characters = new CharacterData[0] };
        }

        Debug.Log($"ServerManager: Calling CharactersManager.Instance.GetCharactersByAccountIDAsync...");
        if (CharactersManager.Instance == null)
        {
            Debug.LogError("ServerManager: CharactersManager.Instance is null!");
            return new CharacterListResult { Success = false, ErrorMessage = "CharactersManager not available", Characters = new CharacterData[0] };
        }
        
        try
        {
            List<Dictionary<string, object>> characterDictionaries = await CharactersManager.Instance.GetCharactersByAccountIDAsync(accountID);
            
            Debug.Log($"ServerManager: Retrieved {characterDictionaries.Count} character records from database");
            
            // Convert Dictionary data to CharacterData structs
            CharacterData[] characters = new CharacterData[characterDictionaries.Count];
            
            for (int i = 0; i < characterDictionaries.Count; i++)
            {
                var charDict = characterDictionaries[i];
                characters[i] = ConvertDictionaryToCharacterData(charDict);
                Debug.Log($"ServerManager: Converted character {i}: {characters[i].Name} (ID: {characters[i].CharID})");
            }
            
            Debug.Log($"ServerManager: Successfully processed character list. Returning {characters.Length} characters.");
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
        if (CharactersManager.Instance != null)
        {
            SpeciesTemplate species = CharactersManager.Instance.GetSpeciesByID(charData.Race);
            if (species != null)
            {
                charData.SpeciesStrength = species.strength;
                charData.SpeciesDexterity = species.dexterity;
                charData.SpeciesConstitution = species.constitution;
                charData.SpeciesIntelligence = species.intelligence;
                charData.SpeciesSpirit = species.spirit;
            }
            else
            {
                Debug.LogWarning($"Species not found for Race ID {charData.Race}. Using default values.");
                // Set default species stats if species not found
                charData.SpeciesStrength = 10;
                charData.SpeciesDexterity = 10;
                charData.SpeciesConstitution = 10;
                charData.SpeciesIntelligence = 10;
                charData.SpeciesSpirit = 10;
            }
        }
        else
        {
            Debug.LogError("CharactersManager.Instance is null! Cannot populate species stats.");
            // Set default species stats
            charData.SpeciesStrength = 10;
            charData.SpeciesDexterity = 10;
            charData.SpeciesConstitution = 10;
            charData.SpeciesIntelligence = 10;
            charData.SpeciesSpirit = 10;
        }
        
        return charData;
    }

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

    #region Server-Side Login Logic
    private async Task<LoginResult> ProcessLogin(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language)
    {
        Debug.Log($"ServerManager: ProcessLogin ENTRY - steamID={steamID}, accountID={accountID}");
        
        if (!IsServer)
        {
            Debug.LogError("ProcessLogin called on client! This should only run on server.");
            return new LoginResult { Success = false, ErrorMessage = "Server-side method called on client", AccountName = "" };
        }

        Debug.Log($"ServerManager: Calling AccountManager.Instance.DetermineLoginMethod...");
        if (AccountManager.Instance == null)
        {
            Debug.LogError("ServerManager: AccountManager.Instance is null!");
            return new LoginResult { Success = false, ErrorMessage = "AccountManager not available", AccountName = "" };
        }
        
        LoginMethod loginMethod = AccountManager.Instance.DetermineLoginMethod(steamID, accountID);
        Debug.Log($"ServerManager: Login method determined: {loginMethod}");
        
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

    // Add other server-specific functionality here as needed
    // Future: Character creation, inventory sync, etc.
}

