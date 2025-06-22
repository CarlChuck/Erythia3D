using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Server-side message filtering logic that integrates with existing ServerManager area mappings
/// Handles channel-specific filtering rules for chat message distribution
/// </summary>
public static class MessageFilter
{
    /// <summary>
    /// Gets list of client IDs that should receive the specified message
    /// Uses existing ServerManager area mappings for efficient filtering
    /// </summary>
    public static List<ulong> GetEligibleClients(ChatMessage message, List<ulong> allConnectedClients)
    {
        var eligibleClients = new List<ulong>();

        foreach (ulong clientId in allConnectedClients)
        {
            if (IsClientEligibleForMessage(clientId, message))
            {
                eligibleClients.Add(clientId);
            }
        }

        return eligibleClients;
    }

    /// <summary>
    /// Determines if a specific client should receive the given message
    /// </summary>
    public static bool IsClientEligibleForMessage(ulong clientId, ChatMessage message)
    {
        switch (message.channel)
        {
            case ChatChannel.Global:
                return true; // Global messages go to everyone

            case ChatChannel.Area:
                return IsClientInSameArea(clientId, message.areaId);

            case ChatChannel.Proximity:
                return IsClientInProximity(clientId, message.senderPosition, GetProximityRange(message.channel));

            case ChatChannel.System:
                return true; // System messages go to everyone

            case ChatChannel.Guild:
                // Future implementation - check guild membership
                return false; // Placeholder

            case ChatChannel.Whisper:
                // Future implementation - check if message is addressed to this client
                return false; // Placeholder

            default:
                return false; // Unknown channels are blocked
        }
    }

    /// <summary>
    /// Checks if client is in the same area as the message
    /// Uses existing ServerManager area mapping
    /// </summary>
    public static bool IsClientInSameArea(ulong clientId, int messageAreaId)
    {
        if (ServerManager.Instance == null)
        {
            Debug.LogWarning("MessageFilter: ServerManager instance not found for area checking");
            return true; // Fallback to allow message
        }

        int? clientArea = ServerManager.Instance.GetClientCurrentArea(clientId);
        return clientArea.HasValue && clientArea.Value == messageAreaId;
    }

    /// <summary>
    /// Checks if client is within proximity range of the message sender
    /// </summary>
    public static bool IsClientInProximity(ulong clientId, Vector3 senderPosition, float proximityRange)
    {
        Vector3 clientPosition = GetClientPosition(clientId);
        float distance = Vector3.Distance(clientPosition, senderPosition);
        return distance <= proximityRange;
    }

    /// <summary>
    /// Gets the current position of a client
    /// Uses PlayerManager instances to find client position
    /// </summary>
    public static Vector3 GetClientPosition(ulong clientId)
    {
        // Find PlayerManager for this client
        var playerManagers = Object.FindObjectsOfType<PlayerManager>();
        var playerManager = playerManagers.FirstOrDefault(pm => pm.OwnerClientId == clientId);
        
        if (playerManager != null)
        {
            return playerManager.LastKnownPosition;
        }

        Debug.LogWarning($"MessageFilter: Could not find PlayerManager for client {clientId}");
        return Vector3.zero; // Fallback position
    }

    /// <summary>
    /// Gets the proximity range for a given channel
    /// Uses ChatNetworkManager channel configurations
    /// </summary>
    public static float GetProximityRange(ChatChannel channel)
    {
        if (ChatNetworkManager.Instance != null)
        {
            var channelConfig = ChatNetworkManager.Instance.GetChannelConfig(channel);
            if (channelConfig != null)
            {
                return channelConfig.proximityRange;
            }
        }

        // Fallback default ranges
        switch (channel)
        {
            case ChatChannel.Proximity:
                return 50f;
            case ChatChannel.Whisper:
                return 10f;
            default:
                return float.MaxValue; // No proximity limit for other channels
        }
    }

    /// <summary>
    /// Validates if a message should be processed at all
    /// Performs basic validation and anti-spam checks
    /// </summary>
    public static bool IsValidMessage(ChatMessage message, ulong senderId)
    {
        // Basic message validation
        if (!message.IsValid())
        {
            return false;
        }

        // Check if sender is in a valid area (if area-based channel)
        if (message.channel == ChatChannel.Area && message.areaId <= 0)
        {
            Debug.LogWarning($"MessageFilter: Invalid area ID {message.areaId} for area chat from client {senderId}");
            return false;
        }

        // Check if sender position is reasonable (for proximity channels)
        if (message.channel == ChatChannel.Proximity)
        {
            // Basic sanity check - position shouldn't be at origin unless that's actually valid
            if (message.senderPosition == Vector3.zero)
            {
                Debug.LogWarning($"MessageFilter: Suspicious sender position for proximity chat from client {senderId}");
                // Don't block entirely, but log for monitoring
            }
        }

        return true;
    }

    /// <summary>
    /// Gets all clients in a specific area using ServerManager mappings
    /// </summary>
    public static List<ulong> GetClientsInArea(int areaId)
    {
        if (ServerManager.Instance != null)
        {
            return ServerManager.Instance.GetClientsInArea(areaId);
        }

        Debug.LogWarning("MessageFilter: ServerManager instance not found for area client lookup");
        return new List<ulong>();
    }

    /// <summary>
    /// Gets all clients within proximity of a position
    /// </summary>
    public static List<ulong> GetClientsInProximity(Vector3 position, float range, List<ulong> candidateClients)
    {
        var proximityClients = new List<ulong>();

        foreach (ulong clientId in candidateClients)
        {
            if (IsClientInProximity(clientId, position, range))
            {
                proximityClients.Add(clientId);
            }
        }

        return proximityClients;
    }
}