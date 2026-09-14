using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using System.Text;

namespace AnoMech.Core.Native;

// Temporary reverse-engineering probe: the game clears the player's cast state every frame for a simulated
// cast, so something it keeps elsewhere marks a real cast. Outside the simulation, with event logging on,
// this logs which ActionManager and CastInfo bytes differ between idle, mid-cast and after a real cast.
internal static unsafe class CastStateProbe
{
    private const int ManagerSize = 2048;
    private const int MaxCasts = 3;

    private static readonly byte[] idleManager = new byte[ManagerSize];
    private static byte[] idleCast = [];
    private static byte[] midManager = new byte[ManagerSize];
    private static byte[] midCast = [];
    private static bool wasCasting;
    private static bool midTaken;
    private static int casts;

    public static void Tick()
    {
        if (casts >= MaxCasts || !Plugin.LogManager.Enabled) return;
        if (Plugin.GameInstance?.World.Combat is { Active: true }) return;
        var manager = ActionManager.Instance();
        var player = Control.GetLocalPlayer();
        if (manager == null || player == null) return;

        var castInfo = (byte*)&player->CastInfo;
        var castSize = SizeOf(&player->CastInfo);
        var casting = player->CastInfo.IsCasting;

        if (!casting && !wasCasting)
        {
            Copy((byte*)manager, idleManager);
            idleCast = Snapshot(castInfo, castSize);
        }
        else if (casting && !midTaken && player->CastInfo.TotalCastTime > 0
                 && player->CastInfo.CurrentCastTime >= player->CastInfo.TotalCastTime / 2)
        {
            Copy((byte*)manager, midManager);
            midCast = Snapshot(castInfo, castSize);
            midTaken = true;
        }
        else if (!casting && wasCasting)
        {
            if (midTaken)
            {
                casts++;
                var afterManager = Snapshot((byte*)manager, ManagerSize);
                var afterCast = Snapshot(castInfo, castSize);
                Plugin.LogManager.LogSkill($"CastStateProbe cast#{casts} action={manager->CastActionId} manager[idle->mid->after] {Diff(idleManager, midManager, afterManager, skipCooldowns: true)}");
                Plugin.LogManager.LogSkill($"CastStateProbe cast#{casts} castInfo size={castSize} [idle->mid->after] {Diff(idleCast, midCast, afterCast, skipCooldowns: false)}");
            }
            midTaken = false;
        }
        wasCasting = casting;
    }

    private static int SizeOf<T>(T* _) where T : unmanaged => sizeof(T);

    private static void Copy(byte* source, byte[] target)
    {
        for (var i = 0; i < target.Length; i++) target[i] = source[i];
    }

    private static byte[] Snapshot(byte* source, int size)
    {
        var bytes = new byte[size];
        Copy(source, bytes);
        return bytes;
    }

    // Cooldown records (0x184..0x7C4) tick during any cast and would bury the signal.
    private static string Diff(byte[] idle, byte[] mid, byte[] after, bool skipCooldowns)
    {
        var text = new StringBuilder();
        var length = Math.Min(idle.Length, Math.Min(mid.Length, after.Length));
        for (var i = 0; i < length; i++)
        {
            if (skipCooldowns && i >= 0x184 && i < 0x7C4) continue;
            if (idle[i] == mid[i] && mid[i] == after[i]) continue;
            text.Append($"0x{i:X}:{idle[i]:X2}->{mid[i]:X2}->{after[i]:X2} ");
        }
        return text.Length == 0 ? "no differences" : text.ToString();
    }
}
