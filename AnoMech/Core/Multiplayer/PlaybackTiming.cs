using System;
using System.Collections.Generic;
using System.Numerics;

namespace AnoMech.Core.Multiplayer;

// Client-side schedule for replaying the host's fixed ticks. Playback starts once
// NetworkPlaybackDelay worth of ticks is buffered and then gently speeds up or slows down to
// keep that buffer, so a late packet stalls the scene briefly instead of breaking it.
public sealed class PlaybackClock
{
    public const float Step = 1f / 60f;
    public const int MaxTicksPerFrame = 8;
    private const double Epsilon = 1e-6;

    private readonly int delayTicks;
    private double accumulator;

    public PlaybackClock(float delaySeconds)
    {
        delayTicks = Math.Max(1, (int)MathF.Round(delaySeconds / Step));
    }

    public bool Started { get; private set; }
    public long NextTick { get; private set; }
    public long LastReceivedTick { get; private set; } = -1;
    public int BufferedTicks => (int)(LastReceivedTick + 1 - NextTick);
    public int DelayTicks => delayTicks;
    public float Alpha => (float)Math.Clamp(accumulator / Step, 0, 1);

    public void OnReceived(long tick)
    {
        if (tick > LastReceivedTick) LastReceivedTick = tick;
    }

    public int Advance(float deltaSeconds)
    {
        if (!Started)
        {
            if (BufferedTicks < delayTicks) return 0;
            Started = true;
            accumulator = 0;
        }

        var rate = Math.Clamp(1.0 + (BufferedTicks - delayTicks) * 0.02, 0.9, 2.0);
        accumulator += deltaSeconds * rate;
        var ran = 0;
        while (accumulator + Epsilon >= Step && BufferedTicks > 0 && ran < MaxTicksPerFrame)
        {
            accumulator = Math.Max(0, accumulator - Step);
            NextTick++;
            ran++;
        }
        if (BufferedTicks == 0 && accumulator > Step) accumulator = Step;
        return ran;
    }
}

// Host-side smoothing of a remote player's 20 Hz reports: render slightly in the past and
// interpolate, extrapolating only briefly past the newest report.
public sealed class PoseBuffer
{
    private const int Capacity = 32;

    // Sprint, with room to spare. The cap on how far extrapolation may project past the newest report.
    private const float MaxSpeedPerSecond = 7f;
    private readonly List<(double TimeMs, NetPose Pose)> samples = [];

    public int Count => samples.Count;

    public void Add(double timeMs, NetPose pose)
    {
        if (samples.Count > 0 && timeMs <= samples[^1].TimeMs) return;
        samples.Add((timeMs, pose));
        if (samples.Count > Capacity) samples.RemoveAt(0);
    }

    public bool TrySample(double renderTimeMs, double maxExtrapolationMs, out NetPose pose)
    {
        pose = default;
        if (samples.Count == 0) return false;
        if (renderTimeMs <= samples[0].TimeMs)
        {
            pose = samples[0].Pose;
            return true;
        }
        for (var i = samples.Count - 1; i > 0; i--)
        {
            var (t1, p1) = samples[i];
            var (t0, p0) = samples[i - 1];
            if (renderTimeMs < t0 || renderTimeMs > t1) continue;
            pose = Lerp(p0, p1, (float)((renderTimeMs - t0) / (t1 - t0)));
            return true;
        }

        var last = samples[^1];
        if (samples.Count == 1)
        {
            pose = last.Pose;
            return true;
        }
        var previous = samples[^2];
        var span = last.TimeMs - previous.TimeMs;
        var ahead = Math.Min(renderTimeMs - last.TimeMs, maxExtrapolationMs);
        // Samples are stamped on arrival, and Transform is Sequenced, so a burst can land two reports
        // a millisecond apart while carrying a full step of real movement. Dividing by that span
        // estimates a velocity hundreds of times too fast, and the projection throws the puppet clear
        // of the arena — where the fence kills its owner on their own client. Bound the projection by
        // what a player can physically cover in that time instead of trusting the sample spacing.
        var projected = span > 0 ? (last.Pose.Position - previous.Pose.Position) * (float)(ahead / span) : Vector3.Zero;
        var reach = MaxSpeedPerSecond * (float)(ahead / 1000d);
        var distance = projected.Length();
        if (distance > reach) projected *= reach / distance;
        pose = new NetPose(last.Pose.Position + projected, last.Pose.Rotation);
        return true;
    }

    public static NetPose Lerp(NetPose a, NetPose b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var delta = (b.Rotation - a.Rotation) % MathF.Tau;
        if (delta > MathF.PI) delta -= MathF.Tau;
        if (delta < -MathF.PI) delta += MathF.Tau;
        return new NetPose(Vector3.Lerp(a.Position, b.Position, t), a.Rotation + delta * t);
    }
}
