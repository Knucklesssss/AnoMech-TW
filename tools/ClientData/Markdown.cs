using System.Text;

namespace ClientData;

public static class Markdown
{
    public static string Write(JobData job, XivapiCheck check, DateTimeOffset readAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {job.Name}（{job.Abbreviation}）{job.Level} 級客戶端資料").AppendLine();
        sb.AppendLine($"- 客戶端版本：`{job.GameVersion}`");
        sb.AppendLine($"- 讀取時間：{readAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine(check.Completed
            ? $"- 對應 xivapi 國際服版本：`{check.Version}`"
            : $"- **未完成 xivapi 核對**：{check.FailureReason}。此清單不得直接用於撰寫規則。");
        sb.AppendLine("- 冷卻組、額外冷卻組為 Action 資料表欄位值，不等於遊戲原生冷卻陣列索引。").AppendLine();

        sb.AppendLine("## 技能").AppendLine();
        sb.AppendLine("| ID | 名稱 | 類型 | 等級 | 詠唱秒 | 冷卻秒 | 冷卻組 | 額外冷卻組 | 充能 | 消耗類型 | 消耗值 | 射程 | 範圍 | 目標 | 連段前置 | 替換為 | 自身附加狀態 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var a in job.Actions)
        {
            var targets = string.Join("／", new[] { a.CanTargetSelf ? "自身" : null, a.CanTargetParty ? "隊友" : null, a.CanTargetHostile ? "敵人" : null }.OfType<string>());
            var replacedBy = string.Join("、", job.Replacements.Where(r => r.From == a.Id).Select(r => $"{r.To}（{r.Source}）"));
            sb.AppendLine($"| {a.Id} | {Cell(a.Name)}{(a.IsRoleAction ? "（職能）" : "")} | {Cell(a.Category)} | {a.Level} | {a.Cast100ms / 10.0:0.#} | {a.Recast100ms / 10.0:0.#} | {a.CooldownGroup} | {a.AdditionalCooldownGroup} | {a.MaxCharges} | {a.CostType} | {a.CostValue} | {a.Range} | {a.EffectRange} | {targets} | {(a.ComboFrom == 0 ? "" : a.ComboFrom.ToString())} | {replacedBy} | {(a.StatusGainSelf == 0 ? "" : a.StatusGainSelf.ToString())} |");
        }

        sb.AppendLine().AppendLine("## 特性").AppendLine();
        sb.AppendLine("| ID | 名稱 | 等級 | 數值 | 說明 |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var t in job.Traits)
            sb.AppendLine($"| {t.Id} | {Cell(t.Name)} | {t.Level} | {t.Value} | {Cell(t.Description)} |");

        sb.AppendLine().AppendLine("## 狀態").AppendLine();
        sb.AppendLine("| ID | 名稱 | 最大層數 | 分類 | 永久 | 可驅散 | 需人工確認 | 說明 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var s in job.Statuses)
            sb.AppendLine($"| {s.Id} | {Cell(s.Name)} | {s.MaxStacks} | {s.StatusCategory} | {(s.IsPermanent ? "是" : "")} | {(s.CanDispel ? "是" : "")} | {(s.DuplicateName ? "同名" : "")} | {Cell(s.Description)} |");

        sb.AppendLine().AppendLine("## 技能說明").AppendLine();
        foreach (var a in job.Actions)
        {
            sb.AppendLine($"### {a.Id} {a.Name}{(a.DescriptionUnresolved ? "（含無法求值的巨集）" : "")}").AppendLine();
            sb.AppendLine(a.Description.Length == 0 ? "（無說明）" : a.Description.Replace("\n", "  \n")).AppendLine();
        }

        sb.AppendLine("## 需人工確認").AppendLine();
        if (job.ManualChecks.Count == 0) sb.AppendLine("無");
        foreach (var m in job.ManualChecks) sb.AppendLine($"- {m}");

        sb.AppendLine().AppendLine("## xivapi 差異").AppendLine();
        if (!check.Completed) { sb.AppendLine("未完成核對。"); return sb.ToString(); }
        sb.AppendLine("各版本差異數：" + string.Join("、", check.DiffCounts.Select(kv => $"{kv.Key}={kv.Value}"))).AppendLine();
        if (check.Diffs.Count == 0) { sb.AppendLine($"與 `{check.Version}` 無差異。"); return sb.ToString(); }
        sb.AppendLine("| 資料表 | 列 | 欄位 | 本機 | xivapi |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var d in check.Diffs)
            sb.AppendLine($"| {d.Sheet} | {d.Row} | {d.Field} | {Cell(d.Local)} | {Cell(d.Remote ?? "（xivapi 無此值）")} |");
        return sb.ToString();
    }

    private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", "<br>");
}
