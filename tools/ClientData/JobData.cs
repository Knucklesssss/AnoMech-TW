namespace ClientData;

public sealed class ToolException(string message) : Exception(message);

public sealed record ActionRow(uint Id, string Name, string Category, byte Level, ushort Cast100ms, ushort Recast100ms,
    byte CooldownGroup, byte AdditionalCooldownGroup, byte MaxCharges, byte CostType, ushort CostValue, sbyte Range,
    byte EffectRange, byte CastType, bool CanTargetSelf, bool CanTargetParty, bool CanTargetHostile, bool IsPlayerAction,
    bool IsRoleAction, bool IsPvP, uint ComboFrom, uint ClassJobCategory, uint StatusGainSelf, string Description,
    bool DescriptionUnresolved);

public sealed record TraitRow(uint Id, string Name, byte Level, short Value, uint ClassJob, string Description, bool DescriptionUnresolved);

public sealed record StatusRow(uint Id, string Name, byte MaxStacks, byte StatusCategory, bool IsPermanent, bool CanDispel, string Description, bool DuplicateName);

public sealed record Replacement(uint From, uint To, string Source);

public sealed record JobData(uint ClassJob, string Abbreviation, string Name, int Level, string GameVersion,
    IReadOnlyList<ActionRow> Actions, IReadOnlyList<TraitRow> Traits, IReadOnlyList<StatusRow> Statuses,
    IReadOnlyList<Replacement> Replacements, IReadOnlyList<string> ManualChecks);

public sealed record XivapiCheck(bool Completed, string? Version, IReadOnlyDictionary<string, int> DiffCounts, IReadOnlyList<FieldDiff> Diffs, string? FailureReason)
{
    public static XivapiCheck Skipped(string reason) => new(false, null, new Dictionary<string, int>(), [], reason);
}
