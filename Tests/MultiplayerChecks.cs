using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Multiplayer;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Top;
using AnoMech.Scenarios.Top.P5Delta;
using AnoMech.Scenarios.Top.P5Sigma;
using LiteNetLib.Utils;

internal static class MultiplayerChecks
{
    public static void Run()
    {
        try
        {
            SeededRandomRepeats();
            HostOnlyDrawsLeaveSharedStreamAlone();
            SchedulerKeepsHostOnlyScope();
            OverridesDropPlayerFieldsAndKeepInstances();
            DeltaStateMatchesAcrossMachines();
            MessagesRoundTripAndRejectGarbage();
            PlaybackClockBuffersAndTracks();
            PoseBufferInterpolates();
            DisconnectedSlotReturnsToAi();
        }
        finally
        {
            MultiplayerContext.End();
        }
        Console.WriteLine("Multiplayer: deterministic random, host-only scope, overrides, Delta state, messages, playback clock, pose buffer and disconnect takeover checks passed.");
    }

    private static void DisconnectedSlotReturnsToAi()
    {
        MultiplayerContext.Begin(MultiplayerRole.Host, 0b0010_0110, null);
        MultiplayerContext.ReleaseHuman(2);
        Check(!MultiplayerContext.IsHumanControlled(2), "a disconnected player's slot goes back to AI");
        Check(MultiplayerContext.IsHumanControlled(1) && MultiplayerContext.IsHumanControlled(5), "other players keep their slots");
        MultiplayerContext.End();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception($"MultiplayerChecks: {message}");
    }

    private static int[] Draws(int count) => Enumerable.Range(0, count).Select(_ => SimRandom.Current.Next(1000000)).ToArray();

    private static void SeededRandomRepeats()
    {
        SimRandom.Reseed(42, 7);
        var a = Draws(6);
        SimRandom.Reseed(42, 7);
        var b = Draws(6);
        SimRandom.Reseed(42, 8);
        var c = Draws(6);
        SimRandom.Disable();
        Check(a.SequenceEqual(b), "same seed and tick draw the same numbers");
        Check(!a.SequenceEqual(c), "the next tick draws different numbers");
        Check(!ReferenceEquals(SimRandom.Current, null), "solo play still has a random source");
    }

    private static void HostOnlyDrawsLeaveSharedStreamAlone()
    {
        SimRandom.Reseed(1, 0);
        var expected = Draws(3);
        SimRandom.Reseed(1, 0);
        using (SimRandom.HostOnly())
            Draws(10);
        var actual = Draws(3);
        SimRandom.Disable();
        Check(expected.SequenceEqual(actual), "AI draws inside HostOnly do not shift the shared stream");
    }

    private static void SchedulerKeepsHostOnlyScope()
    {
        var scheduler = new EventScheduler();
        bool? aiEvent = null, nestedAiEvent = null, scenarioEvent = null;
        using (SimRandom.HostOnly())
            scheduler.Add(1f, () =>
            {
                aiEvent = SimRandom.InHostOnly;
                scheduler.Add(0.5f, () => nestedAiEvent = SimRandom.InHostOnly);
            });
        scheduler.Add(1f, () => scenarioEvent = SimRandom.InHostOnly);
        scheduler.Tick(1.1f);
        scheduler.Tick(1f);
        Check(aiEvent == true && nestedAiEvent == true, "events scheduled by AI (and what they schedule) run host-only");
        Check(scenarioEvent == false && !SimRandom.InHostOnly, "scenario events run in the shared stream");
    }

    private static void OverridesDropPlayerFieldsAndKeepInstances()
    {
        var host = new TopP5SigmaStateOverrides
        {
            NewNorthA = Direction.E,
            CloseFarTether = GlitchType.Mid,
            SpinnerRotation = Rotation.Clockwise,
            TowerNorthFlip = true,
            HelloWorld = AnoMech.Scenarios.Top.P5Sigma.HelloWorldOption.Near,
            Dynamis = true,
            Markers = MarkerMode.Manual,
        };
        var client = MultiplayerOverrides.Deserialize<TopP5SigmaStateOverrides>(MultiplayerOverrides.Serialize(MultiplayerOverrides.Sanitize(host)));
        Check(client.NewNorthA == Direction.E && ReferenceEquals(client.CloseFarTether, GlitchType.Mid) && ReferenceEquals(client.SpinnerRotation, Rotation.Clockwise), "static instances arrive as the same instances");
        Check(client.TowerNorthFlip == true && client.Markers == MarkerMode.Manual, "strategy settings are carried");
        Check(client.HelloWorld == AnoMech.Scenarios.Top.P5Sigma.HelloWorldOption.Auto && client.Dynamis is null, "settings about the local player are reset");
        Check(client.NewNorthB is null, "unset settings stay unset");
        var garbage = MultiplayerOverrides.Deserialize<TopP5SigmaStateOverrides>([5, 1, 2, 3]);
        Check(garbage.NewNorthA is null && garbage.Markers == MarkerMode.System, "a malformed payload yields defaults instead of throwing");
    }

    private static void DeltaStateMatchesAcrossMachines()
    {
        var hostSettings = MultiplayerOverrides.Sanitize(new TopP5DeltaStateOverrides
        {
            EyeSpawn = NorthSouth.South,
            SwivelCannonSide = Side.Left,
            TetherAssignment = PlayerTetherAssignment.CloseInner,
            Monitor = true,
            BeyondDefence = true,
        });
        var clientSettings = MultiplayerOverrides.Deserialize<TopP5DeltaStateOverrides>(MultiplayerOverrides.Serialize(hostSettings));
        Check(hostSettings.TetherAssignment == PlayerTetherAssignment.Auto && hostSettings.Monitor is null && hostSettings.BeyondDefence is null, "Delta player-bound settings are reset");
        Check(ReferenceEquals(clientSettings.SwivelCannonSide, Side.Left) && clientSettings.EyeSpawn == NorthSouth.South, "Delta arena settings are carried");

        for (ulong seed = 1; seed <= 50; seed++)
        {
            SimRandom.Reseed(seed, -1);
            var onHost = new TopP5DeltaState(hostSettings, PartyRole.MainTank);
            SimRandom.Reseed(seed, -1);
            var onClient = new TopP5DeltaState(clientSettings, PartyRole.PhysRangedDps);
            Check(onHost.TetherOrder.SequenceEqual(onClient.TetherOrder) && onHost.FistRotations.SequenceEqual(onClient.FistRotations)
                  && onHost.FistColors.SequenceEqual(onClient.FistColors) && onHost.ArmHandedness.SequenceEqual(onClient.ArmHandedness)
                  && onHost.EyeSpawn == onClient.EyeSpawn && onHost.SwivelCannonSide == onClient.SwivelCannonSide
                  && onHost.OmegaMonitorSide == onClient.OmegaMonitorSide && onHost.PlayerMonitorSide == onClient.PlayerMonitorSide
                  && onHost.PlayerMonitorIndex == onClient.PlayerMonitorIndex && onHost.NearWorldTetherIndex == onClient.NearWorldTetherIndex
                  && onHost.FarWorldTetherIndex == onClient.FarWorldTetherIndex,
                $"Delta state is identical on host (MT) and client (R1) for seed {seed}");
        }
        SimRandom.Disable();
    }

    private static T RoundTrip<T>(Action<NetDataWriter> write, NetDataReaderFunc<T> read, string what)
    {
        var writer = new NetDataWriter();
        write(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer);
        Check(read(reader, out var value), $"{what} decodes");
        Check(reader.AvailableBytes == 0, $"{what} consumes exactly what was written");
        return value;
    }

    private delegate bool NetDataReaderFunc<T>(NetDataReader reader, out T value);

    private static bool Rejects<T>(Action<NetDataWriter> write, NetDataReaderFunc<T> read)
    {
        var writer = new NetDataWriter();
        write(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer);
        return !read(reader, out _);
    }

    private static void MessagesRoundTripAndRejectGarbage()
    {
        var poses = Enumerable.Range(0, 8).Select(i => new NetPose(new Vector3(i, 0.5f, -i), i * 0.1f)).ToArray();
        byte[] owners = [0, 1, 255, 255, 2, 255, 255, 255];
        var looks = new byte[]?[Wire.Slots];
        looks[4] = Enumerable.Range(0, 90).Select(i => (byte)i).ToArray();
        var start = new StartRunDto(3, 5, 1, 0, 0xDEADBEEFCAFEUL, 1f, true, owners, [1, 2, 3], poses, looks);
        var startBack = RoundTrip(start.Write, (NetDataReader r, out StartRunDto v) => StartRunDto.TryRead(r, out v), "StartRun");
        Check(startBack.Seed == start.Seed && startBack.SlotOwners.SequenceEqual(owners) && startBack.OverridePayload.SequenceEqual(new byte[] { 1, 2, 3 })
              && startBack.InitialPoses.SequenceEqual(poses) && startBack.GodMode, "StartRun fields survive");
        Check(startBack.Appearances[4]!.SequenceEqual(looks[4]!) && startBack.Appearances[0] is null && startBack.Appearances[7] is null,
            "each slot's appearance survives and slots without one stay empty");
        Check(Rejects(w => { w.Put((byte)(AppearanceDto.MaxBytes + 1)); }, (NetDataReader r, out int v) => { v = 0; return AppearanceDto.TryRead(r, out _); }),
            "an oversized appearance is rejected");
        Check(Rejects(new StartRunDto(3, 5, 1, 0, 1, 1f, false, [0, 9, 255, 255, 255, 255, 255, 255], [], poses, new byte[]?[Wire.Slots]).Write,
            (NetDataReader r, out StartRunDto v) => StartRunDto.TryRead(r, out v)), "StartRun with an out-of-range player id is rejected");

        var frame = new TickFrame { Tick = 120, PoseMask = 0b10000001, Markers = Enumerable.Repeat(Wire.NoRole, Wire.MarkerSlots).ToArray() };
        frame.Poses[0] = poses[1];
        frame.Poses[7] = poses[6];
        frame.Markers[0] = 3;
        frame.MarkerAcks[4] = 7;
        frame.TimelineMask = 0b00010010;
        frame.Timelines[1] = 25000;
        frame.Timelines[4] = Wire.InferTimeline;
        frame.Invulns.Add((2, 10f));
        frame.Sync = new SyncStateDto(12.5f, [false, true, false, false, false, false, false, false],
            Enumerable.Range(0, 8).Select(i => i == 1 ? new (ushort, ushort)[] { (409, 1), (3009, 2) } : []).ToArray());
        var (runId, frames) = RoundTrip(w => FrameCodec.WriteBatch(w, 9, [frame, new TickFrame { Tick = 121 }]),
            (NetDataReader r, out (uint, List<TickFrame>) v) => { var ok = FrameCodec.TryReadBatch(r, out var id, out var list); v = (id, list); return ok; }, "frame batch");
        var back = frames[0];
        Check(runId == 9 && frames.Count == 2 && frames[1].Tick == 121 && frames[1].PoseMask == 0, "batch keeps run id and every frame");
        Check(back.Tick == 120 && back.PoseMask == frame.PoseMask && back.Poses[0] == poses[1] && back.Poses[7] == poses[6], "only masked poses are sent and restored");
        Check(back.Markers![0] == 3 && back.Markers[1] == Wire.NoRole && back.Invulns.Single() == (2, 10f), "markers and invulnerability survive");
        Check(back.MarkerAcks[4] == 7 && back.MarkerAcks[0] == 0, "marker request acks survive");
        Check(back.TimelineMask == 0b00010010 && back.Timelines[1] == 25000 && back.Timelines[4] == Wire.InferTimeline && back.Timelines[0] == 0,
            "owner animations survive without an id cap");
        Check(frames[1].TimelineMask == 0 && frames[1].Markers == null, "a frame without changes carries no animations or markers");
        Check(back.Sync!.Dead[1] && back.Sync.Statuses[1].SequenceEqual(new (ushort, ushort)[] { (409, 1), (3009, 2) }) && back.Sync.ScenarioElapsed == 12.5f, "sync state survives");

        var badPose = new TickFrame { Tick = 1, PoseMask = 1 };
        badPose.Poses[0] = new NetPose(new Vector3(float.NaN, 0, 0), 0);
        Check(Rejects(w => FrameCodec.WriteBatch(w, 1, [badPose]), (NetDataReader r, out int v) => { v = 0; return FrameCodec.TryReadBatch(r, out _, out _); }), "a NaN pose is rejected");
        var badMarker = new TickFrame { Tick = 1, Markers = Enumerable.Repeat((byte)9, Wire.MarkerSlots).ToArray() };
        Check(Rejects(w => FrameCodec.WriteBatch(w, 1, [badMarker]), (NetDataReader r, out int v) => { v = 0; return FrameCodec.TryReadBatch(r, out _, out _); }), "a marker on slot 9 is rejected");
        Check(Rejects(w => { w.Put(1u); w.Put((byte)65); }, (NetDataReader r, out int v) => { v = 0; return FrameCodec.TryReadBatch(r, out _, out _); }), "an oversized batch is rejected");

        var transform = RoundTrip(new TransformDto(4, poses[3], 4321).Write, (NetDataReader r, out TransformDto v) => TransformDto.TryRead(r, out v), "Transform");
        Check(transform == new TransformDto(4, poses[3], 4321), "Transform and its animation survive");
        var marks = Enumerable.Repeat(Wire.NoRole, Wire.MarkerSlots).ToArray();
        marks[5] = 2;
        var stopped = RoundTrip(new StopRunDto(7, StopReason.HostStopped).Write,
            (NetDataReader r, out StopRunDto v) => StopRunDto.TryRead(r, out v), "StopRun");
        Check(stopped == new StopRunDto(7, StopReason.HostStopped), "StopRun keeps its run id and reason");
        // Reset and Leave used to send the identical StopRun, so the room could not know to follow the host out.
        var left = RoundTrip(new StopRunDto(0, StopReason.HostLeft).Write,
            (NetDataReader r, out StopRunDto v) => StopRunDto.TryRead(r, out v), "StopRun(HostLeft)");
        Check(left.Reason == StopReason.HostLeft && left.Reason != stopped.Reason,
            "Leaving must reach the room as its own reason, distinct from stopping the run");
        var marksBack = RoundTrip(new MarkersDto(4, 11, 1u << 5, marks).Write, (NetDataReader r, out MarkersDto v) => MarkersDto.TryRead(r, out v), "Markers");
        Check(marksBack.RunId == 4 && marksBack.RequestId == 11 && marksBack.ChangedMask == 1u << 5 && marksBack.Markers.SequenceEqual(marks), "a client's marker change survives");
        Check(Rejects(new MarkersDto(4, 1, 1, Enumerable.Repeat((byte)8, Wire.MarkerSlots).ToArray()).Write,
            (NetDataReader r, out MarkersDto v) => MarkersDto.TryRead(r, out v)), "a client marker on slot 8 is rejected");
        Check(Rejects(new MarkersDto(4, 1, 1u << Wire.MarkerSlots, marks).Write,
            (NetDataReader r, out MarkersDto v) => MarkersDto.TryRead(r, out v)), "a change mask past the last sign is rejected");
        var ready = RoundTrip(new ReadyDto(true, "忙碌中（Mounted）").Write, (NetDataReader r, out ReadyDto v) => ReadyDto.TryRead(r, out v), "Ready");
        Check(ready == new ReadyDto(true, "忙碌中（Mounted）"), "the reason a member cannot start survives");
        var lobby = RoundTrip(new LobbyStateDto([new LobbyPlayerDto(0, "房主", 0, true, ""), new LobbyPlayerDto(1, "朋友", Wire.NoRole, false, "不在旅館或住宅室內")], true).Write,
            (NetDataReader r, out LobbyStateDto v) => LobbyStateDto.TryRead(r, out v), "LobbyState");
        Check(lobby.RunActive && lobby.Players.Count == 2 && lobby.Players[0].CanStart
              && lobby.Players[1] == new LobbyPlayerDto(1, "朋友", Wire.NoRole, false, "不在旅館或住宅室內") && !lobby.Players[1].CanStart, "LobbyState survives");
        Check(Rejects(new RoleRequestDto(8).Write, (NetDataReader r, out RoleRequestDto v) => RoleRequestDto.TryRead(r, out v)), "role 8 is rejected");
    }

    private static void PlaybackClockBuffersAndTracks()
    {
        var clock = new PlaybackClock(0.2f);
        Check(clock.DelayTicks == 12, "0.2 s is 12 ticks");
        for (var t = 0; t < 11; t++) clock.OnReceived(t);
        Check(clock.Advance(PlaybackClock.Step) == 0 && !clock.Started, "playback waits for the delay buffer");
        clock.OnReceived(11);

        long received = 11;
        var ran = 0;
        var minBuffer = int.MaxValue;
        var maxBuffer = 0;
        for (var frame = 0; frame < 600; frame++)
        {
            clock.OnReceived(++received);
            ran += clock.Advance(PlaybackClock.Step);
            if (frame < 60) continue;
            minBuffer = Math.Min(minBuffer, clock.BufferedTicks);
            maxBuffer = Math.Max(maxBuffer, clock.BufferedTicks);
        }
        Check(Math.Abs(ran - 600) <= 3, $"steady playback runs one tick per host tick (ran {ran})");
        Check(minBuffer >= 8 && maxBuffer <= 16, $"buffer stays near the delay ({minBuffer}..{maxBuffer})");

        for (var frame = 0; frame < 120; frame++) clock.Advance(PlaybackClock.Step);
        Check(clock.BufferedTicks == 0 && clock.Advance(PlaybackClock.Step) == 0, "playback stalls when the host stops sending");

        for (var t = 0; t < 60; t++) clock.OnReceived(++received);
        var catchUpFrames = 0;
        while (clock.BufferedTicks > clock.DelayTicks && catchUpFrames < 1000)
        {
            clock.Advance(PlaybackClock.Step);
            catchUpFrames++;
        }
        Check(catchUpFrames < 45, $"after a stall playback drains the extra 48 ticks faster than real time ({catchUpFrames} frames)");
    }

    private static void PoseBufferInterpolates()
    {
        var buffer = new PoseBuffer();
        buffer.Add(0, new NetPose(Vector3.Zero, 0));
        buffer.Add(50, new NetPose(new Vector3(5, 0, 0), 0));
        buffer.Add(40, new NetPose(new Vector3(99, 0, 0), 0));
        Check(buffer.Count == 2, "an out-of-order sample is ignored");
        Check(buffer.TrySample(25, 20, out var middle) && MathF.Abs(middle.Position.X - 2.5f) < 1e-4f, "halfway between samples");
        Check(buffer.TrySample(-10, 20, out var early) && early.Position.X == 0, "before the first sample holds the first pose");
        Check(buffer.TrySample(200, 20, out var late) && MathF.Abs(late.Position.X - 7f) < 1e-4f, "extrapolation is capped");
        var wrapped = PoseBuffer.Lerp(new NetPose(Vector3.Zero, 3f), new NetPose(Vector3.Zero, -3f), 0.5f);
        Check(MathF.Abs(MathF.Abs(wrapped.Rotation) - MathF.PI) < 0.01f, "rotation interpolates across the ±π seam");
    }
}
