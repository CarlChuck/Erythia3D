using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkRequestManager
{
    private readonly PlayerManager playerManager;
    private readonly Dictionary<string, NetworkRequest> activeRequests;

    public NetworkRequestManager(PlayerManager manager)
    {
        playerManager = manager;
        activeRequests = new();
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
    private async Task<TResponse> SendRequestAsync<TResponse>(
        string requestType,
        Action sendAction,
        Func<TResponse> getResponseFunc,
        Func<bool> isResponseReceivedFunc,
        Action resetStateAction,
        float timeout = 10f)
    {
        try
        {
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

            return isResponseReceivedFunc() ? getResponseFunc() : default;
        }
        catch (Exception ex)
        {
            Debug.LogError($"NetworkRequestManager: Exception during {requestType} request: {ex.Message}\n{ex.StackTrace}");
            return default;
        }
    }
    #endregion

    #region Specific Request Wrappers
    public async Task<LoginResult> SendLoginRequestAsync(ulong steamID, int accountID, string accountName, string email, string ipAddress, string language)
    {
        Debug.Log($"NetworkRequestManager.SendLoginRequestAsync: Starting login request for steamID={steamID}, accountID={accountID}");
        Debug.Log($"NetworkRequestManager: PlayerManager state - IsSpawned={playerManager.IsSpawned}, IsOwner={playerManager.IsOwner}, NetworkObjectId={playerManager.NetworkObjectId}");
        Debug.Log($"NetworkRequestManager: NetworkManager.Singleton = {(Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.gameObject.name : "null")}");
        
        return await SendRequestAsync<LoginResult>(
            "Login",
            () => {
                Debug.Log($"NetworkRequestManager: About to call PlayerManager.RequestLoginServerRpc");
                playerManager.RequestLoginRpc(steamID, accountID, accountName, email, ipAddress, language);
                Debug.Log($"NetworkRequestManager: Called PlayerManager.RequestLoginServerRpc successfully");
            },
            () => playerManager.currentLoginResult,
            () => playerManager.loginResultReceived,
            () => {
                playerManager.loginResultReceived = false;
                playerManager.currentLoginResult = default;
            }
        );
    }
    public async Task<CharacterListResult> SendCharacterListRequestAsync(int accountID)
    {
        return await SendRequestAsync<CharacterListResult>(
            "CharacterList",
            () => playerManager.RequestCharacterListRpc(accountID),
            () => playerManager.currentCharacterListResult,
            () => playerManager.characterListReceived,
            () => {
                playerManager.characterListReceived = false;
                playerManager.currentCharacterListResult = default;
            }
        );
    }
    public async Task<AccountInventoryResult> SendAccountInventoryRequestAsync(int accountID)
    {
        return await SendRequestAsync<AccountInventoryResult>(
            "AccountInventory",
            () => playerManager.RequestAccountInventoryRpc(accountID),
            () => playerManager.currentAccountInventoryResult,
            () => playerManager.accountInventoryReceived,
            () => {
                playerManager.accountInventoryReceived = false;
                playerManager.currentAccountInventoryResult = default;
            }
        );
    }
    public async Task<CharacterInventoryResult> SendCharacterInventoryRequestAsync(int characterID)
    {
        return await SendRequestAsync<CharacterInventoryResult>(
            "CharacterInventory",
            () => playerManager.RequestCharacterInventoryRpc(characterID),
            () => playerManager.currentCharacterInventoryResult,
            () => playerManager.characterInventoryReceived,
            () => {
                playerManager.characterInventoryReceived = false;
                playerManager.currentCharacterInventoryResult = default;
            }
        );
    }
    public async Task<WorkbenchListResult> SendWorkbenchListRequestAsync(int accountID)
    {
        return await SendRequestAsync<WorkbenchListResult>(
            "WorkbenchList",
            () => playerManager.RequestWorkbenchListRpc(accountID),
            () => playerManager.currentWorkbenchListResult,
            () => playerManager.workbenchListReceived,
            () => {
                playerManager.workbenchListReceived = false;
                playerManager.currentWorkbenchListResult = default;
            }
        );
    }
    public async Task<PlayerZoneInfoResult> SendPlayerZoneInfoRequestAsync(int characterID)
    {
        return await SendRequestAsync<PlayerZoneInfoResult>(
            "PlayerZoneInfo",
            () => playerManager.RequestPlayerZoneInfoRpc(characterID),
            () => playerManager.currentPlayerZoneInfoResult,
            () => playerManager.playerZoneInfoResultReceived,
            () => {
                playerManager.playerZoneInfoResultReceived = false;
                playerManager.currentPlayerZoneInfoResult = default;
            }
        );
    }
    public async Task<WaypointResult> SendWaypointRequestAsync(WaypointRequest request)
    {
        return await SendRequestAsync<WaypointResult>(
            "Waypoint",
            () => playerManager.RequestWaypointRpc(request),
            () => playerManager.currentWaypointResult,
            () => playerManager.waypointResultReceived,
            () => {
                playerManager.waypointResultReceived = false;
                playerManager.currentWaypointResult = default;
            }
        );
    }
    public async Task<ServerZoneLoadResult> SendServerZoneLoadRequestAsync(string zoneName)
    {
        return await SendRequestAsync<ServerZoneLoadResult>(
            "ServerZoneLoad",
            () => playerManager.RequestServerLoadZoneRpc(zoneName),
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
    public async Task<Dictionary<string, object>> SendBatchRequestsAsync(params (string name, Func<Task<object>> request)[] requests)
    {
        Dictionary<string, object> results = new();
        List<Task> tasks = new();

        foreach ((string name, Func<Task<object>> request) in requests)
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
    private List<string> GetActiveRequests()
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

public struct NetworkRequestStats
{
    public int ActiveRequestCount;
    public List<string> ActiveRequestTypes;
} 