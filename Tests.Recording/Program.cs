using AnoMech.Core.Recording;
using System.Text;
using System.Text.Json;

var name = "魔物\"\\\n\t\u0000😀";
var escaped = RecordingJson.Escape(name);
Check(JsonSerializer.Deserialize<string>('"' + escaped + '"') == name, "JSON escaping round-trips.");
Check(JsonSerializer.Deserialize<string>('"' + RecordingJson.Escape("\ud800") + '"') == "\ufffd", "Invalid surrogate is replaced.");
var file = Path.Combine(Path.GetTempPath(), "anomech-recording-" + Guid.NewGuid() + ".jsonl");
try
{
    using var writer = new RecordingWriter(new StreamWriter(new FileStream(file, FileMode.CreateNew,
        FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true });
    Parallel.For(0, 100, i => Check(writer.TryWriteLine($"{{\"capture\":{i}}}"), "Concurrent write succeeds."));
    string[] lines;
    using (var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
        lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    Check(lines.Length == 100 && lines.Select(line => JsonDocument.Parse(line).RootElement.GetProperty("capture").GetInt32()).Distinct().Count() == 100,
        "Every concurrent record is immediately readable as an intact JSON line.");
    Check(writer.Close() && writer.Close() && !writer.IsHealthy && !writer.TryWriteLine("late"), "Close is idempotent and rejects late records.");
}
finally { File.Delete(file); }

var broken = new RecordingWriter(new FailingWriter());
Check(!broken.TryWriteLine("record") && !broken.IsHealthy && broken.ErrorCount == 1 && broken.LastError!.Contains("disk full"), "Write failure becomes visible.");
Check(!broken.TryWriteLine("later") && broken.ErrorCount == 1 && !broken.Close(), "Faulted recording cannot report a successful seal.");
var flushFailure = new RecordingWriter(new FailingWriter(failFlush: true));
Check(flushFailure.TryWriteLine("record") && !flushFailure.TryFlush() && !flushFailure.Close(), "Flush failure prevents successful seal.");
var tracker = new VfxCaptureTracker();
Check(tracker.TryStart(RecordedVfxKind.Actor, 42, out var first, out _), "First VFX is tracked.");
Check(tracker.TryStart(RecordedVfxKind.Actor, 42, out var next, out var replaced)
    && replaced == first && next.Id != first.Id, "Reused native address receives a new recording identity.");
Check(tracker.TryEnd(RecordedVfxKind.Actor, 42, out var ended) && ended == next && tracker.Count == 0, "End removes current identity.");
Console.WriteLine("Recording checks passed.");

static void Check(bool success, string message)
{
    if (!success) throw new Exception(message);
}

sealed class FailingWriter(bool failFlush = false) : StringWriter
{
    public override void WriteLine(string? value)
    {
        if (!failFlush) throw new IOException("disk full");
        base.WriteLine(value);
    }
    public override void Flush() => throw new IOException("flush failed");
}
