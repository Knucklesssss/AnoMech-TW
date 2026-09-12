using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AnoMech.Core.Combat;

public static unsafe class WarriorStatReader
{
    private const uint Strength = 1;
    private const uint WeaponDamage = 12;
    private const uint Tenacity = 19;
    private const uint DirectHit = 22;
    private const uint CriticalHit = 27;
    private const uint Determination = 44;
    private const uint SkillSpeed = 45;

    private static readonly int[] EquippedSlots = [0, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12];
    private static readonly uint[] GearStats = [Strength, Tenacity, DirectHit, CriticalHit, Determination, SkillSpeed];

    public static bool TryRead(ushort itemLevelSync, out WarriorDamageStats? stats, out string reason)
    {
        stats = null;
        reason = string.Empty;

        try
        {
            if (itemLevelSync == 0)
                return Fail("Item-level sync is unavailable.", out reason);
            if (Plugin.PlayerState.ClassJob.RowId != 21)
                return Fail("Only Warrior is supported.", out reason);
            if (Plugin.PlayerState.Level != 90)
                return Fail("Only level 90 is supported.", out reason);

            var uiState = UIState.Instance();
            var inventoryManager = InventoryManager.Instance();
            var equipped = inventoryManager == null
                ? null
                : inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            var localObject = Plugin.ObjectTable.LocalPlayer;
            var localPlayer = localObject == null ? null : (BattleChara*)localObject.Address;
            if (uiState == null || inventoryManager == null || equipped == null || !equipped->IsLoaded || localPlayer == null)
                return Fail("Player equipment data is unavailable.", out reason);
            if (equipped->GetSize() <= EquippedSlots[^1])
                return Fail("Equipped-item data is incomplete.", out reason);

            var itemSheet = Plugin.DataManager.GetExcelSheet<Item>();
            var itemLevelSheet = Plugin.DataManager.GetExcelSheet<ItemLevel>();
            var baseParamSheet = Plugin.DataManager.GetExcelSheet<BaseParam>();
            var materiaSheet = Plugin.DataManager.GetExcelSheet<Materia>();
            var foodSheet = Plugin.DataManager.GetExcelSheet<ItemFood>();
            var tribeSheet = Plugin.DataManager.GetExcelSheet<Tribe>();
            var resistanceSheet = Plugin.DataManager.GetExcelSheet<ResistanceWeaponAdjust>();
            var mandervilleSheet = Plugin.DataManager.GetExcelSheet<MandervilleWeaponEnhance>();
            if (itemSheet == null || itemLevelSheet == null || baseParamSheet == null || materiaSheet == null
                || foodSheet == null || tribeSheet == null || resistanceSheet == null || mandervilleSheet == null)
                return Fail("Required equipment data sheets are unavailable.", out reason);
            if (uiState->PlayerState.Tribe == 0 || !tribeSheet.TryGetRow(uiState->PlayerState.Tribe, out var tribe))
                return Fail("Player tribe data is unavailable.", out reason);

            var values = new int[GearStats.Length];
            values[IndexOf(Strength)] = 390 * 105 / 100 + tribe.STR;
            values[IndexOf(Tenacity)] = 400;
            values[IndexOf(DirectHit)] = 400;
            values[IndexOf(CriticalHit)] = 400;
            values[IndexOf(Determination)] = 390;
            values[IndexOf(SkillSpeed)] = 400;

            var weaponDamage = 0;
            var weaponDelay = 0d;
            foreach (var slot in EquippedSlots)
            {
                var inventoryItem = equipped->Items[slot];
                if (inventoryItem.ItemId == 0)
                {
                    if (slot == 0)
                        return Fail("No equipped weapon was found.", out reason);
                    continue;
                }
                if (!itemSheet.TryGetRow(inventoryItem.ItemId, out var item))
                    return Fail($"Equipped item {inventoryItem.ItemId} is missing from Item.", out reason);
                if (item.LevelItem.RowId == 0 || !itemLevelSheet.TryGetRow(Math.Min(item.LevelItem.RowId, itemLevelSync), out var itemLevel))
                    return Fail($"Item-level data for equipped item {inventoryItem.ItemId} is unavailable.", out reason);

                var baseValues = new int[GearStats.Length];
                var hqValues = new int[GearStats.Length];
                if (!ReadItemParams(item.BaseParam, item.BaseParamValue, baseParamSheet, baseValues,
                        inventoryItem.ItemId, "base", out reason))
                    return false;
                var highQuality = (inventoryItem.Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                if (highQuality && !ReadItemParams(item.BaseParamSpecial, item.BaseParamValueSpecial,
                        baseParamSheet, hqValues, inventoryItem.ItemId, "HQ", out reason))
                    return false;

                var materiaValues = new int[GearStats.Length];
                var customValues = new int[GearStats.Length];
                var isResistance = resistanceSheet.TryGetRow(inventoryItem.ItemId, out _);
                var isManderville = mandervilleSheet.TryGetRow(inventoryItem.ItemId, out _);
                if (isResistance && isManderville)
                    return Fail($"Equipped item {inventoryItem.ItemId} has ambiguous custom-stat data.", out reason);
                if (!ReadMateria(inventoryItem, materiaSheet, baseParamSheet, isManderville,
                        isResistance || isManderville ? customValues : materiaValues, out reason))
                    return false;

                for (var i = 0; i < GearStats.Length; i++)
                {
                    if (!baseParamSheet.TryGetRow(GearStats[i], out var baseParam))
                        return Fail($"BaseParam {GearStats[i]} is unavailable.", out reason);
                    if (item.BaseParamModifier >= baseParam.MeldParam.Count)
                        return Fail($"Item {inventoryItem.ItemId} has an invalid BaseParamModifier.", out reason);
                    var cap = CombatStatMath.SlotCap(ItemLevelBudget(itemLevel, GearStats[i]),
                        SlotWeight(baseParam, slot), baseParam.MeldParam[item.BaseParamModifier]);
                    values[i] = checked(values[i] + CombatStatMath.ItemValue(baseValues[i], hqValues[i],
                        customValues[i], materiaValues[i], cap, item.LevelItem.RowId > itemLevelSync));
                }

                if (slot != 0)
                    continue;
                var hqWeaponDamage = highQuality ? ItemParam(item.BaseParamSpecial, item.BaseParamValueSpecial, WeaponDamage) : 0;
                weaponDamage = Math.Min(itemLevel.PhysicalDamage, checked(item.DamagePhys + hqWeaponDamage));
                weaponDelay = item.Delayms / 1000d;
                if (weaponDamage <= 0 || weaponDelay <= 0)
                    return Fail("The equipped weapon has invalid damage or delay data.", out reason);
            }

            values[IndexOf(Strength)] = checked(values[IndexOf(Strength)] * 105 / 100);
            if (!ApplyFood(localPlayer, foodSheet, baseParamSheet, values, out reason))
                return false;

            stats = new WarriorDamageStats(
                values[IndexOf(Strength)], weaponDamage, values[IndexOf(CriticalHit)],
                values[IndexOf(DirectHit)], values[IndexOf(Determination)], values[IndexOf(Tenacity)],
                values[IndexOf(SkillSpeed)], weaponDelay);
            return true;
        }
        catch (Exception ex)
        {
            stats = null;
            reason = $"Unable to read synchronized Warrior stats: {ex.Message}";
            return false;
        }
    }

    private static bool ReadItemParams<TParams, TValues>(TParams parameters, TValues values,
        Lumina.Excel.ExcelSheet<BaseParam> baseParamSheet, int[] result, uint itemId, string kind, out string reason)
        where TParams : System.Collections.Generic.IReadOnlyList<Lumina.Excel.RowRef<BaseParam>>
        where TValues : System.Collections.Generic.IReadOnlyList<short>
    {
        if (parameters.Count != values.Count)
            return Fail($"Item {itemId} has mismatched {kind} parameter data.", out reason);
        for (var i = 0; i < parameters.Count; i++)
        {
            var stat = parameters[i].RowId;
            if (stat == 0)
            {
                if (values[i] != 0)
                    return Fail($"Item {itemId} has an invalid {kind} BaseParam.", out reason);
                continue;
            }
            if (!baseParamSheet.TryGetRow(stat, out _))
                return Fail($"Item {itemId} references missing BaseParam {stat}.", out reason);
            if (values[i] < 0)
                return Fail($"Item {itemId} has a negative {kind} parameter value.", out reason);
            var index = IndexOf(stat);
            if (index >= 0)
                result[index] = checked(result[index] + values[i]);
        }
        reason = string.Empty;
        return true;
    }

    private static bool ReadMateria(InventoryItem item, Lumina.Excel.ExcelSheet<Materia> materiaSheet,
        Lumina.Excel.ExcelSheet<BaseParam> baseParamSheet, bool manderville, int[] result, out string reason)
    {
        for (var i = 0; i < item.Materia.Length; i++)
        {
            var materiaId = item.Materia[i];
            if (materiaId == 0)
                continue;
            if (!materiaSheet.TryGetRow(materiaId, out var materia) || item.MateriaGrades[i] >= materia.Value.Count)
                return Fail($"Item {item.ItemId} has invalid materia {materiaId}.", out reason);
            var stat = manderville ? MandervilleStat(materiaId) : materia.BaseParam.RowId;
            if (manderville && stat == 0)
                return Fail($"Item {item.ItemId} has unsupported Manderville stat {materiaId}.", out reason);
            if (stat == 0 || !baseParamSheet.TryGetRow(stat, out _))
                return Fail($"Materia {materiaId} has an invalid BaseParam.", out reason);
            var amount = materia.Value[item.MateriaGrades[i]];
            if (amount < 0)
                return Fail($"Materia {materiaId} has a negative value.", out reason);
            var index = IndexOf(stat);
            if (index >= 0)
                result[index] = checked(result[index] + amount);
        }
        reason = string.Empty;
        return true;
    }

    private static bool ApplyFood(BattleChara* player, Lumina.Excel.ExcelSheet<ItemFood> foodSheet,
        Lumina.Excel.ExcelSheet<BaseParam> baseParamSheet, int[] values, out string reason)
    {
        foreach (var status in player->StatusManager.Status)
        {
            if (status.StatusId != 48)
                continue;
            var highQuality = status.Param >= 10000;
            var foodId = highQuality ? status.Param - 10000u : status.Param;
            if (foodId == 0 || !foodSheet.TryGetRow(foodId, out var food))
                return Fail($"Food {foodId} is missing from ItemFood.", out reason);
            foreach (var bonus in food.Params)
            {
                var stat = bonus.BaseParam.RowId;
                if (stat == 0)
                {
                    if (bonus.Value != 0 || bonus.ValueHQ != 0 || bonus.Max != 0 || bonus.MaxHQ != 0)
                        return Fail($"Food {foodId} has an invalid BaseParam.", out reason);
                    continue;
                }
                if (!baseParamSheet.TryGetRow(stat, out _))
                    return Fail($"Food {foodId} references missing BaseParam {stat}.", out reason);
                var index = IndexOf(stat);
                if (index < 0)
                    continue;
                var amount = highQuality ? bonus.ValueHQ : bonus.Value;
                var cap = highQuality ? bonus.MaxHQ : bonus.Max;
                values[index] = checked(values[index] + CombatStatMath.FoodBonus(values[index], amount, cap, bonus.IsRelative));
            }
        }
        reason = string.Empty;
        return true;
    }

    private static int ItemParam<TParams, TValues>(TParams parameters, TValues values, uint stat)
        where TParams : System.Collections.Generic.IReadOnlyList<Lumina.Excel.RowRef<BaseParam>>
        where TValues : System.Collections.Generic.IReadOnlyList<short>
    {
        var result = 0;
        for (var i = 0; i < Math.Min(parameters.Count, values.Count); i++)
            if (parameters[i].RowId == stat)
                result = checked(result + values[i]);
        return result;
    }

    private static int IndexOf(uint stat) => Array.IndexOf(GearStats, stat);

    private static int ItemLevelBudget(ItemLevel itemLevel, uint stat) => stat switch
    {
        Strength => itemLevel.Strength,
        Tenacity => itemLevel.Tenacity,
        DirectHit => itemLevel.DirectHitRate,
        CriticalHit => itemLevel.CriticalHit,
        Determination => itemLevel.Determination,
        SkillSpeed => itemLevel.SkillSpeed,
        _ => 0,
    };

    private static int SlotWeight(BaseParam baseParam, int slot) => slot switch
    {
        0 => baseParam.TwoHandWeaponPercent,
        2 => baseParam.HeadPercent,
        3 => baseParam.ChestPercent,
        4 => baseParam.HandsPercent,
        6 => baseParam.LegsPercent,
        7 => baseParam.FeetPercent,
        8 => baseParam.EarringPercent,
        9 => baseParam.NecklacePercent,
        10 => baseParam.BraceletPercent,
        11 or 12 => baseParam.RingPercent,
        _ => 0,
    };

    private static uint MandervilleStat(ushort materiaId) => materiaId switch
    {
        1403 => CriticalHit,
        1404 => DirectHit,
        1405 => Determination,
        1406 => SkillSpeed,
        1408 => Tenacity,
        _ => 0,
    };

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }
}
