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

    void Start()
    {
        if (DatabaseManager.Instance == null)
        {
            Debug.LogError("DatabaseManager instance is missing in the scene.");
            return;
        }
        InitializeAccountsTableIfNotExists();
        // Use async method for startup logic
    }

    private void InitializeAccountsTableIfNotExists()
    {
        Dictionary<string, string> columns = new Dictionary<string, string>
        {
            {"AccountID", "INT AUTO_INCREMENT PRIMARY KEY"},
            {"Username", "VARCHAR(255)"},
            {"PasswordHash", "VARCHAR(255)"},
            {"Email", "VARCHAR(255)"},
            {"SteamID", "BIGINT UNIQUE"},
            {"LastCharacterID", "INT"},
            {"CreationDate", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP"}
        };

        if (!DatabaseManager.Instance.TableExists(AccountsTableName))
        {
            bool tableCreated = DatabaseManager.Instance.CreateTableIfNotExists(AccountsTableName, columns);
            if (tableCreated)
            {
                Debug.Log("Account table created successfully.");
            }
            else
            {
                Debug.LogWarning("Failed to create Account table.");
            }
        }
    }

    // Hash the password using SHA-256 before storing it in the database
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

    // Create a new account with optional Steam integration
    public bool CreateNewAccount(string username, string password, string email = null, ulong steamId = 0)
    {
        string hashedPassword = HashPassword(password);
        Dictionary<string, object> values = new Dictionary<string, object>
        {
            {"Username", username},
            {"PasswordHash", hashedPassword},
            {"Email", email},
            {"LastCharacterID", 0},
            {"SteamID", steamId}
        };

        bool accountCreated = DatabaseManager.Instance.InsertData(AccountsTableName, values);
        if (accountCreated)
        {
            Debug.Log($"New account created with Username: {username}");
        }
        else
        {
            Debug.LogWarning("Failed to create new account.");
        }
        return accountCreated;
    }

    // Async method to get account by Steam ID
    private async Task<Dictionary<string, object>> GetAccountBySteamIDAsync(ulong steamId)
    {
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
    private async Task<Dictionary<string, object>> GetAccountByUsernameAsync(string username)
    {
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

    public bool SetAccountLastPlayedCharacter(int accountId, int lastCharacterId)
    {

        Dictionary<string, object> values = new Dictionary<string, object>
        {
            { "LastCharacterID", lastCharacterId }
        };

        string whereCondition = "`AccountID` = @where_AccountID";

        Dictionary<string, object> whereParams = new Dictionary<string, object>
        {
            { "@where_AccountID", accountId }
        };

        return DatabaseManager.Instance.UpdateData("accounts", values, whereCondition, whereParams);
    }

    public async Task<bool> VerifyPasswordAsync(string username, string password)
    {
        Dictionary<string, object> account = await GetAccountByUsernameAsync(username);
        if (account == null)
        {
            Debug.LogWarning($"User not found: {username}");
            return false;
        }

        string storedPasswordHash = account["PasswordHash"] as string;
        string inputPasswordHash = HashPassword(password);
        return storedPasswordHash == inputPasswordHash;
    }

    // Synchronous wrapper for VerifyPassword
    public bool VerifyPassword(string username, string password)
    {
        return Task.Run(() => VerifyPasswordAsync(username, password)).GetAwaiter().GetResult();
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