using AnoMech.Core.Map;
using System.Text;

Check(OpcodeData.TryParse(Encoding.UTF8.GetBytes("# table\r\nZoneUp=999\r\nZoneDown=4, 0, 7,4\r\n"), out var parsed)
    && parsed.SequenceEqual(new ushort[] {4, 0, 7, 4}), "Keep A's accepted values and packet direction.");
foreach (var invalid in new[] { "ZoneDown=", "ZoneDown=0,0", "ZoneDown=65536", "ZoneDown=-1", "ZoneDown=1,,2", "ZoneDown=1\nZoneDown=2", "ZoneUp=1" })
    Check(!OpcodeData.TryParse(Encoding.UTF8.GetBytes(invalid), out _), "Reject malformed table: " + invalid);
Check(!OpcodeData.TryParse(new byte[] { 0xff }, out _), "Reject invalid UTF-8.");
Check(!OpcodeData.TryParse(new byte[OpcodeData.MaxBodyBytes + 1], out _), "Reject oversized body.");
Check(!OpcodeData.TryValidate(new uint[] { 1, 65536 }, out _), "Reject cached opcode truncation.");
var runtime = new OpcodeAllowlist();
var input = new ushort[] { 7, 4, 0 };
runtime.Publish(input);
input[0] = 9;
Check(runtime.Contains(7) && runtime.Contains(4) && runtime.Contains(0) && !runtime.Contains(9), "Snapshot must own its sorted copy.");
string key = "old";
uint[] cached = [4, 7];
Check(!OpcodeCacheCommit.TryPersistAndPublish(runtime, "new", new ushort[] { 9 }, () => key, () => cached,
    (k, v) => { key = k; cached = v; }, () => throw new IOException("disk"), out _), "Surface persistence failure.");
Check(key == "old" && cached.SequenceEqual(new uint[] { 4, 7 }) && runtime.Contains(7) && !runtime.Contains(9), "Failed save must preserve config and published snapshot.");
Check(OpcodeCacheCommit.TryPersistAndPublish(runtime, "new", new ushort[] { 9 }, () => key, () => cached,
    (k, v) => { key = k; cached = v; }, () => { }, out _), "Publish successful save.");
Check(key == "new" && runtime.Contains(9) && !runtime.Contains(7), "Publish replacement only after save.");
var owner = new OpcodeUpdateOwner("version");
int commits = 0;
Check(!owner.TryCommit("different", () => commits++), "Reject wrong-version callback.");
Check(owner.TryCommit("version", () => commits++), "Accept live matching callback.");
owner.Dispose();
Check(!owner.TryCommit("version", () => commits++) && commits == 1 && owner.Token.IsCancellationRequested, "Queued callbacks cannot commit after disposal.");
try { await OpcodeData.ReadBoundedAsync(new MemoryStream(new byte[OpcodeData.MaxBodyBytes + 1]), default); throw new Exception("Oversized stream accepted."); }
catch (InvalidDataException) { }
Console.WriteLine("PASS: opcode parsing, legacy packet values, immutable snapshots, save rollback and disposed callback rejection.");
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
