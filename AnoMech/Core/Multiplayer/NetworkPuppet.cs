using System.Numerics;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Multiplayer;

// Shows a network-driven party slot where the network says it is, with the run loop while it
// moves. Only the model moves; the simulated Position is set separately (host: read from the
// model at the tick; client: the host's recorded pose).
internal sealed class NetworkPuppet(SimCharacter member)
{
    private const ushort RunTimelineId = 22;
    private const float MovingSpeed = 0.5f;
    private const float StopDelaySeconds = 0.15f;

    private Vector3? lastPosition;
    private bool running;
    private float stillFor;

    public SimCharacter Member => member;

    public void Apply(NetPose pose, float deltaSeconds)
    {
        if (!member.IsActive) return;
        member.SetNativePose(pose.Position, pose.Rotation);

        var moved = lastPosition is { } previous && deltaSeconds > 0f
                    && Vector2.Distance(new Vector2(previous.X, previous.Z), new Vector2(pose.Position.X, pose.Position.Z)) / deltaSeconds > MovingSpeed;
        lastPosition = pose.Position;

        if (member is ISimPartyMember { Dead: true })
        {
            running = false;
            return;
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
}
