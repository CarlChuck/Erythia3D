using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseManager : MonoBehaviour
{
    protected bool isInitialized = false;
    protected Task initializationTask;
    public event Action OnDataLoaded;

    public Task InitializationTask => initializationTask;

    protected virtual void OnDestroy()
    {
        Destroy(this);
    }


    #region Initialization
    public void StartInitialization()
    {
        if (initializationTask == null || initializationTask.IsCompleted)
        {
            Debug.Log($"Starting {GetType().Name} Initialization...");
            isInitialized = false;
            initializationTask = InitializeAsync();

            initializationTask.ContinueWith(t => {
                if (t.IsFaulted)
                {
                    Debug.LogError($"{GetType().Name} Initialization Failed: {t.Exception}");
                    isInitialized = false;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        else
        {
            Debug.LogWarning($"{GetType().Name} initialization already in progress.");
        }
    }

    protected abstract Task InitializeAsync();

    protected virtual void NotifyDataLoaded()
    {
        OnDataLoaded?.Invoke();
    }
    #endregion

    #region Database Operations
    protected async Task<bool> EnsureTableExistsAsync(string tableName, Dictionary<string, string> columns)
    {
        try
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager.Instance is null. Cannot ensure table '{tableName}' exists.");
                return false;
            }
            await DatabaseManager.Instance.InitializationTask; // Wait for DatabaseManager to initialize
            if (!DatabaseManager.Instance.GetIsInitialized())
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager failed to initialize. Cannot ensure table '{tableName}' exists.");
                return false;
            }

            bool exists = await DatabaseManager.Instance.TableExistsAsync(tableName);
            if (!exists)
            {
                Debug.Log($"Table '{tableName}' does not exist. Attempting to create async...");
                bool created = await DatabaseManager.Instance.CreateTableIfNotExistsAsync(tableName, columns);
                if (created)
                {
                    Debug.Log($"Successfully created table '{tableName}' async.");
                    return true;
                }
                else
                {
                    Debug.LogError($"Failed to create table '{tableName}' async.");
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking/creating table async '{tableName}': {ex.Message}");
            return false;
        }
    }

    protected async Task<bool> SaveDataAsync(string tableName, Dictionary<string, object> values)
    {
        try
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager.Instance is null. Cannot save data to table '{tableName}'.");
                return false;
            }
            await DatabaseManager.Instance.InitializationTask;
            if (!DatabaseManager.Instance.GetIsInitialized())
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager failed to initialize. Cannot save data to table '{tableName}'.");
                return false;
            }

            bool success = await DatabaseManager.Instance.InsertDataAsync(tableName, values);
            if (!success)
            {
                Debug.LogError($"Failed to save data to table '{tableName}'");
            }
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception saving data to '{tableName}': {ex.Message}");
            return false;
        }
    }

    protected async Task<bool> UpdateDataAsync(string tableName, Dictionary<string, object> values, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager.Instance is null. Cannot update data in table '{tableName}'.");
                return false;
            }
            await DatabaseManager.Instance.InitializationTask;
            if (!DatabaseManager.Instance.GetIsInitialized())
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager failed to initialize. Cannot update data in table '{tableName}'.");
                return false;
            }

            bool success = await DatabaseManager.Instance.UpdateDataFinalAsync(tableName, values, whereCondition, whereParams);
            if (!success)
            {
                Debug.LogWarning($"Failed to update data in table '{tableName}'");
            }
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception updating data in '{tableName}': {ex.Message}");
            return false;
        }
    }

    protected async Task<bool> DeleteDataAsync(string tableName, string whereCondition, Dictionary<string, object> whereParams)
    {
        try
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager.Instance is null. Cannot delete data from table '{tableName}'.");
                return false;
            }
            await DatabaseManager.Instance.InitializationTask;
            if (!DatabaseManager.Instance.GetIsInitialized())
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager failed to initialize. Cannot delete data from table '{tableName}'.");
                return false;
            }

            bool success = await DatabaseManager.Instance.DeleteDataFinalAsync(tableName, whereCondition, whereParams);
            if (!success)
            {
                Debug.LogWarning($"Failed to delete data from table '{tableName}'");
            }
            return success;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception deleting data from '{tableName}': {ex.Message}");
            return false;
        }
    }

    protected async Task<List<Dictionary<string, object>>> QueryDataAsync(string query, Dictionary<string, object> parameters = null)
    {
        try
        {
            if (DatabaseManager.Instance == null)
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager.Instance is null. Cannot execute query '{query}'.");
                return new List<Dictionary<string, object>>();
            }
            await DatabaseManager.Instance.InitializationTask;
            if (!DatabaseManager.Instance.GetIsInitialized())
            {
                Debug.LogError($"{GetType().Name}: DatabaseManager failed to initialize. Cannot execute query '{query}'.");
                return new List<Dictionary<string, object>>();
            }

            List<Dictionary<string, object>> results = await DatabaseManager.Instance.ExecuteQueryAsync(query, parameters);
            if (results == null)
            {
                Debug.LogError($"Query execution failed: {query}");
                return new List<Dictionary<string, object>>();
            }
            return results;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception executing query '{query}': {ex.Message}");
            return new List<Dictionary<string, object>>();
        }
    }
    #endregion

    #region Utility Methods
    protected void LogError(string message, Exception ex = null)
    {
        if (ex != null)
        {
            Debug.LogError($"{GetType().Name} Error: {message}\n{ex.Message}\n{ex.StackTrace}");
        }
        else
        {
            Debug.LogError($"{GetType().Name} Error: {message}");
        }
    }

    protected void LogWarning(string message)
    {
        Debug.LogWarning($"{GetType().Name} Warning: {message}");
    }

    protected void LogInfo(string message)
    {
        Debug.Log($"{GetType().Name} Info: {message}");
    }
    #endregion

    public bool GetIsInitialized()
    {
        return isInitialized;
    }
}
public static class SafeConvert
{
    public static int ToInt32(Dictionary<string, object> data, string key, int defaultValue = 0) =>
        data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToInt32(value) : defaultValue;

    public static float ToSingle(Dictionary<string, object> data, string key, float defaultValue = 0f) =>
        data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToSingle(value) : defaultValue;

    public static string ToString(Dictionary<string, object> data, string key, string defaultValue = "") =>
        data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToString(value) : defaultValue;

    public static bool ToBoolean(Dictionary<string, object> data, string key, bool defaultValue = false) =>
         data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToBoolean(value) : defaultValue;

    public static DateTime ToDateTime(Dictionary<string, object> data, string key, DateTime defaultValue = default) =>
        data.TryGetValue(key, out object value) && value != DBNull.Value && value != null ? Convert.ToDateTime(value) : defaultValue;
}