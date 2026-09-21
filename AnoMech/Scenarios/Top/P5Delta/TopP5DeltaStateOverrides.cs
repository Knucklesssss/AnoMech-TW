using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Top.P5Delta;

public enum PlayerTetherAssignment { Auto, CloseAny, CloseInner, CloseOuter, FarAny, FarInner, FarOuter }
public enum HelloWorldOption { Auto, Near, Far, No }

// Which tether group a slot is held to when the AI drives it. Unlike PlayerTetherAssignment this
// names a party slot rather than "whoever is local", so it means the same thing on every machine.
public enum AiTetherGroup { Auto, CloseInner, CloseOuter, FarInner, FarOuter }

// User-controlled overrides for TopP5DeltaState's randomized fields. Bound by
// the scenario's settings UI; null/default values leave the field randomized at
// scenario start. The state ctor consumes this directly.
public sealed class TopP5DeltaStateOverrides
{
    public NorthSouth? EyeSpawn { get; set; }
    public Side? SwivelCannonSide { get; set; }
    public PlayerTetherAssignment TetherAssignment { get; set; }
    public bool? Monitor { get; set; }
    public HelloWorldOption HelloWorld { get; set; }
    public bool? BeyondDefence { get; set; }

    // Eight pins at four bits each. Packed into one int because MultiplayerOverrides serializes
    // plain bools, ints, enums and static instances — an array would not survive the trip, and
    // eight separate properties would be the same data spelled longer.
    public int AiTetherPins { get; set; }

    public AiTetherGroup AiTether(PartyRole role) => (AiTetherGroup)((AiTetherPins >> ((int)role * 4)) & 0xF);

    public void SetAiTether(PartyRole role, AiTetherGroup group)
    {
        var shift = (int)role * 4;
        AiTetherPins = (AiTetherPins & ~(0xF << shift)) | (((int)group & 0xF) << shift);
    }
}
