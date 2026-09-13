using System.Runtime.CompilerServices;

namespace ClientData;

public static class Tool
{
    // Kept out of Main so no Lumina type is resolved before the load handler exists.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            var job = ClientReader.Read(options.GamePath, options.Abbreviation, options.Level);
            if (options.CheckWarrior)
            {
                var failures = WarriorGolden.Check(job);
                foreach (var failure in failures) Console.Error.WriteLine($"戰士標準答案不符：{failure}");
                Console.WriteLine(failures.Count == 0 ? "PASS: 戰士標準答案檢查通過。" : $"FAIL: {failures.Count} 項不符。");
                return failures.Count == 0 ? 0 : 3;
            }
            var check = options.SkipXivapi ? XivapiCheck.Skipped("使用 --skip-xivapi 略過") : XivapiClient.Check(job, options.XivapiVersion);
            Directory.CreateDirectory(options.OutputDirectory);
            var path = Path.Combine(options.OutputDirectory, $"{job.Abbreviation}-{job.Level}.md");
            var temp = path + ".tmp";
            File.WriteAllText(temp, Markdown.Write(job, check, DateTimeOffset.Now));
            File.Move(temp, path, overwrite: true);
            Console.WriteLine($"已寫入 {path}（技能 {job.Actions.Count}、特性 {job.Traits.Count}、狀態 {job.Statuses.Count}、需人工確認 {job.ManualChecks.Count}）");
            return check.Completed ? 0 : 2;
        }
        catch (ToolException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"讀取失敗：{ex}"); return 1; }
    }
}
