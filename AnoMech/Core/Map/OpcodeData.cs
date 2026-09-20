using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Core.Map;

internal static class OpcodeData
{
    internal const int MaxBodyBytes = 64 * 1024;
    internal const int MaxZoneDownOpcodes = 2048;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static bool TryParse(ReadOnlySpan<byte> bytes, out ushort[] opcodes)
    {
        opcodes = [];
        if (bytes.Length > MaxBodyBytes) return false;
        try
        {
            uint[]? values = null;
            foreach (var line in StrictUtf8.GetString(bytes).Split('\n'))
            {
                if (!line.StartsWith("ZoneDown=", StringComparison.Ordinal)) continue;
                if (values != null) return false;
                var tokens = line["ZoneDown=".Length..].TrimEnd('\r').Split(',');
                if (tokens.Length > MaxZoneDownOpcodes) return false;
                values = new uint[tokens.Length];
                for (int i = 0; i < tokens.Length; i++)
                    if (!uint.TryParse(tokens[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                        return false;
            }
            return TryValidate(values, out opcodes);
        }
        catch (DecoderFallbackException) { return false; }
    }

    internal static bool TryValidate(uint[]? values, out ushort[] opcodes)
    {
        opcodes = [];
        if (values == null || values.Length == 0 || values.Length > MaxZoneDownOpcodes
            || values.Any(v => v > ushort.MaxValue) || !values.Any(v => v != 0)) return false;
        opcodes = Array.ConvertAll(values, v => (ushort)v);
        return true;
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken token)
    {
        var bytes = new byte[MaxBodyBytes + 1];
        int count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count), token).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        if (count > MaxBodyBytes) throw new InvalidDataException("Opcode document exceeds 64 KiB.");
        return bytes[..count];
    }
}

// Owns the cancellation token and the callback admission decision for one
// captured game-version session. Dispose never waits for the async download.
internal sealed class OpcodeUpdateOwner : IDisposable
{
    private readonly object commitGate = new();
    private readonly CancellationTokenSource cancellation = new();
    private int disposed;

    internal OpcodeUpdateOwner(string gameVersion)
    {
        GameVersion = gameVersion;
        Token = cancellation.Token;
    }

    internal string GameVersion { get; }
    internal CancellationToken Token { get; }
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    // Dispose and framework commits share this gate. An admitted commit finishes
    // before Dispose returns; callbacks admitted afterwards cannot write config.
    internal bool TryCommit(string capturedGameVersion, Action commit)
    {
        lock (commitGate)
        {
            if (IsDisposed || !string.Equals(capturedGameVersion, GameVersion, StringComparison.Ordinal))
                return false;
            commit();
            return true;
        }
    }

    public void Dispose()
    {
        lock (commitGate)
        {
            if (IsDisposed) return;
            Volatile.Write(ref disposed, 1);
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}

internal static class OpcodeCacheCommit
{
    internal static bool TryPersistAndPublish(
        OpcodeAllowlist runtime,
        string cacheKey,
        ReadOnlySpan<ushort> opcodes,
        Func<string> readKey,
        Func<uint[]> readOpcodes,
        Action<string, uint[]> write,
        Action save,
        out Exception? saveError)
    {
        saveError = null;
        var oldKey = readKey();
        var oldOpcodes = readOpcodes();
        var persisted = new uint[opcodes.Length];
        for (var i = 0; i < opcodes.Length; i++)
            persisted[i] = opcodes[i];

        write(cacheKey, persisted);
        try
        {
            save();
        }
        catch (Exception error)
        {
            // SavePluginConfig's disk atomicity is an SDK concern; this restores
            // only the in-memory fields and leaves the runtime snapshot untouched.
            write(oldKey, oldOpcodes);
            saveError = error;
            return false;
        }

        runtime.Publish(opcodes);
        return true;
    }
}
