using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ChatUIController : MonoBehaviour
{
    private sealed class ChatMessageData
    {
        public ChatChannel Channel;
        public string Time;
        public string Sender;
        public string Receiver;
        public string Message;
        public bool LocalOnly;
    }

    [Header("Scene")]
    [SerializeField] private string loginSceneName = "LoginScene";

    [Header("ServerProject Protocol")]
    [SerializeField] private bool addLocalMessageAfterSend = true;
    [SerializeField] private bool showPacketDebugMessages = true;

    private TMP_Text chatTitleText;
    private TMP_Text accountStatusText;

    private Button allServerTabButton;
    private Button partyTabButton;
    private Button privateTabButton;
    private Button systemTabButton;
    private Button sendButton;
    private Button modalOkButton;
    private Button backToLoginButton;
    private Button accountStatusToggleButton;

    private ScrollRect messageScrollRect;
    private Transform messageContent;

    private TMP_InputField recipientInput;
    private TMP_InputField messageInput;
    private Image recipientInputImage;

    private GameObject accountStatusModal;
    private TMP_Text modalTitleText;
    private TMP_Text modalBodyText;

    private readonly List<ChatMessageData> messages = new List<ChatMessageData>();

    private string username;
    private int idAccount;
    private AccountState accountState;
    private int currentFilter = -1;
    private ChatChannel currentSendChannel = ChatChannel.AllServer;

    private readonly Color normalTargetColor = new Color(0.18f, 0.19f, 0.23f, 1f);
    private readonly Color warningTargetColor = new Color(0.45f, 0.18f, 0.24f, 1f);

    private void Awake()
    {
        username = LoginUIController.GetUsername();
        idAccount = LoginUIController.GetIdAccount();
        accountState = (AccountState)PlayerPrefs.GetInt("AccountState", (int)AccountState.Normal);

        CacheSceneObjects();
        ApplyChatLabels();
        SetupButtons();
        SetupAccountState();
        AddStarterMessages();
        SetChannelAndFilter(ChatChannel.AllServer, -1);
    }

    private void Update()
    {
        //fetch chat result pakage from server then display in UI
        PollServerChatPackets();

        if (messageInput != null &&
            messageInput.isFocused &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SendCurrentMessage();
        }
    }

    private void PollServerChatPackets()
    {
        byte[] data;

        while ((data = ServerConnection.GetChatData()) != null)
        {
            HandleServerChatPacket(data);
        }
    }

    private void HandleServerChatPacket(byte[] data)
    {
        try
        {
            ServerPacketReader reader = new ServerPacketReader(data);

            ServerCmdCode cmd = (ServerCmdCode)reader.ReadInt();

            if (cmd != ServerCmdCode.Chat)
                return;

            int channelValue = reader.ReadInt();
            string nameSender = reader.ReadString();
            int idReceiver = reader.ReadInt();
            string message = reader.ReadString();

            ChatChannel channel = (ChatChannel)channelValue;

            string receiverDisplay = idReceiver > 0 ? "id:" + idReceiver : string.Empty;

            Debug.Log(
                "client receive chat " +
                "channel=" + channel +
                " nameSender=" + nameSender +
                " idReceiver=" + idReceiver +
                " message=" + message
            );

            AddMessage(
                channel,
                nameSender,
                receiverDisplay,
                message,
                false
            );

            RefreshMessages();
        }
        catch (Exception ex)
        {
            Debug.LogError("client fail to read chat pkg " + ex.Message);

            AddMessage(
                ChatChannel.System,
                "client",
                string.Empty,
                "read chat packet failed: " + ex.Message,
                true
            );

            RefreshMessages();
        }
    }

    private async void OnApplicationQuit()
    {
        if (idAccount > 0 && ServerConnection.IsConnected)
        {
            try
            {
                await ServerConnection.SendLogoutAsync(idAccount);
            }
            catch
            {
                // Ignore quit errors.
            }
        }
    }

    private void CacheSceneObjects()
    {
        chatTitleText = GetComponentInScene<TMP_Text>("ChatTitleText");
        accountStatusText = GetComponentInScene<TMP_Text>("AccountStatusText");

        allServerTabButton = GetComponentInScene<Button>("AllTabButton");
        partyTabButton = GetComponentInScene<Button>("PartyTabButton");
        privateTabButton = GetComponentInScene<Button>("PrivateTabButton");
        systemTabButton = GetComponentInScene<Button>("SystemTabButton");
        sendButton = GetComponentInScene<Button>("SendButton");
        backToLoginButton = GetComponentInScene<Button>("BackToLoginButton");
        accountStatusToggleButton = GetComponentInScene<Button>("AccountStatusToggleButton");

        messageScrollRect = GetComponentInScene<ScrollRect>("MessageScroll");

        GameObject contentObject = FindSceneObject("MessageContent");
        messageContent = contentObject != null ? contentObject.transform : null;

        recipientInput = GetComponentInScene<TMP_InputField>("RecipientInput");
        messageInput = GetComponentInScene<TMP_InputField>("MessageInput");
        recipientInputImage = GetComponentInScene<Image>("RecipientInput");

        accountStatusModal = FindSceneObject("AccountStatusModal");
        modalTitleText = GetComponentInScene<TMP_Text>("ModalTitleText");
        modalBodyText = GetComponentInScene<TMP_Text>("ModalBodyText");
        modalOkButton = GetComponentInScene<Button>("ModalOkButton");
    }

    private void ApplyChatLabels()
    {
        SetText("ChatTitleText", "# all-server");
        SetText("AllTabButton_Label", "All-server");
        SetText("PartyTabButton_Label", "Party");
        SetText("PrivateTabButton_Label", "Private");
        SetText("SystemTabButton_Label", "System");
        SetText("SendButton_Label", "Send");
        SetText("ModalOkButton_Label", "OK / Back to Login");
        SetText("BackToLoginButton_Label", "Back to Login");
        SetText("AccountStatusToggleButton_Label", "Account Status");

        if (recipientInput != null)
        {
            recipientInput.text = string.Empty;
            TMP_Text placeholder = recipientInput.placeholder as TMP_Text;
            if (placeholder != null) placeholder.text = "To: @username";
            recipientInput.lineType = TMP_InputField.LineType.SingleLine;
            recipientInput.characterLimit = 24;
            recipientInput.onValueChanged.AddListener(OnPrivateTargetChanged);
        }

        if (messageInput != null)
        {
            messageInput.text = string.Empty;
            TMP_Text placeholder = messageInput.placeholder as TMP_Text;
            if (placeholder != null) placeholder.text = "Message #all-server  |  @username message = private";
            messageInput.lineType = TMP_InputField.LineType.SingleLine;
            messageInput.characterLimit = 180;
        }
    }

    private void SetupButtons()
    {
        if (allServerTabButton != null)
            allServerTabButton.onClick.AddListener(() => SetChannelAndFilter(ChatChannel.AllServer, -1));

        if (partyTabButton != null)
            partyTabButton.onClick.AddListener(() => SetChannelAndFilter(ChatChannel.Party, (int)ChatChannel.Party));

        if (privateTabButton != null)
            privateTabButton.onClick.AddListener(() => SetChannelAndFilter(ChatChannel.Private, (int)ChatChannel.Private));

        if (systemTabButton != null)
            systemTabButton.onClick.AddListener(() => SetChannelAndFilter(ChatChannel.System, (int)ChatChannel.System));

        if (sendButton != null)
            sendButton.onClick.AddListener(SendCurrentMessage);

        if (modalOkButton != null)
            modalOkButton.onClick.AddListener(BackToLoginIfBannedElseClose);

        if (backToLoginButton != null)
            backToLoginButton.onClick.AddListener(BackToLogin);

        if (accountStatusToggleButton != null)
            accountStatusToggleButton.onClick.AddListener(ToggleAccountStatusModal);
    }

    private async void BackToLogin()
    {
        if (idAccount > 0 && ServerConnection.IsConnected)
        {
            try
            {
                await ServerConnection.SendLogoutAsync(idAccount);
            }
            catch
            {
                //ignore logout errors
            }
        }

        SceneManager.LoadScene(loginSceneName);
    }

    private void ToggleAccountStatusModal()
    {
        if (accountStatusModal == null)
            return;

        bool nextState = !accountStatusModal.activeSelf;
        accountStatusModal.SetActive(nextState);

        if (nextState)
        {
            string connectionText = ServerConnection.IsConnected ? "Connected" : "Offline";

            if (modalTitleText != null)
                modalTitleText.text = "ACCOUNT STATUS";

            if (modalBodyText != null)
            {
                modalBodyText.text =
                    "Server: " + connectionText + "\n" +
                    "ID: " + idAccount + "\n" +
                    "Username: " + username + "\n" +
                    "Account State: " + ServerMock.GetStateText(accountState);
            }
        }
    }

    private void SetupAccountState()
    {
        if (accountStatusText != null)
        {
            string connectionText = ServerConnection.IsConnected ? "Connected" : "Offline";
            accountStatusText.text =
                "Server: " + connectionText +
                " | ID: " + idAccount +
                " | User: " + username;
        }

        if (accountState == AccountState.Restricted)
        {
            if (messageInput != null) messageInput.interactable = false;
            if (sendButton != null) sendButton.interactable = false;

            ShowModal(
                "ACCOUNT RESTRICTED",
                "Your account is restricted. You can read chat, but cannot send messages."
            );
        }
        else if (accountState == AccountState.Banned)
        {
            if (messageInput != null) messageInput.interactable = false;
            if (sendButton != null) sendButton.interactable = false;

            ShowModal(
                "ACCOUNT BANNED",
                "This account has been banned! You cannot use chat."
            );
        }
        else if (accountStatusModal != null)
        {
            accountStatusModal.SetActive(false);
        }
    }

    private void AddStarterMessages()
    {
        AddMessage(ChatChannel.System, "Client", string.Empty, "Testinggggggggggggg.", true);

        if (!ServerConnection.IsConnected)
        {
            AddMessage(ChatChannel.System, "Client", string.Empty, "Warning: webSocket is not connected. Login from LoginScene first, or check ws://127.0.0.1:55556/.", true);
        }
    }

    private void SetChannelAndFilter(ChatChannel sendChannel, int filter)
    {
        currentSendChannel = sendChannel == ChatChannel.System ? ChatChannel.AllServer : sendChannel;
        currentFilter = filter;

        if (chatTitleText != null)
        {
            chatTitleText.text = sendChannel == ChatChannel.Private
                ? GetPrivateHeaderText()
                : "# " + GetChannelName(sendChannel);
        }

        if (recipientInput != null)
        {
            recipientInput.gameObject.SetActive(currentSendChannel == ChatChannel.Private);

            if (currentSendChannel == ChatChannel.Private)
                recipientInput.ActivateInputField();
        }

        UpdateInputPlaceholders();
        SetTargetWarning(false);
        RefreshMessages();
    }

    private void OnPrivateTargetChanged(string _)
    {
        if (currentSendChannel != ChatChannel.Private)
            return;

        if (chatTitleText != null)
            chatTitleText.text = GetPrivateHeaderText();

        UpdateInputPlaceholders();
        SetTargetWarning(false);
    }

    private async void SendCurrentMessage()
    {
        if (!ServerMock.CanSendChat(accountState))
        {
            ShowModal("CHAT DISABLED", "Your account status does not allow sending chat messages.");
            return;
        }

        string text = messageInput != null ? messageInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        ChatChannel sendChannel = currentSendChannel;

        string receiver = string.Empty;

        if (TryParsePrivateShortcut(text, out string shortcutReceiver, out string shortcutMessage))
        {
            sendChannel = ChatChannel.Private;
            receiver = shortcutReceiver;
            text = shortcutMessage;

            if (recipientInput != null)
            {
                recipientInput.text = receiver;
                recipientInput.gameObject.SetActive(true);
            }

            currentSendChannel = ChatChannel.Private;
            currentFilter = (int)ChatChannel.Private;
        }
        else if (sendChannel == ChatChannel.Private)
        {
            receiver = recipientInput != null ? recipientInput.text.Trim().TrimStart('@') : string.Empty;

            if (string.IsNullOrWhiteSpace(receiver))
            {
                SetTargetWarning(true);
                ShowModal("PRIVATE CHAT", "Choose a target first. Example: Alice, or type @Alice hello in the message box.");
                return;
            }
        }

        if (sendButton != null)
            sendButton.interactable = false;

        try
        {
            await ServerConnection.SendChatAsync((int)sendChannel, idAccount, receiver, text);

            if (showPacketDebugMessages)
            {
                AddMessage(
                    ChatChannel.System,
                    "Client",
                    string.Empty,
                    "Sent packet cmd=0x0003 channel=" + ((int)sendChannel) + " receiver='" + receiver,
                    true
                );
            }

            if (addLocalMessageAfterSend)
            {
                AddMessage(sendChannel, username, receiver, text, false);
            }
        }
        catch (Exception ex)
        {
            AddMessage(ChatChannel.System, "Client", string.Empty, "Chat send failed: " + ex.Message, true);
        }
        finally
        {
            if (sendButton != null)
                sendButton.interactable = true;
        }

        if (messageInput != null)
        {
            messageInput.text = string.Empty;
            messageInput.ActivateInputField();
        }

        if (chatTitleText != null)
            chatTitleText.text = currentSendChannel == ChatChannel.Private ? GetPrivateHeaderText() : "# " + GetChannelName(currentSendChannel);

        UpdateInputPlaceholders();
        SetupAccountState();
        RefreshMessages();
    }

    // (shortcuts) using the receiver username, for ex: receiver username is "WhyAmIDoingThis"
    // @WhyAmIDoingThis six seven
    // /w WhyAmIDoingThis three six
    // /tell WhyAmIDoingThis idk
    // /dm WhyAmIDoingThis why im doing this
    private bool TryParsePrivateShortcut(string raw, out string receiver, out string message)
    {
        receiver = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string text = raw.Trim();

        if (text.StartsWith("@"))
        {
            int space = text.IndexOf(' ');
            if (space <= 1) return false;

            receiver = text.Substring(1, space - 1).Trim();
            message = text.Substring(space + 1).Trim();

            return !string.IsNullOrWhiteSpace(receiver) && !string.IsNullOrWhiteSpace(message);
        }

        if (text.StartsWith("/w ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/tell ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("/dm ", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = text.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;

            receiver = parts[1].Trim().TrimStart('@');
            message = parts[2].Trim();

            return !string.IsNullOrWhiteSpace(receiver) && !string.IsNullOrWhiteSpace(message);
        }

        return false;
    }

    private void AddMessage(ChatChannel channel, string sender, string receiver, string message, bool localOnly = false)
    {
        messages.Add(new ChatMessageData
        {
            Channel = channel,
            Time = DateTime.Now.ToString("HH:mm"),
            Sender = sender,
            Receiver = receiver,
            Message = message,
            LocalOnly = localOnly
        });
    }

    private void RefreshMessages()
    {
        if (messageContent == null) return;

        for (int i = messageContent.childCount - 1; i >= 0; i--)
        {
            Destroy(messageContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < messages.Count; i++)
        {
            ChatMessageData data = messages[i];
            if (currentFilter != -1 && (int)data.Channel != currentFilter) continue;
            CreateMessageText(data);
        }

        ScrollToBottom();
    }

    private void CreateMessageText(ChatMessageData data)
    {
        GameObject row = new GameObject(
            "Message_" + data.Channel,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement)
        );

        row.transform.SetParent(messageContent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 50f);

        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = data.Channel == ChatChannel.Private ? 56f : 46f;
        layout.flexibleWidth = 1f;

        TMP_Text text = row.GetComponent<TMP_Text>();
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = FormatMessage(data);
    }

    private string FormatMessage(ChatMessageData data)
    {
        string channelName = GetChannelName(data.Channel);
        string color = GetChannelColor(data.Channel);
        string sender = EscapeRichText(data.Sender);
        string receiver = EscapeRichText(data.Receiver);
        string message = EscapeRichText(data.Message);
        string localTag = data.LocalOnly ? " <color=#999999>(local)</color>" : string.Empty;

        if (data.Channel == ChatChannel.Private)
        {
            return
                $"<color=#8ea1e1>[{data.Time}]</color> " +
                $"<mark=#3B213B88><color={color}>  DM  @{sender}  ->  @{receiver}  </color></mark>{localTag}\n" +
                $"<indent=24%>{message}</indent>";
        }

        return
            $"<color=#8ea1e1>[{data.Time}]</color> " +
            $"<color={color}>#{channelName}</color> " +
            $"<b>{sender}</b>{localTag}: {message}";
    }

    private string GetChannelName(ChatChannel channel)
    {
        switch (channel)
        {
            case ChatChannel.AllServer: return "all-server";
            case ChatChannel.Party: return "party";
            case ChatChannel.Private: return "private";
            case ChatChannel.System: return "system";
            default: return "chat";
        }
    }

    private string GetChannelColor(ChatChannel channel)
    {
        switch (channel)
        {
            case ChatChannel.AllServer: return "#FFFFFF";
            case ChatChannel.Party: return "#FEE75C";
            case ChatChannel.Private: return "#EB459E";
            case ChatChannel.System: return "#ED4245";
            default: return "#FFFFFF";
        }
    }

    private string GetPrivateHeaderText()
    {
        string target = recipientInput != null ? recipientInput.text.Trim().TrimStart('@') : string.Empty;
        return string.IsNullOrWhiteSpace(target) ? "@ private" : "@ " + target;
    }

    private void UpdateInputPlaceholders()
    {
        if (messageInput != null && messageInput.placeholder is TMP_Text messagePlaceholder)
        {
            if (currentSendChannel == ChatChannel.Private)
            {
                string target = recipientInput != null ? recipientInput.text.Trim().TrimStart('@') : string.Empty;
                messagePlaceholder.text = string.IsNullOrWhiteSpace(target)
                    ? "Choose target above, then type private message"
                    : "Message @" + target;
            }
            else
            {
                messagePlaceholder.text = "Message #" + GetChannelName(currentSendChannel) + "  |  @username message = private";
            }
        }

        if (recipientInput != null && recipientInput.placeholder is TMP_Text recipientPlaceholder)
            recipientPlaceholder.text = "To: @username";
    }

    private void SetTargetWarning(bool warning)
    {
        if (recipientInputImage != null)
            recipientInputImage.color = warning ? warningTargetColor : normalTargetColor;
    }

    private string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private void ShowModal(string title, string body)
    {
        if (accountStatusModal != null) accountStatusModal.SetActive(true);
        if (modalTitleText != null) modalTitleText.text = title;
        if (modalBodyText != null) modalBodyText.text = body;
    }

    private async void BackToLoginIfBannedElseClose()
    {
        if (accountState == AccountState.Banned)
        {
            if (idAccount > 0 && ServerConnection.IsConnected)
            {
                try { await ServerConnection.SendLogoutAsync(idAccount); }
                catch { }
            }

            SceneManager.LoadScene(loginSceneName);
            return;
        }

        if (accountStatusModal != null) accountStatusModal.SetActive(false);
    }

    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (messageScrollRect != null) messageScrollRect.verticalNormalizedPosition = 0f;
    }

    private void SetText(string objectName, string value)
    {
        TMP_Text text = GetComponentInScene<TMP_Text>(objectName);
        if (text != null) text.text = value;
    }

    private void SetObjectActive(string objectName, bool active)
    {
        GameObject obj = FindSceneObject(objectName);
        if (obj != null) obj.SetActive(active);
    }

    private T GetComponentInScene<T>(string objectName) where T : Component
    {
        GameObject obj = FindSceneObject(objectName);
        return obj != null ? obj.GetComponent<T>() : null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, objectName);
            if (found != null) return found.gameObject;
        }

        return null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }
}