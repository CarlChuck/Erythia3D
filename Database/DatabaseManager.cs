using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using MySqlConnector;
using System.Linq;
using System.Threading.Tasks;


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
    private MySqlConnection dbConnection;


    #region Singleton
    public static DatabaseManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        InitializeDatabase();
    }
    #endregion
    private void Start()
    {
    }

    private void InitializeDatabase()
    {
        try
        {
            // Build connection string
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password,
                SslMode = useSsl ? MySqlSslMode.Required : MySqlSslMode.None,
                ConnectionTimeout = 30,                    
                CharacterSet = "utf8mb4_general_ci", // Explicitly set character set
                AllowUserVariables = true,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                TreatTinyAsBoolean = true,
                UseCompression = false
            };

            connectionString = builder.ConnectionString;
            dbConnection = new MySqlConnection(connectionString);
            Debug.Log($"MySQL database initialized with connection to {server}:{port}/{database}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize MySQL database: {ex.Message}");
        }
    }

    public bool TestConnection()
    {
        try
        {
            if (OpenConnection())
            {
                CloseConnection();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Connection test failed: {ex.Message}");
            return false;
        }
    }

    private bool OpenConnection()
    {
        try
        {
            if (dbConnection == null)
            {
                dbConnection = new MySqlConnection(connectionString);
            }

            if (dbConnection.State != ConnectionState.Open)
            {
                dbConnection.Open();
                if (logQueries) 
                { 
                    Debug.Log("MySQL connection opened"); 
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error opening MySQL connection: {ex.Message}");
            //Debug.LogError($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    private void CloseConnection()
    {
        if (dbConnection != null && dbConnection.State == ConnectionState.Open)
        {
            dbConnection.Close();
            if (logQueries) Debug.Log("MySQL connection closed");
        }
    }


    // Executes a non-query SQL command (INSERT, UPDATE, DELETE, etc.)
    // returns the number of rows affected
    public int ExecuteNonQuery(string commandText, Dictionary<string, object> parameters = null)
    {
        if (logQueries) 
        { 
            Debug.Log($"Executing SQL: {commandText}"); 
        }

        if (!OpenConnection()) 
        { 
            return -1; 
        }

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(commandText, dbConnection))
            {
                // Add parameters if provided
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        MySqlParameter sqlParam = new MySqlParameter(param.Key, param.Value ?? DBNull.Value);
                        cmd.Parameters.Add(sqlParam);
                    }
                }

                return cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing command: {ex.Message}");
            return -1;
        }
        finally
        {
            CloseConnection();
        }
    }

    // Executes a query and returns the first column of the first row.
    // returns The scalar result or null if no result
    public object ExecuteScalar(string commandText, Dictionary<string, object> parameters = null)
    {
        if (logQueries) Debug.Log($"Executing scalar SQL: {commandText}");

        if (!OpenConnection()) return null;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(commandText, dbConnection))
            {
                // Add parameters if provided
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        MySqlParameter sqlParam = new MySqlParameter(param.Key, param.Value ?? DBNull.Value);
                        cmd.Parameters.Add(sqlParam);
                    }
                }

                return cmd.ExecuteScalar();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing scalar: {ex.Message}");
            return null;
        }
        finally
        {
            CloseConnection();
        }
    }

    // Executes a query and returns a DataTable with the results.
    // returns DataTable containing the query results
    public DataTable ExecuteQuery(string commandText, Dictionary<string, object> parameters = null)
    {
        if (logQueries) Debug.Log($"Executing query SQL: {commandText}");

        DataTable result = new DataTable();
        if (!OpenConnection()) return result;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(commandText, dbConnection))
            {
                // Add parameters if provided
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        MySqlParameter sqlParam = new MySqlParameter(param.Key, param.Value ?? DBNull.Value);
                        cmd.Parameters.Add(sqlParam);
                    }
                }

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                {
                    adapter.Fill(result);
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing query: {ex.Message}");
            return result;
        }
        finally
        {
            CloseConnection();
        }
    }

    // Creates a table in the database if it doesn't exist.
    // returns True if successful
    public bool CreateTableIfNotExists(string tableName, Dictionary<string, string> columns)
    {
        try
        {
            string columnsDefinition = "";
            foreach (var column in columns)
            {
                if (columnsDefinition != "") 
                { 
                    columnsDefinition += ", "; 
                }
                columnsDefinition += $"`{column.Key}` {column.Value}";
            }

            string query = $"CREATE TABLE IF NOT EXISTS `{tableName}` ({columnsDefinition})";

            return ExecuteNonQuery(query) >= 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating table: {ex.Message}");
            return false;
        }
    }

    // Checks if a table exists in the database.
    // returns True if the table exists
    public bool TableExists(string tableName)
    {
        string query = $"SELECT 1 FROM information_schema.tables WHERE table_schema = '{database}' AND table_name = '{tableName}' LIMIT 1";
        var result = ExecuteScalar(query);
        return result != null;
    }

    // Inserts data into a table. 
    // returns True if successful
    public bool InsertData(string tableName, Dictionary<string, object> values)
    {
        try
        {
            string columns = string.Join(", ", values.Keys.Select(k => $"`{k}`"));
            string parameters = string.Join(", ", values.Keys.Select(k => $"@{k}"));

            string query = $"INSERT INTO `{tableName}` ({columns}) VALUES ({parameters})";

            Dictionary<string, object> queryParams = new Dictionary<string, object>();
            foreach (var kvp in values)
            {
                queryParams.Add($"@{kvp.Key}", kvp.Value);
            }

            return ExecuteNonQuery(query, queryParams) > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error inserting data: {ex.Message}");
            return false;
        }
    }

    // Inserts multiple rows of data into a table in a single transaction.
    // returns True if successful
    public bool BulkInsertData(string tableName, List<Dictionary<string, object>> valuesList)
    {
        if (valuesList == null || valuesList.Count == 0) return false;

        if (!OpenConnection()) return false;

        MySqlTransaction transaction = null;
        try
        {
            transaction = dbConnection.BeginTransaction();

            // Get column names from the first dictionary
            string columns = string.Join(", ", valuesList[0].Keys.Select(k => $"`{k}`"));
            string paramPlaceholder = string.Join(", ", valuesList[0].Keys.Select(k => $"@{k}"));

            string query = $"INSERT INTO `{tableName}` ({columns}) VALUES ({paramPlaceholder})";

            using (MySqlCommand cmd = new MySqlCommand(query, dbConnection, transaction))
            {
                // Create parameters once
                foreach (var key in valuesList[0].Keys)
                {
                    cmd.Parameters.Add(new MySqlParameter($"@{key}", DBNull.Value));
                }

                int rowsAffected = 0;
                foreach (var values in valuesList)
                {
                    // Update parameter values for each row
                    foreach (var kvp in values)
                    {
                        cmd.Parameters[$"@{kvp.Key}"].Value = kvp.Value ?? DBNull.Value;
                    }

                    rowsAffected += cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return rowsAffected > 0;
            }
        }
        catch (Exception ex)
        {
            transaction?.Rollback();
            Debug.LogError($"Error in bulk insert: {ex.Message}");
            return false;
        }
        finally
        {
            CloseConnection();
        }
    }

    // Updates data in a table.
    // returns True if successful
    public bool UpdateData(string tableName, Dictionary<string, object> values, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            string setClause = string.Join(", ", values.Keys.Select(k => $"`{k}` = @set_{k}"));

            string query = $"UPDATE `{tableName}` SET {setClause} WHERE {whereCondition}";

            Dictionary<string, object> queryParams = new Dictionary<string, object>();

            // Add SET parameters
            foreach (var kvp in values)
            {
                queryParams.Add($"@set_{kvp.Key}", kvp.Value);
            }

            // Add WHERE parameters
            foreach (var kvp in whereParams)
            {
                queryParams.Add(kvp.Key, kvp.Value);
            }

            return ExecuteNonQuery(query, queryParams) > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error updating data: {ex.Message}");
            return false;
        }
    }

    // Deletes data from a table.
    // returns True if successful
    public bool DeleteData(string tableName, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            string query = $"DELETE FROM `{tableName}` WHERE {whereCondition}";

            return ExecuteNonQuery(query, whereParams) > 0;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error deleting data: {ex.Message}");
            return false;
        }
    }

    // Executes a query asynchronously and returns the results as a list of dictionaries.
    // returns Task containing List of Dictionaries with the query results
    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string commandText, Dictionary<string, object> parameters = null)
    {
        return await Task.Run(() => {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            if (!OpenConnection())
            {
                Debug.LogError("Failed to open database connection");
                return results;
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(commandText, dbConnection))
                {
                    // Add parameters if provided
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            MySqlParameter sqlParam = new MySqlParameter(param.Key, param.Value ?? DBNull.Value);
                            cmd.Parameters.Add(sqlParam);
                        }
                    }

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Dictionary<string, object> row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.GetValue(i);
                            }
                            results.Add(row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing async query: {ex.Message}");
            }
            finally
            {
                CloseConnection();
            }

            return results;
        });
    }

    // Gets the last inserted ID for auto-increment columns.
    // returns The last inserted ID
    public long GetLastInsertId()
    {
        if (!OpenConnection()) return -1;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", dbConnection))
            {
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting last insert ID: {ex.Message}");
            return -1;
        }
        finally
        {
            CloseConnection();
        }
    }

    // Executes a stored procedure.
    // returns DataTable with results if procedure returns a resultset
    public DataTable ExecuteStoredProcedure(string procedureName, Dictionary<string, object> parameters = null)
    {
        if (logQueries) Debug.Log($"Executing stored procedure: {procedureName}");

        DataTable result = new DataTable();
        if (!OpenConnection()) return result;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(procedureName, dbConnection))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // Add parameters if provided
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        MySqlParameter sqlParam = new MySqlParameter(param.Key, param.Value ?? DBNull.Value);
                        cmd.Parameters.Add(sqlParam);
                    }
                }

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                {
                    adapter.Fill(result);
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing stored procedure: {ex.Message}");
            return result;
        }
        finally
        {
            CloseConnection();
        }
    }

    private void OnDestroy()
    {
        CloseConnection();
    }

}