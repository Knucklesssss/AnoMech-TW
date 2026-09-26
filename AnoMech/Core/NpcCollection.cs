using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AnoMech.Core;

internal sealed class NpcCollection(string directory)
{
    internal sealed record Entry(uint BaseId, uint NameId, string Name, uint Territory, byte Level);
    private readonly Dictionary<(uint, uint), Entry> seen = new();
    internal string FilePath { get; } = Path.Combine(directory, "npc-collection.jsonl");
    internal IEnumerable<Entry> Entries => seen.Values;
    internal int Count => seen.Count;
    internal void Clear() => seen.Clear();

    // Keys already in the file. `seen` starts empty every load and on Clear, and sampling runs from
    // plugin load, so without this the same NPCs were appended again every session.
    private HashSet<(uint, uint)>? written;

    internal void Add(Entry entry)
    {
        var key = (entry.BaseId, entry.NameId);
        if (seen.ContainsKey(key)) return;
        written ??= ReadWrittenKeys();
        if (!written.Contains(key))
        {
            Directory.CreateDirectory(directory);
            // Commit to memory only after persistence succeeds, so a failed write can retry.
            File.AppendAllText(FilePath, JsonSerializer.Serialize(entry) + Environment.NewLine);
            written.Add(key);
        }
        seen.Add(key, entry);
    }

    private HashSet<(uint, uint)> ReadWrittenKeys()
    {
        var keys = new HashSet<(uint, uint)>();
        if (!File.Exists(FilePath)) return keys;
        foreach (var line in File.ReadLines(FilePath))
        {
            try
            {
                if (JsonSerializer.Deserialize<Entry>(line) is { } saved) keys.Add((saved.BaseId, saved.NameId));
            }
            catch (JsonException)
            {
                // A torn line from an interrupted write only costs that one NPC being written again.
            }
        }
        return keys;
    }
}
