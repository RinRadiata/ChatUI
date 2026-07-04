using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ServerLoginResponse
{
    public bool Success;
    public int IdAccount;
    public string Username;
    public string Message;
}

public static class ServerConnection
{
    public static string ServerUrl = "ws://127.0.0.1:55556/";

    private static ClientWebSocket socket;
    private static readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

    public static bool IsConnected
    {
        get { return socket != null && socket.State == WebSocketState.Open; }
    }

    public static async Task<bool> ConnectAsync(string serverUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(serverUrl))
            ServerUrl = serverUrl;

        if (IsConnected)
            return true;

        await DisconnectAsync();

        socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(ServerUrl), CancellationToken.None);
        return IsConnected;
    }

    public static async Task DisconnectAsync()
    {
        if (socket == null)
            return;

        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnect",
                    CancellationToken.None
                );
            }
        }
        catch
        {
            // ignore disconnect errors.
        }
        finally
        {
            socket.Dispose();
            socket = null;
        }
    }

    public static async Task<ServerLoginResponse> LoginAsync(string username, string password)
    {
        await ConnectAsync(ServerUrl);

        ServerPacketWriter writer = new ServerPacketWriter();
        writer.WriteInt((int)ServerCmdCode.Login);
        writer.WriteString(username);
        writer.WriteString(password);

        await SendRawAsync(writer.ToArray());
        byte[] responseBytes = await ReceiveRawAsync();

        ServerPacketReader reader = new ServerPacketReader(responseBytes);
        int cmd = reader.ReadInt();

        if (cmd != (int)ServerCmdCode.Login)
            throw new Exception("Server returned unexpected packet cmd: " + cmd);

        ServerLoginResponse response = new ServerLoginResponse();
        response.Success = reader.ReadBool();
        response.IdAccount = reader.ReadInt();
        response.Username = reader.ReadString();
        response.Message = reader.ReadString();
        return response;
    }

    public static async Task SendChatAsync(int channel, string sender, string receiver, string message)
    {
        if (!IsConnected)
            await ConnectAsync(ServerUrl);

        ServerPacketWriter writer = new ServerPacketWriter();
        writer.WriteInt((int)ServerCmdCode.Chat);
        writer.WriteInt(channel);
        writer.WriteString(sender);
        writer.WriteString(receiver);
        writer.WriteString(message);

        await SendRawAsync(writer.ToArray());
    }

    public static async Task SendLogoutAsync(int idAccount)
    {
        if (!IsConnected)
            return;

        ServerPacketWriter writer = new ServerPacketWriter();
        writer.WriteInt((int)ServerCmdCode.Logout);
        writer.WriteInt(idAccount);

        await SendRawAsync(writer.ToArray());
        await DisconnectAsync();
    }

    private static async Task SendRawAsync(byte[] data)
    {
        if (!IsConnected)
            throw new Exception("webSocket is NOT connected");

        await sendLock.WaitAsync();

        try
        {
            await socket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None
            );
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static async Task<byte[]> ReceiveRawAsync()
    {
        if (!IsConnected)
            throw new Exception("webSocket is NOT connected");

        byte[] buffer = new byte[8192];
        List<byte> message = new List<byte>();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                CancellationToken.None
            );

            if (result.MessageType == WebSocketMessageType.Close)
                throw new Exception("Server closed the webSocket connection");

            for (int i = 0; i < result.Count; i++)
                message.Add(buffer[i]);
        }
        while (!result.EndOfMessage);

        return message.ToArray();
    }
}