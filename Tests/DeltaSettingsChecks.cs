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
        AiTetherPinLabelsMatchTheirGroups();
        Console.WriteLine("PASS: Delta six tether UI choices assign correct near/far and inner/outer slots for all eight roles, and AI pin labels match their groups");
    }

    // The sheet calls the 0-3 group Close while players call it 遠, so the pin labels are the one
    // place a wiring mistake would be invisible: the setting would read back fine and only the
    // tether indices would come out mirrored. Drive the real radio buttons and check the slots.
    private static void AiTetherPinLabelsMatchTheirGroups()
    {
        (string label, int[] slots)[] cases =
        [
            ("遠-內", [0, 1]), ("遠-外", [2, 3]), ("近-內", [4, 5]), ("近-外", [6, 7]),
        ];
        foreach (var (label, slots) in cases)
        foreach (var role in Enum.GetValues<PartyRole>())
        for (var repeat = 0; repeat < 20; repeat++)
        {
            var settings = new TopP5DeltaSettingsWindow();
            ImGui.ClickLabel = $"{label}##aitether{(int)role}";
            settings.Draw();
            // Solo, so every slot but the player's is AI; pin one the player is not sitting in.
            var player = role == PartyRole.MainTank ? PartyRole.CasterDps : PartyRole.MainTank;
            var state = new TopP5DeltaState(settings.Overrides, player);
            var slot = state.TetherOrder.ToList().IndexOf(role);
            if (!slots.Contains(slot))
                throw new Exception($"Delta AI pin {label}: {role} assigned slot {slot}, expected {string.Join(',', slots)}");
        }
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
        public static void TextDisabled(string text) { }
        public static bool CollapsingHeader(string label) => true;   // open, so the section below it draws
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
