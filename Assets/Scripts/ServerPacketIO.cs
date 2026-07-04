using System;
using System.Collections.Generic;
using System.Text;

public enum ServerCmdCode
{
    Login = 0x0000,
    Logout = 0x0001,
    Register = 0x0002,
    Chat = 0x0003
}

public sealed class ServerPacketWriter
{
    private readonly List<byte> buffer = new List<byte>();

    public byte[] ToArray()
    {
        return buffer.ToArray();
    }

    public void WriteInt(int value)
    {
        buffer.AddRange(BitConverter.GetBytes(value));
    }

    public void WriteBool(bool value)
    {
        buffer.AddRange(BitConverter.GetBytes(value));
    }

    public void WriteString(string value)
    {
        if (value == null)
        {
            WriteInt(0);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt(bytes.Length);
        buffer.AddRange(bytes);
    }
}

public sealed class ServerPacketReader
{
    private readonly byte[] buffer;
    private int offset;

    public ServerPacketReader(byte[] data)
    {
        buffer = data;
        offset = 0;
    }

    public int ReadInt()
    {
        int value = BitConverter.ToInt32(buffer, offset);
        offset += 4;
        return value;
    }

    public bool ReadBool()
    {
        bool value = BitConverter.ToBoolean(buffer, offset);
        offset += 1;
        return value;
    }

    public string ReadString()
    {
        int length = ReadInt();

        if (length <= 0)
            return string.Empty;

        string value = Encoding.UTF8.GetString(buffer, offset, length);
        offset += length;
        return value;
    }
}