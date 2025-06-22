using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component for displaying individual chat messages
/// Can be attached to chat message prefabs for enhanced formatting
/// </summary>
public class ChatMessageUI : MonoBehaviour
{
    [Header("Message Components")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text senderNameText;
    [SerializeField] private TMP_Text timestampText;
    [SerializeField] private TMP_Text channelPrefixText;

    [Header("Visual Components")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image channelColorIndicator;

    [Header("Configuration")]
    [SerializeField] private bool showTimestamp = true;
    [SerializeField] private bool showChannelPrefix = true;
    [SerializeField] private string timestampFormat = "HH:mm";

    private ChatMessage currentMessage;

    /// <summary>
    /// Sets the message data and updates the UI display
    /// </summary>
    public void SetMessage(ChatMessage message)
    {
        currentMessage = message;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        // Set main message text
        if (messageText != null)
        {
            messageText.text = currentMessage.content.ToString();
        }

        // Set sender name
        if (senderNameText != null)
        {
            senderNameText.text = currentMessage.senderName.ToString();
        }

        // Set timestamp
        if (timestampText != null && showTimestamp)
        {
            var messageTime = System.DateTimeOffset.FromUnixTimeSeconds((long)currentMessage.timestamp);
            timestampText.text = messageTime.ToString(timestampFormat);
            timestampText.gameObject.SetActive(true);
        }
        else if (timestampText != null)
        {
            timestampText.gameObject.SetActive(false);
        }

        // Set channel prefix
        if (channelPrefixText != null && showChannelPrefix)
        {
            var channelConfig = GetChannelConfig(currentMessage.channel);
            if (channelConfig != null)
            {
                channelPrefixText.text = channelConfig.channelPrefix;
                channelPrefixText.color = channelConfig.channelColor;
            }
            else
            {
                channelPrefixText.text = $"[{currentMessage.channel}]";
            }
            channelPrefixText.gameObject.SetActive(true);
        }
        else if (channelPrefixText != null)
        {
            channelPrefixText.gameObject.SetActive(false);
        }

        // Set channel color indicator
        if (channelColorIndicator != null)
        {
            var channelConfig = GetChannelConfig(currentMessage.channel);
            if (channelConfig != null)
            {
                channelColorIndicator.color = channelConfig.channelColor;
            }
            else
            {
                channelColorIndicator.color = Color.white;
            }
        }

        // Set background based on message priority or channel
        if (backgroundImage != null)
        {
            UpdateBackground();
        }

        // Apply any special formatting based on message type
        ApplySpecialFormatting();
    }

    private void UpdateBackground()
    {
        Color backgroundColor = Color.clear;

        switch (currentMessage.priority)
        {
            case ChatPriority.System:
                backgroundColor = new Color(1f, 0f, 0f, 0.1f); // Light red for system messages
                break;
            case ChatPriority.High:
                backgroundColor = new Color(1f, 1f, 0f, 0.05f); // Light yellow for important messages
                break;
            default:
                backgroundColor = Color.clear; // Transparent for normal messages
                break;
        }

        backgroundImage.color = backgroundColor;
    }

    private void ApplySpecialFormatting()
    {
        switch (currentMessage.channel)
        {
            case ChatChannel.System:
                // System messages get bold formatting
                if (messageText != null)
                {
                    messageText.fontStyle = FontStyles.Bold;
                }
                break;

            case ChatChannel.Whisper:
                // Whisper messages get italic formatting
                if (messageText != null)
                {
                    messageText.fontStyle = FontStyles.Italic;
                }
                break;

            default:
                // Normal formatting for other channels
                if (messageText != null)
                {
                    messageText.fontStyle = FontStyles.Normal;
                }
                break;
        }

        // Apply channel color to message text if no specific color is set
        var channelConfig = GetChannelConfig(currentMessage.channel);
        if (channelConfig != null && messageText != null)
        {
            // Use a slightly muted version of the channel color for message text
            Color messageColor = channelConfig.channelColor;
            messageColor.a = 0.9f; // Slightly transparent
            messageText.color = messageColor;
        }
    }

    private ChannelConfig GetChannelConfig(ChatChannel channel)
    {
        if (ChatNetworkManager.Instance != null)
        {
            return ChatNetworkManager.Instance.GetChannelConfig(channel);
        }
        else if (ChatManager.Instance != null)
        {
            return ChatManager.Instance.GetChannelConfig(channel);
        }
        return null;
    }

    /// <summary>
    /// Updates the layout to fit content
    /// </summary>
    public void UpdateLayout()
    {
        // Force layout update
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    /// <summary>
    /// Sets whether to show timestamp
    /// </summary>
    public void SetShowTimestamp(bool show)
    {
        showTimestamp = show;
        if (timestampText != null)
        {
            timestampText.gameObject.SetActive(show);
        }
    }

    /// <summary>
    /// Sets whether to show channel prefix
    /// </summary>
    public void SetShowChannelPrefix(bool show)
    {
        showChannelPrefix = show;
        if (channelPrefixText != null)
        {
            channelPrefixText.gameObject.SetActive(show);
        }
    }

    /// <summary>
    /// Gets the current message data
    /// </summary>
    public ChatMessage GetMessage()
    {
        return currentMessage;
    }
}