using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AnoMech.Core.Net;

public sealed class NetPlayer
{
    internal NetPlayer(byte id, string name, NetPeer peer)
    {
        Id = id;
        Name = name;
        Peer = peer;
    }

    public byte Id { get; }
    public string Name { get; }
    internal NetPeer Peer { get; }
    public int RoundTripMs => Peer.RoundTripTime;
    public int PingsReceived { get; internal set; }
}

// LiteNetLib raises every event inside Poll(), so all state here is touched from the thread
// that calls Poll (the framework thread in the plugin) and needs no locking.
public sealed class NetHost : IDisposable
{
    private readonly EventBasedNetListener listener = new();
    private readonly NetManager manager;
    private readonly Dictionary<int, NetPlayer> players = new();
    private readonly NetDataWriter writer = new();
    private readonly Action<string> log;
    private uint sequence;

    public NetHost(uint roomId, Action<string> log)
    {
        RoomId = roomId;
        this.log = log;
        manager = new NetManager(listener)
        {
            AutoRecycle = true,
            IPv6Enabled = false,
            DisconnectTimeout = NetProtocol.DisconnectTimeoutMs,
            UpdateTime = NetProtocol.UpdateTimeMs,
        };
        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
        listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public uint RoomId { get; }
    public int Port => manager.LocalPort;
    public bool IsRunning => manager.IsRunning;
    public IReadOnlyCollection<NetPlayer> Players => players.Values;

    public event Action<NetPlayer>? PlayerJoined;
    public event Action<NetPlayer, string>? PlayerLeft;
    public event Action<NetPlayer, PacketHeader, NetPacketReader>? MessageReceived;

    // firstPort 0 lets the OS pick (used by the loopback checks).
    public bool Start(ushort firstPort = NetProtocol.DefaultPort, int candidates = NetProtocol.PortCandidates)
    {
        for (var i = 0; i < candidates; i++)
        {
            var port = firstPort + i;
            if (!manager.Start(IPAddress.Any, IPAddress.IPv6Any, port)) continue;
            log($"[Net] Host listening on UDP {manager.LocalPort}, room {RoomId:X8}");
            return true;
        }
        log($"[Net] Host could not bind UDP {firstPort}-{firstPort + candidates - 1}");
        return false;
    }

    public void Poll() => manager.PollEvents();

    public void Stop()
    {
        if (manager.IsRunning) manager.Stop();
        players.Clear();
    }

    public void Dispose() => Stop();

    private void OnConnectionRequest(ConnectionRequest request)
    {
        try
        {
            if (!HelloDto.TryRead(request.Data, out var hello)) { Reject(request, RejectReason.Malformed); return; }
            if (hello.ProtocolVersion != NetProtocol.Version) { Reject(request, RejectReason.VersionMismatch); return; }
            if (hello.RoomId != RoomId) { Reject(request, RejectReason.RoomMismatch); return; }
            if (players.Count >= NetProtocol.MaxPlayers - 1) { Reject(request, RejectReason.RoomFull); return; }

            var peer = request.Accept();
            players[peer.Id] = new NetPlayer(NextFreeId(), hello.PlayerName, peer);
        }
        catch (Exception e)
        {
            log($"[Net] Connection request failed: {e.Message}");
        }
    }

    private void Reject(ConnectionRequest request, RejectReason reason)
    {
        writer.Reset();
        new RejectDto(reason, NetProtocol.Version).Write(writer);
        request.Reject(writer);
        log($"[Net] Rejected {request.RemoteEndPoint}: {reason}");
    }

    private void OnPeerConnected(NetPeer peer)
    {
        if (!players.TryGetValue(peer.Id, out var player))
        {
            manager.DisconnectPeer(peer);
            return;
        }
        Send(peer, MessageType.Welcome, w => new WelcomeDto(player.Id).Write(w), DeliveryMethod.ReliableOrdered);
        log($"[Net] Player #{player.Id} {player.Name} joined");
        PlayerJoined?.Invoke(player);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        if (!players.Remove(peer.Id, out var player)) return;
        var reason = NetText.Disconnect(info);
        log($"[Net] Player #{player.Id} {player.Name} left: {info.Reason}");
        PlayerLeft?.Invoke(player, reason);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
    {
        try
        {
            if (!players.TryGetValue(peer.Id, out var player)) return;
            if (!PacketHeader.TryRead(reader, out var header) || header.SenderId != player.Id)
            {
                log($"[Net] Dropped malformed packet from player #{player.Id}");
                return;
            }
            switch (header.Type)
            {
                case MessageType.Ping:
                    player.PingsReceived++;
                    Send(peer, MessageType.Pong, w => new PongDto(header.Sequence, header.SentAtMs).Write(w), DeliveryMethod.Unreliable);
                    break;
                default:
                    MessageReceived?.Invoke(player, header, reader);
                    break;
            }
        }
        catch (Exception e)
        {
            log($"[Net] Receive failed: {e.Message}");
        }
    }

    public void Send(NetPlayer player, MessageType type, Action<NetDataWriter> payload, DeliveryMethod method)
        => Send(player.Peer, type, payload, method);

    public void Broadcast(MessageType type, Action<NetDataWriter> payload, DeliveryMethod method)
    {
        foreach (var player in players.Values)
            Send(player.Peer, type, payload, method);
    }

    private void Send(NetPeer peer, MessageType type, Action<NetDataWriter> payload, DeliveryMethod method)
    {
        writer.Reset();
        new PacketHeader(type, ++sequence, NetProtocol.HostPlayerId, NetProtocol.NowMs).Write(writer);
        payload(writer);
        peer.Send(writer, method);
    }

    private byte NextFreeId()
    {
        for (byte id = 1; id < NetProtocol.MaxPlayers; id++)
            if (players.Values.All(p => p.Id != id)) return id;
        throw new InvalidOperationException("Room is full"); // unreachable: RoomFull is checked first
    }
}
