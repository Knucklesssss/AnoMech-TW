using System;
using System.Collections.Generic;
using System.Reflection;

namespace AnoMech.Core.Native;

// Every [Signature] on this branch is Fallible, so a scan that does not match the
// TC binary leaves a null delegate instead of aborting plugin load. This walks the
// pointer classes once initialization is done and writes the outcome to the log —
// the point being that a bad scan is visible up front rather than as a crash part
// way through a scenario.
internal static class SignatureReport
{
    private static readonly Dictionary<string, bool> RawScans = new();

    // For the handful of sites that scan directly instead of via [Signature].
    // Returns 0 when the pattern is not found; callers skip the dependent hook.
    public static nint TryScanText(string name, string signature)
    {
        try
        {
            var addr = Plugin.SigScanner.ScanText(signature);
            Record(name, addr != 0);
            return addr;
        }
        catch (Exception ex)
        {
            Record(name, false);
            Plugin.Log.Error($"[SignatureReport] raw scan threw: {name} — {ex.Message}");
            return 0;
        }
    }

    // For addresses ClientStructs resolves for us; a miss there shows up as zero.
    public static nint TrackAddress(string name, nint address)
    {
        Record(name, address != 0);
        return address;
    }

    // Logged as they happen, not only in the summary: these sites are constructed
    // at different points in startup, so an outcome could otherwise land after Log.
    private static void Record(string name, bool ok)
    {
        RawScans[name] = ok;
        if (ok) Plugin.Log.Info($"[SignatureReport]   OK      {name}");
        else Plugin.Log.Error($"[SignatureReport]   MISSING {name}");
    }

    public static void Log(params Type[] pointerClasses)
    {
        var resolved = new List<string>();
        var missing = new List<string>();

        foreach (var type in pointerClasses)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            // Pointer classes hold their delegates as properties in most files and as
            // plain fields in others, so both have to be walked or a miss goes unlisted.
            foreach (var prop in type.GetProperties(Flags))
            {
                if (!typeof(Delegate).IsAssignableFrom(prop.PropertyType)) continue;
                var name = $"{type.Name}.{prop.Name}";
                (prop.GetValue(null) is null ? missing : resolved).Add(name);
            }

            foreach (var field in type.GetFields(Flags))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                // Auto-property backing fields would list every property a second time.
                if (field.Name.Contains("k__BackingField")) continue;
                var name = $"{type.Name}.{field.Name}";
                (field.GetValue(null) is null ? missing : resolved).Add(name);
            }
        }

        foreach (var (name, ok) in RawScans)
            (ok ? resolved : missing).Add(name);

        Plugin.Log.Info("[SignatureReport] ===== TC API13 signature check =====");
        Plugin.Log.Info($"[SignatureReport] {resolved.Count} resolved, {missing.Count} missing");
        foreach (var name in resolved) Plugin.Log.Info($"[SignatureReport]   OK      {name}");
        foreach (var name in missing) Plugin.Log.Error($"[SignatureReport]   MISSING {name}");
        Plugin.Log.Info("[SignatureReport] ===================================");
    }
}
