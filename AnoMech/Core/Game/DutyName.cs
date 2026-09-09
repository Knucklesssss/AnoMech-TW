using Lumina.Excel.Sheets;

namespace AnoMech.Core.Game;

// Duty names come from the running client's own sheet rather than a hardcoded
// translation, so the menu always shows the official wording for whatever
// language the client is in - and follows it if that wording ever changes.
internal static class DutyName
{
    public static string? ForTerritory(uint territoryId)
    {
        var territory = Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
        var name = territory?.ContentFinderCondition.ValueNullable?.Name.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
