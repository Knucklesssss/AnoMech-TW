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
    public const int Slots = 8;
    public const int MarkerSlots = 17;
    public const int MaxStatusesPerMember = 16;
    public const int MaxOverrideBytes = 512;
    public const int MaxFramesPerBatch = 64;
    public const int MaxInvulnsPerFrame = 8;
    public const float CoordinateLimit = 4096f;

    public static bool ValidRole(byte role, bool allowNone) => role < Slots || (allowNone && role == NoRole);

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

public readonly record struct LobbyPlayerDto(byte Id, string Name, byte Role, bool Ready, bool CanStart);

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
            writer.Put(p.CanStart);
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
                || !reader.TryGetBool(out var ready) || !reader.TryGetBool(out var canStart))
                return false;
            players.Add(new LobbyPlayerDto(id, name ?? "", role, ready, canStart));
        }
        if (!reader.TryGetBool(out var runActive)) return false;
        state = new LobbyStateDto(players, runActive);
        return true;
    }
}

public readonly record struct ReadyDto(bool Ready, bool CanStart)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(Ready);
        writer.Put(CanStart);
    }

    public static bool TryRead(NetDataReader reader, out ReadyDto ready)
    {
        ready = default;
        if (!reader.TryGetBool(out var r) || !reader.TryGetBool(out var c)) return false;
        ready = new ReadyDto(r, c);
        return true;
    }
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
    NetPose[] InitialPoses)
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
        start = new StartRunDto(runId, scenario, strat, waymark, seed, scale, god, owners, payload, poses);
        return true;
    }
}

public enum StopReason : byte
{
    HostStopped = 1,
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

public readonly record struct TransformDto(uint RunId, NetPose Pose)
{
    public void Write(NetDataWriter writer)
    {
        writer.Put(RunId);
        Pose.Write(writer);
    }

    public static bool TryRead(NetDataReader reader, out TransformDto transform)
    {
        transform = default;
        if (!reader.TryGetUInt(out var runId) || !NetPose.TryRead(reader, out var pose)) return false;
        transform = new TransformDto(runId, pose);
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
    public List<(byte Role, float Seconds)> Invulns { get; } = [];
    public SyncStateDto? Sync { get; set; }
}

public static class FrameCodec
{
    private const byte HasMarkers = 1;
    private const byte HasInvulns = 2;
    private const byte HasSync = 4;

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
        var flags = (byte)((frame.Markers != null ? HasMarkers : 0) | (frame.Invulns.Count > 0 ? HasInvulns : 0) | (frame.Sync != null ? HasSync : 0));
        writer.Put(frame.Tick);
        writer.Put(flags);
        writer.Put(frame.PoseMask);
        for (var slot = 0; slot < Wire.Slots; slot++)
            if ((frame.PoseMask & (1 << slot)) != 0) frame.Poses[slot].Write(writer);
        if (frame.Markers != null)
            for (var i = 0; i < Wire.MarkerSlots; i++) writer.Put(frame.Markers[i]);
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
        frame.Sync?.Write(writer);
    }

    public static bool TryRead(NetDataReader reader, out TickFrame frame)
    {
        frame = null!;
        if (!reader.TryGetUInt(out var tick) || !reader.TryGetByte(out var flags) || !reader.TryGetByte(out var mask)) return false;
        if ((flags & ~(HasMarkers | HasInvulns | HasSync)) != 0) return false;
        var result = new TickFrame { Tick = tick, PoseMask = mask };
        for (var slot = 0; slot < Wire.Slots; slot++)
            if ((mask & (1 << slot)) != 0 && !NetPose.TryRead(reader, out result.Poses[slot])) return false;
        if ((flags & HasMarkers) != 0)
        {
            var markers = new byte[Wire.MarkerSlots];
            for (var i = 0; i < Wire.MarkerSlots; i++)
                if (!reader.TryGetByte(out markers[i]) || !Wire.ValidRole(markers[i], true)) return false;
            result.Markers = markers;
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
        if ((flags & HasSync) != 0)
        {
            if (!SyncStateDto.TryRead(reader, out var sync)) return false;
            result.Sync = sync;
        }
        frame = result;
        return true;
    }
}
