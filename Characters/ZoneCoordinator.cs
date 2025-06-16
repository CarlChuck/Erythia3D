using UnityEngine;
using System.Threading.Tasks;
using System;

public class ZoneCoordinator
{
    private readonly PlayerManager playerManager;
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

            // Step 1: Get player zone information
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            
            if (string.IsNullOrEmpty(zoneInfo.ZoneName))
            {
                Debug.LogError($"ZoneCoordinator: Failed to determine zone for character {characterID}");
                return;
            }

            // Step 2: Load the appropriate zone scene
            await LoadCharacterZoneAsync(zoneInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error in SetupSelectedCharacterAsync: {ex.Message}");
        }
    }
    public async Task SetupSelectedCharacterLegacyAsync(PlayerStatBlock selectedCharacter)
    {
        try
        {
            int characterID = selectedCharacter.GetCharacterID();

            // Step 1: Get player zone information
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            
            if (string.IsNullOrEmpty(zoneInfo.ZoneName))
            {
                Debug.LogError($"ZoneCoordinator: Failed to determine zone for character {characterID}");
                return;
            }

            // Step 2: Load the appropriate zone scene
            await LoadCharacterZoneAsync(zoneInfo);

            // Step 3: Create and position PlayerController (legacy)
            await SetupPlayerControllerAsync(selectedCharacter, zoneInfo);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error in SetupSelectedCharacterLegacyAsync: {ex.Message}");
        }
    }

    private async Task<PlayerZoneInfo> GetPlayerZoneInfoInternalAsync(int characterID)
    {
        try
        {
            // Use NetworkRequestManager for cleaner request handling
            PlayerZoneInfoResult result = await playerManager.requestManager.SendPlayerZoneInfoRequestAsync(characterID);
            
            if (result.Success)
            {
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
            // Step 1: Request server to load the zone first
            await RequestServerLoadZoneAsync(zoneInfo.ZoneName);

            // Step 2: Unload MainMenu when transitioning to gameplay zones
            await UnloadMainMenuIfNeeded(zoneInfo.ZoneName);

            // Step 3: Load zone on client side
            await LoadZoneOnClient(zoneInfo.ZoneName);
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

            GameObject controllerGO = GameObject.Instantiate(playerManager.PlayerControllerPrefab);
            PlayerController controller = controllerGO.GetComponent<PlayerController>();
            
            if (controller == null)
            {
                Debug.LogError("ZoneCoordinator: PlayerController component not found on instantiated prefab!");
                GameObject.Destroy(controllerGO);
                return;
            }

            // Step 3: Setup controller
            controller.SetCharacterPosition(spawnPosition);

            // TODO: Link controller to selected character
            // controller.SetPlayerStatBlock(selectedCharacter);
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
        // Use NetworkRequestManager for cleaner request handling
        ServerZoneLoadResult result = await playerManager.requestManager.SendServerZoneLoadRequestAsync(zoneName);

        if (!result.Success)
        {
            Debug.LogError($"ZoneCoordinator: Server failed to load zone '{zoneName}': {result.ErrorMessage}");
            throw new Exception($"Server zone load failed: {result.ErrorMessage}");
        }Debug.Log($"ZoneCoordinator: Server successfully loaded zone '{zoneName}'");        
    }

    private async Task UnloadMainMenuIfNeeded(string zoneName)
    {

    }

    private async Task LoadZoneOnClient(string zoneName)
    {
       
    }
    #endregion

    #region Public Interface for PlayerManager
    public async Task<PlayerZoneInfo> GetPlayerZoneInfoAsync(int characterID)
    {
        return await GetPlayerZoneInfoInternalAsync(characterID);
    }
    public async Task<Vector3> GetSpawnPositionAsync(int characterID)
    {
        try
        {
            PlayerZoneInfo zoneInfo = await GetPlayerZoneInfoInternalAsync(characterID);
            Vector3 spawnPosition = await DetermineSpawnPositionAsync(zoneInfo);
            
            return spawnPosition;
        }
        catch (Exception ex)
        {
            Debug.LogError($"ZoneCoordinator: Error getting spawn position for character {characterID}: {ex.Message}");
            return Vector3.zero;
        }
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
            }
            else
            {

                Vector3? waypointPosition = await GetMarketWaypointPositionWithRetryAsync(zoneInfo.ZoneName);
                
                if (waypointPosition.HasValue)
                {
                    spawnPosition = waypointPosition.Value;
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
            
            Vector3? waypointPosition = await GetMarketWaypointPositionAsync(zoneName);
            
            if (waypointPosition.HasValue)
            {
                return waypointPosition;
            }
            
            // Wait before retrying (except on last attempt)
            if (attempt < maxRetries)
            {
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
                return result.WaypointPosition;
            }
            else
            {
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