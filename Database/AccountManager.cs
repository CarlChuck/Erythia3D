using Steamworks; // TODO: add Steamworks later
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AccountManager : BaseManager
{
    private const string AccountsTableName = "Accounts";

    #region Singleton
    public static AccountManager Instance => GetInstance<AccountManager>();

    protected override void Awake()
    {
        base.Awake();
    }
    #endregion

    private void Start()
    {
        StartInitialization();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override async Task InitializeAsync()
    {
        try
        {
            // 1. Ensure Table Exists
            await EnsureAccountsTableExistsAsync();

            // 2. Mark as Initialized
            isInitialized = true;
            LogInfo("AccountManager Initialization Complete.");
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
        LogInfo("Checking and initializing accounts table async...");
        bool tableOK = await EnsureTableExistsAsync(AccountsTableName, GetAccountTableDefinition());

        if (!tableOK)
        {
            throw new Exception("Failed to initialize accounts database table async.");
        }
        LogInfo("Accounts table checked/initialized async.");
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
            {"CreationDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };
    }

    #region Hash Password
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
    #endregion

    public async Task<bool> CreateNewAccountAsync(string username, string password, string email, ulong steamId)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            LogError("Username and password cannot be empty.");
            return false;
        }

        string hashedPassword = HashPassword(password);
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"Username", username},
            {"PasswordHash", hashedPassword},
            {"Email", string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email },
            {"SteamID", steamId == 0 ? (object)DBNull.Value : (long)steamId },
            // LastCharacterID defaults to 0 in DB
        };

        try
        {
            bool success = await SaveDataAsync(AccountsTableName, values);
            if (!success)
            {
                LogWarning($"Failed to insert new account for Username: {username}. Possible duplicate Username, Email, or SteamID?");
            }
            return success;
        }
        catch (Exception ex)
        {
            LogError($"Exception during account creation for {username}", ex);
            return false;
        }
    }

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
            return await UpdateDataAsync(AccountsTableName, values, whereCondition, whereParams);
        }
        catch (Exception ex)
        {
            LogError($"Exception setting last played character for AccountID {accountId}", ex);
            return false;
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
}