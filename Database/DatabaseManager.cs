using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using MySqlConnector; // Ensure you are using this namespace
using System.Linq;
using System.Threading.Tasks;
using System.Data.Common; // Required for DbDataReader

public class DatabaseManager : MonoBehaviour
{
    [Header("MySQL Connection Settings")]
    [SerializeField] private string server = "localhost";
    [SerializeField] private int port = 3306;
    [SerializeField] private string database = "game_data";
    [SerializeField] private string username = "root";
    [SerializeField] private string password = "";
    [SerializeField] private bool useSsl = false;
    [SerializeField] private bool logQueries = true;

    private string connectionString;

    #region Singleton
    public static DatabaseManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate DatabaseManager detected. Destroying self.");
            Destroy(gameObject);
            return;
        }
        InitializeDatabaseConnectionString(); // Just prepare the string
    }
    #endregion

    private void InitializeDatabaseConnectionString()
    {
        try
        {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password,
                SslMode = useSsl ? MySqlSslMode.Required : MySqlSslMode.None,
                ConnectionTimeout = 30,
                CharacterSet = "utf8mb4_general_ci",
                AllowUserVariables = true,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                TreatTinyAsBoolean = true,
                UseCompression = false,
                // Important for async operations: Ensure pooling is enabled (default is true)
                Pooling = true,
                MinimumPoolSize = 0, // Adjust as needed
                MaximumPoolSize = 100 // Adjust as needed
            };
            connectionString = builder.ConnectionString;
            Debug.Log($"MySQL connection string configured for {server}:{port}/{database}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to build MySQL connection string: {ex.Message}");
            // Consider disabling the component or setting an error state
            enabled = false;
        }
    }


    #region Asynchronous Methods

    // Executes a query asynchronously and returns the results as a list of dictionaries.
    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            Debug.LogError("Database connection string is not initialized.");
            return null; // Indicate failure
        }

        List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
        if (logQueries) Debug.Log($"Executing async query: {commandText}");

        // Create and dispose connection for each call, leveraging pooling
        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                await connection.OpenAsync(); // Use async open

                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    // Use DbDataReader for compatibility and async methods
                    using (DbDataReader reader = await cmd.ExecuteReaderAsync()) // Use async execute
                    {
                        while (await reader.ReadAsync()) // Use async read
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = await reader.GetFieldValueAsync<object>(i); // Use async get
                            }
                            results.Add(row);
                        }
                    } // Reader disposed here
                } // Command disposed here
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing async query: {commandText}\n{ex.Message}\n{ex.StackTrace}");
                return null; // Indicate failure by returning null
            }
            // Connection disposed (and closed) automatically by 'using' block
        }
        return results;
    }

    // Executes a non-query SQL command (INSERT, UPDATE, DELETE, etc.) asynchronously
    // returns the number of rows affected or -1 on failure
    public async Task<int> ExecuteNonQueryAsync(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return -1; }
        if (logQueries) Debug.Log($"Executing async non-query: {commandText}");

        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                await connection.OpenAsync();
                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }
                    return await cmd.ExecuteNonQueryAsync(); // Use async execute
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing async non-query: {commandText}\n{ex.Message}\n{ex.StackTrace}");
                return -1; // Indicate failure
            }
        }
    }

    // Executes a query asynchronously and returns the first column of the first row.
    public async Task<object> ExecuteScalarAsync(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return null; }
        if (logQueries) Debug.Log($"Executing async scalar: {commandText}");

        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                await connection.OpenAsync();
                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }
                    return await cmd.ExecuteScalarAsync(); // Use async execute
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing async scalar: {commandText}\n{ex.Message}\n{ex.StackTrace}");
                return null; // Indicate failure
            }
        }
    }

    // Gets the last inserted ID asynchronously.
    public async Task<long> GetLastInsertIdAsync()
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return -1; }

        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                await connection.OpenAsync();
                using (var cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", connection))
                {
                    // ExecuteScalarAsync returns object, needs conversion
                    object result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt64(result);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting last insert ID async: {ex.Message}\n{ex.StackTrace}");
                return -1; // Indicate failure
            }
        }
    }


    // Asynchronous check if a table exists
    public async Task<bool> TableExistsAsync(string tableName)
    {
        // Note: information_schema queries might be slower on some MySQL versions.
        // An alternative is trying to select from the table and catching the specific error.
        string query = $"SELECT 1 FROM information_schema.tables WHERE table_schema = @dbName AND table_name = @tableName LIMIT 1";
        var parameters = new Dictionary<string, object> {
            { "@dbName", database }, // Use parameter for database name
            { "@tableName", tableName }
        };
        var result = await ExecuteScalarAsync(query, parameters);
        return result != null && result != DBNull.Value;
    }


    // Asynchronous Table Creation
    public async Task<bool> CreateTableIfNotExistsAsync(string tableName, Dictionary<string, string> columns)
    {
        try
        {
            string columnsDefinition = string.Join(", ", columns.Select(column => $"`{column.Key}` {column.Value}"));
            string query = $"CREATE TABLE IF NOT EXISTS `{tableName}` ({columnsDefinition}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;"; // Specify engine and charset

            int result = await ExecuteNonQueryAsync(query);
            // ExecuteNonQuery returns rows affected; for CREATE TABLE it's often 0 on success.
            // We need to check for >= 0 (success) rather than > 0.
            // A more reliable check might be to call TableExistsAsync again afterwards.
            if (result >= 0)
            {
                return await TableExistsAsync(tableName); // Verify creation
            }
            return false; // NonQuery failed (< 0)
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating table async: {tableName}\n{ex.Message}");
            return false;
        }
    }

    // Asynchronous Insert
    public async Task<bool> InsertDataAsync(string tableName, Dictionary<string, object> values)
    {
        try
        {
            string columns = string.Join(", ", values.Keys.Select(k => $"`{k}`"));
            string parameters = string.Join(", ", values.Keys.Select(k => $"@{k}"));
            string query = $"INSERT INTO `{tableName}` ({columns}) VALUES ({parameters})";

            // Prepare parameters with @ prefix
            Dictionary<string, object> queryParams = values.ToDictionary(kvp => $"@{kvp.Key}", kvp => kvp.Value);

            int rowsAffected = await ExecuteNonQueryAsync(query, queryParams);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error inserting data async: {ex.Message}");
            return false;
        }
    }

    // Asynchronous Update
    public async Task<bool> UpdateDataAsync(string tableName, Dictionary<string, object> values, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            string setClause = string.Join(", ", values.Keys.Select(k => $"`{k}` = @set_{k}"));
            string query = $"UPDATE `{tableName}` SET {setClause} WHERE {whereCondition}";

            Dictionary<string, object> queryParams = new Dictionary<string, object>();
            // Add SET parameters (prefixing keys to avoid collision with WHERE keys)
            foreach (var kvp in values) { queryParams.Add($"@set_{kvp.Key}", kvp.Value); }
            // Add WHERE parameters (using original keys)
            foreach (var kvp in whereParams) { queryParams.Add(kvp.Key, kvp.Value); }

            int rowsAffected = await ExecuteNonQueryAsync(query, queryParams);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error updating data async: {ex.Message}");
            return false;
        }
    }

    // Asynchronous Delete
    public async Task<bool> DeleteDataAsync(string tableName, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            string query = $"DELETE FROM `{tableName}` WHERE {whereCondition}";
            int rowsAffected = await ExecuteNonQueryAsync(query, whereParams);
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error deleting data async: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Synchronous Methods (Refactored for Scoped Connections) - Keep if needed, but prefer async

    public int ExecuteNonQuery(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return -1; }
        if (logQueries) Debug.Log($"Executing sync non-query: {commandText}");

        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open(); // Sync open
                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null) { foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value); }
                    return cmd.ExecuteNonQuery(); // Sync execute
                }
            }
            catch (Exception ex) { Debug.LogError($"Error in ExecuteNonQuery: {ex.Message}"); return -1; }
        } // Connection disposed/closed
    }

    public object ExecuteScalar(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return null; }
        if (logQueries) Debug.Log($"Executing sync scalar: {commandText}");

        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null) { foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value); }
                    return cmd.ExecuteScalar(); // Sync execute
                }
            }
            catch (Exception ex) { Debug.LogError($"Error in ExecuteScalar: {ex.Message}"); return null; }
        }
    }

    public DataTable ExecuteQuery(string commandText, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(connectionString)) { Debug.LogError("DB connection string not initialized."); return new DataTable(); }
        if (logQueries) Debug.Log($"Executing sync query: {commandText}");

        DataTable result = new DataTable();
        using (var connection = new MySqlConnection(connectionString))
        {
            try
            {
                connection.Open();
                using (var cmd = new MySqlCommand(commandText, connection))
                {
                    if (parameters != null) { foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value); }
                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(result);
                    }
                }
            }
            catch (Exception ex) { Debug.LogError($"Error in ExecuteQuery: {ex.Message}"); }
        }
        return result;
    }

    // You would refactor InsertData, UpdateData, DeleteData, TableExists, CreateTableIfNotExists
    // similarly if you need synchronous versions, always using the scoped 'using (var connection = ...)' pattern.
    // Example:
    public bool TableExists(string tableName)
    {
        string query = $"SELECT 1 FROM information_schema.tables WHERE table_schema = @dbName AND table_name = @tableName LIMIT 1";
        var parameters = new Dictionary<string, object> {
            { "@dbName", database },
            { "@tableName", tableName }
        };
        var result = ExecuteScalar(query, parameters);
        return result != null && result != DBNull.Value;
    }

    public bool CreateTableIfNotExists(string tableName, Dictionary<string, string> columns)
    {
        try
        {
            string columnsDefinition = string.Join(", ", columns.Select(column => $"`{column.Key}` {column.Value}"));
            string query = $"CREATE TABLE IF NOT EXISTS `{tableName}` ({columnsDefinition}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";
            int result = ExecuteNonQuery(query);
            if (result >= 0) return TableExists(tableName); // Verify
            return false;
        }
        catch (Exception ex) { Debug.LogError($"Error creating table sync: {tableName}\n{ex.Message}"); return false; }
    }

    //... Add synchronous versions of Insert, Update, Delete, GetLastInsertId using the scoped connection pattern if absolutely necessary ...

    #endregion
    public string GetConnectionString()
    {
        return connectionString;
    }
}