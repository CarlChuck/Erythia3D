using UnityEngine;
using System.Threading.Tasks;
using System;

/// <summary>
/// Helper class for PlayerManager to handle zone loading and PlayerController instantiation
/// Uses PlayerManager's RPC methods for server communication
/// </summary>
public class ZoneCoordinator
{
    private PlayerManager playerManager;

    public ZoneCoordinator(PlayerManager manager)
    {
        playerManager = manager;
    }

    #region Character Setup and Zone Loading
    public async Task SetupSelectedCharacterAsync(PlayerStatBlock selectedCharacter)
    {
        try
        {
            int characterID = selectedCharacter.GetCharacterID();
            
            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Setting up selected character {characterID}, determining appropriate zone...");
            }

            // Step 1: Get player zone information
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoAsync(characterID);
            
            if (string.IsNullOrEmpty(zoneInfo.ZoneName))
            {
                Debug.LogError($"ZoneCoordinator: Failed to determine zone for character {characterID}");
                return;
            }

            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Character {characterID} should be in zone '{zoneInfo.ZoneName}' (ZoneID: {zoneInfo.ZoneID})");
            }

            // Step 2: Load the appropriate zone scene
            await LoadCharacterZoneAsync(zoneInfo);

            // Step 3: Create and position PlayerController
            await SetupPlayerControllerAsync(selectedCharacter, zoneInfo);

            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Successfully set up selected character {characterID} in zone '{zoneInfo.ZoneName}'");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error in SetupSelectedCharacterAsync: {ex.Message}");
        }
    }

    private async Task<PlayerZoneInfo> GetPlayerZoneInfoAsync(int characterID)
    {
        try
        {
            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Requesting zone info for character {characterID} via RPC...");
            }

            // Use NetworkRequestManager for cleaner request handling
            PlayerZoneInfoResult result = await playerManager.requestManager.SendPlayerZoneInfoRequestAsync(characterID);
            
            if (result.Success)
            {
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Received zone info - Zone: {result.ZoneInfo.ZoneName}, RequiresWaypoint: {result.ZoneInfo.RequiresMarketWaypoint}");
                }
                return result.ZoneInfo;
            }
            else
            {
                Debug.LogError($"ZoneCoordinator: Zone info request failed: {result.ErrorMessage}");
                return GetFallbackZoneInfo(characterID);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error getting player zone info via RPC: {ex.Message}");
            return GetFallbackZoneInfo(characterID);
        }
    }

    private async Task LoadCharacterZoneAsync(PlayerZoneInfo zoneInfo)
    {
        try
        {
            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Loading zone '{zoneInfo.ZoneName}' for character {zoneInfo.CharacterID}");
            }

            // Step 1: Request server to load the zone first
            await RequestServerLoadZoneAsync(zoneInfo.ZoneName);

            // Step 2: Unload MainMenu when transitioning to gameplay zones
            await UnloadMainMenuIfNeeded(zoneInfo.ZoneName);

            // Step 3: Load zone on client side
            await LoadZoneOnClient(zoneInfo.ZoneName);

            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Successfully loaded zone '{zoneInfo.ZoneName}'");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error loading zone '{zoneInfo.ZoneName}': {ex.Message}");
            throw;
        }
    }

    private async Task SetupPlayerControllerAsync(PlayerStatBlock selectedCharacter, PlayerZoneInfo zoneInfo)
    {
        try
        {
            // Step 1: Determine spawn position
            Vector3 spawnPosition = await DetermineSpawnPositionAsync(zoneInfo);

            // Step 2: Instantiate PlayerController
            if (playerManager.PlayerControllerPrefab == null)
            {
                Debug.LogError("ZoneCoordinator: PlayerControllerPrefab is null! Cannot instantiate PlayerController.");
                return;
            }

            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Instantiating PlayerController at position {spawnPosition}");
            }

            GameObject controllerGO = GameObject.Instantiate(playerManager.PlayerControllerPrefab);
            PlayerController controller = controllerGO.GetComponent<PlayerController>();
            
            if (controller == null)
            {
                Debug.LogError("ZoneCoordinator: PlayerController component not found on instantiated prefab!");
                GameObject.Destroy(controllerGO);
                return;
            }

            // Step 3: Setup controller
            controller.SetCharacterPosition(spawnPosition, playerManager.DebugMode);

            // TODO: Link controller to selected character
            // controller.SetPlayerStatBlock(selectedCharacter);

            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Successfully created and positioned PlayerController");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error setting up PlayerController: {ex.Message}");
        }
    }
    #endregion

    #region Zone Loading Helpers
    private async Task RequestServerLoadZoneAsync(string zoneName)
    {
        if (playerManager.DebugMode)
        {
            Debug.Log($"ZoneCoordinator: Requesting server to load zone '{zoneName}'");
        }

        // Use NetworkRequestManager for cleaner request handling
        ServerZoneLoadResult result = await playerManager.requestManager.SendServerZoneLoadRequestAsync(zoneName);

        if (!result.Success)
        {
            Debug.LogError($"ZoneCoordinator: Server failed to load zone '{zoneName}': {result.ErrorMessage}");
            throw new Exception($"Server zone load failed: {result.ErrorMessage}");
        }

        if (playerManager.DebugMode)
        {
            Debug.Log($"ZoneCoordinator: Server successfully loaded zone '{zoneName}'");
        }
    }

    private async Task UnloadMainMenuIfNeeded(string zoneName)
    {
        if (PersistentSceneManager.Instance != null && zoneName != "MainMenu")
        {
            if (PersistentSceneManager.Instance.IsSceneLoaded("MainMenu"))
            {
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Unloading MainMenu for transition to gameplay zone '{zoneName}'");
                }
                PersistentSceneManager.Instance.UnloadMainMenuForGameplay();
                await Task.Delay(500); // Small delay for unloading
            }
        }
    }

    private async Task LoadZoneOnClient(string zoneName)
    {
        bool loadSuccess = false;
        
        if (PersistentSceneManager.Instance != null)
        {
            // Load zone additively
            PersistentSceneManager.Instance.LoadZone(zoneName, (success) =>
            {
                loadSuccess = success;
            });

            // Wait for zone loading to complete (with timeout)
            float timeout = 30f;
            float timer = 0f;
            bool loadComplete = false;

            while (timer < timeout && !loadComplete)
            {
                await Task.Delay(100);
                timer += 0.1f;
                
                if (PersistentSceneManager.Instance.IsZoneLoaded(zoneName))
                {
                    loadComplete = true;
                    loadSuccess = true;
                    break;
                }
            }

            if (!loadComplete)
            {
                throw new Exception($"Zone loading timeout for '{zoneName}'");
            }
        }
        else
        {
            throw new Exception("PersistentSceneManager.Instance is null!");
        }

        if (!loadSuccess)
        {
            throw new Exception($"Failed to load zone '{zoneName}'");
        }

        // Longer delay to ensure ZoneManager initialization completes on server
        await Task.Delay(2000);
    }
    #endregion

    #region Spawn Position Determination
    private async Task<Vector3> DetermineSpawnPositionAsync(PlayerZoneInfo zoneInfo)
    {
        try
        {
            Vector3 spawnPosition;

            if (zoneInfo.SpawnPosition.HasValue && !zoneInfo.RequiresMarketWaypoint)
            {
                // Use stored database position
                spawnPosition = zoneInfo.SpawnPosition.Value;
                
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Using stored spawn position: {spawnPosition}");
                }
            }
            else
            {
                // Need to get MarketWaypoint position from server with retry logic
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Database position not available, requesting MarketWaypoint for zone '{zoneInfo.ZoneName}'");
                }

                Vector3? waypointPosition = await GetMarketWaypointPositionWithRetryAsync(zoneInfo.ZoneName);
                
                if (waypointPosition.HasValue)
                {
                    spawnPosition = waypointPosition.Value;
                    
                    if (playerManager.DebugMode)
                    {
                        Debug.Log($"ZoneCoordinator: Using MarketWaypoint position: {spawnPosition}");
                    }
                }
                else
                {
                    // Fallback to origin if no waypoint found
                    spawnPosition = Vector3.zero;
                    Debug.LogWarning($"ZoneCoordinator: No MarketWaypoint found for zone '{zoneInfo.ZoneName}', using origin (0,0,0)");
                }
            }

            // Check for problematic origin position
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogWarning($"ZoneCoordinator: WARNING - Character positioned at origin (0,0,0). This might cause 'hanging in space' if there's no ground at origin!");
            }

            return spawnPosition;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error determining spawn position: {ex.Message}");
            return Vector3.zero; // Safe fallback
        }
    }

    private async Task<Vector3?> GetMarketWaypointPositionWithRetryAsync(string zoneName)
    {
        int maxRetries = 3;
        int retryDelay = 1000; // 1 second between retries
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Attempting to get MarketWaypoint for zone '{zoneName}' (attempt {attempt}/{maxRetries})");
            }
            
            Vector3? waypointPosition = await GetMarketWaypointPositionAsync(zoneName);
            
            if (waypointPosition.HasValue)
            {
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Successfully found MarketWaypoint on attempt {attempt}");
                }
                return waypointPosition;
            }
            
            // Wait before retrying (except on last attempt)
            if (attempt < maxRetries)
            {
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: MarketWaypoint not found, waiting {retryDelay}ms before retry...");
                }
                await Task.Delay(retryDelay);
            }
        }
        
        Debug.LogWarning($"ZoneCoordinator: Failed to find MarketWaypoint for zone '{zoneName}' after {maxRetries} attempts");
        return null;
    }

    private async Task<Vector3?> GetMarketWaypointPositionAsync(string zoneName)
    {
        if (playerManager.SelectedPlayerCharacter == null)
        {
            Debug.LogError("ZoneCoordinator: Cannot get waypoint - no character selected");
            return null;
        }

        int characterID = playerManager.SelectedPlayerCharacter.GetCharacterID();

        try
        {
            if (playerManager.DebugMode)
            {
                Debug.Log($"ZoneCoordinator: Requesting MarketWaypoint position for zone '{zoneName}' via RPC...");
            }

            // Create waypoint request
            WaypointRequest request = new WaypointRequest
            {
                CharacterID = characterID,
                ZoneName = zoneName
            };
            
            // Use NetworkRequestManager for cleaner request handling
            WaypointResult result = await playerManager.requestManager.SendWaypointRequestAsync(request);
            
            if (result.Success && result.HasWaypoint)
            {
                if (playerManager.DebugMode)
                {
                    Debug.Log($"ZoneCoordinator: Received MarketWaypoint position: {result.WaypointPosition}");
                }
                return result.WaypointPosition;
            }
            else
            {
                if (playerManager.DebugMode)
                {
                    Debug.LogWarning($"ZoneCoordinator: No waypoint available for zone '{zoneName}': {result.ErrorMessage}");
                }
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error getting MarketWaypoint position via RPC: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Helper Methods
    private PlayerZoneInfo GetFallbackZoneInfo(int characterID)
    {
        return new PlayerZoneInfo
        {
            CharacterID = characterID,
            ZoneID = 1,
            ZoneName = "IthoriaSouth",
            SpawnPosition = null,
            RequiresMarketWaypoint = true
        };
    }
    #endregion
} 