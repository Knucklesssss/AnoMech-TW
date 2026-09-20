using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ThreadingTask = System.Threading.Tasks.Task;

namespace AnoMech.Core.Map;

internal sealed class OpcodeUpdater : IDisposable
{
    private readonly OpcodeAllowlist runtime;
    private readonly Configuration config;
    private readonly OpcodeUpdateOwner? owner;

    internal OpcodeUpdater(OpcodeAllowlist runtime)
    {
        this.runtime = runtime;
        config = Plugin.Config;
        // Keep A's previous allowlist while a version refresh is pending or fails.
        var validCache = OpcodeData.TryValidate(config.ZoneDownOpcodes, out var cached);
        if (validCache) runtime.Publish(cached);
        var version = CaptureGameVersion();
        if (string.IsNullOrEmpty(version))
        {
            Plugin.Log.Warning("[OpcodeUpdater] Native game version unavailable; keeping previous opcodes.");
            return;
        }
        owner = new OpcodeUpdateOwner(version);
        var cacheKey = $"{version}_{typeof(OpcodeUpdater).Assembly.GetName().Version}";
        if (validCache && cacheKey == config.ZoneFirewallGameVersion)
        {
            Plugin.Log.Information("[OpcodeUpdater] Opcodes are current.");
            return;
        }
        _ = DownloadAsync(owner, cacheKey);
    }

    private static unsafe string? CaptureGameVersion()
    {
        var framework = Framework.Instance();
        return framework == null ? null : new string(framework->GameVersionString);
    }

    private async ThreadingTask DownloadAsync(OpcodeUpdateOwner sessionOwner, string cacheKey)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionOwner.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var client = new HttpClient();
            // Preserve A's upstream branch and redirect policy.
            var url = $"https://github.com/kawaii/Hyperborea/raw/main/opcodes/{Uri.EscapeDataString(sessionOwner.GameVersion)}.txt";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var body = await OpcodeData.ReadBoundedAsync(stream, timeout.Token).ConfigureAwait(false);
            if (!OpcodeData.TryParse(body, out var opcodes))
                throw new InvalidDataException("Opcode document failed validation.");
            if (sessionOwner.IsDisposed) return;
            await Plugin.Framework.Run(() => sessionOwner.TryCommit(sessionOwner.GameVersion, () =>
            {
                if (!OpcodeCacheCommit.TryPersistAndPublish(runtime, cacheKey, opcodes,
                        () => config.ZoneFirewallGameVersion, () => config.ZoneDownOpcodes,
                        (key, values) => { config.ZoneFirewallGameVersion = key; config.ZoneDownOpcodes = values; },
                        config.Save, out var error))
                {
                    Plugin.Log.Warning($"[OpcodeUpdater] Cache save failed; keeping previous opcodes: {error?.Message}");
                    return;
                }
                Plugin.Log.Information($"[OpcodeUpdater] ZoneDown opcodes updated: {runtime.Count} entries.");
            })).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (sessionOwner.IsDisposed) { }
        catch (Exception error)
        {
            if (!sessionOwner.IsDisposed)
                Plugin.Log.Warning($"[OpcodeUpdater] Failed to fetch opcodes: {error.Message}");
        }
    }

    public void Dispose() => owner?.Dispose();
}
