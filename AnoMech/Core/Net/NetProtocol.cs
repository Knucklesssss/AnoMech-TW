using System;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AnoMech.Core.Net;

public static class NetProtocol
{
    public const ushort Version = 3; // P2/P4 timing and P6 catalog require matching simulation builds.
    public const uint Magic = 0x414D5031; // "AMP1"
    public const int MaxPlayers = 8;
    public const byte HostPlayerId = 0;
    public const int MaxPacketBytes = 2400;
    public const int MaxNameLength = 32;
    public const int DisconnectTimeoutMs = 5000;
    // LiteNetLib flushes queued sends on its logic thread every UpdateTime; the default 15 ms added that much lag.
    public const int UpdateTimeMs = 2;
    public const ushort DefaultPort = 42420;
    public const int PortCandidates = 10;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static double NowMs => Clock.Elapsed.TotalMilliseconds;
}

public enum MessageType : byte
{
    Welcome = 1,
    Ping = 2,
    Pong = 3,
    LobbyState = 10,
    SetReady = 11,
    RequestRole = 12,
    StartRun = 13,
    Frames = 14,
    StopRun = 15,
    Transform = 16,
    RunFailed = 17,
    Markers = 18,
    Appearance = 19,
    RestartRequest = 20,
}

public enum RejectReason : byte
{
    Malformed = 1,
    VersionMismatch = 2,
    RoomMismatch = 3,
    RoomFull = 4,
}

public readonly record struct PacketHeader(MessageType Type, uint Sequence, byte SenderId, double SentAtMs)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put((byte)Type);
        writer.Put(Sequence);
        writer.Put(SenderId);
        writer.Put(SentAtMs);
    }

    public static bool TryRead(NetDataReader reader, out PacketHeader header)
    {
        header = default;
        if (reader.AvailableBytes > NetProtocol.MaxPacketBytes) return false;
        if (!reader.TryGetByte(out var type) || !Enum.IsDefined((MessageType)type)) return false;
        if (!reader.TryGetUInt(out var sequence) || !reader.TryGetByte(out var sender) || !reader.TryGetDouble(out var sentAt)) return false;
        if (sender >= NetProtocol.MaxPlayers || !double.IsFinite(sentAt)) return false;
        header = new PacketHeader((MessageType)type, sequence, sender, sentAt);
        return true;
    }
}

// Sent as LiteNetLib connection data, so a wrong version or a stale room is refused before a
// peer exists. Magic and version come first and must keep this layout in every future version.
public readonly record struct HelloDto(ushort ProtocolVersion, uint RoomId, string PlayerName)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(NetProtocol.Magic);
        writer.Put(ProtocolVersion);
        writer.Put(RoomId);
        writer.Put(PlayerName, NetProtocol.MaxNameLength);
    }

    public static bool TryRead(NetDataReader reader, out HelloDto hello)
    {
        hello = default;
        if (!reader.TryGetUInt(out var magic) || magic != NetProtocol.Magic) return false;
        if (!reader.TryGetUShort(out var version) || !reader.TryGetUInt(out var roomId) || !reader.TryGetString(out var name)) return false;
        name ??= "";
        hello = new HelloDto(version, roomId, name.Length > NetProtocol.MaxNameLength ? name[..NetProtocol.MaxNameLength] : name);
        return true;
    }
}

public readonly record struct RejectDto(RejectReason Reason, ushort HostProtocolVersion)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(NetProtocol.Magic);
        writer.Put((byte)Reason);
        writer.Put(HostProtocolVersion);
    }

    public static bool TryRead(NetDataReader reader, out RejectDto reject)
    {
        reject = default;
        if (!reader.TryGetUInt(out var magic) || magic != NetProtocol.Magic) return false;
        if (!reader.TryGetByte(out var reason) || !Enum.IsDefined((RejectReason)reason)) return false;
        if (!reader.TryGetUShort(out var version)) return false;
        reject = new RejectDto((RejectReason)reason, version);
        return true;
    }
}

public readonly record struct WelcomeDto(byte PlayerId)
{
    public void Write(NetDataWriter writer) => writer.Put(PlayerId);

    public static bool TryRead(NetDataReader reader, out WelcomeDto welcome)
    {
        welcome = default;
        if (!reader.TryGetByte(out var id) || id == NetProtocol.HostPlayerId || id >= NetProtocol.MaxPlayers) return false;
        welcome = new WelcomeDto(id);
        return true;
    }
}

public readonly record struct PongDto(uint EchoSequence, double EchoSentAtMs)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(EchoSequence);
        writer.Put(EchoSentAtMs);
    }

    public static bool TryRead(NetDataReader reader, out PongDto pong)
    {
        pong = default;
        if (!reader.TryGetUInt(out var sequence) || !reader.TryGetDouble(out var sentAt) || !double.IsFinite(sentAt)) return false;
        pong = new PongDto(sequence, sentAt);
        return true;
    }
}

public static class NetText
{
    public static string Reject(RejectDto reject) => reject.Reason switch
    {
        RejectReason.Malformed => "房主無法辨識你的連線資料，請雙方更新到同一版插件。",
        RejectReason.VersionMismatch => $"插件版本不同（房主協定 v{reject.HostProtocolVersion}，你的協定 v{NetProtocol.Version}），請雙方更新到同一版。",
        RejectReason.RoomMismatch => "這個邀請碼屬於已關閉的房間，請向房主索取新的邀請碼。",
        RejectReason.RoomFull => $"房間已滿（最多 {NetProtocol.MaxPlayers} 人）。",
        _ => "房主拒絕了連線。",
    };

    public static string Disconnect(DisconnectInfo info)
    {
        if (info.Reason == DisconnectReason.ConnectionRejected && info.AdditionalData != null
            && RejectDto.TryRead(info.AdditionalData, out var reject))
            return Reject(reject);
        return info.Reason switch
        {
            DisconnectReason.ConnectionFailed => "連不到房主：房主的埠沒有開放、房間已關閉，或 Windows 防火牆擋住了連線。",
            DisconnectReason.Timeout => $"連線逾時：超過 {NetProtocol.DisconnectTimeoutMs / 1000} 秒沒有收到對方的資料。",
            DisconnectReason.RemoteConnectionClose => "對方關閉了連線。",
            DisconnectReason.DisconnectPeerCalled => "已中斷連線。",
            DisconnectReason.ConnectionRejected => "房主拒絕了連線。",
            _ => $"連線中斷（{info.Reason}）。",
        };
    }
}
