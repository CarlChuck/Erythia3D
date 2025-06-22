using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Client-side chat manager that coordinates chat functionality between UI and network systems
/// Integrates with existing UIManager and follows the established singleton pattern
/// </summary>
public class ChatManager : MonoBehaviour
{
    #region Singleton
    public static ChatManager Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Chat should persist across scenes
        }
        else if (Instance != this)
        {
            Debug.LogWarning("ChatManager Awake: Another instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    [Header("Chat Configuration")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private int maxLocalHistorySize = 100;
    [SerializeField] private ChatChannel defaultChannel = ChatChannel.Global;

    [Header("UI References")]
    [SerializeField] private ChatUI chatUI;
    [SerializeField] private UIManager uiManager;

    // Chat state
    private Dictionary<ChatChannel, List<ChatMessage>> chatHistory = new Dictionary<ChatChannel, List<ChatMessage>>();
    private HashSet<ChatChannel> subscribedChannels = new HashSet<ChatChannel>();
    private ChatChannel currentActiveChannel = ChatChannel.Global;
    private List<ChannelConfig> availableChannels = new List<ChannelConfig>();
    
    // Initialization state
    private bool isInitialized = false;

    #region Initialization
    private void Start()
    {
        InitializeChatSystem();
    }

    private void InitializeChatSystem()
    {
        LogDebug("Initializing ChatManager...");

        // Initialize chat history for all channels
        foreach (ChatChannel channel in Enum.GetValues(typeof(ChatChannel)))
        {
            chatHistory[channel] = new List<ChatMessage>();
        }

        // Subscribe to default channels
        subscribedChannels.Add(ChatChannel.Global);
        subscribedChannels.Add(ChatChannel.Area);
        subscribedChannels.Add(ChatChannel.Proximity);
        subscribedChannels.Add(ChatChannel.System);

        // Get available channels from ChatNetworkManager if available
        if (ChatNetworkManager.Instance != null)
        {
            availableChannels = ChatNetworkManager.Instance.GetAllChannelConfigs();
        }
        else
        {
            // Fallback: create default channel configs
            CreateDefaultChannelConfigs();
        }

        // Initialize UI if available
        if (chatUI != null)
        {
            chatUI.Initialize(this);
        }
        else
        {
            LogDebug("ChatUI reference not set - UI will need to be initialized manually");
        }

        currentActiveChannel = defaultChannel;
        isInitialized = true;

        LogDebug("ChatManager initialization complete");
    }

    private void CreateDefaultChannelConfigs()
    {
        availableChannels = new List<ChannelConfig>
        {
            new ChannelConfig(ChatChannel.Global),
            new ChannelConfig(ChatChannel.Area),
            new ChannelConfig(ChatChannel.Proximity),
            new ChannelConfig(ChatChannel.System)
        };
    }
    #endregion

    #region Message Handling
    /// <summary>
    /// Receives a chat message from the network layer
    /// </summary>
    public void ReceiveMessage(ChatMessage message)
    {
        if (!isInitialized)
        {
            LogDebug("ChatManager not initialized, queuing message");
            // Could implement a message queue here if needed
            return;
        }

        LogDebug($"Received message in channel {message.channel}: {message.content}");

        // Store message in history
        StoreMessageInHistory(message);

        // Update UI
        if (chatUI != null)
        {
            chatUI.DisplayMessage(message);
        }

        // Trigger any message received events
        OnMessageReceived?.Invoke(message);
    }

    /// <summary>
    /// Sends a chat message through the network layer
    /// </summary>
    public void SendMessage(string content, ChatChannel channel = ChatChannel.Global)
    {
        if (!isInitialized)
        {
            LogDebug("ChatManager not initialized, cannot send message");
            return;
        }

        if (string.IsNullOrEmpty(content.Trim()))
        {
            LogDebug("Cannot send empty message");
            return;
        }

        // Validate channel subscription
        if (!subscribedChannels.Contains(channel))
        {
            LogDebug($"Not subscribed to channel {channel}, cannot send message");
            return;
        }

        // Get player name from PlayerManager
        string playerName = GetCurrentPlayerName();
        Vector3 playerPosition = GetCurrentPlayerPosition();
        int playerAreaId = GetCurrentPlayerAreaId();

        // Create message
        var message = new ChatMessage(
            senderId: 0, // Will be set by server
            senderName: playerName,
            content: content,
            channel: channel,
            senderPosition: playerPosition,
            areaId: playerAreaId
        );

        // Send through PlayerManager
        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null)
        {
            localPlayerManager.SendChatMessageRpc(message);
            LogDebug($"Sent message to channel {channel}: {content}");
        }
        else
        {
            LogDebug("LocalPlayerManager not found, cannot send message");
        }
    }
    #endregion

    #region Channel Management
    /// <summary>
    /// Handles notification that player joined a channel
    /// </summary>
    public void OnChannelJoined(ChatChannel channel)
    {
        bool wasAdded = subscribedChannels.Add(channel);
        if (wasAdded)
        {
            LogDebug($"Joined channel: {channel}");
            
            // Update UI
            if (chatUI != null)
            {
                chatUI.OnChannelSubscriptionChanged(channel, true);
            }

            OnChannelSubscriptionChanged?.Invoke(channel, true);
        }
    }

    /// <summary>
    /// Handles notification that player left a channel
    /// </summary>
    public void OnChannelLeft(ChatChannel channel)
    {
        bool wasRemoved = subscribedChannels.Remove(channel);
        if (wasRemoved)
        {
            LogDebug($"Left channel: {channel}");
            
            // Update UI
            if (chatUI != null)
            {
                chatUI.OnChannelSubscriptionChanged(channel, false);
            }

            // Switch to a different channel if this was the active one
            if (currentActiveChannel == channel && subscribedChannels.Count > 0)
            {
                SwitchToChannel(subscribedChannels.First());
            }

            OnChannelSubscriptionChanged?.Invoke(channel, false);
        }
    }

    /// <summary>
    /// Requests to join a chat channel
    /// </summary>
    public void JoinChannel(ChatChannel channel)
    {
        if (subscribedChannels.Contains(channel))
        {
            LogDebug($"Already subscribed to channel {channel}");
            return;
        }

        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null)
        {
            localPlayerManager.JoinChatChannelRpc(channel);
            LogDebug($"Requested to join channel: {channel}");
        }
    }

    /// <summary>
    /// Requests to leave a chat channel
    /// </summary>
    public void LeaveChannel(ChatChannel channel)
    {
        if (!subscribedChannels.Contains(channel))
        {
            LogDebug($"Not subscribed to channel {channel}");
            return;
        }

        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null)
        {
            localPlayerManager.LeaveChatChannelRpc(channel);
            LogDebug($"Requested to leave channel: {channel}");
        }
    }

    /// <summary>
    /// Switches the active chat channel
    /// </summary>
    public void SwitchToChannel(ChatChannel channel)
    {
        if (!subscribedChannels.Contains(channel))
        {
            LogDebug($"Cannot switch to unsubscribed channel {channel}");
            return;
        }

        currentActiveChannel = channel;
        LogDebug($"Switched to channel: {channel}");

        // Update UI
        if (chatUI != null)
        {
            chatUI.OnActiveChannelChanged(channel);
        }

        OnActiveChannelChanged?.Invoke(channel);
    }
    #endregion

    #region History Management
    private void StoreMessageInHistory(ChatMessage message)
    {
        if (!chatHistory.ContainsKey(message.channel))
        {
            chatHistory[message.channel] = new List<ChatMessage>();
        }

        var history = chatHistory[message.channel];
        history.Add(message);

        // Limit history size
        if (history.Count > maxLocalHistorySize)
        {
            history.RemoveRange(0, history.Count - maxLocalHistorySize);
        }
    }

    /// <summary>
    /// Gets chat history for a specific channel
    /// </summary>
    public List<ChatMessage> GetChannelHistory(ChatChannel channel)
    {
        if (chatHistory.TryGetValue(channel, out var history))
        {
            return new List<ChatMessage>(history); // Return copy
        }
        return new List<ChatMessage>();
    }

    /// <summary>
    /// Clears chat history for a specific channel
    /// </summary>
    public void ClearChannelHistory(ChatChannel channel)
    {
        if (chatHistory.ContainsKey(channel))
        {
            chatHistory[channel].Clear();
            LogDebug($"Cleared history for channel: {channel}");
        }
    }
    #endregion

    #region Helper Methods
    private string GetCurrentPlayerName()
    {
        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null && localPlayerManager.SelectedPlayerCharacter != null)
        {
            return localPlayerManager.SelectedPlayerCharacter.GetCharacterName();
        }
        return "Unknown Player";
    }

    private Vector3 GetCurrentPlayerPosition()
    {
        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null)
        {
            return localPlayerManager.LastKnownPosition;
        }
        return Vector3.zero;
    }

    private int GetCurrentPlayerAreaId()
    {
        var localPlayerManager = PlayerManager.LocalInstance;
        if (localPlayerManager != null)
        {
            return localPlayerManager.CurrentAreaId;
        }
        return 0;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ChatManager] {message}");
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Gets the currently active chat channel
    /// </summary>
    public ChatChannel GetActiveChannel()
    {
        return currentActiveChannel;
    }

    /// <summary>
    /// Gets all channels the player is subscribed to
    /// </summary>
    public HashSet<ChatChannel> GetSubscribedChannels()
    {
        return new HashSet<ChatChannel>(subscribedChannels);
    }

    /// <summary>
    /// Gets all available channel configurations
    /// </summary>
    public List<ChannelConfig> GetAvailableChannels()
    {
        return new List<ChannelConfig>(availableChannels);
    }

    /// <summary>
    /// Gets channel configuration for a specific channel
    /// </summary>
    public ChannelConfig GetChannelConfig(ChatChannel channel)
    {
        return availableChannels.FirstOrDefault(c => c.channelType == channel);
    }

    /// <summary>
    /// Sets the chat UI reference (for runtime initialization)
    /// </summary>
    public void SetChatUI(ChatUI chatUIInstance)
    {
        chatUI = chatUIInstance;
        if (isInitialized && chatUI != null)
        {
            chatUI.Initialize(this);
        }
    }

    /// <summary>
    /// Checks if the chat system is initialized
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }
    #endregion

    #region Events
    /// <summary>
    /// Event triggered when a message is received
    /// </summary>
    public event Action<ChatMessage> OnMessageReceived;

    /// <summary>
    /// Event triggered when channel subscription changes
    /// </summary>
    public event Action<ChatChannel, bool> OnChannelSubscriptionChanged;

    /// <summary>
    /// Event triggered when active channel changes
    /// </summary>
    public event Action<ChatChannel> OnActiveChannelChanged;
    #endregion
}
/// <summary>
/// Enum representing different chat channels available in the game
/// </summary>
public enum ChatChannel : byte
{
    Global = 0,
    Area = 1,
    Proximity = 2,
    Guild = 3,      // Future extension
    Whisper = 4,    // Future extension
    System = 5      // System messages
}

/// <summary>
/// Enum representing the priority level of chat messages
/// </summary>
public enum ChatPriority : byte
{
    Low = 0,
    Normal = 1,
    High = 2,
    System = 3
}