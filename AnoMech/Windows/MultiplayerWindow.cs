using System;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Multiplayer;
using AnoMech.Core.Net;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

internal sealed class MultiplayerWindow : Window, IDisposable
{
    private static readonly Vector4 Good = new(0.45f, 0.9f, 0.45f, 1f);
    private static readonly Vector4 Bad = new(1f, 0.45f, 0.45f, 1f);
    private static readonly string[] RoleLabels = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4", "未分配"];
    private const int UnassignedIndex = 8;

    private readonly MultiplayerSession session;
    private int hostMode = (int)HostMode.Internet;
    private string inviteInput = "";
    private bool directConnection;

    public MultiplayerWindow(MultiplayerSession session)
        : base("多人同步###AnoMechConnectionTest")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.session = session;
    }

    public override void OnOpen() => directConnection = HostReadiness.PublicAddressOnThisPc();

    public void Dispose() { }

    public override void Draw()
    {
        if (session.RunStatus.Length > 0) ImGui.TextColored(Good, session.RunStatus);
        DrawIdentity();
        if (ImGui.CollapsingHeader("建立房間##nethost", ImGuiTreeNodeFlags.DefaultOpen)) DrawHost();
        if (ImGui.CollapsingHeader("加入房間##netjoin", ImGuiTreeNodeFlags.DefaultOpen)) DrawJoin();
    }

    private void DrawIdentity()
    {
        var config = Plugin.Config;
        var name = config.MultiplayerName;
        if (name.Length == 0 && Plugin.ObjectTable.LocalPlayer is { } local)
        {
            name = local.Name.TextValue;
            config.MultiplayerName = name;
            config.Save();
        }
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("顯示名稱##mpname", ref name, 32))
        {
            config.MultiplayerName = name;
            config.Save();
        }
        var share = config.MultiplayerShareAppearance;
        if (ImGui.Checkbox("同步真人外觀##mpappearance", ref share))
        {
            config.MultiplayerShareAppearance = share;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("開啟後，房內其他人的畫面上你的角色會穿你本人的臉、裝備與武器。\n只送原版外觀（Penumbra／Glamourer 的改造不包含），下次連線或開始場景時生效。\n關閉則顯示預設隊友外觀。");
    }

    private static string RoleLabel(byte role) => role < 8 ? RoleLabels[role] : RoleLabels[UnassignedIndex];

    private void DrawHost()
    {
        var net = session.Net;
        var hosting = net.Host is not null;
        ImGui.BeginDisabled(hosting || session.IsClientConnected);
        ImGui.RadioButton("開房給朋友連線##modeinternet", ref hostMode, (int)HostMode.Internet);
        ImGui.SameLine();
        ImGui.RadioButton("只在這台電腦測試##modelocal", ref hostMode, (int)HostMode.LocalOnly);
        if (!hosting && ImGui.Button("建立房間##hoststart")) net.StartHosting((HostMode)hostMode);
        ImGui.EndDisabled();
        if (hosting && ImGui.Button("關閉房間##hoststop")) net.StopHosting();

        var needsRouter = net.Report?.NeedsRouterForwarding ?? !directConnection;
        if (hostMode == (int)HostMode.Internet && !needsRouter)
            ImGui.TextColored(Good, "你的電腦直接連上網路，開房不用設定路由器。");
        if (hostMode == (int)HostMode.Internet && needsRouter && ImGui.TreeNode("路由器要怎麼設定（插件說要設定才需要看）##forwardhelp"))
        {
            ImGui.TextWrapped(HostReadiness.ManualForwardingHelp(net.Report?.Port ?? NetProtocol.DefaultPort));
            ImGui.TreePop();
        }

        if (net.HostMessage.Length > 0)
        {
            if (net.Report is { } verdict) ImGui.TextColored(verdict.CanHost ? Good : Bad, net.HostMessage);
            else ImGui.TextWrapped(net.HostMessage);
        }

        if (net.Report is { } report)
        {
            foreach (var step in report.Steps)
            {
                var mark = step.Ok switch { true => "[通過]", false => "[失敗]", null => "[略過]" };
                if (step.Ok is { } passed) ImGui.TextColored(passed ? Good : Bad, $"{mark} {step.Name}：{step.Detail}");
                else ImGui.TextDisabled($"{mark} {step.Name}：{step.Detail}");
            }
            foreach (var problem in report.Problems)
                ImGui.TextWrapped($"・{problem}");
        }

        if (net.InviteText is { } invite)
        {
            var shown = invite;
            ImGui.InputText("邀請碼##invitecode", ref shown, 64, ImGuiInputTextFlags.ReadOnly);
            ImGui.SameLine();
            if (ImGui.Button("複製##copyinvite")) ImGui.SetClipboardText(invite);
            ImGui.TextWrapped(needsRouter
                ? "把邀請碼傳給要一起玩的朋友，朋友連得進來才算成功；連不進來請檢查路由器設定，以及 Windows 跳出的防火牆詢問有沒有按「允許」。邀請碼含有你的網路位址，請不要公開。"
                : "把邀請碼傳給要一起玩的朋友。你的電腦直接連上網路，不用設定路由器；連不進來請檢查 Windows 跳出的防火牆詢問有沒有按「允許」。邀請碼含有你的網路位址，請不要公開。");
        }

        if (net.Host is not { } host) return;
        ImGui.Separator();
        ImGui.TextUnformatted("房間成員：分配好職能、大家都準備後，在主視窗選場景按「多人開始」。");
        foreach (var entry in session.HostLobby)
        {
            ImGui.TextUnformatted(entry.Id == NetProtocol.HostPlayerId ? $"{entry.Name}（房主）" : $"#{entry.Id} {entry.Name}");
            ImGui.SameLine(200);
            var index = entry.Role < 8 ? entry.Role : UnassignedIndex;
            ImGui.SetNextItemWidth(90);
            ImGui.BeginDisabled(session.RunActive);
            if (ImGui.Combo($"##hostrole{entry.Id}", ref index, RoleLabels, RoleLabels.Length))
                session.HostSetRole(entry.Id, index == UnassignedIndex ? Wire.NoRole : (byte)index);
            ImGui.EndDisabled();
            if (entry.Id == NetProtocol.HostPlayerId) continue;
            ImGui.SameLine();
            var rtt = host.Players.FirstOrDefault(p => p.Id == entry.Id)?.RoundTripMs;
            var place = entry.CanStart ? "可以開始" : entry.Blocker;
            ImGui.TextColored(entry.Ready && entry.CanStart ? Good : Bad, $"{(entry.Ready ? "已準備" : "未準備")}　{place}　RTT {rtt} ms");
        }
        if (host.Players.Count == 0) ImGui.TextDisabled("尚無玩家連入");
    }

    private void DrawJoin()
    {
        var net = session.Net;
        var client = net.Client;
        var busy = client.State is ClientState.Connecting or ClientState.Connected;

        ImGui.TextDisabled("加入者用任何網路都可以（包含手機網路），不用設定路由器，貼上房主給的邀請碼即可。");
        ImGui.BeginDisabled(net.Host is not null);
        ImGui.InputText("邀請碼##joininput", ref inviteInput, 64);
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("連線##joinconnect")) net.Join(inviteInput);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!busy);
        if (ImGui.Button("中斷##joindisconnect")) client.Disconnect();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(busy || client.Invite is null);
        if (ImGui.Button("重新連線##joinreconnect")) client.Reconnect();
        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (net.JoinError is { } error) ImGui.TextColored(Bad, error);
        var state = client.State switch
        {
            ClientState.Idle => "尚未連線",
            ClientState.Connecting => "連線中",
            ClientState.Connected => "已連線",
            _ => "已斷線",
        };
        ImGui.TextWrapped($"狀態：{state}　{client.LastMessage}");
        if (client.State != ClientState.Connected) return;
        ImGui.TextWrapped($"玩家編號 #{client.PlayerId}　RTT {(client.LastRttMs < 0 ? "—" : $"{client.LastRttMs:0} ms")}　收到 Pong {client.PongsReceived} 次");

        ImGui.Separator();
        var ready = session.ClientReady;
        if (ImGui.Checkbox("準備好了##mpready", ref ready)) session.ClientReady = ready;
        ImGui.SameLine();
        ImGui.TextUnformatted("想要的職能：");
        ImGui.SameLine();
        var requested = session.ClientRequestedRole < 8 ? session.ClientRequestedRole : UnassignedIndex;
        ImGui.SetNextItemWidth(90);
        ImGui.BeginDisabled(session.ClientRunActive);
        if (ImGui.Combo("##clientrole", ref requested, RoleLabels, RoleLabels.Length))
            session.ClientRequestRole(requested == UnassignedIndex ? Wire.NoRole : (byte)requested);
        ImGui.EndDisabled();

        var delay = Plugin.Config.MultiplayerPlaybackDelay;
        ImGui.SetNextItemWidth(200);
        if (ImGui.SliderFloat("播放延遲（秒）##mpdelay", ref delay, 0.017f, 0.5f, "%.3f"))
        {
            Plugin.Config.MultiplayerPlaybackDelay = delay;
            Plugin.Config.Save();
        }
        ImGui.TextDisabled("最低 0.017 秒（一幀）。越低王的動作越即時；網路不穩、畫面會停頓時調高到 0.05～0.1。下一場開始時生效。");

        if (session.ClientLobby is not { } lobby) return;
        ImGui.TextUnformatted("房間成員：");
        foreach (var player in lobby.Players)
        {
            var status = player.Id == NetProtocol.HostPlayerId ? "房主" : player.Ready ? "已準備" : "未準備";
            ImGui.TextUnformatted($"#{player.Id} {player.Name}　{RoleLabel(player.Role)}　{status}{(player.CanStart ? "" : $"　{player.Blocker}")}");
        }
    }
}
