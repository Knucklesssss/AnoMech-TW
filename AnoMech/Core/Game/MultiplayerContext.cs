using System;

namespace AnoMech.Core.Game;

public enum MultiplayerRole
{
    Off,
    Host,
    Client,
}

// What the simulation itself needs to know about an active multiplayer run. Kept free of
// networking so scenario and AI code (and the tests that link them) can read it.
public static class MultiplayerContext
{
    private static int humanSlotMask;

    public static MultiplayerRole Role { get; private set; }
    public static bool InRun => Role != MultiplayerRole.Off;
    public static bool IsHost => Role == MultiplayerRole.Host;
    public static bool IsClient => Role == MultiplayerRole.Client;

    // Scenario settings chosen by the host, with player-bound fields cleared (MultiplayerOverrides).
    public static byte[]? OverridePayload { get; set; }

    // Host only: AI-granted invulnerability is replayed on clients, which do not run AI.
    public static Action<int, float>? InvulnGranted { get; set; }

    // Host only: party slots moved by a remote player, which AI must leave alone.
    public static bool IsHumanControlled(int slot) => slot is >= 0 and < 32 && ((humanSlotMask >> slot) & 1) != 0;

    // Host only: a remote player left mid-run, so AI takes the slot over.
    public static void ReleaseHuman(int slot)
    {
        if (slot is >= 0 and < 32) humanSlotMask &= ~(1 << slot);
    }

    public static void Begin(MultiplayerRole role, int humanSlots, byte[]? overridePayload)
    {
        Role = role;
        humanSlotMask = humanSlots;
        OverridePayload = overridePayload;
        InvulnGranted = null;
    }

    public static void End()
    {
        Role = MultiplayerRole.Off;
        humanSlotMask = 0;
        OverridePayload = null;
        InvulnGranted = null;
        SimRandom.Disable();
    }
}
