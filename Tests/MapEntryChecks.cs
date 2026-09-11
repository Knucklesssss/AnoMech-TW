using System.Numerics;
using AnoMech.Core.Map;

internal static class MapEntryChecks
{
    public static void Run()
    {
        using var map = new MapController();
        var target = new TargetInstance(1122, Vector3.Zero, Vector3.Zero);
        ZoneSession.EntrySucceeds = false;
        map.TryLoad(target, 90, 635);
        if (map.IsInInstance)
            throw new Exception("Failed native entry must not start a simulated instance.");
        ZoneSession.EntrySucceeds = true;
        map.TryLoad(target, 90, 635);
        if (!map.IsInInstance)
            throw new Exception("Successful native entry must start the simulated instance.");
        if (!map.TryLoad(target, 90, 635))
            throw new Exception("Restart in the same territory must remain available.");
        if (map.TryLoad(new TargetInstance(777, Vector3.Zero, Vector3.Zero), 70, 375))
            throw new Exception("Cross-territory switch must require Leave before starting.");
        map.Unload();
        if (map.IsInInstance || map.IsZoneLoaded)
            throw new Exception("Leave must clear loaded-instance state.");
        if (!map.TryLoad(new TargetInstance(777, Vector3.Zero, Vector3.Zero), 70, 375))
            throw new Exception("Another territory must be available after Leave.");
        Console.WriteLine("Map entry failure does not start a simulation; successful entry and leave update state.");
    }
}

// Only the live native boundaries are replaced. MapController above is production code.
namespace AnoMech.Core.Map
{
    public sealed class ZoneSession : IDisposable
    {
        public static bool EntrySucceeds = true;
        public bool IsActive { get; private set; }
        public static bool IsSupportedStartLocation() => true;
        public void Enter(uint territory, Vector3 position, byte level, ushort itemLevel)
            => IsActive = EntrySucceeds;
        public void Revert(bool dispose) => IsActive = false;
        public void ApplyWeather(byte weather) { }
        public void SetWeather(byte weather, float transition = 0.5f) { }
        public void Dispose() { }
    }
    internal sealed class MapEffects : IDisposable
    {
        public bool Loaded { get; set; }
        public void Apply(uint flags, byte index) { }
        public void Dispose() { }
    }
    internal static class DirectorFunctions
    {
        public static int DisableSpawnAreaColliders(Vector3 center, float radius) => 0;
    }
}

namespace AnoMech.Helpers
{
    internal static class InstanceContentDirectorHelper
    {
        public static void Commence() { }
        public static void ProcessDirectorUpdate(uint category, uint a, uint b, uint c, uint d, uint e, uint f) { }
    }
}
