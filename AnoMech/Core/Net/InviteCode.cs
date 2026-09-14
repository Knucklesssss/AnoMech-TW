using System;
using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace AnoMech.Core.Net;

public enum InviteError
{
    None,
    Empty,
    BadPrefix,
    BadCharacter,
    BadLength,
    ChecksumMismatch,
    UnsupportedFormat,
    ProtocolMismatch,
    InvalidAddress,
}

// Not a credential: it carries only where to connect. The room id lets the host refuse a code
// left over from an earlier room with a clear message instead of a silent timeout.
public readonly record struct InviteCode(ushort ProtocolVersion, IPAddress Address, ushort Port, uint RoomId)
{
    public const string Prefix = "AMP";
    private const byte Format = 1;
    private const int ByteLength = 15;   // format 1 + version 2 + IPv4 4 + port 2 + room 4 + crc 2
    private const int CharLength = 24;   // 120 bits / 5
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford: no I L O U

    public static InviteCode Create(IPAddress address, ushort port, uint roomId)
        => new(NetProtocol.Version, address, port, roomId);

    public string Encode()
    {
        var bytes = new byte[ByteLength];
        bytes[0] = Format;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1), ProtocolVersion);
        Address.MapToIPv4().GetAddressBytes().CopyTo(bytes, 3);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(7), Port);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(9), RoomId);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(13), Crc16(bytes.AsSpan(0, 13)));

        var chars = ToBase32(bytes);
        var text = new StringBuilder(Prefix);
        for (var i = 0; i < chars.Length; i += 4)
            text.Append('-').Append(chars, i, 4);
        return text.ToString();
    }

    public static InviteError TryDecode(string? text, out InviteCode code)
    {
        code = default;
        if (string.IsNullOrWhiteSpace(text)) return InviteError.Empty;

        var compact = new StringBuilder(text.Length);
        foreach (var ch in text)
            if (ch != '-' && !char.IsWhiteSpace(ch)) compact.Append(char.ToUpperInvariant(ch));
        var body = compact.ToString();
        if (!body.StartsWith(Prefix, StringComparison.Ordinal)) return InviteError.BadPrefix;
        body = body[Prefix.Length..];
        if (body.Length != CharLength) return InviteError.BadLength;
        if (!TryFromBase32(body, out var bytes)) return InviteError.BadCharacter;
        if (Crc16(bytes.AsSpan(0, 13)) != BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(13))) return InviteError.ChecksumMismatch;
        if (bytes[0] != Format) return InviteError.UnsupportedFormat;

        var address = new IPAddress(bytes.AsSpan(3, 4));
        var port = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(7));
        code = new InviteCode(
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)), address, port,
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9)));
        if (code.ProtocolVersion != NetProtocol.Version) return InviteError.ProtocolMismatch;
        if (port == 0 || address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast)) return InviteError.InvalidAddress;
        return InviteError.None;
    }

    public static string Describe(InviteError error, InviteCode decoded) => error switch
    {
        InviteError.None => "邀請碼正確。",
        InviteError.Empty => "請先貼上邀請碼。",
        InviteError.BadPrefix => "這不是 AnoMech 邀請碼（應該以 AMP- 開頭）。",
        InviteError.BadCharacter => "邀請碼含有無效字元，請重新複製完整的邀請碼。",
        InviteError.BadLength => "邀請碼長度不對，可能沒有複製完整。",
        InviteError.ChecksumMismatch => "邀請碼檢查碼不符，可能複製時有字元遺漏或打錯。",
        InviteError.UnsupportedFormat => "邀請碼格式不支援，請雙方更新到同一版插件。",
        InviteError.ProtocolMismatch => $"邀請碼來自不同版本的插件（房主協定 v{decoded.ProtocolVersion}，你的協定 v{NetProtocol.Version}），請雙方更新到同一版。",
        InviteError.InvalidAddress => "邀請碼裡的位址無效，請房主重新建立房間。",
        _ => "邀請碼無法解析。",
    };

    internal static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    private static char[] ToBase32(byte[] bytes)
    {
        var chars = new char[bytes.Length * 8 / 5];
        int buffer = 0, bits = 0, index = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                chars[index++] = Alphabet[(buffer >> bits) & 31];
            }
            buffer &= (1 << bits) - 1;
        }
        return chars;
    }

    private static bool TryFromBase32(string text, out byte[] bytes)
    {
        bytes = new byte[text.Length * 5 / 8];
        int buffer = 0, bits = 0, index = 0;
        foreach (var raw in text)
        {
            var ch = raw switch { 'O' => '0', 'I' or 'L' => '1', _ => raw };
            var value = Alphabet.IndexOf(ch);
            if (value < 0) return false;
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
                buffer &= (1 << bits) - 1;
            }
        }
        return true;
    }
}
