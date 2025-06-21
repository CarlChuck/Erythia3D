using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for NetworkBehaviours that should be culled based on player area
/// This implements area-based network culling for the single-server architecture
/// </summary>
public abstract class AreaNetworkBehaviour : NetworkBehaviour
{
    [Header("Area Culling Configuration")]
    [SerializeField] protected int areaId;
    [SerializeField] protected bool enableAreaCulling = true;
    
    public int AreaId
    {
        get
        {
            return areaId; 
            
        }
        set
        {
            areaId = value; 
            
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer || !enableAreaCulling)
        {
            return;
        }

        // Set up area-based culling
        NetworkObject.CheckObjectVisibility = CheckAreaBasedVisibility;
            
        if (areaId == 0)
        {
            Debug.LogWarning($"AreaNetworkBehaviour on {gameObject.name} has no areaId assigned (areaId=0). Defaulting to visible to all clients.");
        }
    }

    /// <summary>
    /// Check if this object should be visible to a specific client based on their current area
    /// </summary>
    private bool CheckAreaBasedVisibility(ulong clientId)
    {
        // Always visible on server
        if (!IsServer)
        {
            return true;
        }

        // If area culling is disabled, always visible
        if (!enableAreaCulling)
        {
            return true;
        }

        // If no area assigned, visible to all (with warning)
        if (areaId == 0)
        {
            return true;
        }

        // Get client's current area from ServerManager
        if (ServerManager.Instance != null)
        {
            int? clientArea = ServerManager.Instance.GetClientCurrentArea(clientId);
            bool shouldBeVisible = clientArea.HasValue && clientArea.Value == areaId;
            
            // Optional debug logging for visibility checks
            if (Debug.isDebugBuild && Time.frameCount % 300 == 0) // Log every ~5 seconds at 60fps
            {
                Debug.Log($"[AreaCulling] Object {gameObject.name} (Area: {areaId}) -> Client {clientId} (Area: {clientArea?.ToString() ?? "None"}): {(shouldBeVisible ? "Visible" : "Hidden")}");
            }
            
            return shouldBeVisible;
        }
        
        // Fallback: if no ServerManager, show to all clients
        Debug.LogWarning($"AreaNetworkBehaviour on {gameObject.name} cannot find ServerManager instance. Defaulting to visible.");
        return true;
    }

    /// <summary>
    /// Force a visibility update for all clients
    /// Call this when the object's area assignment changes
    /// </summary>
    private void RefreshVisibility()
    {
        if (IsServer && NetworkObject != null)
        {
            NetworkObject.CheckObjectVisibility = CheckAreaBasedVisibility;
        }
    }

    /// <summary>
    /// Change the area this object belongs to and refresh visibility
    /// </summary>
    public void SetArea(int newAreaId)
    {
        if (!IsServer)
        {
            return;
        }

        int oldAreaId = areaId;
        areaId = newAreaId;
            
        Debug.Log($"Changed object {gameObject.name} area from '{oldAreaId}' to '{newAreaId}'");
        RefreshVisibility();
    }

    /// <summary>
    /// Get all clients that should be able to see this object
    /// </summary>
    private System.Collections.Generic.List<ulong> GetVisibleClients()
    {
        if (!IsServer || ServerManager.Instance == null)
        {
            return new System.Collections.Generic.List<ulong>();
        }

        return ServerManager.Instance.GetClientsInArea(areaId);
    }

    #region Debug Helpers
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo || !Application.isPlaying)
        {
            return;
        }

        // Draw area information in scene view
        GUIStyle style = new();
        style.normal.textColor = Color.yellow;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Area: {areaId}", style);

        if (!IsServer || NetworkObject == null)
        {
            return;
        }

        var visibleClients = GetVisibleClients();
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"Visible to {visibleClients.Count} clients", style);
    }
    #endregion
}