using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side chat network manager that handles message distribution and filtering
/// Integrates with existing ServerManager and area management system
/// </summary>
public class ChatNetworkManager : NetworkBehaviour
{
    #region Singleton
    public static ChatNetworkManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple ChatNetworkManager instances detected. Destroying this one on GameObject: {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("Chat Configuration")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private List<ChannelConfig> channelConfigurations = new List<ChannelConfig>();
    
    [Header("Rate Limiting")]
    [SerializeField] private int maxMessagesPerMinute = 30;
    [SerializeField] private float messageCooldownSeconds = 1f;

    // Chat state tracking
    private readonly Dictionary<ulong, List<ChatMessage>> playerMessageHistory = new Dictionary<ulong, List<ChatMessage>>();
    private readonly Dictionary<ulong, float> playerLastMessageTime = new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, HashSet<ChatChannel>> playerChannelSubscriptions = new Dictionary<ulong, HashSet<ChatChannel>>();
    private readonly Dictionary<ChatChannel, ChannelConfig> channelConfigLookup = new Dictionary<ChatChannel, ChannelConfig>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsServer)
        {
            LogDebug("ChatNetworkManager spawned on client - disabling server-only functionality");
            return;
        }

        LogDebug("ChatNetworkManager spawned on server - initializing chat system");
        InitializeChannelConfigurations();
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        base.OnNetworkDespawn();
    }

    #region Initialization
    private void InitializeChannelConfigurations()
    {
        // Initialize default channel configurations if none are set
        if (channelConfigurations.Count == 0)
        {
            channelConfigurations.Add(new ChannelConfig(ChatChannel.Global));
            channelConfigurations.Add(new ChannelConfig(ChatChannel.Area));
            channelConfigurations.Add(new ChannelConfig(ChatChannel.Proximity));
            channelConfigurations.Add(new ChannelConfig(ChatChannel.System));
        }

        // Build lookup dictionary
        channelConfigLookup.Clear();
        foreach (var config in channelConfigurations)
        {
            if (config.IsValid())
            {
                channelConfigLookup[config.channelType] = config;
                LogDebug($"Registered chat channel: {config.channelName} ({config.channelType})");
            }
            else
            {
                Debug.LogWarning($"Invalid channel configuration for {config.channelType}");
            }
        }
    }
    #endregion

    #region Client Connection Management
    private void OnClientConnected(ulong clientId)
    {
        LogDebug($"Client {clientId} connected - initializing chat state");
        
        // Initialize player chat state
        if (!playerMessageHistory.ContainsKey(clientId))
        {
            playerMessageHistory[clientId] = new List<ChatMessage>();
        }
        
        if (!playerChannelSubscriptions.ContainsKey(clientId))
        {
            playerChannelSubscriptions[clientId] = new HashSet<ChatChannel>();
            // Auto-subscribe to default channels
            playerChannelSubscriptions[clientId].Add(ChatChannel.Global);
            playerChannelSubscriptions[clientId].Add(ChatChannel.Area);
            playerChannelSubscriptions[clientId].Add(ChatChannel.Proximity);
            playerChannelSubscriptions[clientId].Add(ChatChannel.System);
        }
        
        playerLastMessageTime[clientId] = 0f;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        LogDebug($"Client {clientId} disconnected - cleaning up chat state");
        
        // Clean up player chat state
        playerMessageHistory.Remove(clientId);
        playerChannelSubscriptions.Remove(clientId);
        playerLastMessageTime.Remove(clientId);
    }
    #endregion

    #region Message Processing
    /// <summary>
    /// Processes incoming chat message from client
    /// </summary>
    [Rpc(SendTo.Server)]
    public void SendChatMessageServerRpc(ChatMessage message, ulong senderId)
    {
        if (!ValidateMessage(message, senderId))
        {
            LogDebug($"Message validation failed for client {senderId}");
            return;
        }

        // Update message with server-side data
        message.senderId = senderId;
        message.timestamp = Time.time;

        // Get sender's current area and position from ServerManager if available
        if (ServerManager.Instance != null)
        {
            message.areaId = ServerManager.Instance.GetClientCurrentArea(senderId) ?? 0;
            message.senderPosition = GetPlayerPosition(senderId);
        }

        LogDebug($"Processing chat message from client {senderId} in channel {message.channel}: {message.content}");

        // Store message in history
        StoreMessageInHistory(senderId, message);

        // Get eligible recipients and send message
        var eligibleClients = GetEligibleClients(message);
        if (eligibleClients.Count > 0)
        {
            SendMessageToClients(message, eligibleClients);
            LogDebug($"Sent chat message to {eligibleClients.Count} clients");
        }
        else
        {
            LogDebug("No eligible clients found for message");
        }
    }

    /// <summary>
    /// Sends chat message to specified clients via PlayerManager
    /// </summary>
    private void SendMessageToClients(ChatMessage message, List<ulong> clientIds)
    {
        foreach (ulong clientId in clientIds)
        {
            // Find the PlayerManager for this client
            var playerManagers = FindObjectsOfType<PlayerManager>();
            var playerManager = playerManagers.FirstOrDefault(pm => pm.OwnerClientId == clientId);
            
            if (playerManager != null)
            {
                playerManager.ReceiveChatMessageRpc(message);
            }
        }
    }
    #endregion

    #region Channel Management
    /// <summary>
    /// Handles client joining a specific chat channel
    /// </summary>
    [Rpc(SendTo.Server)]
    public void JoinChannelServerRpc(ChatChannel channel, ulong clientId)
    {
        
        if (!channelConfigLookup.ContainsKey(channel))
        {
            LogDebug($"Client {clientId} tried to join unknown channel {channel}");
            return;
        }

        var config = channelConfigLookup[channel];
        if (!config.isEnabled)
        {
            LogDebug($"Client {clientId} tried to join disabled channel {channel}");
            return;
        }

        if (!playerChannelSubscriptions.ContainsKey(clientId))
        {
            playerChannelSubscriptions[clientId] = new HashSet<ChatChannel>();
        }

        bool wasAdded = playerChannelSubscriptions[clientId].Add(channel);
        if (wasAdded)
        {
            LogDebug($"Client {clientId} joined channel {channel}");
            
            // Notify client via PlayerManager
            var playerManagers = FindObjectsOfType<PlayerManager>();
            var playerManager = playerManagers.FirstOrDefault(pm => pm.OwnerClientId == clientId);
            if (playerManager != null)
            {
                playerManager.NotifyChannelJoinedRpc(channel);
            }
        }
    }

    /// <summary>
    /// Handles client leaving a specific chat channel
    /// </summary>
    [Rpc(SendTo.Server)]
    public void LeaveChannelServerRpc(ChatChannel channel, ulong clientId)
    {
        
        if (playerChannelSubscriptions.ContainsKey(clientId))
        {
            bool wasRemoved = playerChannelSubscriptions[clientId].Remove(channel);
            if (wasRemoved)
            {
                LogDebug($"Client {clientId} left channel {channel}");
                
                // Notify client via PlayerManager
                var playerManagers = FindObjectsOfType<PlayerManager>();
                var playerManager = playerManagers.FirstOrDefault(pm => pm.OwnerClientId == clientId);
                if (playerManager != null)
                {
                    playerManager.NotifyChannelLeftRpc(channel);
                }
            }
        }
    }

    #endregion

    #region Message Validation and Filtering
    private bool ValidateMessage(ChatMessage message, ulong senderId)
    {
        // Basic message validation
        if (!message.IsValid())
        {
            return false;
        }

        // Check if channel exists and is enabled
        if (!channelConfigLookup.TryGetValue(message.channel, out var channelConfig) || !channelConfig.isEnabled)
        {
            return false;
        }

        // Rate limiting check
        if (playerLastMessageTime.ContainsKey(senderId))
        {
            float timeSinceLastMessage = Time.time - playerLastMessageTime[senderId];
            if (timeSinceLastMessage < messageCooldownSeconds)
            {
                LogDebug($"Rate limit exceeded for client {senderId}");
                return false;
            }
        }

        // Check message length
        if (message.content.Length > channelConfig.maxMessageLength)
        {
            return false;
        }

        // Update last message time
        playerLastMessageTime[senderId] = Time.time;

        return true;
    }

    private List<ulong> GetEligibleClients(ChatMessage message)
    {
        var eligibleClients = new List<ulong>();

        foreach (var kvp in playerChannelSubscriptions)
        {
            ulong clientId = kvp.Key;
            var subscribedChannels = kvp.Value;

            // Skip if client is not subscribed to this channel
            if (!subscribedChannels.Contains(message.channel))
            {
                continue;
            }

            // Apply channel-specific filtering
            if (IsClientEligibleForMessage(clientId, message))
            {
                eligibleClients.Add(clientId);
            }
        }

        return eligibleClients;
    }

    private bool IsClientEligibleForMessage(ulong clientId, ChatMessage message)
    {
        if (!channelConfigLookup.TryGetValue(message.channel, out var channelConfig))
        {
            return false;
        }

        switch (message.channel)
        {
            case ChatChannel.Global:
                return true; // Global messages go to everyone subscribed

            case ChatChannel.Area:
                // Area-based filtering using ServerManager
                if (ServerManager.Instance != null)
                {
                    int? clientArea = ServerManager.Instance.GetClientCurrentArea(clientId);
                    return clientArea.HasValue && clientArea.Value == message.areaId;
                }
                return true; // Fallback if ServerManager not available

            case ChatChannel.Proximity:
                // Proximity-based filtering
                Vector3 clientPosition = GetPlayerPosition(clientId);
                float distance = Vector3.Distance(clientPosition, message.senderPosition);
                return distance <= channelConfig.proximityRange;

            case ChatChannel.System:
                return true; // System messages go to everyone subscribed

            default:
                return false; // Unknown channels are blocked
        }
    }
    #endregion

    #region Helper Methods
    private void StoreMessageInHistory(ulong senderId, ChatMessage message)
    {
        if (!playerMessageHistory.ContainsKey(senderId))
        {
            playerMessageHistory[senderId] = new List<ChatMessage>();
        }

        var history = playerMessageHistory[senderId];
        history.Add(message);

        // Limit history size
        const int maxHistorySize = 100;
        if (history.Count > maxHistorySize)
        {
            history.RemoveRange(0, history.Count - maxHistorySize);
        }
    }

    private Vector3 GetPlayerPosition(ulong clientId)
    {
        // Try to get position from PlayerManager
        var playerManagers = FindObjectsOfType<PlayerManager>();
        var playerManager = playerManagers.FirstOrDefault(pm => pm.OwnerClientId == clientId);
        
        if (playerManager != null)
        {
            return playerManager.transform.position;
        }

        return Vector3.zero; // Fallback position
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ChatNetworkManager] {message}");
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Gets the configuration for a specific channel
    /// </summary>
    public ChannelConfig GetChannelConfig(ChatChannel channel)
    {
        channelConfigLookup.TryGetValue(channel, out var config);
        return config;
    }

    /// <summary>
    /// Gets all available channel configurations
    /// </summary>
    public List<ChannelConfig> GetAllChannelConfigs()
    {
        return channelConfigurations.Where(c => c.IsValid()).ToList();
    }

    /// <summary>
    /// Sends a system message to specific clients
    /// </summary>
    public void SendSystemMessage(string content, List<ulong> targetClients = null)
    {
        if (!IsServer) return;

        var systemMessage = new ChatMessage(
            senderId: 0, // System sender ID
            senderName: "System",
            content: content,
            channel: ChatChannel.System,
            senderPosition: Vector3.zero,
            areaId: 0,
            priority: ChatPriority.System
        );

        var eligibleClients = targetClients ?? NetworkManager.Singleton.ConnectedClientsList.Select(c => c.ClientId).ToList();
        
        if (eligibleClients.Count > 0)
        {
            SendMessageToClients(systemMessage, eligibleClients);
        }
    }
    #endregion
}