using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using AnoMech.Core;

namespace AnoMech.Windows;

internal sealed class NpcCollectorWindow : Window
{
    private readonly NpcCollection collection = new(Plugin.PluginInterface.ConfigDirectory.FullName);
    private string? lastError;
    private bool auto = true;
    private double lastSample;
    private string filter = "";

    public NpcCollectorWindow() : base("AnoMech 怪物採集器###AnoMechNpc")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 300),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    /// <summary>由 Plugin 的 framework update 呼叫。每秒取樣一次就夠——這只是抄 id，不需要逐幀。</summary>
    public void Tick(float deltaSeconds)
    {
        if (!auto) return;
        lastSample += deltaSeconds;
        if (lastSample < 1.0) return;
        lastSample = 0;
        Sample();
    }

    private void Sample()
    {
        try
        {
            SampleObjects();
            lastError = null;
        }
        catch (Exception error)
        {
            auto = false;
            lastError = error.Message;
            Plugin.Log.Warning($"[NpcCollector] 採集失敗：{error.Message}");
        }
    }

    private void SampleObjects()
    {
        var territory = Plugin.ClientState.TerritoryType;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IBattleNpc npc) continue;
            // 刻意**不**只收 BattleNpcSubKind.Enemy：機制的 AOE 多半由「隱形 helper」
            // 施放，它們的 kind 未必是 Enemy，濾掉就等於漏掉最關鍵的那批 id。
            collection.Add(new NpcCollection.Entry(obj.BaseId, npc.NameId, obj.Name.TextValue, territory, npc.Level));
        }
    }

    public override void Draw()
    {
        ImGui.Checkbox("自動採集（每秒掃描一次）", ref auto);
        ImGui.SameLine();
        if (ImGui.Button("立刻掃一次")) Sample();
        ImGui.SameLine();
        if (ImGui.Button("清空本次清單")) collection.Clear();

        ImGui.TextDisabled($"本次已採集 {collection.Count} 種 NPC；每筆立即附加保存，清空清單不會刪除檔案。");
        ImGui.TextWrapped($"保存位置：{collection.FilePath}");
        if (lastError is not null) ImGui.TextWrapped($"採集失敗：{lastError}。請排除問題後重新開啟自動採集或立即掃描。");
        ImGui.TextDisabled("可採集任何副本中實際出現的 NPC 與隱形機制來源；不代表該副本已能模擬。");

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##f", "過濾名稱", ref filter, 64);
        ImGui.Separator();

        if (!ImGui.BeginTable("npc", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp))
            return;
        ImGui.TableSetupColumn("BNpcBase", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("BNpcName", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("等級", ImGuiTableColumnFlags.WidthStretch, 0.4f);
        ImGui.TableSetupColumn("區域", ImGuiTableColumnFlags.WidthStretch, 0.7f);
        ImGui.TableSetupColumn("名稱", ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        foreach (var e in collection.Entries.OrderBy(v => v.Territory).ThenBy(v => v.BaseId))
        {
            if (filter.Length > 0 && e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(e.BaseId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(e.NameId.ToString());
            ImGui.TableNextColumn(); ImGui.Text(e.Level.ToString());
            ImGui.TableNextColumn(); ImGui.Text(e.Territory.ToString());
            ImGui.TableNextColumn(); ImGui.Text(e.Name);
        }
        ImGui.EndTable();
    }
}
