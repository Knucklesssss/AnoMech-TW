namespace ClientData;

public sealed record Options(string Abbreviation, string GamePath, int Level, string OutputDirectory, bool SkipXivapi, string? XivapiVersion, bool CheckWarrior)
{
    private const string Usage = "用法：ClientData <職業縮寫> [--game 路徑] [--level 90] [--out 目錄] [--skip-xivapi] [--xivapi-version 7.3] [--check-war]";

    public static Options Parse(string[] args)
    {
        string? abbreviation = null, output = null, version = null;
        var game = "Z:/FINAL FANTASY XIV TC/game";
        var level = 90;
        bool skip = false, check = false;
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ToolException($"{args[i]} 缺少參數值。");
            switch (args[i])
            {
                case "--game": game = Next(); break;
                case "--level": level = int.TryParse(Next(), out var parsed) ? parsed : throw new ToolException("--level 必須是整數。"); break;
                case "--out": output = Next(); break;
                case "--skip-xivapi": skip = true; break;
                case "--xivapi-version": version = Next(); break;
                case "--check-war": check = true; break;
                default:
                    if (args[i].StartsWith("--") || abbreviation != null) throw new ToolException($"無法辨識的參數：{args[i]}\n{Usage}");
                    abbreviation = args[i].ToUpperInvariant();
                    break;
            }
        }
        if (check) abbreviation ??= "WAR";
        if (abbreviation == null) throw new ToolException(Usage);
        if (check && abbreviation != "WAR") throw new ToolException("--check-war 只能用於 WAR。");
        if (level != 90) throw new ToolException("目前只支援 90 級。");
        if (!Directory.Exists(Path.Combine(game, "sqpack")) || !File.Exists(Path.Combine(game, "ffxivgame.ver")))
            throw new ToolException($"找不到客戶端資料：{game}");
        return new(abbreviation, game, level, output ?? Path.Combine(RepoRoot(), "docs", "client-data"), skip, version, check);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AnoMech.sln"))) return dir.FullName;
        throw new ToolException("找不到 AnoMech.sln，請用 --out 指定輸出目錄。");
    }
}
