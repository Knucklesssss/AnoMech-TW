using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;

namespace AnoMech.Windows;

/// <summary>
/// 站位編輯器：先觀察 AiManager 實際 fire 的 movement step，再以角色／時間區間編輯。
/// 僅在單機場景提供覆寫；P3 Hello World 與小電視保留原始路線。
/// </summary>
public sealed class PositionEditorWindow : Window
{
    private static readonly string[] PracticeRoleLabels = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];

    private string? selectedPracticeStepId;
    private string? selectedIntervalId;
    private string practiceFilter = string.Empty;
    private string operationStatus = string.Empty;
    private string? editorScenarioKey;
    private string? loadedIntervalId;
    private const int StepsPerPage = 100;
    private readonly List<PracticeObservedMove> filteredSteps = new();
    private long filteredVersion = -1;
    private string previousFilter = string.Empty;
    private int practicePage;
    private readonly Vector2[] stepDraft = new Vector2[PracticePositionLogic.PartySlotCount];
    private string? draftStepId;
    private string? draftFingerprint;

    private float intervalStart;
    private float intervalEnd;
    private int intervalSlot;
    private Vector2 intervalStartPosition;
    private Vector2 intervalEndPosition;
    private bool intervalHasEnd;

    public PositionEditorWindow() : base("AnoMech 站位編輯器###AnoMechPositions")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public override void Draw()
    {
        var world = Plugin.GameInstance?.World;
        DrawPracticeEditor(world);

    }

    private void DrawPracticeEditor(SimWorld? world)
    {
        ImGui.TextUnformatted("單機練習站位");
        ImGui.TextDisabled("多人連線、P3 Hello World 與小電視不提供站位覆寫。");
        ImGui.TextDisabled("先按「開始」並讓場景執行到目標步驟；只保存觀察到的原始指紋，動態分支不符時自動退回原路線。");

        var practice = world?.PracticePositions;
        if (practice is null || !practice.IsActive)
        {
            editorScenarioKey = null;
            selectedPracticeStepId = null;
            selectedIntervalId = null;
            loadedIntervalId = null;
            filteredVersion = -1;
            filteredSteps.Clear();
            draftStepId = null;
            ImGui.TextDisabled("目前沒有可編輯的單機場景。開始練習後會列出已執行的移動步驟。");
            return;
        }
        if (practice.CurrentScenario is { } scenario)
        {
            if (!string.Equals(editorScenarioKey, scenario.Key, StringComparison.Ordinal))
            {
                editorScenarioKey = scenario.Key;
                selectedPracticeStepId = null;
                selectedIntervalId = null;
                loadedIntervalId = null;
                operationStatus = string.Empty;
                filteredVersion = -1;
                practicePage = 0;
                draftStepId = null;
            }
            ImGui.TextUnformatted($"場景：{scenario.Name}    打法：{scenario.StratName}");
        }

        var mode = practice.Mode;
        if (ImGui.RadioButton("原始演算法##practice-mode", mode == PracticePositionMode.Original))
            SetMode(practice, PracticePositionMode.Original);
        ImGui.SameLine();
        if (ImGui.RadioButton("自訂練習路線##practice-mode", mode == PracticePositionMode.Custom))
            SetMode(practice, PracticePositionMode.Custom);
        ImGui.SameLine();
        ImGui.TextDisabled(practice.UseCustom ? "目前會套用已保存的自訂目標" : "目前完全沿用原始分工／時點／配速");

        if (!string.IsNullOrEmpty(practice.LastError))
            ImGui.TextColored(new Vector4(1f, 0.35f, 0.25f, 1f), $"保存／讀取錯誤：{practice.LastError}");
        if (!string.IsNullOrEmpty(operationStatus))
            ImGui.TextDisabled(operationStatus);

        var steps = practice.ObservedSteps;
        ImGui.TextUnformatted($"已觀察 {steps.Count} 步；軌跡區間 {practice.IntervalOverlays.Count} 個（區間邊界含頭尾）");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##practice-filter", "篩選步驟 ID／狀態（不把數千軌跡塞進下拉）", ref practiceFilter, 128);

        if (filteredVersion != practice.ObservationVersion || !string.Equals(previousFilter, practiceFilter, StringComparison.Ordinal))
        {
            filteredSteps.Clear();
            foreach (var step in steps)
                if (string.IsNullOrWhiteSpace(practiceFilter)
                    || step.Id.Contains(practiceFilter, StringComparison.OrdinalIgnoreCase)
                    || step.Status.Contains(practiceFilter, StringComparison.OrdinalIgnoreCase))
                    filteredSteps.Add(step);
            filteredVersion = practice.ObservationVersion;
            previousFilter = practiceFilter;
            if (selectedPracticeStepId is null || !filteredSteps.Any(step => step.Id == selectedPracticeStepId))
                selectedPracticeStepId = filteredSteps.Count > 0 ? filteredSteps[^1].Id : null;
        }
        var visible = filteredSteps;
        if (visible.Count == 0)
        {
            ImGui.TextDisabled(steps.Count == 0
                ? "尚未觸發任何移動步驟；請先讓場景執行。"
                : "沒有符合篩選條件的步驟。");
            DrawIntervalEditor(practice, null);
            return;
        }

        var pageCount = Math.Max(1, (visible.Count + StepsPerPage - 1) / StepsPerPage);
        practicePage = Math.Clamp(practicePage, 0, pageCount - 1);
        ImGui.BeginDisabled(practicePage == 0);
        if (ImGui.SmallButton("上一頁")) practicePage--;
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextUnformatted($"{practicePage + 1}/{pageCount} 頁，每頁 {StepsPerPage} 步");
        ImGui.SameLine();
        ImGui.BeginDisabled(practicePage == pageCount - 1);
        if (ImGui.SmallButton("下一頁")) practicePage++;
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.SmallButton("最新步驟"))
        {
            practicePage = pageCount - 1;
            selectedPracticeStepId = visible[^1].Id;
        }

        if (ImGui.BeginTable("practice-step-list", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.SizingStretchProp,
                new Vector2(0, 190)))
        {
            ImGui.TableSetupColumn("步驟", ImGuiTableColumnFlags.WidthFixed, 82f);
            ImGui.TableSetupColumn("時間", ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn("抵達", ImGuiTableColumnFlags.WidthFixed, 78f);
            ImGui.TableSetupColumn("結果", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            for (var row = practicePage * StepsPerPage; row < Math.Min(visible.Count, (practicePage + 1) * StepsPerPage); row++)
            {
                var step = visible[row];
                ImGui.PushID(step.Id);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable(step.Id, string.Equals(step.Id, selectedPracticeStepId),
                        ImGuiSelectableFlags.SpanAllColumns))
                    selectedPracticeStepId = step.Id;
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{step.Time:F2}s");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(step.ArrivalTime > step.Time ? $"{step.ArrivalTime:F2}s" : "—");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(step.Status);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        var selected = selectedPracticeStepId is null ? null : practice.FindObserved(selectedPracticeStepId);
        DrawSelectedStep(practice, world, selected);
        DrawIntervalEditor(practice, selected);
    }

    private void SetMode(PracticePositions practice, PracticePositionMode mode)
    {
        if (practice.SetMode(mode))
            operationStatus = mode == PracticePositionMode.Custom ? "已保存：自訂練習路線" : "已保存：原始演算法";
        else
            operationStatus = $"未保存：{practice.LastError ?? "未知錯誤"}";
    }

    private void DrawSelectedStep(PracticePositions practice, SimWorld? world, PracticeObservedMove? selected)
    {
        if (selected is null) return;
        ImGui.Separator();
        ImGui.TextUnformatted($"步驟 {selected.Id}：原始 {selected.Time:F2}s");
        ImGui.TextDisabled($"原始目標指紋：{selected.OriginalFingerprint}    狀態：{selected.Status}");

        if (draftStepId != selected.Id || draftFingerprint != selected.OriginalFingerprint)
        {
            var overlay = practice.GetStepOverlay(selected.Id);
            var sameBranch = overlay?.OriginalFingerprint == selected.OriginalFingerprint;
            for (var i = 0; i < stepDraft.Length; i++)
                stepDraft[i] = (sameBranch ? overlay!.Positions[i] : null) ?? selected.Original[i] ?? Vector2.Zero;
            draftStepId = selected.Id;
            draftFingerprint = selected.OriginalFingerprint;
        }
        var playerLocal = PlayerLocal(world);
        if (ImGui.BeginTable($"practice-targets:{selected.Id}", 5,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("角色", ImGuiTableColumnFlags.WidthFixed, 52f);
            ImGui.TableSetupColumn("原始目標", ImGuiTableColumnFlags.WidthFixed, 108f);
            ImGui.TableSetupColumn("上次執行目標", ImGuiTableColumnFlags.WidthFixed, 108f);
            ImGui.TableSetupColumn("自訂目標", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("動作", ImGuiTableColumnFlags.WidthFixed, 150f);
            ImGui.TableHeadersRow();
            for (var i = 0; i < PracticePositionLogic.PartySlotCount; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(PracticeRoleLabels[i]);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Format(selected.Original[i]));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Format(selected.Effective[i]));
                ImGui.TableNextColumn();
                var editable = stepDraft[i];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat2($"##custom:{selected.Id}:{i}", ref editable, 0.1f, -30f, 30f, "%.2f"))
                    stepDraft[i] = editable;
                if (ImGui.IsItemDeactivatedAfterEdit()) SaveStepSlot(practice, selected.Id, i, stepDraft[i]);
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"原始##reset:{selected.Id}:{i}"))
                    SaveStepReset(practice, selected.Id, i);
                ImGui.SameLine();
                ImGui.BeginDisabled(playerLocal is null);
                if (ImGui.SmallButton($"擷取##capture:{selected.Id}:{i}"))
                    SaveStepSlot(practice, selected.Id, i, playerLocal!.Value);
                ImGui.EndDisabled();
            }
            ImGui.EndTable();
        }

        ImGui.BeginDisabled(world?.Map.IsInInstance != true);
        if (ImGui.Button($"暫停預覽（不動玩家）##{selected.Id}"))
        {
            var preview = selected.Original;
            if (practice.UseCustom)
            {
                preview = PracticePositionLogic.ApplyStep(preview, selected.OriginalFingerprint,
                    practice.GetStepOverlay(selected.Id)).Positions;
                preview = PracticePositionLogic.ApplyIntervals(preview, selected.Step,
                    selected.OriginalFingerprint, practice.IntervalOverlays).Positions;
            }
            PreviewStep(world, preview);
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("暫停時間軸，只定位存活 AI；不移動玩家或機制物件。可從操作列繼續。");
        ImGui.SameLine();
        if (ImGui.Button($"以此步驟填入區間錨點##{selected.Id}"))
        {
            intervalStart = selected.Time;
            intervalEnd = selected.Time;
            intervalSlot = 0;
            intervalStartPosition = selected.Effective[0] ?? Vector2.Zero;
            intervalEndPosition = intervalStartPosition;
            selectedIntervalId = null;
            loadedIntervalId = null;
            operationStatus = "已填入區間欄位；請調整角色與時間後建立。";
        }
    }

    private void SaveStepSlot(PracticePositions practice, string stepId, int slot, Vector2 position)
    {
        var ok = practice.TrySetStepSlot(stepId, slot, position);
        operationStatus = ok ? "已保存：步驟自訂目標" : $"未保存：{practice.LastError ?? "未知錯誤"}";
        draftStepId = null;
    }

    private void SaveStepReset(PracticePositions practice, string stepId, int slot)
    {
        var ok = practice.TryClearStepSlot(stepId, slot);
        operationStatus = ok ? "已保存：該角色恢復原始目標" : $"未保存：{practice.LastError ?? "未知錯誤"}";
        draftStepId = null;
    }

    private void DrawIntervalEditor(PracticePositions practice, PracticeObservedMove? selected)
    {
        ImGui.Separator();
        ImGui.TextUnformatted("錄製軌跡／連續移動：時間區間錨點覆寫");
        ImGui.TextDisabled("區間含頭尾，只覆寫範圍內既有移動事件；不新增原始路線沒有的時點。原始分支指紋逐步檢查。");

        var intervals = practice.IntervalOverlays;
        if (selectedIntervalId is not null)
        {
            var existing = intervals.FirstOrDefault(interval =>
                string.Equals(interval.Id, selectedIntervalId, StringComparison.Ordinal));
            if (existing is null)
            {
                selectedIntervalId = null;
                loadedIntervalId = null;
            }
            else if (!string.Equals(loadedIntervalId, selectedIntervalId, StringComparison.Ordinal))
            {
                LoadInterval(existing);
                loadedIntervalId = selectedIntervalId;
            }
        }
        else
        {
            loadedIntervalId = null;
        }

        if (ImGui.BeginTable("practice-interval-list", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.SizingStretchProp,
                new Vector2(0, 100)))
        {
            ImGui.TableSetupColumn("區間", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("時間", ImGuiTableColumnFlags.WidthFixed, 125f);
            ImGui.TableSetupColumn("角色", ImGuiTableColumnFlags.WidthFixed, 55f);
            ImGui.TableSetupColumn("錨點", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var interval in intervals)
            {
                ImGui.PushID($"practice-interval:{interval.Id}");
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable(interval.Id, string.Equals(interval.Id, selectedIntervalId)))
                {
                    selectedIntervalId = interval.Id;
                    LoadInterval(interval);
                    loadedIntervalId = interval.Id;
                    operationStatus = string.Empty;
                }
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{interval.StartTime:F2}–{interval.EndTime:F2}s");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(PracticeRoleLabels[Math.Clamp(interval.Slot, 0, PracticeRoleLabels.Length - 1)]);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Format(interval.StartPosition)
                    + (interval.EndPosition is { } end ? $" → {Format(end)}" : "（固定）"));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        ImGui.SetNextItemWidth(100);
        ImGui.InputFloat("開始秒##interval", ref intervalStart, 0.1f, 1f, "%.2f");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        ImGui.InputFloat("結束秒##interval", ref intervalEnd, 0.1f, 1f, "%.2f");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.Combo("角色##interval", ref intervalSlot, PracticeRoleLabels, PracticeRoleLabels.Length);
        intervalSlot = Math.Clamp(intervalSlot, 0, PracticeRoleLabels.Length - 1);
        ImGui.SetNextItemWidth(220);
        ImGui.DragFloat2("起點錨點##interval", ref intervalStartPosition, 0.1f, -30f, 30f, "%.2f");
        ImGui.SameLine();
        ImGui.Checkbox("線性終點##interval", ref intervalHasEnd);
        if (intervalHasEnd)
        {
            ImGui.SetNextItemWidth(220);
            ImGui.DragFloat2("終點錨點##interval", ref intervalEndPosition, 0.1f, -30f, 30f, "%.2f");
        }

        var update = selectedIntervalId is not null;
        if (ImGui.Button(update ? "保存區間修改" : "建立區間覆寫"))
        {
            var end = intervalHasEnd ? intervalEndPosition : (Vector2?)null;
            var ok = update
                ? practice.TryUpdateInterval(selectedIntervalId!, intervalStart, intervalEnd, intervalSlot,
                    intervalStartPosition, end)
                : practice.TryAddInterval(intervalStart, intervalEnd, intervalSlot, intervalStartPosition, end);
            operationStatus = ok ? "已保存：軌跡時間區間覆寫" : $"未保存：{practice.LastError ?? "未知錯誤"}";
            if (ok && !update)
            {
                selectedIntervalId = practice.IntervalOverlays.LastOrDefault()?.Id;
                loadedIntervalId = null;
            }
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(!update);
        if (ImGui.Button("刪除區間"))
        {
            var ok = practice.TryDeleteInterval(selectedIntervalId!);
            operationStatus = ok ? "已保存：刪除軌跡區間" : $"未保存：{practice.LastError ?? "未知錯誤"}";
            if (ok)
            {
                selectedIntervalId = null;
                loadedIntervalId = null;
            }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("恢復原始路線"))
        {
            var ok = practice.TryRestoreOriginal();
            operationStatus = ok ? "已保存：清除本場全部自訂路線" : $"未保存：{practice.LastError ?? "未知錯誤"}";
            if (ok)
            {
                selectedIntervalId = null;
                loadedIntervalId = null;
            }
        }
    }

    private void LoadInterval(PracticeIntervalOverlay interval)
    {
        intervalStart = interval.StartTime;
        intervalEnd = interval.EndTime;
        intervalSlot = Math.Clamp(interval.Slot, 0, PracticeRoleLabels.Length - 1);
        intervalStartPosition = interval.StartPosition;
        intervalEndPosition = interval.EndPosition ?? interval.StartPosition;
        intervalHasEnd = interval.EndPosition is not null;
    }

    private static Vector2? PlayerLocal(SimWorld? world)
    {
        if (world?.Map.IsInInstance == true && Plugin.ObjectTable.LocalPlayer is { } player)
        {
            var local = world.Coordinates.ToLocal(player.Position);
            return new Vector2(local.X, local.Z);
        }
        return null;
    }

    private static string Format(Vector2? position)
        => position is { } value ? $"({value.X:F2}, {value.Y:F2})" : "—";

    private static string Format(Vector2 position) => $"({position.X:F2}, {position.Y:F2})";

    private static void PreviewStep(SimWorld? world, IReadOnlyList<Vector2?> positions)
    {
        if (world?.Map.IsInInstance != true || !world.PracticePositions.IsActive ||
            Core.Game.MultiplayerContext.InRun || Plugin.GameInstance is not { } game) return;
        game.Paused = true;
        for (var i = 0; i < positions.Count && i < PracticePositionLogic.PartySlotCount; i++)
        {
            if (positions[i] is not { } target) continue;
            var member = world.Party.Get(i);
            if (member is null or SimPlayer || !member.IsAlive()) continue;
            member.SetPosition(new Core.Game.Placement(new Vector3(target.X, 0f, target.Y), member.Rotation));
        }
    }

}
