using Steamworks;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class AccountManager : MonoBehaviour
{
    private const string AccountsTableName = "Accounts";
    private Task tableInitializationTask;
    public bool IsTableInitialized { get; private set; } = false;

    #region Singleton
    public static AccountManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    #endregion

    private async void Start()
    {
        if (DatabaseManager.Instance == null)
        {
            Debug.LogError("DatabaseManager instance is missing. AccountManager cannot function.");
            enabled = false; // Disable component if DB manager is missing
            return;
        }
        tableInitializationTask = InitializeAccountsTableIfNotExistsAsync();
        // Await it here (Start is allowed to be async void)
        await tableInitializationTask;
    }
    public async Task WaitForInitialization()
    {
        // Wait for the task created in Start to complete
        if (tableInitializationTask != null)
        {
            await tableInitializationTask;
        }
        // Or, if Start hasn't run yet, wait until the flag is set (less ideal)
        // while (!IsTableInitialized) { await Task.Yield(); }
    }
    private async Task InitializeAccountsTableIfNotExistsAsync()
    {
        Debug.Log("Checking and initializing Accounts data table...");
        Dictionary<string, string> columns = GetAccountTableDefinition(); // Use helper

        try
        {
            Debug.Log($"Table '{AccountsTableName}' does not exist. Attempting to create async...");
            bool tableCreated = await DatabaseManager.Instance.CreateTableIfNotExistsAsync(AccountsTableName, columns);
            if (tableCreated)
            {
                Debug.Log("Accounts table created successfully async.");
            }
            else
            {
                // Throw an exception if critical table creation fails
                throw new Exception($"Failed to create critical table: {AccountsTableName}");
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during Accounts table initialization: {ex.Message}. Account management may fail.");
            // Depending on your game's needs, you might want to disable functionality here
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

    // Create a new account with optional Steam integration
    public async Task<bool> CreateNewAccountAsync(string username, string password, string email, ulong steamId)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogError("Username and password cannot be empty.");
            return false;
        }

        string hashedPassword = HashPassword(password);
        Dictionary<string, object> values = new Dictionary<string, object> {
            {"Username", username},
            {"PasswordHash", hashedPassword},
            {"Email", string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email },
            {"SteamID", steamId == 0 ? (object)DBNull.Value : (long)steamId }, // Store as signed BIGINT potentially, ensure DB type matches
            // LastCharacterID defaults to 0 in DB
        };

        try
        {
            bool success = await DatabaseManager.Instance.InsertDataAsync(AccountsTableName, values);
            if (!success)
            {
                Debug.LogWarning($"Failed to insert new account for Username: {username}. Possible duplicate Username, Email, or SteamID?");
            }
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during account creation for {username}: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    // Async method to get account by Steam ID
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
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);

            if (results.Count > 0)
            {
                return results[0]; // Return the first match found
            }
            else
            {
                Debug.LogWarning($"No account found with Steam ID: {steamId}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error retrieving account by Steam ID: {ex.Message}");
            return null;
        }
    }

    // Async method to get account by username
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
            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);

            if (results.Count > 0)
            {
                return results[0]; // Return the first match found
            }
            else
            {
                Debug.LogWarning($"No account found with Username: {username}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error retrieving account by username: {ex.Message}");
            return null;
        }
    }

    // Synchronous wrapper methods for backwards compatibility
    public Dictionary<string, object> GetAccountBySteamID(ulong steamId)
    {
        return Task.Run(() => GetAccountBySteamIDAsync(steamId)).GetAwaiter().GetResult();
    }

    public Dictionary<string, object> GetAccountByUsername(string username)
    {
        return Task.Run(() => GetAccountByUsernameAsync(username)).GetAwaiter().GetResult();
    }

    public async Task<bool> SetAccountLastPlayedCharacterAsync(int accountId, int lastCharacterId)
    {
        if (accountId <= 0) 
        { 
            Debug.LogError("Invalid AccountID provided."); 
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
            // Use UpdateDataAsync
            return await DatabaseManager.Instance.UpdateDataAsync(AccountsTableName, values, whereCondition, whereParams);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception setting last played character for AccountID {accountId}: {ex.Message}");
            return false;
        }
    }
    public bool SetAccountLastPlayedCharacter(int accountId, int lastCharacterId)
    {
        Debug.LogWarning($"Calling synchronous SetAccountLastPlayedCharacter for AccountID '{accountId}'. This will block the main thread!");
        try
        {
            // Call the async helper and block for the result
            return SetAccountLastPlayedCharacterAsync(accountId, lastCharacterId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in synchronous SetAccountLastPlayedCharacter wrapper: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> VerifyPasswordAsync(string username, string password)
    {
        Dictionary<string, object> account = await GetAccountByUsernameAsync(username);
        if (account == null)
        {
            Debug.Log($"Password verification failed: User not found ({username})");
            return false; // User not found
        }

        string storedPasswordHash = account["PasswordHash"] as string;
        if (string.IsNullOrEmpty(storedPasswordHash))
        {
            Debug.LogError($"Password verification error: Stored password hash is null or empty for user {username}.");
            return false; // Data integrity issue
        }

        string inputPasswordHash = HashPassword(password);
        bool match = storedPasswordHash.Equals(inputPasswordHash, StringComparison.OrdinalIgnoreCase); // Case-insensitive compare for hex strings
        if (!match) { Debug.Log($"Password verification failed for user {username}."); }
        return match;
    }

    // Synchronous wrapper for VerifyPassword
    public bool VerifyPassword(string username, string password)
    {
        Debug.LogWarning($"Calling synchronous VerifyPassword for '{username}'. This will block the main thread!");
        try { return VerifyPasswordAsync(username, password).GetAwaiter().GetResult(); }
        catch (Exception ex) { Debug.LogError($"Error in sync VerifyPassword wrapper: {ex.Message}"); return false; }
    }

    // Generate a random password for Steam-created accounts
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