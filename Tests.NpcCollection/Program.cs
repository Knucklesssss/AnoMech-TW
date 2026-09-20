using AnoMech.Core;
using System.Text.Json;

var directory = Path.Combine(Path.GetTempPath(), "anomech-npc-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var collection = new NpcCollection(directory);
    var entry = new NpcCollection.Entry(1, 2, "怪物\"\n../資料", 1122, 90);
    collection.Add(entry);
    collection.Add(entry);
    var lines = File.ReadAllLines(collection.FilePath);
    Check(collection.Count == 1 && lines.Length == 1, "Duplicate NPC is written once.");
    Check(JsonSerializer.Deserialize<NpcCollection.Entry>(lines[0]) == entry, "Names round-trip as one JSON line.");
    Check(Path.GetDirectoryName(collection.FilePath) == directory, "Output stays in the configured directory.");
    collection.Clear();
    Check(collection.Count == 0 && File.ReadAllLines(collection.FilePath).Length == 1, "Clear preserves saved records.");
    collection.Add(entry with { NameId = 3 });
    Check(File.ReadAllLines(collection.FilePath).Length == 2, "New records append without replacing old ones.");

    var blocked = Path.Combine(directory, "blocked");
    File.WriteAllText(blocked, "occupied");
    var retry = new NpcCollection(blocked);
    try { retry.Add(entry); throw new Exception("Write unexpectedly succeeded."); }
    catch (IOException) { }
    Check(retry.Count == 0, "Failed writes do not commit in memory.");
    File.Delete(blocked);
    retry.Add(entry);
    Check(retry.Count == 1 && File.Exists(retry.FilePath), "Failed records can retry.");
    Console.WriteLine("NPC collection checks passed.");
}
finally { Directory.Delete(directory, recursive: true); }

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}
