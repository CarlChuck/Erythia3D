using Steamworks; // TODO: add Steamworks later
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;


public class AccountManager : BaseManager
{
    private const string AccountsTableName = "Accounts";

    #region Singleton
    public static AccountManager Instance;

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate AccountManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }        
    }
    #endregion

    #region Initialize
    protected override async Task InitializeAsync()
    {
        try
        {
            // 1. Ensure Table Exists
            await EnsureAccountsTableExistsAsync();

            // 2. Mark as Initialized
            isInitialized = true;
            NotifyDataLoaded();
        }
        catch (Exception ex)
        {
            LogError("AccountManager Initialization Failed", ex);
            isInitialized = false;
        }
    }
    private async Task EnsureAccountsTableExistsAsync()
    {
        bool tableOK = await EnsureTableExistsAsync(AccountsTableName, GetAccountTableDefinition());

        if (!tableOK)
        {
            throw new Exception("Failed to initialize accounts database table async.");
        }
    }
    #endregion


    public async Task<bool> CreateNewAccountAsync(string username, string password, string email, ulong steamId, string language, string ipAddress = "0.0.0.0")
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            LogError("Username and password cannot be empty.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            LogWarning("IP Address is missing during account creation.");
        }
        if (string.IsNullOrWhiteSpace(language))
        {
            LogWarning("Language is missing during account creation.");
        }

        string hashedPassword = HashPassword(password);
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"Username", username},
            {"PasswordHash", hashedPassword},
            {"Email", string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email },
            {"SteamID", steamId == 0 ? (object)DBNull.Value : (long)steamId },
            {"LastLoginIP", string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress },
            {"Language", string.IsNullOrWhiteSpace(language) ? (object)DBNull.Value : language },
            // LastCharacterID defaults to 0 in DB
        };

        try
        {
            bool success = await SaveDataAsync(AccountsTableName, values);
            if (!success)
            {
                LogWarning($"Failed to insert new account for Username: {username}. Possible duplicate Username, Email, or SteamID?");
                return false;
            }

            // Account created, now retrieve it to get the AccountID
            Dictionary<string, object> accountData = await GetAccountByUsernameAsync(username);
            if (accountData != null && accountData.TryGetValue("AccountID", out object accountIdObj))
            {
                if (int.TryParse(accountIdObj.ToString(), out int newAccountId))
                {
                    await InventoryManager.Instance.AddOwnedWorkbenchAsync(newAccountId, 1);
                    return true;
                }
                else
                {
                    LogError($"Account for '{username}' created, but failed to parse AccountID from database: {accountIdObj}");
                    return false;
                }
            }
            else
            {
                LogError($"Account for '{username}' created, but failed to retrieve details to get AccountID.");
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError($"Exception during account creation for {username}", ex);
            return false;
        }
    }

    #region Password Stuff
    private string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(password);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2")); // Convert to hex string
            }
            return sb.ToString();
        }
    }
    public async Task<bool> VerifyPasswordAsync(string username, string password)
    {
        Dictionary<string, object> account = await GetAccountByUsernameAsync(username);
        if (account == null)
        {
            LogInfo($"Password verification failed: User not found ({username})");
            return false; // User not found
        }

        string storedPasswordHash = account["PasswordHash"] as string;
        if (string.IsNullOrEmpty(storedPasswordHash))
        {
            LogError($"Password verification error: Stored password hash is null or empty for user {username}.");
            return false; // Data integrity issue
        }

        string inputPasswordHash = HashPassword(password);
        bool match = storedPasswordHash.Equals(inputPasswordHash, StringComparison.OrdinalIgnoreCase); // Case-insensitive compare for hex strings
        if (!match) { LogInfo($"Password verification failed for user {username}."); }
        return match;
    }
    public string GenerateRandomPassword()
    {
        byte[] randomBytes = new byte[16];
        using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes);
    }
    #endregion

    #region Getters
    public async Task<Dictionary<string, object>> GetAccountBySteamIDAsync(ulong steamId)
    {
        if (steamId == 0)
        {
            return null;
        }
        string query = "SELECT * FROM Accounts WHERE SteamID = @SteamID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@SteamID", steamId } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);

            if (results.Count > 0)
            {
                return results[0]; // Return the first match found
            }
            else
            {
                LogWarning($"No account found with Steam ID: {steamId}");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account by Steam ID", ex);
            return null;
        }
    }
    public async Task<Dictionary<string, object>> GetAccountByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }
        string query = "SELECT * FROM Accounts WHERE Username = @Username";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@Username", username } };

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);

            if (results.Count > 0)
            {
                return results[0]; // Return the first match found
            }
            else
            {
                LogWarning($"No account found with Username: {username}");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account by username", ex);
            return null;
        }
    }
    public async Task<Dictionary<string, object>> GetAccountByAccountIDAsync(int accountId)
    {
        Debug.Log($"AccountManager.GetAccountByAccountIDAsync: Starting lookup for AccountID={accountId}");
        
        if (accountId <= 0)
        {
            Debug.Log($"AccountManager.GetAccountByAccountIDAsync: Invalid AccountID ({accountId}) - returning null");
            return null;
        }
        
        // Check if AccountManager is initialized
        if (!isInitialized)
        {
            Debug.LogError($"AccountManager.GetAccountByAccountIDAsync: AccountManager not initialized!");
            return null;
        }
        
        string query = "SELECT * FROM Accounts WHERE AccountID = @AccountID";
        Dictionary<string, object> parameters = new Dictionary<string, object> { { "@AccountID", accountId } };
        Debug.Log($"AccountManager.GetAccountByAccountIDAsync: Executing query '{query}' with AccountID={accountId}");

        try
        {
            List<Dictionary<string, object>> results = await QueryDataAsync(query, parameters);
            Debug.Log($"AccountManager.GetAccountByAccountIDAsync: Query returned {results.Count} results");

            if (results.Count > 0)
            {
                var account = results[0];
                Debug.Log($"AccountManager.GetAccountByAccountIDAsync: Found account - Username='{account.GetValueOrDefault("Username", "null")}', AccountID={account.GetValueOrDefault("AccountID", "null")}");
                return account; // Return the first match found
            }
            else
            {
                LogWarning($"No account found with AccountID: {accountId}");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error retrieving account by AccountID", ex);
            return null;
        }
    }

    public LoginMethod DetermineLoginMethod(ulong steamId, int accountId)
    {
        if (steamId != 0 && accountId != 0)
        {
            LogWarning("Both SteamID and AccountID are provided. Prioritizing SteamID login.");
            return LoginMethod.SteamID;
        }
        else if (steamId != 0)
        {
            return LoginMethod.SteamID;
        }
        else if (accountId != 0)
        {
            return LoginMethod.AccountID;
        }
        else
        {
            LogWarning("Neither SteamID nor AccountID provided. Cannot determine login method.");
            return LoginMethod.None;
        }
    }

    private Dictionary<string, string> GetAccountTableDefinition()
    {
        return new Dictionary<string, string>
        {
            {"AccountID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"Username", "VARCHAR(255)"},
            {"PasswordHash", "VARCHAR(255)"},
            {"Email", "VARCHAR(255)"},
            {"SteamID", "BIGINT UNIQUE"},
            {"LastCharacterID", "INT"},
            {"Status", "INT DEFAULT 0"},
            {"LastLogin", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"},
            {"LastLoginIP", "VARCHAR(45) DEFAULT '0.0.0.0'"},
            {"Language", "VARCHAR(10)"},
            {"CreationDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };
    }
    #endregion

    #region Update/Setters
    public async Task<bool> SetAccountLastPlayedCharacterAsync(int accountId, int lastCharacterId)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided.");
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "LastCharacterID", lastCharacterId < 0 ? 0 : lastCharacterId } // Ensure non-negative ID
        };
        string whereCondition = "`AccountID` = @where_AccountID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_AccountID", accountId }
        };

        try
        {
            bool success = await UpdateDataAsync(AccountsTableName, values, whereCondition, whereParams);
            if (!success)
            {
                 LogWarning($"Failed to set last played character for AccountID: {accountId}. Account might not exist?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception setting last played character for AccountID {accountId}", ex);
            return false;
        }
    }

    public async Task<bool> UpdateAccountStatusAsync(int accountId, int newStatus)
    {
        if (accountId <= 0)
        {
            LogError("Invalid AccountID provided for status update.");
            return false;
        }

        Dictionary<string, object> valuesToUpdate = new Dictionary<string, object>
        {
            { "Status", newStatus }
        };

        string whereCondition = "`AccountID` = @where_AccountID";
        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_AccountID", accountId }
        };

        try
        {
            bool success = await UpdateDataAsync(AccountsTableName, valuesToUpdate, whereCondition, whereParams);
            if (success)
            {
                LogInfo($"Account status updated successfully for AccountID: {accountId} to Status: {newStatus}");
            }
            else
            {
                LogWarning($"Failed to update account status for AccountID: {accountId}. Account might not exist?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception updating account status for AccountID {accountId}", ex);
            return false;
        }
    }
    #endregion
}

public enum LoginMethod
{
    None,
    SteamID,
    AccountID
}

[System.Serializable]
public struct LoginResult : INetworkSerializable
{
    public bool Success;
    public string ErrorMessage;
    public int AccountID;
    public string AccountName;
    public ulong SteamID;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref ErrorMessage);
        serializer.SerializeValue(ref AccountID);
        serializer.SerializeValue(ref AccountName);
        serializer.SerializeValue(ref SteamID);
    }
}