using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Helper class for PlayerManager to manage network requests and responses
/// Provides generic timeout handling and response tracking
/// </summary>
public class NetworkRequestManager
{
    private PlayerManager playerManager;
    private Dictionary<string, NetworkRequest> activeRequests;

    public NetworkRequestManager(PlayerManager manager)
    {
        playerManager = manager;
        activeRequests = new Dictionary<string, NetworkRequest>();
    }

    #region Generic Request Pattern
    /// <summary>
    /// Generic method for sending network requests with timeout handling
    /// </summary>
    /// <typeparam name="TResponse">Type of response expected</typeparam>
    /// <param name="requestType">Unique identifier for this request type</param>
    /// <param name="sendAction">Action that sends the RPC</param>
    /// <param name="getResponseFunc">Function that gets the current response</param>
    /// <param name="isResponseReceivedFunc">Function that checks if response was received</param>
    /// <param name="resetStateAction">Action that resets the response state</param>
    /// <param name="timeout">Timeout in seconds (default 10)</param>
    /// <returns>Response or default value if timeout/error</returns>
    public async Task<TResponse> SendRequestAsync<TResponse>(
        string requestType,
        Action sendAction,
        Func<TResponse> getResponseFunc,
        Func<bool> isResponseReceivedFunc,
        Action resetStateAction,
        float timeout = 10f)
    {
        try
        {
            if (playerManager.DebugMode)
            {
                Debug.Log($"NetworkRequestManager: Sending {requestType} request...");
            }

            // Reset state for new request
            resetStateAction?.Invoke();

            // Track this request
            string requestId = $"{requestType}_{Time.time}";
            NetworkRequest request = new NetworkRequest
            {
                RequestType = requestType,
                StartTime = Time.time,
                Timeout = timeout
            };
            activeRequests[requestId] = request;

            // Send the RPC
            sendAction?.Invoke();

            // Wait for response with timeout
            float elapsed = 0f;
            while (!isResponseReceivedFunc() && elapsed < timeout)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }

            // Clean up request tracking
            activeRequests.Remove(requestId);

            if (!isResponseReceivedFunc())
            {
                Debug.LogError($"NetworkRequestManager: {requestType} request timed out after {timeout} seconds.");
                return default(TResponse);
            }

            if (playerManager.DebugMode)
            {
                Debug.Log($"NetworkRequestManager: {requestType} request completed successfully in {elapsed:F1} seconds.");
            }

            return getResponseFunc();
        }
        catch (Exception ex)
        {
            Debug.LogError($"NetworkRequestManager: Exception during {requestType} request: {ex.Message}\n{ex.StackTrace}");
            return default(TResponse);
        }
    }
    #endregion

    #region Specific Request Wrappers
    /// <summary>
    /// Login request wrapper
    /// </summary>
    public async Task<LoginResult> SendLoginRequestAsync(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language)
    {
        return await SendRequestAsync<LoginResult>(
            "Login",
            () => playerManager.RequestLoginServerRpc(steamID, accountID, accountName, email, ipAddress, language),
            () => playerManager.currentLoginResult,
            () => playerManager.loginResultReceived,
            () => {
                playerManager.loginResultReceived = false;
                playerManager.currentLoginResult = default;
            }
        );
    }

    /// <summary>
    /// Character list request wrapper
    /// </summary>
    public async Task<CharacterListResult> SendCharacterListRequestAsync(int accountID)
    {
        return await SendRequestAsync<CharacterListResult>(
            "CharacterList",
            () => playerManager.RequestCharacterListServerRpc(accountID),
            () => playerManager.currentCharacterListResult,
            () => playerManager.characterListReceived,
            () => {
                playerManager.characterListReceived = false;
                playerManager.currentCharacterListResult = default;
            }
        );
    }

    /// <summary>
    /// Account inventory request wrapper
    /// </summary>
    public async Task<AccountInventoryResult> SendAccountInventoryRequestAsync(int accountID)
    {
        return await SendRequestAsync<AccountInventoryResult>(
            "AccountInventory",
            () => playerManager.RequestAccountInventoryServerRpc(accountID),
            () => playerManager.currentAccountInventoryResult,
            () => playerManager.accountInventoryReceived,
            () => {
                playerManager.accountInventoryReceived = false;
                playerManager.currentAccountInventoryResult = default;
            }
        );
    }

    /// <summary>
    /// Character inventory request wrapper
    /// </summary>
    public async Task<CharacterInventoryResult> SendCharacterInventoryRequestAsync(int characterID)
    {
        return await SendRequestAsync<CharacterInventoryResult>(
            "CharacterInventory",
            () => playerManager.RequestCharacterInventoryServerRpc(characterID),
            () => playerManager.currentCharacterInventoryResult,
            () => playerManager.characterInventoryReceived,
            () => {
                playerManager.characterInventoryReceived = false;
                playerManager.currentCharacterInventoryResult = default;
            }
        );
    }

    /// <summary>
    /// Workbench list request wrapper
    /// </summary>
    public async Task<WorkbenchListResult> SendWorkbenchListRequestAsync(int accountID)
    {
        return await SendRequestAsync<WorkbenchListResult>(
            "WorkbenchList",
            () => playerManager.RequestWorkbenchListServerRpc(accountID),
            () => playerManager.currentWorkbenchListResult,
            () => playerManager.workbenchListReceived,
            () => {
                playerManager.workbenchListReceived = false;
                playerManager.currentWorkbenchListResult = default;
            }
        );
    }

    /// <summary>
    /// Player zone info request wrapper
    /// </summary>
    public async Task<PlayerZoneInfoResult> SendPlayerZoneInfoRequestAsync(int characterID)
    {
        return await SendRequestAsync<PlayerZoneInfoResult>(
            "PlayerZoneInfo",
            () => playerManager.RequestPlayerZoneInfoServerRpc(characterID),
            () => playerManager.currentPlayerZoneInfoResult,
            () => playerManager.playerZoneInfoResultReceived,
            () => {
                playerManager.playerZoneInfoResultReceived = false;
                playerManager.currentPlayerZoneInfoResult = default;
            }
        );
    }

    /// <summary>
    /// Waypoint request wrapper
    /// </summary>
    public async Task<WaypointResult> SendWaypointRequestAsync(WaypointRequest request)
    {
        return await SendRequestAsync<WaypointResult>(
            "Waypoint",
            () => playerManager.RequestWaypointServerRpc(request),
            () => playerManager.currentWaypointResult,
            () => playerManager.waypointResultReceived,
            () => {
                playerManager.waypointResultReceived = false;
                playerManager.currentWaypointResult = default;
            }
        );
    }

    /// <summary>
    /// Server zone load request wrapper
    /// </summary>
    public async Task<ServerZoneLoadResult> SendServerZoneLoadRequestAsync(string zoneName)
    {
        return await SendRequestAsync<ServerZoneLoadResult>(
            "ServerZoneLoad",
            () => playerManager.RequestServerLoadZoneServerRpc(zoneName),
            () => playerManager.currentServerZoneLoadResult,
            () => playerManager.serverZoneLoadResultReceived,
            () => {
                playerManager.serverZoneLoadResultReceived = false;
                playerManager.currentServerZoneLoadResult = default;
            },
            timeout: 20f // Longer timeout for zone loading
        );
    }
    #endregion

    #region Batch Operations
    /// <summary>
    /// Send multiple requests in parallel and wait for all to complete
    /// </summary>
    public async Task<Dictionary<string, object>> SendBatchRequestsAsync(params (string name, Func<Task<object>> request)[] requests)
    {
        Dictionary<string, object> results = new Dictionary<string, object>();
        List<Task> tasks = new List<Task>();

        foreach (var (name, request) in requests)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    object result = await request();
                    lock (results)
                    {
                        results[name] = result;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"NetworkRequestManager: Batch request '{name}' failed: {ex.Message}");
                    lock (results)
                    {
                        results[name] = null;
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);
        return results;
    }
    #endregion

    #region Request Monitoring
    /// <summary>
    /// Get information about currently active requests
    /// </summary>
    public List<string> GetActiveRequests()
    {
        List<string> active = new List<string>();
        float currentTime = Time.time;

        foreach (var kvp in activeRequests)
        {
            NetworkRequest request = kvp.Value;
            float elapsed = currentTime - request.StartTime;
            active.Add($"{request.RequestType} ({elapsed:F1}s/{request.Timeout}s)");
        }

        return active;
    }

    /// <summary>
    /// Cancel all active requests (useful for cleanup)
    /// </summary>
    public void CancelAllRequests()
    {
        if (activeRequests.Count > 0)
        {
            Debug.LogWarning($"NetworkRequestManager: Cancelling {activeRequests.Count} active requests.");
            activeRequests.Clear();
        }
    }

    /// <summary>
    /// Check if any requests have timed out and log warnings
    /// </summary>
    public void CheckForTimeouts()
    {
        float currentTime = Time.time;
        List<string> timedOut = new List<string>();

        foreach (var kvp in activeRequests)
        {
            NetworkRequest request = kvp.Value;
            float elapsed = currentTime - request.StartTime;
            
            if (elapsed > request.Timeout)
            {
                timedOut.Add(kvp.Key);
                Debug.LogWarning($"NetworkRequestManager: Request '{request.RequestType}' has timed out after {elapsed:F1} seconds.");
            }
        }

        // Clean up timed out requests
        foreach (string requestId in timedOut)
        {
            activeRequests.Remove(requestId);
        }
    }
    #endregion

    #region Helper Classes
    private class NetworkRequest
    {
        public string RequestType;
        public float StartTime;
        public float Timeout;
    }
    #endregion

    #region Public Utilities
    /// <summary>
    /// Get network request statistics
    /// </summary>
    public NetworkRequestStats GetStats()
    {
        return new NetworkRequestStats
        {
            ActiveRequestCount = activeRequests.Count,
            ActiveRequestTypes = new List<string>(GetActiveRequests())
        };
    }
    #endregion
}

/// <summary>
/// Statistics about network requests
/// </summary>
public struct NetworkRequestStats
{
    public int ActiveRequestCount;
    public List<string> ActiveRequestTypes;
} 