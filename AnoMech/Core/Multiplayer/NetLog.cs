using System;
using System.IO;
using System.Text;

namespace AnoMech.Core.Multiplayer;

// Always-on multiplayer trace, next to the opt-in event log. Nothing here fires per frame — joins,
// run start/stop, limit break arbitration, sync drift, dropped packets — so it can stay on without a
// setting to forget to tick before the bug happens. Local file only; nothing is uploaded.
internal static class NetLog
{
    private static readonly object Gate = new();

    private static string Directory => Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "logs");
    private static string FilePath => Path.Combine(Directory, $"net-{DateTime.Now:yyyy-MM-dd}.log");

    internal static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // The trace must never break the game.
        }
    }
}
