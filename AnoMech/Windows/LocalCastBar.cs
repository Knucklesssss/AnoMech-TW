using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AnoMech.Windows;

// The game clears the local player's cast state every frame, so its own cast bar never shows a simulated cast.
internal static class LocalCastBar
{
    public static void Draw()
    {
        if (Plugin.GameInstance?.World.Combat is not { Active: true, CastingAction: not 0 } combat) return;
        var name = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(combat.CastingAction, out var row)
            ? row.Name.ExtractText() : "";
        var viewport = ImGui.GetMainViewport();
        var size = new Vector2(260, 12) * ImGuiHelpers.GlobalScale;
        var min = viewport.Pos + new Vector2((viewport.Size.X - size.X) / 2, viewport.Size.Y * 0.62f);
        var draw = ImGui.GetForegroundDrawList();
        draw.AddRectFilled(min - Vector2.One, min + size + Vector2.One, 0xC0000000, 3);
        draw.AddRectFilled(min, min + new Vector2(size.X * combat.CastProgress, size.Y), 0xFF40C8F8, 3);
        var label = $"{name}  {combat.CastRemaining:0.0}";
        draw.AddText(min - new Vector2(0, ImGui.GetTextLineHeight() + 2), 0xFFFFFFFF, label);
    }
}
