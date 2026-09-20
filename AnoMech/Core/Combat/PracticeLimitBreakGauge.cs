using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AnoMech.Core.Combat;

// Owned only by an opt-in practice runtime; restore the native party gauge on exit.
internal sealed unsafe class PracticeLimitBreakGauge
{
    private bool saved;
    private byte bars;
    private ushort units;
    private ushort barUnits;

    internal void Update(bool available)
    {
        var gauge = LimitBreakController.StaticAddressPointers.pInstance;
        if (gauge == null || Plugin.ClientState.IsPvP || gauge->BarUnits > ushort.MaxValue / 3) return;
        if (!saved)
        {
            bars = gauge->BarCount;
            units = gauge->CurrentUnits;
            barUnits = gauge->BarUnits;
            saved = true;
        }
        if (gauge->BarUnits == 0) gauge->BarUnits = 1;
        gauge->BarCount = 3;
        gauge->CurrentUnits = available ? (ushort)(3 * gauge->BarUnits) : (ushort)0;
    }

    internal void Restore()
    {
        var gauge = LimitBreakController.StaticAddressPointers.pInstance;
        if (!saved || gauge == null) return;
        gauge->BarCount = bars;
        gauge->CurrentUnits = units;
        gauge->BarUnits = barUnits;
        saved = false;
    }
}
