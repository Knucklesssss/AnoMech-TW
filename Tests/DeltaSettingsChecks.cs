using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Top.P5Delta;
using Dalamud.Bindings.ImGui;

internal static class DeltaSettingsChecks
{
    public static void Run()
    {
        // Delta's real tether application sends Remote to slots 0-3 and Local to 4-7.
        (string label, int[] slots)[] cases =
        [
            ("近-任意##tether", [4, 5, 6, 7]), ("近-內##tether", [4, 5]), ("近-外##tether", [6, 7]),
            ("遠-任意##tether", [0, 1, 2, 3]), ("遠-內##tether", [0, 1]), ("遠-外##tether", [2, 3]),
        ];
        foreach (var (label, slots) in cases)
        foreach (var role in Enum.GetValues<PartyRole>())
        for (var repeat = 0; repeat < 20; repeat++)
        {
            var settings = new TopP5DeltaSettingsWindow();
            ImGui.ClickLabel = label;
            settings.Draw();
            var state = new TopP5DeltaState(settings.Overrides, role);
            var slot = state.TetherOrder.ToList().IndexOf(role);
            if (!slots.Contains(slot))
                throw new Exception($"Delta {label}: {role} assigned slot {slot}, expected {string.Join(',', slots)}");
        }
        Console.WriteLine("PASS: Delta six tether UI choices assign correct near/far and inner/outer slots for all eight roles");
    }
}

// Only the native UI boundary is replaced; production Draw and state assignment run unchanged.
namespace Dalamud.Bindings.ImGui
{
    public static class ImGui
    {
        public static string? ClickLabel { get; set; }
        private static int disabled;
        public static bool Button(string label) => false;
        public static bool RadioButton(string label, bool selected)
        {
            if (disabled != 0 || label != ClickLabel) return false;
            ClickLabel = null;
            return true;
        }
        public static void SameLine() { }
        public static void BeginDisabled() => disabled++;
        public static void EndDisabled() => disabled--;
    }
}

namespace AnoMech.Scenarios
{
    internal static class SettingsGrid
    {
        public static bool Begin(string id) => true;
        public static void Row(string label) { }
        public static void End() { }
    }
}
