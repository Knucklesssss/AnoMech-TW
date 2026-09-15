using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AnoMech.Core.Net;

public enum ClientState
{
    Idle,
    Connecting,
    Connected,
    Disconnected,
}

public sealed class NetClient : IDisposable
{
    private const double PingIntervalMs = 1000;

    private readonly EventBasedNetListener listener = new();
    private readonly NetManager manager;
    private readonly NetDataWriter writer = new();
    private readonly Func<string> playerName;
    private readonly Action<string> log;
    private NetPeer? peer;
    private uint sequence;
    private double lastPingAt = double.NegativeInfinity;

    public NetClient(Func<string> playerName, Action<string> log)
    {
        this.playerName = playerName;
        this.log = log;
        manager = new NetManager(listener)
        {
            AutoRecycle = true,
            IPv6Enabled = false,
            DisconnectTimeout = NetProtocol.DisconnectTimeoutMs,
            UpdateTime = NetProtocol.UpdateTimeMs,
        };
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
        listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public ClientState State { get; private set; } = ClientState.Idle;
    public byte PlayerId { get; private set; }
    public double LastRttMs { get; private set; } = -1;
    public int PongsReceived { get; private set; }
    public string LastMessage { get; private set; } = "";
    public InviteCode? Invite { get; private set; }
    public event Action<PacketHeader, NetPacketReader>? MessageReceived;

    public void Connect(InviteCode invite)
    {
        Disconnect();
        if (!manager.IsRunning && !manager.Start())
        {
            State = ClientState.Disconnected;
            LastMessage = "無法啟動本機 UDP 網路。";
            return;
        }
        Invite = invite;
        PlayerId = 0;
        LastRttMs = -1;
        PongsReceived = 0;
        lastPingAt = double.NegativeInfinity;

        writer.Reset();
        new HelloDto(NetProtocol.Version, invite.RoomId, playerName()).Write(writer);
        peer = manager.Connect(invite.Address.ToString(), invite.Port, writer);
        State = ClientState.Connecting;
        LastMessage = $"正在連線到 {invite.Address}:{invite.Port}…";
        log($"[Net] Connecting to {invite.Address}:{invite.Port}, room {invite.RoomId:X8}");
    }

    public void Reconnect()
    {
        if (Invite is { } invite) Connect(invite);
    }

    public void Disconnect()
    {
        if (peer != null)
        {
            manager.DisconnectPeer(peer);
            peer = null;
        }
        if (State is ClientState.Connecting or ClientState.Connected)
        {
            State = ClientState.Disconnected;
            LastMessage = "已中斷連線。";
            LastRttMs = -1;
        }
    }

    public void Poll()
    {
        manager.PollEvents();
        if (State != ClientState.Connected || peer == null || NetProtocol.NowMs - lastPingAt < PingIntervalMs) return;
        lastPingAt = NetProtocol.NowMs;
        writer.Reset();
        new PacketHeader(MessageType.Ping, ++sequence, PlayerId, NetProtocol.NowMs).Write(writer);
        peer.Send(writer, DeliveryMethod.Unreliable);
    }

    public void Send(MessageType type, Action<NetDataWriter> payload, DeliveryMethod method)
    {
        if (State != ClientState.Connected || peer == null) return;
        writer.Reset();
        new PacketHeader(type, ++sequence, PlayerId, NetProtocol.NowMs).Write(writer);
        payload(writer);
        peer.Send(writer, method);
    }

    public void Dispose()
    {
        Disconnect();
        if (manager.IsRunning) manager.Stop();
    }

    private void OnPeerDisconnected(NetPeer disconnected, DisconnectInfo info)
    {
        // A Disconnect()+Connect() pair leaves the old peer's event in the queue; ignore it.
        if (!ReferenceEquals(disconnected, peer)) return;
        peer = null;
        State = ClientState.Disconnected;
        LastRttMs = -1;
        LastMessage = NetText.Disconnect(info);
        log($"[Net] Disconnected: {info.Reason}");
    }

    private void OnNetworkReceive(NetPeer from, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        try
        {
            if (!ReferenceEquals(from, peer)) return;
            if (!PacketHeader.TryRead(reader, out var header) || header.SenderId != NetProtocol.HostPlayerId)
            {
                log("[Net] Dropped malformed packet from host");
                return;
            }
            switch (header.Type)
            {
                case MessageType.Welcome when WelcomeDto.TryRead(reader, out var welcome):
                    PlayerId = welcome.PlayerId;
                    State = ClientState.Connected;
                    LastMessage = $"已連線，你的玩家編號是 #{PlayerId}。";
                    log($"[Net] Welcomed as player #{PlayerId}");
                    break;
                case MessageType.Pong when State == ClientState.Connected && PongDto.TryRead(reader, out var pong):
                    LastRttMs = NetProtocol.NowMs - pong.EchoSentAtMs;
                    PongsReceived++;
                    break;
                case MessageType.Welcome or MessageType.Pong:
                    log($"[Net] Malformed {header.Type} from host");
                    break;
                default:
                    if (State == ClientState.Connected) MessageReceived?.Invoke(header, reader);
                    break;
            }
        }
        catch (Exception e)
        {
            log($"[Net] Receive failed: {e.Message}");
        }
    }
}
