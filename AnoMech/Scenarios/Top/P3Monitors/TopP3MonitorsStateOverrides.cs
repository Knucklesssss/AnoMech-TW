namespace AnoMech.Scenarios.Top.P3Monitors;

// User-controlled overrides for TopP3MonitorsState's randomized fields. Bound by the
// scenario's settings UI; null leaves the field randomized at scenario start.
public sealed class TopP3MonitorsStateOverrides
{
    // 0-7, or null to let the roll decide which recorded slot the player stands in.
    // Slots 1, 2 and 5 are the ones that end up holding a monitor.
    public int? PlayerSlot { get; set; }

    // null = rolled; true = Omega's screen points east.
    public bool? ScreenFacesEast { get; set; }
}
