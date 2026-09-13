using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace ClientData;

public static class ClientReader
{
    public static JobData Read(string gamePath, string abbreviation, int level)
    {
        var data = new GameData(Path.Combine(gamePath, "sqpack"), new LuminaOptions
        {
            DefaultExcelLanguage = Language.TraditionalChinese,
            PanicOnSheetChecksumMismatch = true,
        });
        var gameVersion = File.ReadAllText(Path.Combine(gamePath, "ffxivgame.ver")).Trim();
        var jobs = data.GetExcelSheet<ClassJob>()!.Where(j => j.JobIndex > 0).ToList();
        var job = jobs.FirstOrDefault(j => j.Abbreviation.ExtractText() == abbreviation);
        if (job.RowId == 0)
            throw new ToolException($"找不到職業縮寫 {abbreviation}。可用：{string.Join(", ", jobs.Select(j => j.Abbreviation.ExtractText()))}");
        var categoryFlag = typeof(ClassJobCategory).GetProperty(abbreviation)
            ?? throw new ToolException($"職業分類表沒有 {abbreviation} 欄位。");
        var parent = job.ClassJobParent.RowId == job.RowId ? 0 : job.ClassJobParent.RowId;
        var categories = data.GetExcelSheet<ClassJobCategory>()!;
        var actions = data.GetExcelSheet<LuminaAction>()!;
        bool InCategory(LuminaAction a) => categories.GetRowOrDefault(a.ClassJobCategory.RowId) is { } c && (bool)categoryFlag.GetValue(c)!;

        var selected = actions
            .Where(a => !a.IsPvP && a.ClassJobLevel >= 1 && a.ClassJobLevel <= level
                && (a.ClassJob.RowId == job.RowId || (parent != 0 && a.ClassJob.RowId == parent) || (a.IsRoleAction && InCategory(a))))
            .Select(a => a.RowId)
            .ToHashSet();
        if (selected.Count == 0) throw new ToolException($"{abbreviation} 在 {level} 級查無技能。");

        var replacements = new List<Replacement>();
        foreach (var r in data.GetExcelSheet<ActionIndirection>()!.Where(r => r.ClassJob.RowId == job.RowId))
        {
            if (actions.GetRowOrDefault(r.Name.RowId) is not { } to || actions.GetRowOrDefault(r.PreviousComboAction.RowId) is not { } from) continue;
            if (to.ClassJobLevel > level || from.ClassJobLevel > level) continue;
            selected.Add(to.RowId);
            selected.Add(from.RowId);
            replacements.Add(new(from.RowId, to.RowId, "技能替換表"));
        }

        var manual = new List<string>();
        var actionText = data.GetExcelSheet<ActionTransient>()!;
        var actionCategories = data.GetExcelSheet<ActionCategory>()!;
        var actionRows = selected.OrderBy(id => id).Select(id =>
        {
            var a = actions.GetRow(id);
            var (text, unresolved) = MacroText.Evaluate(actionText.GetRowOrDefault(id)?.Description.ToMacroString() ?? "", job.RowId, level);
            if (unresolved) manual.Add($"技能 {id} {a.Name.ExtractText()} 的說明含無法求值的巨集");
            return new ActionRow(id, a.Name.ExtractText(), actionCategories.GetRowOrDefault(a.ActionCategory.RowId)?.Name.ExtractText() ?? "",
                a.ClassJobLevel, a.Cast100ms, a.Recast100ms, a.CooldownGroup, a.AdditionalCooldownGroup, a.MaxCharges,
                a.PrimaryCostType, a.PrimaryCostValue, a.Range, a.EffectRange, a.CastType, a.CanTargetSelf, a.CanTargetParty,
                a.CanTargetHostile, a.IsPlayerAction, a.IsRoleAction, a.IsPvP, a.ActionCombo.RowId, a.ClassJobCategory.RowId,
                a.StatusGainSelf.RowId, text, unresolved);
        }).ToList();

        var traitText = data.GetExcelSheet<TraitTransient>()!;
        var traitRows = data.GetExcelSheet<Trait>()!
            .Where(t => t.Level >= 1 && t.Level <= level && (t.ClassJob.RowId == job.RowId || (parent != 0 && t.ClassJob.RowId == parent)))
            .OrderBy(t => t.Level).ThenBy(t => t.RowId)
            .Select(t =>
            {
                var (text, unresolved) = MacroText.Evaluate(traitText.GetRowOrDefault(t.RowId)?.Description.ToMacroString() ?? "", job.RowId, level);
                if (unresolved) manual.Add($"特性 {t.RowId} {t.Name.ExtractText()} 的說明含無法求值的巨集");
                return new TraitRow(t.RowId, t.Name.ExtractText(), t.Level, t.Value, t.ClassJob.RowId, text, unresolved);
            }).ToList();

        var nameGroups = actionRows.Where(a => a.Name.Length > 0).GroupBy(a => a.Name).ToList();
        foreach (var g in nameGroups.Where(g => g.Count() > 1))
            manual.Add($"技能名稱「{g.Key}」對應多個 ID：{string.Join("、", g.Select(a => a.Id))}");
        var idsByName = nameGroups.ToDictionary(g => g.Key, g => g.Min(a => a.Id));
        foreach (var t in traitRows)
            foreach (var (from, to) in TraitUpgrades.Find(t.Description, idsByName))
            {
                replacements.Add(new(from, to, $"特性 {t.Id}"));
                manual.Add($"特性 {t.Id} {t.Name}：{from} → {to} 由說明文字比對得出");
            }

        var statuses = data.GetExcelSheet<Status>()!;
        var statusIdsByName = statuses.Where(s => s.RowId != 0)
            .GroupBy(s => s.Name.ExtractText())
            .Where(g => g.Key.Length >= 2)
            .ToDictionary(g => g.Key, g => g.Select(s => s.RowId).ToList());
        var allText = string.Join("\n", actionRows.Select(a => a.Description).Concat(traitRows.Select(t => t.Description)));
        var statusIds = new SortedSet<uint>();
        foreach (var (name, ids) in statusIdsByName)
            if (allText.Contains(name, StringComparison.Ordinal)) statusIds.UnionWith(ids);
        foreach (var a in actionRows.Where(a => a.StatusGainSelf != 0))
            statusIds.UnionWith(statusIdsByName.GetValueOrDefault(statuses.GetRow(a.StatusGainSelf).Name.ExtractText()) ?? [a.StatusGainSelf]);
        var statusRows = statusIds.Select(id =>
        {
            var s = statuses.GetRow(id);
            var name = s.Name.ExtractText();
            var duplicate = statusIdsByName.TryGetValue(name, out var same) && same.Count > 1;
            var (text, unresolved) = MacroText.Evaluate(s.Description.ToMacroString(), job.RowId, level);
            if (duplicate) manual.Add($"狀態 {id}「{name}」與其他狀態同名：{string.Join("、", same!)}");
            if (unresolved) manual.Add($"狀態 {id}「{name}」的說明含無法求值的巨集");
            return new StatusRow(id, name, s.MaxStacks, s.StatusCategory, s.IsPermanent, s.CanDispel, text, duplicate);
        }).ToList();

        return new JobData(job.RowId, abbreviation, job.Name.ExtractText(), level, gameVersion, actionRows, traitRows, statusRows,
            replacements.Distinct().ToList(), manual.Distinct().ToList());
    }
}
