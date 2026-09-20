using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Net;
using LiteNetLib.Utils;

namespace AnoMech.Core.Multiplayer;

public static class Wire
{
    public const byte NoRole = 255;
    public const byte NoPlayer = 255;
    // A slot whose owner's own animation is not relayed: the viewer infers run/stand from motion.
    public const ushort InferTimeline = ushort.MaxValue;
    public const int Slots = 8;
    public const int MarkerSlots = 17;
    public const int MaxStatusesPerMember = 16;
    public const int MaxOverrideBytes = 512;
    public const int MaxFramesPerBatch = 64;
    public const int MaxInvulnsPerFrame = 8;
    public const float CoordinateLimit = 4096f;

    public static bool ValidRole(byte role, bool allowNone) => role < Slots || (allowNone && role == NoRole);

    // The client used to begin with an empty mask, so IsHumanControlled was always false there and the AI
    // pressed limit breaks for remote players that the host left alone.
    public static int HumanSlotMask(byte[] slotOwners)
    {
        var mask = 0;
        for (var slot = 0; slot < Wire.Slots && slot < slotOwners.Length; slot++)
            if (slotOwners[slot] != Wire.NoPlayer) mask |= 1 << slot;
        return mask;
    }

    public static bool TryGetFinite(this NetDataReader reader, float limit, out float value)
        => reader.TryGetFloat(out value) && float.IsFinite(value) && MathF.Abs(value) <= limit;
}

public readonly record struct NetPose(Vector3 Position, float Rotation)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(Position.X);
        writer.Put(Position.Y);
        writer.Put(Position.Z);
        writer.Put(Rotation);
    }

    public static bool TryRead(NetDataReader reader, out NetPose pose)
    {
        pose = default;
        if (!reader.TryGetFinite(Wire.CoordinateLimit, out var x) || !reader.TryGetFinite(Wire.CoordinateLimit, out var y)
            || !reader.TryGetFinite(Wire.CoordinateLimit, out var z) || !reader.TryGetFinite(100f, out var rotation))
            return false;
        pose = new NetPose(new Vector3(x, y, z), rotation);
        return true;
    }
}

public readonly record struct LobbyPlayerDto(byte Id, string Name, byte Role, bool Ready, string Blocker)
{
    public bool CanStart => Blocker.Length == 0;
}

public sealed record LobbyStateDto(IReadOnlyList<LobbyPlayerDto> Players, bool RunActive)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put((byte)Players.Count);
        foreach (var p in Players)
        {
            writer.Put(p.Id);
            writer.Put(p.Name, NetProtocol.MaxNameLength);
            writer.Put(p.Role);
            writer.Put(p.Ready);
            writer.Put(p.Blocker, ReadyDto.MaxBlockerLength);
        }
        writer.Put(RunActive);
    }

    public static bool TryRead(NetDataReader reader, out LobbyStateDto state)
    {
        state = null!;
        if (!reader.TryGetByte(out var count) || count > NetProtocol.MaxPlayers) return false;
        var players = new List<LobbyPlayerDto>(count);
        for (var i = 0; i < count; i++)
        {
            if (!reader.TryGetByte(out var id) || id >= NetProtocol.MaxPlayers || !reader.TryGetString(out var name)
                || !reader.TryGetByte(out var role) || !Wire.ValidRole(role, true)
                || !reader.TryGetBool(out var ready) || !reader.TryGetString(out var blocker))
                return false;
            players.Add(new LobbyPlayerDto(id, name ?? "", role, ready, ReadyDto.Clip(blocker)));
        }
        if (!reader.TryGetBool(out var runActive)) return false;
        state = new LobbyStateDto(players, runActive);
        return true;
    }
}

// Blocker says why the member cannot start right now (empty when it can), so the host is not left guessing.
public readonly record struct ReadyDto(bool Ready, string Blocker)
{
    public const int MaxBlockerLength = 48;

    public void Write(NetDataWriter writer)
    {
        writer.Put(Ready);
        writer.Put(Blocker, MaxBlockerLength);
    }

    public static bool TryRead(NetDataReader reader, out ReadyDto ready)
    {
        ready = default;
        if (!reader.TryGetBool(out var r) || !reader.TryGetString(out var blocker)) return false;
        ready = new ReadyDto(r, Clip(blocker));
        return true;
    }

    public static string Clip(string? text) => text is null ? "" : text.Length > MaxBlockerLength ? text[..MaxBlockerLength] : text;
}

public readonly record struct RoleRequestDto(byte Role)
{
    public void Write(NetDataWriter writer) => writer.Put(Role);

    public static bool TryRead(NetDataReader reader, out RoleRequestDto request)
    {
        request = default;
        if (!reader.TryGetByte(out var role) || !Wire.ValidRole(role, true)) return false;
        request = new RoleRequestDto(role);
        return true;
    }
}

public sealed record StartRunDto(
    uint RunId,
    ushort ScenarioIndex,
    byte Strat,
    byte Waymark,
    ulong Seed,
    float EventTimeScale,
    bool GodMode,
    byte[] SlotOwners,
    byte[] OverridePayload,
    NetPose[] InitialPoses,
    byte[]?[] Appearances)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put(ScenarioIndex);
        writer.Put(Strat);
        writer.Put(Waymark);
        writer.Put(Seed);
        writer.Put(EventTimeScale);
        writer.Put(GodMode);
        for (var i = 0; i < Wire.Slots; i++) writer.Put(SlotOwners[i]);
        writer.Put((ushort)OverridePayload.Length);
        writer.Put(OverridePayload);
        for (var i = 0; i < Wire.Slots; i++) InitialPoses[i].Write(writer);
        for (var i = 0; i < Wire.Slots; i++) AppearanceDto.Write(writer, Appearances[i]);
    }

    public static bool TryRead(NetDataReader reader, out StartRunDto start)
    {
        start = null!;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetUShort(out var scenario) || !reader.TryGetByte(out var strat)
            || !reader.TryGetByte(out var waymark) || !reader.TryGetULong(out var seed)
            || !reader.TryGetFloat(out var scale) || !float.IsFinite(scale) || scale is < 0.05f or > 20f
            || !reader.TryGetBool(out var god))
            return false;
        var owners = new byte[Wire.Slots];
        for (var i = 0; i < Wire.Slots; i++)
            if (!reader.TryGetByte(out owners[i]) || (owners[i] >= NetProtocol.MaxPlayers && owners[i] != Wire.NoPlayer)) return false;
        if (!reader.TryGetUShort(out var payloadLength) || payloadLength > Wire.MaxOverrideBytes || reader.AvailableBytes < payloadLength) return false;
        var payload = new byte[payloadLength];
        reader.GetBytes(payload, payloadLength);
        var poses = new NetPose[Wire.Slots];
        for (var i = 0; i < Wire.Slots; i++)
            if (!NetPose.TryRead(reader, out poses[i])) return false;
        var appearances = new byte[]?[Wire.Slots];
        for (var i = 0; i < Wire.Slots; i++)
            if (!AppearanceDto.TryRead(reader, out appearances[i])) return false;
        start = new StartRunDto(runId, scenario, strat, waymark, seed, scale, god, owners, payload, poses, appearances);
        return true;
    }
}

// A player's look as raw bytes (PlayerAppearance owns the layout); empty means "keep the preset look".
public static class AppearanceDto
{
    public const int MaxBytes = 128;

    public static void Write(NetDataWriter writer, byte[]? blob)
    {
        var length = blob is null ? 0 : blob.Length;
        writer.Put((byte)length);
        if (blob is not null) writer.Put(blob);
    }

    public static bool TryRead(NetDataReader reader, out byte[]? blob)
    {
        blob = null;
        if (!reader.TryGetByte(out var length) || length > MaxBytes || reader.AvailableBytes < length) return false;
        if (length == 0) return true;
        blob = new byte[length];
        reader.GetBytes(blob, length);
        return true;
    }
}

public enum StopReason : byte
{
    HostStopped = 1,
    // The host left the instance, not just the run: the room follows them out.
    HostLeft = 2,
}

public readonly record struct StopRunDto(uint RunId, StopReason Reason)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put((byte)Reason);
    }

    public static bool TryRead(NetDataReader reader, out StopRunDto stop)
    {
        stop = default;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetByte(out var reason) || !Enum.IsDefined((StopReason)reason)) return false;
        stop = new StopRunDto(runId, (StopReason)reason);
        return true;
    }
}

public readonly record struct RunFailedDto(uint RunId, string Reason)
{
    public const int MaxReasonLength = 64;

    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put(Reason, MaxReasonLength);
    }

    public static bool TryRead(NetDataReader reader, out RunFailedDto failed)
    {
        failed = default;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetString(out var reason)) return false;
        reason ??= "";
        failed = new RunFailedDto(runId, reason.Length > MaxReasonLength ? reason[..MaxReasonLength] : reason);
        return true;
    }
}

// Timeline is the base ActionTimeline the sender's own character is playing, so others see
// its real walk, jump or emote instead of a run inferred from motion.
public readonly record struct TransformDto(uint RunId, NetPose Pose, ushort Timeline)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        Pose.Write(writer);
        writer.Put(Timeline);
    }

    public static bool TryRead(NetDataReader reader, out TransformDto transform)
    {
        transform = default;
        if (!reader.TryGetUInt(out var runId) || !NetPose.TryRead(reader, out var pose) || !reader.TryGetUShort(out var timeline)) return false;
        transform = new TransformDto(runId, pose, timeline);
        return true;
    }
}

// A client's hand-placed party signs. Only the signs in ChangedMask are applied, so two players
// marking at once do not erase each other; the host echoes RequestId back in its frames.
public sealed record MarkersDto(uint RunId, uint RequestId, uint ChangedMask, byte[] Markers)
{
    public const uint AllSigns = (1u << Wire.MarkerSlots) - 1;

    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put(RequestId);
        writer.Put(ChangedMask);
        for (var i = 0; i < Wire.MarkerSlots; i++) writer.Put(Markers[i]);
    }

    public static bool TryRead(NetDataReader reader, out MarkersDto markers)
    {
        markers = null!;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetUInt(out var requestId)
            || !reader.TryGetUInt(out var mask) || (mask & ~AllSigns) != 0) return false;
        var slots = new byte[Wire.MarkerSlots];
        for (var i = 0; i < Wire.MarkerSlots; i++)
            if (!reader.TryGetByte(out slots[i]) || !Wire.ValidRole(slots[i], true)) return false;
        markers = new MarkersDto(runId, requestId, mask, slots);
        return true;
    }
}

// A client asking for the shared bar. Carries no object id: SimEnemy ids are allocated per machine,
// so geometry is rebuilt from the caster's position and the point it aimed at instead.
public readonly record struct LimitBreakUsedDto(uint RunId, uint RequestId, uint ActionId, Vector3 CasterPosition, Vector3? Aim)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        writer.Put(RequestId);
        writer.Put(ActionId);
        writer.Put(CasterPosition.X);
        writer.Put(CasterPosition.Y);
        writer.Put(CasterPosition.Z);
        writer.Put(Aim.HasValue);
        if (Aim is { } aim)
        {
            writer.Put(aim.X);
            writer.Put(aim.Y);
            writer.Put(aim.Z);
        }
    }

    public static bool TryRead(NetDataReader reader, out LimitBreakUsedDto used)
    {
        used = default;
        if (!reader.TryGetUInt(out var runId) || !reader.TryGetUInt(out var requestId) || !reader.TryGetUInt(out var actionId)
            || !reader.TryGetFinite(Wire.CoordinateLimit, out var cx) || !reader.TryGetFinite(Wire.CoordinateLimit, out var cy)
            || !reader.TryGetFinite(Wire.CoordinateLimit, out var cz) || !reader.TryGetBool(out var hasAim)) return false;
        Vector3? aim = null;
        if (hasAim)
        {
            if (!reader.TryGetFinite(Wire.CoordinateLimit, out var ax) || !reader.TryGetFinite(Wire.CoordinateLimit, out var ay)
                || !reader.TryGetFinite(Wire.CoordinateLimit, out var az)) return false;
            aim = new Vector3(ax, ay, az);
        }
        used = new LimitBreakUsedDto(runId, requestId, actionId, new Vector3(cx, cy, cz), aim);
        return true;
    }
}

public sealed record SyncStateDto(float ScenarioElapsed, bool[] Dead, (ushort Id, ushort Stacks)[][] Statuses)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(ScenarioElapsed);
        for (var i = 0; i < Wire.Slots; i++)
        {
            writer.Put(Dead[i]);
            var statuses = Statuses[i];
            var count = Math.Min(statuses.Length, Wire.MaxStatusesPerMember);
            writer.Put((byte)count);
            for (var s = 0; s < count; s++)
            {
                writer.Put(statuses[s].Id);
                writer.Put(statuses[s].Stacks);
            }
        }
    }

    public static bool TryRead(NetDataReader reader, out SyncStateDto sync)
    {
        sync = null!;
        if (!reader.TryGetFloat(out var elapsed) || !float.IsFinite(elapsed)) return false;
        var dead = new bool[Wire.Slots];
        var statuses = new (ushort, ushort)[Wire.Slots][];
        for (var i = 0; i < Wire.Slots; i++)
        {
            if (!reader.TryGetBool(out dead[i]) || !reader.TryGetByte(out var count) || count > Wire.MaxStatusesPerMember) return false;
            statuses[i] = new (ushort, ushort)[count];
            for (var s = 0; s < count; s++)
            {
                if (!reader.TryGetUShort(out var id) || !reader.TryGetUShort(out var stacks)) return false;
                statuses[i][s] = (id, stacks);
            }
        }
        sync = new SyncStateDto(elapsed, dead, statuses);
        return true;
    }
}

// One fixed simulation tick as the host ran it. Poses are sent only for slots that changed.
public sealed class TickFrame
{
    public uint Tick { get; init; }
    public byte PoseMask { get; set; }
    public NetPose[] Poses { get; } = new NetPose[Wire.Slots];
    public byte[]? Markers { get; set; }
    // Sent with Markers: per slot, the last marker RequestId from that slot's player the table includes.
    public uint[] MarkerAcks { get; } = new uint[Wire.Slots];
    public byte TimelineMask { get; set; }
    public ushort[] Timelines { get; } = new ushort[Wire.Slots];
    public List<(byte Role, float Seconds)> Invulns { get; } = [];
    public byte LimitBreakHolder { get; set; } = LimitBreakArbiter.Nobody;
    public List<(byte Role, uint ActionId, Vector3 CasterPosition, Vector3? Aim)> LimitBreaks { get; } = [];
    public uint[] LimitBreakAcks { get; } = new uint[Wire.Slots];
    // Judging moved to the host, so the reason has to travel to the room that only sees the wipe.
    public string? FailReason { get; set; }
    public SyncStateDto? Sync { get; set; }
}

public static class FrameCodec
{
    private const byte HasMarkers = 1;
    private const byte HasInvulns = 2;
    private const byte HasSync = 4;
    private const byte HasTimelines = 8;
    private const byte HasLimitBreaks = 16;
    private const byte HasFailReason = 32;
    private const int MaxLimitBreaksPerFrame = 8;
    private const int MaxFailReasonBytes = 256;

    public static void WriteBatch(NetDataWriter writer, uint runId, IReadOnlyList<TickFrame> frames)
    {
        writer.Put(runId);
        writer.Put((byte)frames.Count);
        foreach (var frame in frames) Write(writer, frame);
    }

    public static bool TryReadBatch(NetDataReader reader, out uint runId, out List<TickFrame> frames)
    {
        frames = [];
        if (!reader.TryGetUInt(out runId) || !reader.TryGetByte(out var count) || count > Wire.MaxFramesPerBatch) return false;
        for (var i = 0; i < count; i++)
        {
            if (!TryRead(reader, out var frame)) return false;
            frames.Add(frame);
        }
        return true;
    }

    public static void Write(NetDataWriter writer, TickFrame frame)
    {
        var failReason = ClampFailReason(frame.FailReason);
        var flags = (byte)((frame.Markers != null ? HasMarkers : 0) | (frame.Invulns.Count > 0 ? HasInvulns : 0)
                           | (frame.Sync != null ? HasSync : 0) | (frame.TimelineMask != 0 ? HasTimelines : 0)
                           | (frame.LimitBreaks.Count > 0 || frame.LimitBreakHolder != LimitBreakArbiter.Nobody ? HasLimitBreaks : 0)
                           | (failReason != null ? HasFailReason : 0));
        writer.Put(frame.Tick);
        writer.Put(flags);
        writer.Put(frame.PoseMask);
        for (var slot = 0; slot < Wire.Slots; slot++)
            if ((frame.PoseMask & (1 << slot)) != 0) frame.Poses[slot].Write(writer);
        if (frame.Markers != null)
        {
            for (var i = 0; i < Wire.MarkerSlots; i++) writer.Put(frame.Markers[i]);
            for (var i = 0; i < Wire.Slots; i++) writer.Put(frame.MarkerAcks[i]);
        }
        if (frame.TimelineMask != 0)
        {
            writer.Put(frame.TimelineMask);
            for (var slot = 0; slot < Wire.Slots; slot++)
                if ((frame.TimelineMask & (1 << slot)) != 0) writer.Put(frame.Timelines[slot]);
        }
        if (frame.Invulns.Count > 0)
        {
            var count = Math.Min(frame.Invulns.Count, Wire.MaxInvulnsPerFrame);
            writer.Put((byte)count);
            for (var i = 0; i < count; i++)
            {
                writer.Put(frame.Invulns[i].Role);
                writer.Put(frame.Invulns[i].Seconds);
            }
        }
        if ((flags & HasLimitBreaks) != 0)
        {
            writer.Put(frame.LimitBreakHolder);
            for (var i = 0; i < Wire.Slots; i++) writer.Put(frame.LimitBreakAcks[i]);
            var count = Math.Min(frame.LimitBreaks.Count, MaxLimitBreaksPerFrame);
            writer.Put((byte)count);
            for (var i = 0; i < count; i++)
            {
                var (role, actionId, caster, aim) = frame.LimitBreaks[i];
                writer.Put(role);
                writer.Put(actionId);
                writer.Put(caster.X);
                writer.Put(caster.Y);
                writer.Put(caster.Z);
                writer.Put(aim.HasValue);
                if (aim is { } point)
                {
                    writer.Put(point.X);
                    writer.Put(point.Y);
                    writer.Put(point.Z);
                }
            }
        }
        if (failReason != null) writer.Put(failReason);
        frame.Sync?.Write(writer);
    }

    // The reader rejects an empty or over-long reason and poisons the whole batch, so the writer
    // must never hand it one: drop empty, and cut to the byte budget without splitting a character.
    private static string? ClampFailReason(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return null;
        if (System.Text.Encoding.UTF8.GetByteCount(reason) <= MaxFailReasonBytes) return reason;
        var clamped = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var rune in reason.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxFailReasonBytes) break;
            bytes += rune.Utf8SequenceLength;
            clamped.Append(rune);
        }
        return clamped.Length > 0 ? clamped.ToString() : null;
    }

    public static bool TryRead(NetDataReader reader, out TickFrame frame)
    {
        frame = null!;
        if (!reader.TryGetUInt(out var tick) || !reader.TryGetByte(out var flags) || !reader.TryGetByte(out var mask)) return false;
        if ((flags & ~(HasMarkers | HasInvulns | HasSync | HasTimelines | HasLimitBreaks | HasFailReason)) != 0) return false;
        var result = new TickFrame { Tick = tick, PoseMask = mask };
        for (var slot = 0; slot < Wire.Slots; slot++)
            if ((mask & (1 << slot)) != 0 && !NetPose.TryRead(reader, out result.Poses[slot])) return false;
        if ((flags & HasMarkers) != 0)
        {
            var markers = new byte[Wire.MarkerSlots];
            for (var i = 0; i < Wire.MarkerSlots; i++)
                if (!reader.TryGetByte(out markers[i]) || !Wire.ValidRole(markers[i], true)) return false;
            result.Markers = markers;
            for (var i = 0; i < Wire.Slots; i++)
                if (!reader.TryGetUInt(out result.MarkerAcks[i])) return false;
        }
        if ((flags & HasTimelines) != 0)
        {
            if (!reader.TryGetByte(out var timelineMask) || timelineMask == 0) return false;
            result.TimelineMask = timelineMask;
            for (var slot = 0; slot < Wire.Slots; slot++)
                if ((timelineMask & (1 << slot)) != 0 && !reader.TryGetUShort(out result.Timelines[slot])) return false;
        }
        if ((flags & HasInvulns) != 0)
        {
            if (!reader.TryGetByte(out var count) || count == 0 || count > Wire.MaxInvulnsPerFrame) return false;
            for (var i = 0; i < count; i++)
            {
                if (!reader.TryGetByte(out var role) || !Wire.ValidRole(role, false) || !reader.TryGetFinite(3600f, out var seconds)) return false;
                result.Invulns.Add((role, seconds));
            }
        }
        if ((flags & HasLimitBreaks) != 0)
        {
            if (!reader.TryGetByte(out var holder) || !Wire.ValidRole(holder, true)) return false;
            result.LimitBreakHolder = holder;
            for (var i = 0; i < Wire.Slots; i++)
                if (!reader.TryGetUInt(out result.LimitBreakAcks[i])) return false;
            if (!reader.TryGetByte(out var lbCount) || lbCount > MaxLimitBreaksPerFrame) return false;
            for (var i = 0; i < lbCount; i++)
            {
                if (!reader.TryGetByte(out var role) || !Wire.ValidRole(role, false) || !reader.TryGetUInt(out var actionId)
                    || !reader.TryGetFinite(Wire.CoordinateLimit, out var cx) || !reader.TryGetFinite(Wire.CoordinateLimit, out var cy)
                    || !reader.TryGetFinite(Wire.CoordinateLimit, out var cz) || !reader.TryGetBool(out var hasAim)) return false;
                Vector3? aim = null;
                if (hasAim)
                {
                    if (!reader.TryGetFinite(Wire.CoordinateLimit, out var ax) || !reader.TryGetFinite(Wire.CoordinateLimit, out var ay)
                        || !reader.TryGetFinite(Wire.CoordinateLimit, out var az)) return false;
                    aim = new Vector3(ax, ay, az);
                }
                result.LimitBreaks.Add((role, actionId, new Vector3(cx, cy, cz), aim));
            }
        }
        if ((flags & HasFailReason) != 0)
        {
            if (!reader.TryGetString(out var reason) || reason.Length == 0
                || System.Text.Encoding.UTF8.GetByteCount(reason) > MaxFailReasonBytes) return false;
            result.FailReason = reason;
        }
        if ((flags & HasSync) != 0)
        {
            if (!SyncStateDto.TryRead(reader, out var sync)) return false;
            result.Sync = sync;
        }
        frame = result;
        return true;
    }
}
