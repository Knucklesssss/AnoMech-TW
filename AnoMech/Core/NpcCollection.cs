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

    internal void Add(Entry entry)
    {
        var key = (entry.BaseId, entry.NameId);
        if (seen.ContainsKey(key)) return;
        Directory.CreateDirectory(directory);
        // Commit to memory only after persistence succeeds, so a failed write can retry.
        File.AppendAllText(FilePath, JsonSerializer.Serialize(entry) + Environment.NewLine);
        seen.Add(key, entry);
    }
}
