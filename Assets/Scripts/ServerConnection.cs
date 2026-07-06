using System;
using System.Collections.Concurrent;
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

    private static readonly ConcurrentQueue<byte[]> receiveQueue = new ConcurrentQueue<byte[]>();

    private static readonly ConcurrentQueue<byte[]> loginQueue = new ConcurrentQueue<byte[]>();
    private static readonly ConcurrentQueue<byte[]> chatQueue = new ConcurrentQueue<byte[]>();

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
        _ = StartReceiveLoop(CancellationToken.None);
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
        byte[] responseBytes = GetLoginData();

        while (responseBytes == null || responseBytes.Length == 0)
        {
            await Task.Delay(100);
            responseBytes = GetLoginData();
        }

        ServerPacketReader reader = new ServerPacketReader(responseBytes);
        reader.ReadInt();
        ServerLoginResponse response = new ServerLoginResponse();
        response.Success = reader.ReadBool();
        response.IdAccount = reader.ReadInt();
        response.Username = reader.ReadString();
        response.Message = reader.ReadString();
        return response;
    }

    public static async Task SendChatAsync(int channel, int sender, string receiver, string message)
    {
        if (!IsConnected)
            await ConnectAsync(ServerUrl);

        ServerPacketWriter writer = new ServerPacketWriter();
        writer.WriteInt((int)ServerCmdCode.Chat);
        writer.WriteInt(channel);
        writer.WriteInt(sender);

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

    private static async Task StartReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();

        try
        {
            while (!token.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                } while (!result.EndOfMessage);

                byte[] fullMessage = messageBuffer.ToArray();
                messageBuffer.Clear();

                HandlePacket(fullMessage);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("ReceiveLoop stopped.");
        }
        catch (Exception ex)
        {
            Debug.Log($"Socket: Mất kết nối tới Server! {ex}");
        }
    }
    private static void HandlePacket(byte[] data)
    {
        ServerPacketReader reader = new ServerPacketReader(data);
        ServerCmdCode cmd = (ServerCmdCode)reader.ReadInt();

        switch (cmd)
        {
            case ServerCmdCode.Login:
                Debug.Log("client receive login");
                loginQueue.Enqueue(data);
                break;

            case ServerCmdCode.Chat:
                Debug.Log("client receive chat");
                chatQueue.Enqueue(data);
                break;

            default:
                Debug.Log($"client receive unknown pkg cmd: {cmd}");
                receiveQueue.Enqueue(data);
                break;
        }
    }
    public static byte[] GetLoginData()
    {
        if (loginQueue.TryDequeue(out var data))
            return data;
        return null;
    }
    public static byte[] GetChatData()
    {
        if (chatQueue.TryDequeue(out var data))
            return data;
        return null;
    }
    public static void ClearAllQueues()
    {
        ClearQueue(loginQueue);
        ClearQueue(chatQueue);
        ClearQueue(receiveQueue);
    }
    private static void ClearQueue(ConcurrentQueue<byte[]> queue)
    {
        while (queue.TryDequeue(out _)) { }
    }
}