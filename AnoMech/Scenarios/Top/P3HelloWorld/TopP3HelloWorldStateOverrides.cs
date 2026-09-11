namespace AnoMech.Scenarios.Top.P3HelloWorld;

// User-controlled overrides for TopP3HelloWorldState's randomized fields. Bound by
// the scenario's settings UI; null leaves the field randomized at scenario start.
public sealed class TopP3HelloWorldStateOverrides
{
    // 0-3, or null to let the roll decide which slot of the cycle the player lands in.
    public int? PlayerSlot { get; set; }
    public bool TransitionFirstArmsSouth { get; set; } = true;
    public int? TransitionPlayerSlot { get; set; }
    public bool TransitionAutoMarkers { get; set; } = true;
}
