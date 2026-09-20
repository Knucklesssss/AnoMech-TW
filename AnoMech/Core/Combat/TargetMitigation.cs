using System;
using System.Collections.Generic;
using System.Linq;

namespace AnoMech.Core.Combat;

public readonly record struct MitigationEffect(uint Action, string Name, float Remaining, float Physical, float Magical);
public readonly record struct MitigationReport(float Time, string Attack, string Result);

// Local practice feedback only; never changes HP, death rules or network state.
public static class TargetMitigation
{
    private static readonly Dictionary<(object Target, uint Action), float> effects = new();
    private static readonly List<MitigationReport> reports = new();
    public static IReadOnlyList<MitigationReport> Reports => reports;
    public static float Clock { get; private set; }
    public static string LastUse { get; set; } = "尚未施放目標減傷";
    public static bool Supported(uint action) => action is 7535 or 7560 or 7549 or 2887;
    public static MitigationEffect Rule(uint action) => action switch
    {
        7535 => new(action, "雪仇", 15, .1f, .1f),
        7560 => new(action, "昏亂", 15, .05f, .1f),
        7549 => new(action, "牽制", 15, .1f, .05f),
        2887 => new(action, "武裝解除", 10, .1f, .1f),
        _ => default,
    };
    public static void Apply(object target, uint action)
    {
        if (!Supported(action)) return;
        effects[(target, action)] = Clock + Rule(action).Remaining;
    }
    public static MitigationEffect[] Active(object target) => effects
        .Where(x => ReferenceEquals(x.Key.Target, target) && x.Value > Clock)
        .Select(x => Rule(x.Key.Action) with { Remaining = x.Value - Clock }).OrderBy(x => x.Action).ToArray();
    public static float Reduction(object target, bool magical)
        => 1f - Active(target).Aggregate(1f, (factor, effect) => factor * (1f - (magical ? effect.Magical : effect.Physical)));
    public static void Record(object? source, string attack, bool? magical)
    {
        var active = source == null ? [] : Active(source);
        var result = source == null ? "來源未對應，未判定" : magical == null ? "傷害類型未確認，未判定"
            : active.Length == 0 ? "未覆蓋（本機）"
            : $"有覆蓋：{Reduction(source, magical.Value):P1}（{(magical.Value ? "魔法" : "物理")}） · "
                + string.Join("、", active.Select(x => $"{x.Name} 剩 {x.Remaining:F1} 秒"));
        // Several helper rays can resolve the same attack in one frame.
        if (reports.Count > 0 && reports[^1] == new MitigationReport(Clock, attack, result)) return;
        if (reports.Count == 32) reports.RemoveAt(0);
        reports.Add(new(Clock, attack, result));
    }
    public static void Tick(float seconds)
    {
        Clock += Math.Max(0, seconds);
        foreach (var key in effects.Where(x => x.Value <= Clock).Select(x => x.Key).ToArray()) effects.Remove(key);
    }
    public static void Clear()
    {
        effects.Clear(); reports.Clear(); Clock = 0; LastUse = "尚未施放目標減傷";
    }
}
