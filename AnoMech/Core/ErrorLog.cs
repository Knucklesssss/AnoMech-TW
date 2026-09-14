using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AnoMech.Core;

// Always-on bug log, independent of the opt-in event log. Each distinct problem is written once
// with full detail; repeats only count, and the counts are appended as a summary on unload so
// reports can be compared by frequency. Local file only; nothing is uploaded.
internal static class ErrorLog
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, int> Counts = [];

    internal static string Directory => Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "errors");
    private static string FilePath => Path.Combine(Directory, $"errors-{DateTime.Now:yyyy-MM-dd}.log");

    internal static void Record(string category, string message, Exception? exception = null)
    {
        try
        {
            var key = $"{category}|{message}";
            lock (Gate)
            {
                Counts.TryGetValue(key, out var seen);
                Counts[key] = seen + 1;
                if (seen > 0) return;
                System.IO.Directory.CreateDirectory(Directory);
                var line = $"[{DateTime.Now:HH:mm:ss}] v{typeof(ErrorLog).Assembly.GetName().Version} {Context()} [{category}] {message}";
                if (exception != null) line += Environment.NewLine + exception;
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // The bug log must never break the game.
        }
    }

    internal static void WriteSummary()
    {
        try
        {
            lock (Gate)
            {
                var repeated = Counts.Where(c => c.Value > 1).OrderByDescending(c => c.Value).ToList();
                if (repeated.Count > 0)
                {
                    var text = new StringBuilder($"[{DateTime.Now:HH:mm:ss}] 本次重複次數統計：").AppendLine();
                    foreach (var (key, count) in repeated) text.AppendLine($"  {count} 次 [{key.Replace("|", "] ")}");
                    System.IO.Directory.CreateDirectory(Directory);
                    File.AppendAllText(FilePath, text.ToString(), Encoding.UTF8);
                }
                Counts.Clear();
            }
        }
        catch
        {
        }
    }

    internal static void OpenFolder()
    {
        System.IO.Directory.CreateDirectory(Directory);
        Process.Start(new ProcessStartInfo { FileName = Directory, UseShellExecute = true });
    }

    private static string Context()
    {
        try
        {
            var job = Plugin.PlayerState.ClassJob.IsValid ? Plugin.PlayerState.ClassJob.RowId : 0;
            return $"job={job} territory={Plugin.ClientState.TerritoryType}";
        }
        catch
        {
            return "job=? territory=?";
        }
    }
}
