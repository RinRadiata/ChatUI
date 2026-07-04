using System.Collections.Generic;

public enum AccountState
{
    Normal = 0,
    Restricted = 1,
    Banned = 2
}

public enum ChatChannel
{
    AllServer = 0,
    Party = 3,
    Private = 4,
    System = 5
}

public sealed class LoginResult
{
    public bool Success;
    public string Username;
    public AccountState State;
    public string Message;
}

public static class ServerMock
{
    private sealed class AccountRecord
    {
        public readonly string Password;
        public readonly AccountState State;

        public AccountRecord(string password, AccountState state)
        {
            Password = password;
            State = state;
        }
    }

    //testing accounts states
    private static readonly Dictionary<string, AccountRecord> Accounts =
        new Dictionary<string, AccountRecord>()
        {
            { "player", new AccountRecord("123456", AccountState.Normal) },
            { "restricted", new AccountRecord("123456", AccountState.Restricted) },
            { "banned", new AccountRecord("123456", AccountState.Banned) },
            { "gm", new AccountRecord("123456", AccountState.Normal) }
        };

    public static LoginResult Login(string username, string password)
    {
        username = (username ?? string.Empty).Trim();
        password = password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Success = false,
                Username = username,
                State = AccountState.Normal,
                Message = "Please enter username and password."
            };
        }

        if (!Accounts.TryGetValue(username, out AccountRecord account))
        {
            return new LoginResult
            {
                Success = false,
                Username = username,
                State = AccountState.Normal,
                Message = "Account not found."
            };
        }

        if (account.Password != password)
        {
            return new LoginResult
            {
                Success = false,
                Username = username,
                State = account.State,
                Message = "Wrong password."
            };
        }

        if (account.State == AccountState.Banned)
        {
            return new LoginResult
            {
                Success = false,
                Username = username,
                State = AccountState.Banned,
                Message = "This account has been banned. Please contact support."
            };
        }

        if (account.State == AccountState.Restricted)
        {
            return new LoginResult
            {
                Success = true,
                Username = username,
                State = AccountState.Restricted,
                Message = "Login successful, but this account is restricted. Chat sending is disabled."
            };
        }

        return new LoginResult
        {
            Success = true,
            Username = username,
            State = AccountState.Normal,
            Message = "Login successful. Welcome to the server."
        };
    }

    public static string ForgotPassword(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Enter your username first, then press Forgot password.";
        }

        return "If this account exists, a password reset message has been sent by the server.";
    }

    public static bool CanSendChat(AccountState state)
    {
        return state == AccountState.Normal;
    }

    public static string GetStateText(AccountState state)
    {
        switch (state)
        {
            case AccountState.Normal: return "Normal";
            case AccountState.Restricted: return "Restricted";
            case AccountState.Banned: return "Banned";
            default: return "Unknown";
        }
    }
}