using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using AnoMech.Core.Map;
using AnoMech.Core;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Multiplayer;
using AnoMech.Scenarios;
using static AnoMech.Core.Game.Game;

namespace AnoMech.Windows;

public unsafe class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool _leftPanelOpen = true;
    internal IScenario? SelectedScenario => _selectedScenario;
    private IScenario? _selectedScenario;

    internal PartyRole? SelectedRoleOverride => _roleOverride;
    private PartyRole? _roleOverride;

    // Index into the selected scenario's AiStrats; reset to the first strat whenever the
    // selected scenario changes. Passed to RunScenario as selectedAi on a (non-solo) Start.
    // -1 when a grouped scenario's selected region has no strats (Start is then gated off).
    internal int SelectedStrat => _selectedStrat;
    private int _selectedStrat;

    // Index into the selected scenario's WaymarkPresets; reset to the first preset when the
    // selected scenario changes. Passed to RunScenario as selectedWaymark on Start. Ignored
    // by scenarios that declare no presets.
    internal int SelectedWaymark => _selectedWaymark;
    private int _selectedWaymark;

    // The region/group label currently selected in the strat picker, for scenarios that
    // declare StratGroups. Null until a grouped scenario is drawn (then it snaps to the
    // first group); stays null for ungrouped scenarios. Filters AiStrats under the buttons.
    private string? _selectedStratGroup;

    // Remembers the last region the user picked per grouped scenario. On a scenario switch
    // _selectedStratGroup is restored from here instead of being reset, so coming back to a
    // scenario keeps its previously selected region rather than snapping to the first.
    private readonly Dictionary<IScenario, string> _stratGroupMemory = new();

    // Index 0 = Auto (null override); indices 1..8 map to (PartyRole)(idx - 1).
    // Labels are the canonical raid role abbreviations: MT/OT tanks, H1/H2 healers
    // (H1 = regen), M1/M2 melee DPS, R1/R2 ranged DPS (R1 = phys).
    private static readonly string[] RoleLabels =
        ["自動", "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2"];

#if DEBUG
    private readonly DebugMenu debugMenu;
#endif

    // <Version> from AnoMech.csproj flows into the assembly version; surface it in the
    // title bar. Use a ### id so the window identity stays "MainWindow" across versions.
    private static string TitleWithVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        var version = v is null ? "" : $" v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        return $"AnoMech{version}###MainWindow";
    }

    public MainWindow(Plugin plugin)
        : base(TitleWithVersion())
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(220, 80),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Flags |= ImGuiWindowFlags.AlwaysAutoResize;

        this.plugin = plugin;
        IsOpen = false;

        // Small gear in the title bar opens the settings window (same toggle as /anomech config).
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            IconOffset = new Vector2(2f, 1f),
            Click = _ => plugin.ToggleConfigUi(),
            ShowTooltip = () => ImGui.SetTooltip("設定"),
        });
#if DEBUG
        debugMenu = new DebugMenu(plugin);
#endif
    }

    public void Dispose() { }

    private bool _wasInInstance;

    // While the fake-zone instance is loaded, pin the window open and uncollapsible
    // so the user can always reach Reset/Leave/God-mode without re-opening it.
    public override void PreOpenCheck()
    {
        var inInstance = plugin.Game.World.Map.IsInInstance;
        if (inInstance)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            Flags |= ImGuiWindowFlags.NoCollapse;
            if (!_wasInInstance)
            {
                Collapsed = false;
                CollapsedCondition = ImGuiCond.Always;
            }
        }
        else
        {
            ShowCloseButton = true;
            RespectCloseHotkey = true;
            Flags &= ~ImGuiWindowFlags.NoCollapse;
            if (_wasInInstance)
                CollapsedCondition = ImGuiCond.FirstUseEver;
        }
        _wasInInstance = inInstance;
    }

    public override void Draw()
    {
        var leftWidth = _leftPanelOpen ? ScenarioPanelWidth() : 30f;

        if (ImGui.BeginTable("##layout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthFixed, leftWidth);
            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawScenariosPanel();
            ImGui.TableSetColumnIndex(1);
            DrawMainContent();
            ImGui.EndTable();
        }
    }

    // Size the left panel to the widest scenario label so names never clip as scenarios are added.
    private float ScenarioPanelWidth()
    {
        var style = ImGui.GetStyle();
        var widest = 0f;
        foreach (var zone in plugin.Game.Zones)
        {
            widest = Math.Max(widest, ImGui.CalcTextSize(zone.Name).X);
            foreach (var phase in plugin.Game.PhasesOf(zone))
                foreach (var scenario in plugin.Game.ScenariosOf(phase))
                    widest = Math.Max(widest, ImGui.CalcTextSize(DisplayName(scenario)).X);
        }
        var measured = widest + style.FramePadding.X * 2 + style.CellPadding.X * 2;
        return Math.Max(180f, measured);
    }

    private void DrawScenariosPanel()
    {
        if (_leftPanelOpen)
        {
            ImGui.TextUnformatted("場景");
            ImGui.SameLine();
            if (ImGui.SmallButton("<##collapse")) _leftPanelOpen = false;
            ImGui.Separator();

            foreach (var zone in plugin.Game.Zones)
            {
                if (!ImGui.CollapsingHeader(zone.Name, ImGuiTreeNodeFlags.DefaultOpen)) continue;
                ImGui.Indent();
                foreach (var phase in plugin.Game.PhasesOf(zone))
                    foreach (var scenario in plugin.Game.ScenariosOf(phase))
                    {
                        var selected = _selectedScenario == scenario;
                        if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                        ImGui.PushID(scenario.Name);
                        if (ImGui.Button(DisplayName(scenario), new Vector2(-1, 0)))
                            SelectScenario(scenario);
                        ImGui.PopID();
                        if (selected) ImGui.PopStyleColor();
                    }
                ImGui.Unindent();
            }
        }
        else
        {
            if (ImGui.Button(">##expand")) _leftPanelOpen = true;
        }
    }

    // Select a scenario and reset its per-scenario UI state (strat, waymark, remembered region).
    private void SelectScenario(IScenario scenario)
    {
        _selectedScenario = scenario;
        _selectedStrat = 0;
        _selectedWaymark = 0;
        // Restore the last region picked for this scenario; null self-heals to its first region when drawn.
        _selectedStratGroup = _stratGroupMemory.GetValueOrDefault(scenario);
    }

    // Distinct, ordered region labels from the strats' IScenarioAi.Group; empty = ungrouped.
    private static IReadOnlyList<string> StratGroups(IScenario scenario)
    {
        var groups = new List<string>();
        foreach (var ai in scenario.AiStrats)
            if (ai.Group is { } g && !groups.Contains(g)) groups.Add(g);
        return groups;
    }

    private void DrawMainContent()
    {
        if (_selectedScenario == null)
        {
            ImGui.TextDisabled("請選擇場景");
            return;
        }

        var game = plugin.Game;

        ImGui.TextUnformatted(FullName(_selectedScenario));
        ImGui.Separator();
        DrawLocationHint();

        var multiplayer = Plugin.Multiplayer;
        if (multiplayer.IsClientConnected || multiplayer.HostControlsRun)
            ImGui.TextDisabled("職能由「多人同步」視窗（/ano net）分配。");
        else
            DrawRoleSelector();
        DrawStratSelector();
        DrawWaymarkSelector();

        if (multiplayer.IsClientConnected)
            DrawMultiplayerClientControls(game, multiplayer);
        else if (multiplayer.HostControlsRun)
            DrawMultiplayerHostControls(game, multiplayer);
        else
            DrawLocalControls(game);
        DrawAfterControls(game);
    }

    private void DrawLocalControls(AnoMech.Core.Game.Game game)
    {
        if (_selectedScenario is null) return;
        var inInn = ZoneSession.CanStartHere();
        var busy = ZoneSession.IsPlayerBusy();
        var envReady = inInn && !busy;
        var hasStrat = HasStartableStrat();
        var canStart = envReady && hasStrat;
        ImGui.BeginDisabled(!canStart);
        if (ImGui.Button("開始##start")) game.RunScenario(_selectedScenario, _roleOverride, _selectedStrat, _selectedWaymark);
        ImGui.EndDisabled();
        if (!canStart && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(!inInn
                ? "場景只能在旅館、個人／公會住宅或個人房間內開始。"
                : busy
                    ? "忙碌中無法開始（過場動畫、NPC 事件、製作、交易、區域切換等）。"
                    : "這個地區目前還沒有可用的戰術。");
        }
        ImGui.SameLine();
        if (ImGui.Button("重置##reset")) game.Reset();
        if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (ImGui.Button("離開##leave")) game.Leave();
        }

        if (_selectedScenario.SupportsSolo)
        {
            ImGui.BeginDisabled(!envReady);
            if (ImGui.Button("單人開始##startsolo")) game.RunScenario(_selectedScenario, _roleOverride, selectedAi: null, _selectedWaymark);
            ImGui.EndDisabled();
            if (!envReady && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip(!inInn
                    ? "場景只能在旅館、個人／公會住宅或個人房間內開始。"
                    : "忙碌中無法開始（過場動畫、NPC 事件、製作、交易、區域切換等）。");
            }
        }

        var god = game.GodMode;
        if (ImGui.Checkbox("無敵模式##godmode", ref god)) game.GodMode = god;
    }

    private void DrawMultiplayerHostControls(AnoMech.Core.Game.Game game, MultiplayerSession multiplayer)
    {
        if (_selectedScenario is not { } scenario) return;
        var hasStrat = HasStartableStrat();
        var canStart = multiplayer.CanHostStart(scenario, out var reason) && hasStrat;
        if (!hasStrat) reason = "這個地區目前還沒有可用的戰術。";
        ImGui.BeginDisabled(!canStart);
        if (ImGui.Button("多人開始##mpstart")) multiplayer.HostStartRun(scenario, _selectedStrat, _selectedWaymark);
        ImGui.EndDisabled();
        if (!canStart && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(reason);
        ImGui.SameLine();
        if (ImGui.Button("重置##reset"))
        {
            multiplayer.HostStopRun();
            game.Reset();
        }
        if (game.World.Map.IsInInstance)
        {
            ImGui.SameLine();
            if (ImGui.Button("離開##leave"))
            {
                multiplayer.HostStopRun();
                game.Leave();
            }
        }
        ImGui.BeginDisabled(multiplayer.RunActive);
        var god = game.GodMode;
        if (ImGui.Checkbox("無敵模式##godmode", ref god)) game.GodMode = god;
        ImGui.EndDisabled();
        ImGui.TextDisabled("多人房間：場景設定以房主為準，與個人有關的點名設定會改為隨機。");
    }

    private void DrawMultiplayerClientControls(AnoMech.Core.Game.Game game, MultiplayerSession multiplayer)
    {
        ImGui.TextWrapped(multiplayer.ClientRunActive
            ? "多人場景進行中，由房主控制開始與重置。"
            : "已加入多人房間：由房主選擇場景並開始。");
        if (!multiplayer.ClientRunActive && game.World.Map.IsInInstance && ImGui.Button("離開##leave"))
            game.Leave();
    }

    private void DrawAfterControls(AnoMech.Core.Game.Game game)
    {
        if (_selectedScenario is null) return;
#if DEBUG
        debugMenu.DrawSpeedControl();
#endif

        if (game.Paused) ImGui.TextDisabled("（場景已暫停 —— 按「重置」清除）");

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("場景設定##scenariocfg", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent();
            _selectedScenario.DrawSettings();
            ImGui.Unindent();
        }

#if DEBUG
        ImGui.Spacing();
        if (ImGui.CollapsingHeader("偵錯##debug"))
        {
            debugMenu.DrawDebugContent();
        }
#endif
    }

    // Drawn below the strat picker for scenarios that declare WaymarkPresets. _selectedWaymark
    // is the index passed to RunScenario on Start; changing it while a scenario is loaded
    // re-places the markers immediately (same live-feedback loop as the position readout).
    private void DrawWaymarkSelector()
    {
        if (_selectedScenario is null) return;
        var presets = _selectedScenario.Phase.Zone.WaymarkPresets;
        if (presets.Count == 0) return;
        if (_selectedWaymark < 0 || _selectedWaymark >= presets.Count) _selectedWaymark = 0;

        var labels = new string[presets.Count];
        for (var i = 0; i < presets.Count; i++) labels[i] = presets[i].Name;

        ImGui.TextUnformatted("場地標記：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        if (ImGui.Combo("##waymarks", ref _selectedWaymark, labels, labels.Length)
            && plugin.Game.World.Map.IsInInstance)
            plugin.Game.World.PlaceWaymarks(presets[_selectedWaymark].Markers);
    }

    private void DrawRoleSelector()
    {
        var idx = _roleOverride is { } role ? (int)role + 1 : 0;
        ImGui.TextUnformatted("選擇你的職能：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("##role", ref idx, RoleLabels, RoleLabels.Length))
            _roleOverride = idx == 0 ? null : (PartyRole)(idx - 1);
    }

    // Only meaningful when a scenario offers more than one strat; hidden otherwise.
    // When the scenario declares StratGroups, a region-button row is drawn above the
    // dropdown and the dropdown is filtered to the selected region.
    private void DrawStratSelector()
    {
        if (_selectedScenario is null) return;
        var strats = _selectedScenario.AiStrats;
        var groups = StratGroups(_selectedScenario);
        if (groups.Count > 0)
        {
            DrawGroupedStratSelector(strats, groups);
            return;
        }

        if (strats.Count <= 1) return;
        _selectedStrat = Math.Clamp(_selectedStrat, 0, strats.Count - 1);
        var labels = new string[strats.Count];
        for (var i = 0; i < strats.Count; i++) labels[i] = strats[i].Name;
        ImGui.TextUnformatted("選擇戰術：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280);
        ImGui.Combo("##strat", ref _selectedStrat, labels, labels.Length);
    }

    // Region buttons + a region-filtered strat dropdown. _selectedStrat stays an
    // absolute index into AiStrats (what RunScenario consumes); it is reconciled here
    // each frame to the selected region, or set to -1 when that region has no strats.
    private void DrawGroupedStratSelector(IReadOnlyList<IScenarioAi> strats, IReadOnlyList<string> groups)
    {
        if (!GroupsContain(groups, _selectedStratGroup))
            _selectedStratGroup = groups[0];

        ImGui.TextUnformatted("地區：");
        for (var i = 0; i < groups.Count; i++)
        {
            ImGui.SameLine();
            var group = groups[i];
            var selected = _selectedStratGroup == group;
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            ImGui.PushID($"region{i}");
            if (ImGui.Button(group))
            {
                _selectedStratGroup = group;
                _stratGroupMemory[_selectedScenario!] = group; // remember across scenario switches
            }
            ImGui.PopID();
            if (selected) ImGui.PopStyleColor();
        }

        var filtered = new List<int>();
        for (var i = 0; i < strats.Count; i++)
            if (strats[i].Group == _selectedStratGroup) filtered.Add(i);

        ImGui.TextUnformatted("選擇戰術：");
        ImGui.SameLine();
        if (filtered.Count == 0)
        {
            _selectedStrat = -1;
            ImGui.TextDisabled("（這個地區目前還沒有戰術）");
            return;
        }

        if (!filtered.Contains(_selectedStrat)) _selectedStrat = filtered[0];
        var localIdx = filtered.IndexOf(_selectedStrat);
        var labels = new string[filtered.Count];
        for (var i = 0; i < filtered.Count; i++) labels[i] = strats[filtered[i]].Name;
        ImGui.SetNextItemWidth(280);
        if (ImGui.Combo("##strat", ref localIdx, labels, labels.Length))
            _selectedStrat = filtered[localIdx];
    }

    // True when Start may run a strat: ungrouped scenarios are always fine; grouped
    // scenarios require the current selection to be a real strat in the active region.
    private bool HasStartableStrat()
    {
        if (_selectedScenario is not { } scenario) return false;
        if (StratGroups(scenario).Count == 0) return true;
        var strats = scenario.AiStrats;
        return _selectedStrat >= 0 && _selectedStrat < strats.Count
            && strats[_selectedStrat].Group == _selectedStratGroup;
    }

    private static bool GroupsContain(IReadOnlyList<string> groups, string? group)
    {
        if (group is null) return false;
        for (var i = 0; i < groups.Count; i++)
            if (groups[i] == group) return true;
        return false;
    }

    private void DrawLocationHint()
    {
        if (ZoneSession.CanStartHere()) return;
        ImGui.TextDisabled("請進入旅館或住宅室內開始");
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("支援旅館、個人／公會住宅與個人房間。不支援屋外住宅區、工房或公寓大廳。");
    }
}
