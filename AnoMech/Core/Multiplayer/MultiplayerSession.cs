using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Map;
using AnoMech.Core.Net;
using AnoMech.Core.SimObjects;
using AnoMech.Helpers;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using LiteNetLib;
using LiteNetLib.Utils;
using SimGame = AnoMech.Core.Game.Game;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AnoMech.Core.Multiplayer;

// Host-authoritative multiplayer run. The host simulates the scenario at a fixed 60 Hz and sends
// every tick's party poses, markers and AI-granted invulnerability. Clients replay the same ticks
// with the same random seed about NetworkPlaybackDelay behind, so every mechanic resolves against
// the host's positions and lands on the same result; a once-a-second state check reports any
// divergence and applies the host's deaths.
internal sealed unsafe class MultiplayerSession : IDisposable
{
    public const float Step = PlaybackClock.Step;
    private const int MaxHostTicksPerFrame = 5;
    private const float SendIntervalSeconds = 0.05f;
    private const double RemoteInterpolationMs = 100;
    private const double RemoteExtrapolationMs = 150;
    private const int SyncIntervalTicks = 60;
    private const int FrameBatchBytes = 1000;

    public sealed class LobbyEntry
    {
        public byte Id { get; init; }
        public string Name { get; set; } = "";
        public byte Role { get; set; } = Wire.NoRole;
        public bool Ready { get; set; }
        public bool CanStart { get; set; }
    }

    private sealed class RemoteHuman(byte role, NetworkPuppet puppet)
    {
        public byte Role { get; } = role;
        public NetworkPuppet Puppet { get; } = puppet;
        public PoseBuffer Buffer { get; } = new();
        public bool Connected { get; set; } = true;
    }

    private sealed class HostRun(uint runId, ulong seed)
    {
        public uint RunId { get; } = runId;
        public ulong Seed { get; } = seed;
        public long Tick { get; set; }
        public double Accumulator { get; set; }
        public float SendTimer { get; set; }
        public Dictionary<byte, RemoteHuman> Remote { get; } = new();
        public NetPose[] LastSent { get; } = new NetPose[Wire.Slots];
        public byte[] LastMarkers { get; set; } = Enumerable.Repeat((byte)254, Wire.MarkerSlots).ToArray();
        public List<(byte Role, float Seconds)> PendingInvulns { get; } = [];
        public List<TickFrame> Pending { get; } = [];
    }

    private sealed class ClientRun(uint runId, ulong seed, byte role, float delaySeconds)
    {
        public uint RunId { get; } = runId;
        public ulong Seed { get; } = seed;
        public byte Role { get; } = role;
        public PlaybackClock Clock { get; } = new(delaySeconds);
        public Dictionary<uint, TickFrame> Frames { get; } = new();
        public NetPose[] Logic { get; } = new NetPose[Wire.Slots];
        public NetPose[] VisualPrevious { get; } = new NetPose[Wire.Slots];
        public NetPose[] VisualCurrent { get; } = new NetPose[Wire.Slots];
        public NetworkPuppet?[] Puppets { get; } = new NetworkPuppet?[Wire.Slots];
        public float SendTimer { get; set; }
        public float PreviousTimeScale { get; init; }
        public bool PreviousGodMode { get; init; }
    }

    private readonly SimGame game;
    private readonly Dictionary<byte, LobbyEntry> hostLobby = new();
    private NetHost? attachedHost;
    private ClientState lastClientState = ClientState.Idle;
    private (bool Ready, bool CanStart)? lastSentReady;
    private HostRun? hostRun;
    private ClientRun? clientRun;
    private uint runCounter;

    public MultiplayerSession(SimGame game)
    {
        this.game = game;
        Net = new ConnectionTestSession(LocalName, message => Plugin.Log.Info(message));
        Net.Client.MessageReceived += OnClientMessage;
    }

    public ConnectionTestSession Net { get; }

    public bool IsHosting => Net.Host is not null;
    public bool HostHasPlayers => Net.Host is { Players.Count: > 0 };
    public bool HostControlsRun => IsHosting && (HostHasPlayers || hostRun is not null);
    public bool IsClientConnected => Net.Client.State == ClientState.Connected;
    public bool RunActive => hostRun is not null || clientRun is not null;
    public bool ClientRunActive => clientRun is not null;
    public LobbyStateDto? ClientLobby { get; private set; }
    public bool ClientReady { get; set; }
    public byte ClientRequestedRole { get; private set; } = Wire.NoRole;
    public int MismatchCount { get; private set; }

    public IReadOnlyList<LobbyEntry> HostLobby => hostLobby.Values.OrderBy(e => e.Id).ToList();

    public string RunStatus => hostRun is { } host
        ? $"多人場景進行中：第 {host.Tick} 幀（每秒 60 幀）"
        : clientRun is { } client
            ? client.Clock.Started
                ? $"多人場景進行中：第 {client.Clock.NextTick} 幀，緩衝 {client.Clock.BufferedTicks} 幀（目標 {client.Clock.DelayTicks}），同步差異 {MismatchCount} 次"
                : $"正在緩衝房主的畫面（{Math.Max(0, client.Clock.BufferedTicks)}/{client.Clock.DelayTicks} 幀）…"
            : "";

    // Returns true when the multiplayer run drove Game.Tick this frame.
    public bool Update(float deltaSeconds)
    {
        Net.Poll();
        SyncHostAttachment();
        SyncClientConnection();

        if (hostRun is not null)
        {
            HostRunUpdate(hostRun, deltaSeconds);
            return true;
        }
        if (clientRun is not null)
        {
            ClientRunUpdate(clientRun, deltaSeconds);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (hostRun is not null) EndHostRun(broadcast: true);
        if (clientRun is not null) EndClientRun(null, reset: false);
        Net.Dispose();
    }

    // ---------------- Host ----------------

    public void HostSetRole(byte playerId, byte role)
    {
        if (!hostLobby.TryGetValue(playerId, out var entry) || !Wire.ValidRole(role, true)) return;
        if (role != Wire.NoRole && hostLobby.Values.FirstOrDefault(e => e != entry && e.Role == role) is { } holder)
            holder.Role = entry.Role;
        entry.Role = role;
        BroadcastLobby();
    }

    public bool CanHostStart(IScenario scenario, out string reason)
    {
        reason = "";
        if (Net.Host is null) { reason = "尚未建立房間。"; return false; }
        if (scenario.Phase.Zone is not TopZone) { reason = "多人同步目前只支援絕歐米茄（P2～P6）。"; return false; }
        if (!ZoneSession.CanStartHere() || ZoneSession.IsPlayerBusy()) { reason = "房主必須在旅館或住宅室內，且沒有忙碌。"; return false; }
        if (hostLobby.Count <= 1) { reason = "尚無玩家加入。"; return false; }
        foreach (var entry in hostLobby.Values)
        {
            if (entry.Role == Wire.NoRole) { reason = $"{entry.Name} 尚未分配職能。"; return false; }
            if (entry.Id == NetProtocol.HostPlayerId) continue;
            if (!entry.Ready) { reason = $"{entry.Name} 尚未準備。"; return false; }
            if (!entry.CanStart) { reason = $"{entry.Name} 不在旅館或住宅室內，或正在忙碌。"; return false; }
        }
        return true;
    }

    public void HostStartRun(IScenario scenario, int strat, int waymark)
    {
        if (!CanHostStart(scenario, out var reason))
        {
            Chat($"無法開始多人場景：{reason}");
            return;
        }
        if (hostRun is not null) EndHostRun(broadcast: true);

        var scenarioIndex = game.Scenarios.ToList().IndexOf(scenario);
        var self = hostLobby[NetProtocol.HostPlayerId];
        var seed = (ulong)Random.Shared.NextInt64();
        var humanMask = hostLobby.Values.Where(e => e.Id != NetProtocol.HostPlayerId).Aggregate(0, (mask, e) => mask | (1 << e.Role));

        MultiplayerContext.Begin(MultiplayerRole.Host, humanMask, null);
        SimRandom.Reseed(seed, -1);
        if (!game.StartScenarioNow(scenario, (PartyRole)self.Role, strat, waymark))
        {
            MultiplayerContext.End();
            Chat("房主這邊無法開始場景（請確認在旅館或住宅室內，且沒有切換到其他副本）。");
            return;
        }

        var run = new HostRun(++runCounter, seed);
        MultiplayerContext.InvulnGranted = (role, seconds) => run.PendingInvulns.Add(((byte)role, seconds));
        var party = game.World.Party;
        var owners = Enumerable.Repeat(Wire.NoPlayer, Wire.Slots).ToArray();
        foreach (var entry in hostLobby.Values)
        {
            owners[entry.Role] = entry.Id;
            if (entry.Id == NetProtocol.HostPlayerId || party.Get(entry.Role) is not { } member) continue;
            member.NetworkDriven = true;
            WriteName(member, entry.Name);
            run.Remote[entry.Id] = new RemoteHuman(entry.Role, new NetworkPuppet(member));
        }

        var poses = new NetPose[Wire.Slots];
        for (var slot = 0; slot < Wire.Slots; slot++)
            if (party.Get(slot) is { } member)
                poses[slot] = new NetPose(member.Position, member.Rotation);
        Array.Copy(poses, run.LastSent, Wire.Slots);

        var start = new StartRunDto(run.RunId, (ushort)scenarioIndex, (byte)strat, (byte)waymark, seed,
            game.EventTimeScale, game.GodMode, owners, MultiplayerContext.OverridePayload ?? [], poses);
        Net.Host!.Broadcast(MessageType.StartRun, start.Write, DeliveryMethod.ReliableOrdered);
        hostRun = run;
        BroadcastLobby();
        Chat($"多人場景開始：{SimGame.FullName(scenario)}");
    }

    public void HostStopRun()
    {
        if (hostRun is not null) EndHostRun(broadcast: true);
    }

    private void SyncHostAttachment()
    {
        if (ReferenceEquals(Net.Host, attachedHost)) return;
        if (attachedHost is not null)
        {
            attachedHost.MessageReceived -= OnHostMessage;
            attachedHost.PlayerJoined -= OnPlayerJoined;
            attachedHost.PlayerLeft -= OnPlayerLeft;
            if (hostRun is not null) EndHostRun(broadcast: false);
            hostLobby.Clear();
        }
        attachedHost = Net.Host;
        if (attachedHost is null) return;
        attachedHost.MessageReceived += OnHostMessage;
        attachedHost.PlayerJoined += OnPlayerJoined;
        attachedHost.PlayerLeft += OnPlayerLeft;
        hostLobby[NetProtocol.HostPlayerId] = new LobbyEntry
        {
            Id = NetProtocol.HostPlayerId,
            Name = LocalName(),
            Role = (byte)PartyPresets.SkipRoleForJob(LocalJob()),
            Ready = true,
        };
    }

    private void OnPlayerJoined(NetPlayer player)
    {
        hostLobby[player.Id] = new LobbyEntry { Id = player.Id, Name = player.Name };
        AssignRole(hostLobby[player.Id], Wire.NoRole);
        BroadcastLobby();
        Chat($"{player.Name} 加入了房間。");
    }

    private void OnPlayerLeft(NetPlayer player, string reason)
    {
        hostLobby.Remove(player.Id);
        if (hostRun?.Remote.TryGetValue(player.Id, out var remote) == true)
        {
            remote.Connected = false;
            Chat($"{player.Name} 已斷線，他的角色會停在原地。");
        }
        else
        {
            Chat($"{player.Name} 離開了房間。");
        }
        BroadcastLobby();
    }

    private void OnHostMessage(NetPlayer player, PacketHeader header, NetPacketReader reader)
    {
        if (!hostLobby.TryGetValue(player.Id, out var entry)) return;
        switch (header.Type)
        {
            case MessageType.SetReady when ReadyDto.TryRead(reader, out var ready):
                entry.Ready = ready.Ready;
                entry.CanStart = ready.CanStart;
                BroadcastLobby();
                break;
            case MessageType.RequestRole when RoleRequestDto.TryRead(reader, out var request):
                AssignRole(entry, request.Role);
                BroadcastLobby();
                break;
            case MessageType.Transform when TransformDto.TryRead(reader, out var transform):
                if (hostRun is { } run && transform.RunId == run.RunId && run.Remote.TryGetValue(player.Id, out var remote))
                    remote.Buffer.Add(NetProtocol.NowMs, transform.Pose);
                break;
            case MessageType.RunFailed when RunFailedDto.TryRead(reader, out var failed):
                Chat($"{entry.Name} 無法開始場景：{failed.Reason}");
                break;
            default:
                Plugin.Log.Warning($"[Multiplayer] Dropped {header.Type} from player #{player.Id}");
                break;
        }
    }

    private void AssignRole(LobbyEntry entry, byte requested)
    {
        bool Free(byte role) => hostLobby.Values.All(e => e == entry || e.Role != role);
        if (requested != Wire.NoRole && Free(requested))
        {
            entry.Role = requested;
            return;
        }
        if (entry.Role != Wire.NoRole) return;
        for (byte role = 0; role < Wire.Slots; role++)
        {
            if (!Free(role)) continue;
            entry.Role = role;
            return;
        }
    }

    private void BroadcastLobby()
    {
        if (Net.Host is not { } host) return;
        if (hostLobby.TryGetValue(NetProtocol.HostPlayerId, out var self))
            self.CanStart = ZoneSession.CanStartHere() && !ZoneSession.IsPlayerBusy();
        var state = new LobbyStateDto(
            HostLobby.Select(e => new LobbyPlayerDto(e.Id, e.Name, e.Role, e.Ready, e.CanStart)).ToList(),
            hostRun is not null);
        host.Broadcast(MessageType.LobbyState, state.Write, DeliveryMethod.ReliableOrdered);
    }

    private void HostRunUpdate(HostRun run, float deltaSeconds)
    {
        var renderTime = NetProtocol.NowMs - RemoteInterpolationMs;
        foreach (var remote in run.Remote.Values)
            if (remote.Connected && remote.Buffer.TrySample(renderTime, RemoteExtrapolationMs, out var pose))
                remote.Puppet.Apply(pose, deltaSeconds);

        run.Accumulator += deltaSeconds;
        var ran = 0;
        while (run.Accumulator >= Step && ran < MaxHostTicksPerFrame)
        {
            run.Accumulator -= Step;
            RunHostTick(run);
            ran++;
        }
        if (run.Accumulator > Step) run.Accumulator = Step;

        run.SendTimer += deltaSeconds;
        if (run.SendTimer < SendIntervalSeconds) return;
        run.SendTimer = 0f;
        FlushFrames(run);
    }

    private void RunHostTick(HostRun run)
    {
        run.PendingInvulns.Clear();
        SimRandom.Reseed(run.Seed, run.Tick);
        game.Tick(Step);

        var party = game.World.Party;
        var frame = new TickFrame { Tick = (uint)run.Tick };
        for (var slot = 0; slot < Wire.Slots; slot++)
        {
            if (party.Get(slot) is not { } member) continue;
            var pose = new NetPose(member.Position, member.Rotation);
            if (pose == run.LastSent[slot]) continue;
            frame.PoseMask |= (byte)(1 << slot);
            frame.Poses[slot] = pose;
            run.LastSent[slot] = pose;
        }
        var markers = ReadMarkers(party);
        if (!markers.SequenceEqual(run.LastMarkers))
        {
            frame.Markers = markers;
            run.LastMarkers = markers;
        }
        frame.Invulns.AddRange(run.PendingInvulns);
        if (run.Tick % SyncIntervalTicks == 0) frame.Sync = CaptureSync();
        run.Pending.Add(frame);
        run.Tick++;
    }

    private void FlushFrames(HostRun run)
    {
        if (Net.Host is not { } host || run.Pending.Count == 0) return;
        var measure = new NetDataWriter();
        var batch = new List<TickFrame>();
        var bytes = 0;
        foreach (var frame in run.Pending)
        {
            measure.Reset();
            FrameCodec.Write(measure, frame);
            if (batch.Count > 0 && (bytes + measure.Length > FrameBatchBytes || batch.Count == Wire.MaxFramesPerBatch))
            {
                SendBatch(host, run.RunId, batch);
                batch = [];
                bytes = 0;
            }
            batch.Add(frame);
            bytes += measure.Length;
        }
        SendBatch(host, run.RunId, batch);
        run.Pending.Clear();
    }

    private static void SendBatch(NetHost host, uint runId, List<TickFrame> batch)
        => host.Broadcast(MessageType.Frames, w => FrameCodec.WriteBatch(w, runId, batch), DeliveryMethod.ReliableOrdered);

    private void EndHostRun(bool broadcast)
    {
        if (hostRun is not { } run) return;
        if (broadcast)
            Net.Host?.Broadcast(MessageType.StopRun, new StopRunDto(run.RunId, StopReason.HostStopped).Write, DeliveryMethod.ReliableOrdered);
        foreach (var remote in run.Remote.Values) remote.Puppet.Member.NetworkDriven = false;
        hostRun = null;
        MultiplayerContext.End();
        BroadcastLobby();
    }

    // ---------------- Client ----------------

    public void ClientRequestRole(byte role)
    {
        ClientRequestedRole = role;
        Net.Client.Send(MessageType.RequestRole, new RoleRequestDto(role).Write, DeliveryMethod.ReliableOrdered);
    }

    private void SyncClientConnection()
    {
        var state = Net.Client.State;
        if (state == ClientState.Connected && lastClientState != ClientState.Connected)
        {
            lastSentReady = null;
            ClientRequestRole(ClientRequestedRole == Wire.NoRole ? (byte)PartyPresets.SkipRoleForJob(LocalJob()) : ClientRequestedRole);
        }
        if (state != ClientState.Connected)
        {
            ClientLobby = null;
            if (clientRun is not null) EndClientRun("與房主的連線中斷，多人場景已結束。", reset: true);
        }
        lastClientState = state;
        if (state != ClientState.Connected) return;

        var ready = (ClientReady, ZoneSession.CanStartHere() && !ZoneSession.IsPlayerBusy());
        if (lastSentReady == ready) return;
        lastSentReady = ready;
        Net.Client.Send(MessageType.SetReady, new ReadyDto(ready.Item1, ready.Item2).Write, DeliveryMethod.ReliableOrdered);
    }

    private void OnClientMessage(PacketHeader header, NetPacketReader reader)
    {
        switch (header.Type)
        {
            case MessageType.LobbyState when LobbyStateDto.TryRead(reader, out var lobby):
                ClientLobby = lobby;
                break;
            case MessageType.StartRun when StartRunDto.TryRead(reader, out var start):
                ClientStart(start);
                break;
            case MessageType.Frames when FrameCodec.TryReadBatch(reader, out var runId, out var frames):
                if (clientRun is not { } run || runId != run.RunId) break;
                foreach (var frame in frames)
                {
                    run.Frames[frame.Tick] = frame;
                    run.Clock.OnReceived(frame.Tick);
                }
                break;
            case MessageType.StopRun when StopRunDto.TryRead(reader, out var stop):
                if (clientRun is { } active && stop.RunId == active.RunId)
                    EndClientRun("房主結束了多人場景。", reset: true);
                break;
            default:
                Plugin.Log.Warning($"[Multiplayer] Dropped malformed or unexpected {header.Type} from host");
                break;
        }
    }

    private void ClientStart(StartRunDto start)
    {
        if (clientRun is not null) EndClientRun(null, reset: false);

        string? failure = null;
        var previousTimeScale = game.EventTimeScale;
        var previousGodMode = game.GodMode;
        var role = Array.IndexOf(start.SlotOwners, Net.Client.PlayerId);
        if (start.ScenarioIndex >= game.Scenarios.Count || game.Scenarios[start.ScenarioIndex].Phase.Zone is not TopZone)
            failure = "雙方插件的場景清單不同，請更新到同一版。";
        else if (role < 0)
            failure = "房主沒有為你分配職能。";
        else if (!ZoneSession.CanStartHere() || ZoneSession.IsPlayerBusy())
            failure = "你不在旅館或住宅室內，或正在忙碌。";

        if (failure is null)
        {
            MultiplayerContext.Begin(MultiplayerRole.Client, 0, start.OverridePayload);
            SimRandom.Reseed(start.Seed, -1);
            game.EventTimeScale = start.EventTimeScale;
            game.GodMode = start.GodMode;
            if (!game.StartScenarioNow(game.Scenarios[start.ScenarioIndex], (PartyRole)role, start.Strat, start.Waymark))
            {
                MultiplayerContext.End();
                game.EventTimeScale = previousTimeScale;
                game.GodMode = previousGodMode;
                failure = "場景無法在你這邊啟動（請先離開其他副本回到房間）。";
            }
        }
        if (failure is not null)
        {
            Chat($"無法加入房主的場景：{failure}");
            Net.Client.Send(MessageType.RunFailed, new RunFailedDto(start.RunId, failure).Write, DeliveryMethod.ReliableOrdered);
            return;
        }

        var run = new ClientRun(start.RunId, start.Seed, (byte)role, Math.Clamp(Plugin.Config.MultiplayerPlaybackDelay, 0.05f, 1f))
        {
            PreviousTimeScale = previousTimeScale,
            PreviousGodMode = previousGodMode,
        };
        var party = game.World.Party;
        for (var slot = 0; slot < Wire.Slots; slot++)
        {
            if (party.Get(slot) is not { } member) continue;
            var pose = start.InitialPoses[slot];
            run.Logic[slot] = run.VisualPrevious[slot] = run.VisualCurrent[slot] = pose;
            member.SetLogicPose(pose.Position, pose.Rotation);
            if (slot == role) continue;
            member.NetworkDriven = true;
            member.SetNativePose(pose.Position, pose.Rotation);
            run.Puppets[slot] = new NetworkPuppet(member);
            if (start.SlotOwners[slot] != Wire.NoPlayer && ClientLobby?.Players.FirstOrDefault(p => p.Id == start.SlotOwners[slot]) is { Name.Length: > 0 } owner)
                WriteName(member, owner.Name);
        }
        MismatchCount = 0;
        clientRun = run;
        Chat($"房主開始了多人場景：{SimGame.FullName(game.Scenarios[start.ScenarioIndex])}");
    }

    private void ClientRunUpdate(ClientRun run, float deltaSeconds)
    {
        var first = run.Clock.NextTick;
        var count = run.Clock.Advance(deltaSeconds);
        for (var i = 0; i < count; i++)
        {
            var tick = (uint)(first + i);
            if (!run.Frames.Remove(tick, out var frame))
            {
                EndClientRun($"缺少房主第 {tick} 幀的資料，多人場景已結束。", reset: true);
                return;
            }
            RunClientTick(run, frame);
        }

        var alpha = run.Clock.Alpha;
        for (var slot = 0; slot < Wire.Slots; slot++)
            run.Puppets[slot]?.Apply(PoseBuffer.Lerp(run.VisualPrevious[slot], run.VisualCurrent[slot], alpha), deltaSeconds);

        run.SendTimer += deltaSeconds;
        if (run.SendTimer < SendIntervalSeconds || Plugin.ObjectTable.LocalPlayer is not { } local) return;
        run.SendTimer = 0f;
        var pose = new NetPose(game.World.Coordinates.ToLocal(local.Position), local.Rotation);
        Net.Client.Send(MessageType.Transform, new TransformDto(run.RunId, pose).Write, DeliveryMethod.Sequenced);
    }

    private void RunClientTick(ClientRun run, TickFrame frame)
    {
        var party = game.World.Party;
        if (frame.Markers is { } markers) ApplyMarkers(party, markers);
        foreach (var (role, seconds) in frame.Invulns) party.GiveInvuln((PartyRole)role, seconds);

        for (var slot = 0; slot < Wire.Slots; slot++)
        {
            if ((frame.PoseMask & (1 << slot)) != 0) run.Logic[slot] = frame.Poses[slot];
            run.VisualPrevious[slot] = run.VisualCurrent[slot];
            run.VisualCurrent[slot] = run.Logic[slot];
            if (party.Get(slot) is { } member) member.NetworkLogicPose = (run.Logic[slot].Position, run.Logic[slot].Rotation);
        }

        SimRandom.Reseed(run.Seed, frame.Tick);
        game.Tick(Step);
        if (frame.Sync is { } sync) CompareSync(sync, frame.Tick);
    }

    private void CompareSync(SyncStateDto host, uint tick)
    {
        var local = CaptureSync();
        var issues = new List<string>();
        if (BitConverter.SingleToInt32Bits(host.ScenarioElapsed) != BitConverter.SingleToInt32Bits(local.ScenarioElapsed))
            issues.Add($"場景時間 房主 {host.ScenarioElapsed:F4} / 本機 {local.ScenarioElapsed:F4}");
        var party = game.World.Party;
        for (var slot = 0; slot < Wire.Slots; slot++)
        {
            var label = ((PartyRole)slot).ToString();
            if (host.Dead[slot] != local.Dead[slot])
            {
                issues.Add($"{label} 死亡 房主 {host.Dead[slot]} / 本機 {local.Dead[slot]}");
                if (host.Dead[slot] && party.Get(slot) is { } member && member.IsAlive())
                    member.Die("房主判定死亡（同步修正）");
            }
            if (!host.Statuses[slot].SequenceEqual(local.Statuses[slot]))
                issues.Add($"{label} 狀態 房主 [{Format(host.Statuses[slot])}] / 本機 [{Format(local.Statuses[slot])}]");
        }
        if (issues.Count == 0) return;
        MismatchCount++;
        Plugin.Log.Warning($"[Multiplayer] State mismatch at tick {tick}: {string.Join("; ", issues)}");
    }

    private void EndClientRun(string? message, bool reset)
    {
        if (clientRun is { } run)
        {
            game.EventTimeScale = run.PreviousTimeScale;
            game.GodMode = run.PreviousGodMode;
        }
        clientRun = null;
        MultiplayerContext.End();
        if (reset) game.Reset();
        if (message is not null) Chat(message);
    }

    // ---------------- Shared ----------------

    private SyncStateDto CaptureSync()
    {
        var party = game.World.Party;
        var dead = new bool[Wire.Slots];
        var statuses = new (ushort, ushort)[Wire.Slots][];
        for (var slot = 0; slot < Wire.Slots; slot++)
        {
            var member = party.Get(slot);
            dead[slot] = member is ISimPartyMember { Dead: true };
            statuses[slot] = member?.ActiveStatuses
                                   .Select(s => (s.StatusId, s.Stacks))
                                   .OrderBy(s => s.StatusId).ThenBy(s => s.Stacks)
                                   .Take(Wire.MaxStatusesPerMember)
                                   .ToArray() ?? [];
        }
        return new SyncStateDto(game.ScenarioElapsed, dead, statuses);
    }

    private static byte[] ReadMarkers(SimParty party)
    {
        var markers = new byte[Wire.MarkerSlots];
        for (var sign = 0; sign < Wire.MarkerSlots; sign++)
        {
            markers[sign] = Wire.NoRole;
            var marked = Markings.Get((Sign)sign).ObjectId;
            if (marked == 0) continue;
            for (byte slot = 0; slot < Wire.Slots; slot++)
                if (party.Get(slot) is { } member && member.GameObjectId.ObjectId == marked)
                    markers[sign] = slot;
        }
        return markers;
    }

    private static void ApplyMarkers(SimParty party, byte[] markers)
    {
        for (var sign = 0; sign < Wire.MarkerSlots; sign++)
        {
            if (markers[sign] != Wire.NoRole && party.Get(markers[sign]) is { } member)
                Markings.Set((Sign)sign, member.GameObjectId);
            else
                Markings.Clear((Sign)sign);
        }
    }

    private static void WriteName(SimCharacter member, string name)
    {
        var chara = member.BattleCharaPtr;
        if (chara != null && name.Length > 0) GameObjectHelper.WriteName((NativeGameObject*)chara, name);
    }

    private static string Format((ushort Id, ushort Stacks)[] statuses)
        => string.Join(",", statuses.Select(s => s.Stacks > 1 ? $"{s.Id}x{s.Stacks}" : s.Id.ToString()));

    private static string LocalName() => Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? "玩家";

    private static uint LocalJob() => Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;

    private static void Chat(string message) => Plugin.ChatGui.Print($"[AnoMech 多人] {message}");
}
