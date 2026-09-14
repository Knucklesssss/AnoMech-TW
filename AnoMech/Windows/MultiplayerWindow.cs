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
    private int hostMode = (int)HostMode.Upnp;
    private string inviteInput = "";

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

    public void Dispose() { }

    public override void Draw()
    {
        if (session.RunStatus.Length > 0) ImGui.TextColored(Good, session.RunStatus);
        if (ImGui.CollapsingHeader("建立房間##nethost", ImGuiTreeNodeFlags.DefaultOpen)) DrawHost();
        if (ImGui.CollapsingHeader("加入房間##netjoin", ImGuiTreeNodeFlags.DefaultOpen)) DrawJoin();
    }

    private static string RoleLabel(byte role) => role < 8 ? RoleLabels[role] : RoleLabels[UnassignedIndex];

    private void DrawHost()
    {
        var net = session.Net;
        var hosting = net.Host is not null;
        ImGui.BeginDisabled(hosting || session.IsClientConnected);
        ImGui.RadioButton("自動開埠（UPnP）##modeupnp", ref hostMode, (int)HostMode.Upnp);
        ImGui.SameLine();
        ImGui.RadioButton("我已手動設定埠轉發##modemanual", ref hostMode, (int)HostMode.ManualForwarding);
        ImGui.SameLine();
        ImGui.RadioButton("僅本機測試##modelocal", ref hostMode, (int)HostMode.LocalOnly);
        if (!hosting && ImGui.Button("建立房間##hoststart")) net.StartHosting((HostMode)hostMode);
        ImGui.EndDisabled();
        if (hosting && ImGui.Button("關閉房間##hoststop")) _ = net.StopHosting();

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
            if (!report.CanHost && report.Upnp is not null)
                ImGui.TextWrapped(HostReadiness.ManualForwardingHelp(report.Port));
        }

        if (net.InviteText is { } invite)
        {
            var shown = invite;
            ImGui.InputText("邀請碼##invitecode", ref shown, 64, ImGuiInputTextFlags.ReadOnly);
            ImGui.SameLine();
            if (ImGui.Button("複製##copyinvite")) ImGui.SetClipboardText(invite);
            ImGui.TextWrapped("通過代表本機條件沒問題；請朋友實際貼上邀請碼連線，連進來才算真正成功。若 Windows 防火牆詢問，請允許遊戲存取網路。");
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
            var place = entry.CanStart ? "在房間內" : "不在旅館／住宅";
            ImGui.TextColored(entry.Ready && entry.CanStart ? Good : Bad, $"{(entry.Ready ? "已準備" : "未準備")}　{place}　RTT {rtt} ms");
        }
        if (host.Players.Count == 0) ImGui.TextDisabled("尚無玩家連入");
    }

    private void DrawJoin()
    {
        var net = session.Net;
        var client = net.Client;
        var busy = client.State is ClientState.Connecting or ClientState.Connected;

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
        if (ImGui.SliderFloat("播放延遲（秒）##mpdelay", ref delay, 0.1f, 0.5f))
        {
            Plugin.Config.MultiplayerPlaybackDelay = delay;
            Plugin.Config.Save();
        }
        ImGui.TextDisabled("延遲越大越不容易卡頓，但王的動作會晚一點出現；下一場開始時生效。");

        if (session.ClientLobby is not { } lobby) return;
        ImGui.TextUnformatted("房間成員：");
        foreach (var player in lobby.Players)
        {
            var status = player.Id == NetProtocol.HostPlayerId ? "房主" : player.Ready ? "已準備" : "未準備";
            ImGui.TextUnformatted($"#{player.Id} {player.Name}　{RoleLabel(player.Role)}　{status}");
        }
    }
}
