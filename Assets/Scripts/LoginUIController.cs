using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoginUIController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string chatSceneName = "ChatUIScene";

    private TMP_InputField usernameInput;
    private TMP_InputField passwordInput;
    private Button loginButton;
    private Button forgotPasswordButton;

    private TMP_Text serverMessageText;
    private TMP_Text errorText;

    private GameObject accountStatusModal;
    private TMP_Text modalTitleText;
    private TMP_Text modalBodyText;
    private Button modalOkButton;

    private Action modalOkAction;

    private static int idAccount;
    private static string username;

    private void Awake()
    {
        usernameInput = GetComponentInScene<TMP_InputField>("UsernameInput");
        passwordInput = GetComponentInScene<TMP_InputField>("PasswordInput");

        loginButton = GetComponentInScene<Button>("LoginButton");
        forgotPasswordButton = GetComponentInScene<Button>("ForgotPasswordButton");

        serverMessageText = GetComponentInScene<TMP_Text>("ServerMessageText");
        errorText = GetComponentInScene<TMP_Text>("ErrorText");

        accountStatusModal = FindSceneObject("AccountStatusModal");
        modalTitleText = GetComponentInScene<TMP_Text>("ModalTitleText");
        modalBodyText = GetComponentInScene<TMP_Text>("ModalBodyText");
        modalOkButton = GetComponentInScene<Button>("ModalOkButton");

        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);

        if (forgotPasswordButton != null)
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);

        if (modalOkButton != null)
            modalOkButton.onClick.AddListener(OnModalOkClicked);

        if (accountStatusModal != null)
            accountStatusModal.SetActive(false);

        if (errorText != null)
            errorText.text = string.Empty;

        if (serverMessageText != null)
            serverMessageText.text = "Message from server: Welcome.";

        Application.runInBackground = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnLoginClicked();
        }
    }

    private async void OnLoginClicked()
    {
        string username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowLoginError("Please enter username and password.");
            return;
        }

        if (serverMessageText != null)
            serverMessageText.text = "Message from server: Connecting...";

        if (errorText != null)
            errorText.text = string.Empty;

        if (loginButton != null)
            loginButton.interactable = false;

        try
        {
            ServerLoginResponse result = await ServerConnection.LoginAsync(username, password);

            string message = string.IsNullOrWhiteSpace(result.Message)
                ? (result.Success ? "Login successful." : "Login failed.")
                : result.Message;

            if (serverMessageText != null)
                serverMessageText.text = "Message from server: " + message;

            if (errorText != null)
                errorText.text = result.Success ? string.Empty : message;

            if (!result.Success)
            {
                ShowModal("LOGIN FAILED", message, null);
                return;
            }

            PlayerPrefs.SetString("Username", string.IsNullOrWhiteSpace(result.Username) ? username : result.Username);
            PlayerPrefs.SetInt("IdAccount", result.IdAccount);
            idAccount = result.IdAccount;
            username = result.Username;

            // login packet currently returns success, idAccount, username, message only.
            // until the server sends account State, treat successful accounts as Normal.
            PlayerPrefs.SetInt("AccountState", (int)AccountState.Normal);
            PlayerPrefs.Save();

            LoadChatScene();
        }
        catch (Exception ex)
        {
            ShowLoginError("Cannot connect/login to server: " + ex.Message);
        }
        finally
        {
            if (loginButton != null)
                loginButton.interactable = true;
        }
    }

    private void ShowLoginError(string message)
    {
        if (serverMessageText != null)
            serverMessageText.text = "Message from server: " + message;

        if (errorText != null)
            errorText.text = message;
    }

    private void OnForgotPasswordClicked()
    {
        string username = usernameInput != null ? usernameInput.text : string.Empty;
        string message = ServerMock.ForgotPassword(username);

        if (serverMessageText != null)
            serverMessageText.text = "Message from server: " + message;

        if (errorText != null)
            errorText.text = message;
    }

    private void ShowModal(string title, string body, Action okAction)
    {
        if (accountStatusModal != null)
            accountStatusModal.SetActive(true);

        if (modalTitleText != null)
            modalTitleText.text = title;

        if (modalBodyText != null)
            modalBodyText.text = body;

        modalOkAction = okAction;
    }

    private void OnModalOkClicked()
    {
        if (accountStatusModal != null)
            accountStatusModal.SetActive(false);

        Action action = modalOkAction;
        modalOkAction = null;

        if (action != null)
            action.Invoke();
    }

    private void LoadChatScene()
    {
        SceneManager.LoadScene(chatSceneName);
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

            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);

            if (found != null)
                return found;
        }

        return null;
    }

    public static int GetIdAccount()
    {
        return idAccount;
    }
    public static string GetUsername()
    {
        return username;
    }
}