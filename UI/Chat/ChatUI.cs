using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chat UI component that integrates with the existing UI system
/// Handles chat display, input, and channel management
/// </summary>
public class ChatUI : MonoBehaviour
{
    [Header("Chat Window Components")]
    [SerializeField] private GameObject chatWindow;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private Transform chatContent;
    [SerializeField] private GameObject chatMessagePrefab;

    [Header("Input Components")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;

    [Header("Channel Components")]
    [SerializeField] private TMP_Dropdown channelDropdown;
    [SerializeField] private Transform channelTabContainer;
    [SerializeField] private GameObject channelTabPrefab;

    [Header("Configuration")]
    [SerializeField] private bool autoScrollToBottom = true;
    [SerializeField] private int maxDisplayedMessages = 100;
    [SerializeField] private float messageDisplayDuration = 300f; // 5 minutes

    // State
    private ChatManager chatManager;
    private bool isInitialized = false;
    private List<GameObject> messageGameObjects = new List<GameObject>();
    private Dictionary<ChatChannel, Toggle> channelTabs = new Dictionary<ChatChannel, Toggle>();

    #region Initialization
    /// <summary>
    /// Initializes the chat UI with the given manager
    /// </summary>
    public void Initialize(ChatManager manager)
    {
        if (isInitialized)
        {
            Debug.LogWarning("[ChatUI] Already initialized, skipping");
            return;
        }

        chatManager = manager;

        // Validate required components
        if (!ValidateComponents())
        {
            Debug.LogError("[ChatUI] Missing required components, cannot initialize");
            return;
        }

        SetupInputHandlers();
        SetupChannelDropdown();
        SetupChannelTabs();
        
        // Initially hide chat window (will be shown when tab is selected)
        if (chatWindow != null)
        {
            chatWindow.SetActive(false);
        }

        isInitialized = true;
        Debug.Log("[ChatUI] Initialized successfully");
    }

    private bool ValidateComponents()
    {
        bool isValid = true;

        if (chatScrollRect == null)
        {
            Debug.LogError("[ChatUI] chatScrollRect is not assigned");
            isValid = false;
        }

        if (chatContent == null)
        {
            Debug.LogError("[ChatUI] chatContent is not assigned");
            isValid = false;
        }

        if (chatInputField == null)
        {
            Debug.LogError("[ChatUI] chatInputField is not assigned");
            isValid = false;
        }

        // Other components are optional but log warnings
        if (chatMessagePrefab == null)
        {
            Debug.LogWarning("[ChatUI] chatMessagePrefab is not assigned - will create basic text objects");
        }

        return isValid;
    }

    private void SetupInputHandlers()
    {
        // Setup input field
        if (chatInputField != null)
        {
            chatInputField.onEndEdit.AddListener(OnInputFieldEndEdit);
            chatInputField.characterLimit = 512; // Match ChatMessage limit
        }

        // Setup send button
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }
    }

    private void SetupChannelDropdown()
    {
        if (channelDropdown == null || chatManager == null)
            return;

        channelDropdown.ClearOptions();
        
        var availableChannels = chatManager.GetAvailableChannels();
        var options = new List<TMP_Dropdown.OptionData>();

        foreach (var channelConfig in availableChannels)
        {
            if (channelConfig.showInChannelList)
            {
                options.Add(new TMP_Dropdown.OptionData(channelConfig.channelName));
            }
        }

        channelDropdown.AddOptions(options);
        channelDropdown.onValueChanged.AddListener(OnChannelDropdownChanged);
    }

    private void SetupChannelTabs()
    {
        if (channelTabContainer == null || chatManager == null)
            return;

        // Clear existing tabs
        foreach (Transform child in channelTabContainer)
        {
            Destroy(child.gameObject);
        }
        channelTabs.Clear();

        // Create tabs for subscribed channels
        var subscribedChannels = chatManager.GetSubscribedChannels();
        foreach (var channel in subscribedChannels)
        {
            CreateChannelTab(channel);
        }
    }

    private void CreateChannelTab(ChatChannel channel)
    {
        if (channelTabContainer == null)
            return;

        GameObject tabObject;
        
        if (channelTabPrefab != null)
        {
            tabObject = Instantiate(channelTabPrefab, channelTabContainer);
        }
        else
        {
            // Create basic tab if no prefab available
            tabObject = new GameObject($"Tab_{channel}");
            tabObject.transform.SetParent(channelTabContainer);
            tabObject.AddComponent<RectTransform>();
        }

        // Setup toggle component
        var toggle = tabObject.GetComponent<Toggle>();
        if (toggle == null)
        {
            toggle = tabObject.AddComponent<Toggle>();
        }

        // Setup label
        var label = tabObject.GetComponentInChildren<TMP_Text>();
        if (label == null)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(tabObject.transform);
            label = labelObject.AddComponent<TMP_Text>();
        }

        var channelConfig = chatManager.GetChannelConfig(channel);
        if (channelConfig != null)
        {
            label.text = channelConfig.channelPrefix;
            label.color = channelConfig.channelColor;
        }
        else
        {
            label.text = channel.ToString();
        }

        // Setup toggle behavior
        toggle.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                chatManager.SwitchToChannel(channel);
            }
        });

        channelTabs[channel] = toggle;

        // Set active if this is the current channel
        if (chatManager.GetActiveChannel() == channel)
        {
            toggle.isOn = true;
        }
    }
    #endregion

    #region Message Display
    /// <summary>
    /// Displays a chat message in the UI
    /// </summary>
    public void DisplayMessage(ChatMessage message)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[ChatUI] Not initialized, cannot display message");
            return;
        }

        CreateMessageUI(message);
        CleanupOldMessages();

        if (autoScrollToBottom)
        {
            ScrollToBottom();
        }
    }

    private void CreateMessageUI(ChatMessage message)
    {
        GameObject messageObject;

        if (chatMessagePrefab != null)
        {
            messageObject = Instantiate(chatMessagePrefab, chatContent);
        }
        else
        {
            // Create basic message object
            messageObject = CreateBasicMessageObject();
        }

        // Setup message content
        var messageComponent = messageObject.GetComponent<ChatMessageUI>();
        if (messageComponent != null)
        {
            messageComponent.SetMessage(message);
        }
        else
        {
            // Fallback: setup basic text
            SetupBasicMessageText(messageObject, message);
        }

        messageGameObjects.Add(messageObject);
    }

    private GameObject CreateBasicMessageObject()
    {
        var messageObject = new GameObject("ChatMessage");
        messageObject.transform.SetParent(chatContent);
        
        var rectTransform = messageObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(0, 30);

        var text = messageObject.AddComponent<TMP_Text>();
        text.fontSize = 14;
        text.color = Color.white;

        return messageObject;
    }

    private void SetupBasicMessageText(GameObject messageObject, ChatMessage message)
    {
        var text = messageObject.GetComponent<TMP_Text>();
        if (text != null)
        {
            var channelConfig = chatManager?.GetChannelConfig(message.channel);
            string prefix = channelConfig?.channelPrefix ?? $"[{message.channel}]";
            
            text.text = $"{prefix} {message.senderName}: {message.content}";
            
            if (channelConfig != null)
            {
                text.color = channelConfig.channelColor;
            }
        }
    }

    private void CleanupOldMessages()
    {
        while (messageGameObjects.Count > maxDisplayedMessages)
        {
            if (messageGameObjects[0] != null)
            {
                Destroy(messageGameObjects[0]);
            }
            messageGameObjects.RemoveAt(0);
        }
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.normalizedPosition = new Vector2(0, 0);
        }
    }
    #endregion

    #region Input Handling
    private void OnInputFieldEndEdit(string text)
    {
        // Send message on Enter key
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendMessage();
        }
    }

    private void OnSendButtonClicked()
    {
        SendMessage();
    }

    private void SendMessage()
    {
        if (chatInputField == null || chatManager == null)
            return;

        string messageText = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(messageText))
            return;

        ChatChannel activeChannel = chatManager.GetActiveChannel();
        chatManager.SendMessage(messageText, activeChannel);

        // Clear input field
        chatInputField.text = "";
        
        // Refocus input field for continued typing
        chatInputField.ActivateInputField();
    }

    private void OnChannelDropdownChanged(int optionIndex)
    {
        if (chatManager == null)
            return;

        var availableChannels = chatManager.GetAvailableChannels()
            .Where(c => c.showInChannelList)
            .ToList();

        if (optionIndex >= 0 && optionIndex < availableChannels.Count)
        {
            var selectedChannel = availableChannels[optionIndex].channelType;
            chatManager.SwitchToChannel(selectedChannel);
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Handles channel subscription changes
    /// </summary>
    public void OnChannelSubscriptionChanged(ChatChannel channel, bool subscribed)
    {
        if (subscribed)
        {
            if (!channelTabs.ContainsKey(channel))
            {
                CreateChannelTab(channel);
            }
        }
        else
        {
            if (channelTabs.TryGetValue(channel, out var tab))
            {
                Destroy(tab.gameObject);
                channelTabs.Remove(channel);
            }
        }
    }

    /// <summary>
    /// Handles active channel changes
    /// </summary>
    public void OnActiveChannelChanged(ChatChannel channel)
    {
        // Update channel tabs
        foreach (var kvp in channelTabs)
        {
            kvp.Value.isOn = (kvp.Key == channel);
        }

        // Update dropdown
        if (channelDropdown != null)
        {
            var availableChannels = chatManager.GetAvailableChannels()
                .Where(c => c.showInChannelList)
                .ToList();
            
            for (int i = 0; i < availableChannels.Count; i++)
            {
                if (availableChannels[i].channelType == channel)
                {
                    channelDropdown.value = i;
                    break;
                }
            }
        }

        // Load channel history
        LoadChannelHistory(channel);
    }

    private void LoadChannelHistory(ChatChannel channel)
    {
        if (chatManager == null)
            return;

        // Clear current messages
        foreach (var messageObj in messageGameObjects)
        {
            if (messageObj != null)
                Destroy(messageObj);
        }
        messageGameObjects.Clear();

        // Load history for this channel
        var history = chatManager.GetChannelHistory(channel);
        foreach (var message in history)
        {
            CreateMessageUI(message);
        }

        if (autoScrollToBottom)
        {
            ScrollToBottom();
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Shows or hides the chat window
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (chatWindow != null)
        {
            chatWindow.SetActive(visible);
        }

        if (visible && chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// Focuses the chat input field
    /// </summary>
    public void FocusInput()
    {
        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// Clears all displayed messages
    /// </summary>
    public void ClearMessages()
    {
        foreach (var messageObj in messageGameObjects)
        {
            if (messageObj != null)
                Destroy(messageObj);
        }
        messageGameObjects.Clear();
    }
    #endregion

    #region Unity Events
    private void Update()
    {
        // Handle global chat hotkeys
        if (Input.GetKeyDown(KeyCode.Return) && !chatInputField.isFocused)
        {
            FocusInput();
        }
    }

    private void OnDestroy()
    {
        // Cleanup event listeners
        if (chatInputField != null)
        {
            chatInputField.onEndEdit.RemoveAllListeners();
        }

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
        }

        if (channelDropdown != null)
        {
            channelDropdown.onValueChanged.RemoveAllListeners();
        }
    }
    #endregion
}