using UnityEngine;

/// <summary>
/// Configuration data for chat channels
/// Defines behavior and appearance of different chat channels
/// </summary>
[System.Serializable]
public class ChannelConfig
{
    [Header("Channel Identity")]
    public ChatChannel channelType;
    public string channelName;
    public string channelPrefix;
    public Color channelColor = Color.white;

    [Header("Channel Behavior")]
    public bool isEnabled = true;
    public bool requiresOptIn = false;
    public bool showInChannelList = true;
    
    [Header("Filtering Settings")]
    public float proximityRange = 50f;
    public string requiredAreaTag = "";
    public int maxMessageLength = 512;
    
    [Header("Rate Limiting")]
    public float messageCooldown = 1f;
    public int maxMessagesPerMinute = 30;

    /// <summary>
    /// Default constructor
    /// </summary>
    public ChannelConfig()
    {
        channelType = ChatChannel.Global;
        channelName = "Global";
        channelPrefix = "[G]";
        channelColor = Color.white;
    }

    /// <summary>
    /// Constructor with channel type
    /// </summary>
    public ChannelConfig(ChatChannel type)
    {
        channelType = type;
        SetDefaultsForChannel(type);
    }

    /// <summary>
    /// Sets appropriate defaults based on channel type
    /// </summary>
    private void SetDefaultsForChannel(ChatChannel type)
    {
        switch (type)
        {
            case ChatChannel.Global:
                channelName = "Global";
                channelPrefix = "[G]";
                channelColor = Color.white;
                proximityRange = float.MaxValue;
                requiresOptIn = false;
                maxMessagesPerMinute = 10;
                break;
                
            case ChatChannel.Area:
                channelName = "Area";
                channelPrefix = "[A]";
                channelColor = Color.cyan;
                proximityRange = float.MaxValue;
                requiresOptIn = false;
                maxMessagesPerMinute = 20;
                break;
                
            case ChatChannel.Proximity:
                channelName = "Local";
                channelPrefix = "[L]";
                channelColor = Color.yellow;
                proximityRange = 50f;
                requiresOptIn = false;
                maxMessagesPerMinute = 30;
                break;
                
            case ChatChannel.Guild:
                channelName = "Guild";
                channelPrefix = "[GLD]";
                channelColor = Color.green;
                proximityRange = float.MaxValue;
                requiresOptIn = true;
                maxMessagesPerMinute = 20;
                break;
                
            case ChatChannel.Whisper:
                channelName = "Whisper";
                channelPrefix = "[W]";
                channelColor = Color.magenta;
                proximityRange = float.MaxValue;
                requiresOptIn = false;
                maxMessagesPerMinute = 15;
                break;
                
            case ChatChannel.System:
                channelName = "System";
                channelPrefix = "[SYS]";
                channelColor = Color.red;
                proximityRange = float.MaxValue;
                requiresOptIn = false;
                maxMessagesPerMinute = 100;
                showInChannelList = false;
                break;
        }
    }

    /// <summary>
    /// Validates the channel configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(channelName) && 
               !string.IsNullOrEmpty(channelPrefix) &&
               proximityRange > 0 &&
               maxMessageLength > 0 &&
               maxMessagesPerMinute > 0;
    }
}