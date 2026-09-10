using System;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.Native;
using AnoMech.Scenarios.Top.P3Monitors;
using AnoMech.Windows;
using AnoMech.Pointers;
using CSFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace AnoMech;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFlyTextGui FlyText { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    private const string CommandName = "/anomech";
    private const string CommandAlias = "/ano";

    public Configuration Configuration { get; init; }
    internal static Configuration Config { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("AnoMech");
    public Game Game { get; }
    // SimObjects reach engine singletons through these statics (mirrors the
    // Plugin.* PluginService pattern).
    internal static Game GameInstance { get; private set; } = null!;
    // Session-lifetime input hooks, owned here (not Game) so they're hooked once
    // per load rather than per scenario. SimPlayer is the sole writer of their
    // flags — it reconciles them from its own state each tick.
    internal static LocalPlayerInputHooks PlayerInputHooks { get; private set; } = null!;
    internal static LogManager LogManager { get; private set; } = null!;
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
#if DEBUG
    private DamageDebugWindow DamageDebugWindow { get; init; }
#endif

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config = Configuration;

        LogManager = new LogManager();
        if (Config.EnableEventLogging) LogManager.Open();

        PlayerInputHooks = new LocalPlayerInputHooks(GameInterop);
        Game = new Game();
        GameInstance = Game;
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
#if DEBUG
        DamageDebugWindow = new DamageDebugWindow(this);
        WindowSystem.AddWindow(DamageDebugWindow);
#endif

        if (Config.OpenSimMenuOnInn && ZoneSession.IsInInn())
            MainWindow.IsOpen = true;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "開啟 AnoMech。子指令：config、start、reset、leave、mark"
        });
        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "/anomech 的別名"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        DutyState.DutyStarted += OnDutyStarted;
        DutyState.DutyWiped += OnDutyWiped;
        DutyState.DutyCompleted += OnDutyCompleted;

        // Initialize Pointers
        CharacterManagerPointers.Initialize();
        EventFrameworkPointers.Initialize();
        EventObjectManagerPointers.Initialize();
        EventObjectPointers.Initialize();
        GameMainPointers.Initialize();
        ModelContainerPointers.Initialize();
        PacketDispatcherPointers.Initialize();
        RsfPointers.Initialize();
        StatusManagerPointers.Initialize();
        TimelineContainerPointers.Initialize();
        VfxContainerPointers.Initialize();
        VfxObjectPointers.Initialize();
        VfxDataPointers.Initialize();

        SignatureReport.Log(
            typeof(CharacterManagerPointers), typeof(EventFrameworkPointers),
            typeof(EventObjectManagerPointers), typeof(EventObjectPointers),
            typeof(GameMainPointers), typeof(ModelContainerPointers),
            typeof(PacketDispatcherPointers), typeof(RsfPointers),
            typeof(StatusManagerPointers), typeof(TimelineContainerPointers),
            typeof(VfxContainerPointers), typeof(VfxObjectPointers),
            typeof(VfxDataPointers));

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyWiped -= OnDutyWiped;
        DutyState.DutyCompleted -= OnDutyCompleted;

        WindowSystem.RemoveAllWindows();

        Game.Dispose();
        // After Game.Dispose so World.Dispose → SimPlayer.Despawn can still clear
        // the lock flags through the hooks before they're torn down.
        PlayerInputHooks.Dispose();
        LogManager.Dispose();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
#if DEBUG
        DamageDebugWindow.Dispose();
#endif

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandAlias);
    }

    private unsafe void OnFrameworkUpdate(IFramework framework)
    {
        Markings.TickPriming();
        // FrameDeltaTime, not framework.UpdateDelta: UpdateDelta is wall-clock
        // truncated to whole ms, so summing it drifts. FrameDeltaTime is the
        // full-precision delta the game ticks its own animations with.
        var fw = CSFramework.Instance();
        if (fw == null) return;
        Game.Tick(fw->FrameDeltaTime);
    }

    private void OnTerritoryChanged(ushort territory)
    {
        var row = DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory);
        var isInn = row?.TerritoryIntendedUse.RowId == 2; // TerritoryIntendedUse.Inn
        if (!isInn)
        {
            var name = row?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            LogManager.LogEnterInstance(territory, name);
        }

        if (!isInn)
        {
            MainWindow.IsOpen = false;
            return;
        }
        if (Config.OpenSimMenuOnInn)
            MainWindow.IsOpen = true;
    }

    private void OnDutyStarted(object? sender, ushort territory)
        => LogManager.LogCombatStart(territory);

    private void OnDutyWiped(object? sender, ushort territory)
        => LogManager.LogCombatEnd(territory, wipe: true);

    private void OnDutyCompleted(object? sender, ushort territory)
        => LogManager.LogCombatEnd(territory, wipe: false);

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.StartsWith("mark", StringComparison.OrdinalIgnoreCase))
        {
            OnMarkCommand(trimmed[4..].Trim());
            return;
        }

        switch (trimmed)
        {
            case "config":
                ConfigWindow.Toggle();
                break;
            case "start":
                StartSelectedScenario(solo: false);
                break;
            case "start solo":
                StartSelectedScenario(solo: true);
                break;
            case "reset":
                Game.Reset();
                break;
            case "leave":
                Game.Leave();
                break;
            default:
                MainWindow.Toggle();
                break;
        }
    }

    // The game's own /mk macro cannot resolve <mo> onto a doppel until that doppel has
    // carried a sign once, so a fresh party silently ignores the whole macro. This writes
    // MarkingController directly, which has no such warm-up. Deliberately gated to the P3
    // monitors scenario: every other phase marks fine through the party list or /mk <t>,
    // and a global marking command would be a second, divergent way to do the same thing.
    private void OnMarkCommand(string args)
    {
        if (Game.ActiveScenario is not TopP3MonitorsScenario)
        {
            PrintMarkMessage("/ano mark 只能在「探測式檢知波動砲」進行中使用。");
            return;
        }

        if (args.Length == 0)
        {
            PrintMarkMessage("用法：/ano mark attack1（標在滑鼠指向的對象，沒有就標在目標上）、/ano mark clear 清除全部。");
            return;
        }

        if (args.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Markings.ClearAll();
            PrintMarkMessage("已清除所有標記。");
            return;
        }

        if (!Enum.TryParse<Sign>(args, ignoreCase: true, out var sign) || !Enum.IsDefined(sign))
        {
            PrintMarkMessage($"認不得標記「{args}」。可用：attack1-8、bind1-3、ignore1-2、square、circle、cross、triangle。");
            return;
        }

        var target = TargetManager.MouseOverTarget ?? TargetManager.Target;
        if (target == null)
        {
            PrintMarkMessage("沒有指向或選取任何對象。");
            return;
        }

        Markings.Set(sign, target.GameObjectId);
        PrintMarkMessage($"{sign} → {target.Name}");
    }

    private static void PrintMarkMessage(string text)
        => ChatGui.Print(new XivChatEntry
        {
            Type = XivChatType.SystemMessage,
            Message = new SeStringBuilder().AddText($"[AnoMech] {text}").Build(),
        });

    private void StartSelectedScenario(bool solo)
    {
        if (!ZoneSession.IsInInn())
        {
            Log.Warning("Scenarios can only be started from an inn.");
            return;
        }
        if (ZoneSession.IsPlayerBusy())
        {
            Log.Warning("Cannot start a scenario while you are busy (cutscene, NPC event, crafting, etc.).");
            return;
        }
        if (MainWindow.SelectedScenario is not { } scenario)
            return;
        if (solo && !scenario.SupportsSolo)
        {
            Log.Warning($"{scenario.Name} does not support Solo mode.");
            return;
        }
        if (!solo && MainWindow.SelectedStrat < 0)
        {
            Log.Warning("No strat selected for the current region.");
            return;
        }
        Game.RunScenario(scenario, MainWindow.SelectedRoleOverride, solo ? null : MainWindow.SelectedStrat, MainWindow.SelectedWaymark);
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
