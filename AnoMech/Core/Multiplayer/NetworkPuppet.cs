using System.Numerics;
using AnoMech.Core.SimObjects;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.Multiplayer;

// Shows a network-driven party slot where the network says it is. A human owner's own base
// animation is played when relayed; otherwise (AI slots) the run loop is inferred from motion.
// Only the model moves; the simulated Position is set separately (host: read from the model at
// the tick; client: the host's recorded pose).
internal sealed class NetworkPuppet(SimCharacter member)
{
    private const ushort RunTimelineId = 22;
    private const float MovingSpeed = 0.5f;
    private const float StopDelaySeconds = 0.15f;

    private Vector3? lastPosition;
    private bool running;
    private float stillFor;
    private ushort? playing;

    public SimCharacter Member => member;

    public void Apply(NetPose pose, float deltaSeconds, ushort timeline = Wire.InferTimeline)
    {
        if (!member.IsActive) return;
        member.SetNativePose(pose.Position, pose.Rotation);

        var moved = lastPosition is { } previous && deltaSeconds > 0f
                    && Vector2.Distance(new Vector2(previous.X, previous.Z), new Vector2(pose.Position.X, pose.Position.Z)) / deltaSeconds > MovingSpeed;
        lastPosition = pose.Position;

        if (member is ISimPartyMember { Dead: true })
        {
            running = false;
            playing = null;
            return;
        }
        if (timeline != Wire.InferTimeline)
        {
            PlayOwnerTimeline(timeline);
            return;
        }
        if (playing is not null)
        {
            member.ResetActionTimeline();
            playing = null;
        }
        if (moved)
        {
            stillFor = 0f;
            if (running) return;
            member.PlayActionTimeline(RunTimelineId, baseOverride: RunTimelineId);
            running = true;
        }
        else if (running && (stillFor += deltaSeconds) >= StopDelaySeconds)
        {
            member.ResetActionTimeline();
            running = false;
        }
    }

    // Restarting a looping timeline every sample makes it twitch, so only a change is played.
    // BaseOverride holds it: the model is placed, not walked, so the engine sees speed 0 and
    // would otherwise drop back to idle. Remote ids reach a native call, so unknown rows are dropped.
    private void PlayOwnerTimeline(ushort timeline)
    {
        running = false;
        if (playing == timeline) return;
        if (timeline != 0 && !Plugin.DataManager.GetExcelSheet<ActionTimeline>().HasRow(timeline)) return;
        playing = timeline;
        if (timeline == 0) member.ResetActionTimeline();
        else member.PlayActionTimeline(timeline, baseOverride: timeline);
    }
}
