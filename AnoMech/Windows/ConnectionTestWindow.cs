using System;
using System.Numerics;
using AnoMech.Core.Net;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AnoMech.Windows;

public sealed class ConnectionTestWindow : Window, IDisposable
{
    private static readonly Vector4 Good = new(0.45f, 0.9f, 0.45f, 1f);
    private static readonly Vector4 Bad = new(1f, 0.45f, 0.45f, 1f);

    private readonly ConnectionTestSession session;
    private int hostMode = (int)HostMode.Upnp;
    private string inviteInput = "";

    public ConnectionTestWindow()
        : base("多人連線測試###AnoMechConnectionTest")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        session = new ConnectionTestSession(
            () => Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? "玩家",
            message => Plugin.Log.Info(message));
    }

    // Called every frame from Plugin.OnFrameworkUpdate, even while the window is closed,
    // so an open room keeps answering its players.
    public void Poll() => session.Poll();

    public void Dispose() => session.Dispose();

    public override void Draw()
    {
        if (ImGui.CollapsingHeader("建立房間##nethost", ImGuiTreeNodeFlags.DefaultOpen)) DrawHost();
        if (ImGui.CollapsingHeader("加入房間##netjoin", ImGuiTreeNodeFlags.DefaultOpen)) DrawJoin();
    }

    private void DrawHost()
    {
        var hosting = session.Host is not null;
        ImGui.BeginDisabled(hosting);
        ImGui.RadioButton("自動開埠（UPnP）##modeupnp", ref hostMode, (int)HostMode.Upnp);
        ImGui.SameLine();
        ImGui.RadioButton("我已手動設定埠轉發##modemanual", ref hostMode, (int)HostMode.ManualForwarding);
        ImGui.SameLine();
        ImGui.RadioButton("僅本機測試##modelocal", ref hostMode, (int)HostMode.LocalOnly);
        ImGui.EndDisabled();

        if (!hosting)
        {
            if (ImGui.Button("建立房間##hoststart")) session.StartHosting((HostMode)hostMode);
        }
        else if (ImGui.Button("關閉房間##hoststop"))
        {
            _ = session.StopHosting();
        }

        if (session.HostMessage.Length > 0)
        {
            if (session.Report is { } verdict) ImGui.TextColored(verdict.CanHost ? Good : Bad, session.HostMessage);
            else ImGui.TextWrapped(session.HostMessage);
        }

        if (session.Report is not { } report) return;
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

        if (session.InviteText is { } invite)
        {
            var shown = invite;
            ImGui.InputText("邀請碼##invitecode", ref shown, 64, ImGuiInputTextFlags.ReadOnly);
            ImGui.SameLine();
            if (ImGui.Button("複製##copyinvite")) ImGui.SetClipboardText(invite);
            ImGui.TextWrapped("通過代表本機條件沒問題；請朋友實際貼上邀請碼連線，連進來才算真正成功。若 Windows 防火牆詢問，請允許遊戲存取網路。");
        }

        if (session.Host is not { } host) return;
        if (host.Players.Count == 0)
        {
            ImGui.TextDisabled("尚無玩家連入");
            return;
        }
        foreach (var player in host.Players)
            ImGui.TextWrapped($"#{player.Id} {player.Name}　RTT {player.RoundTripMs} ms　收到 Ping {player.PingsReceived} 次");
    }

    private void DrawJoin()
    {
        var client = session.Client;
        var busy = client.State is ClientState.Connecting or ClientState.Connected;

        ImGui.InputText("邀請碼##joininput", ref inviteInput, 64);
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("連線##joinconnect")) session.Join(inviteInput);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!busy);
        if (ImGui.Button("中斷##joindisconnect")) client.Disconnect();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(busy || client.Invite is null);
        if (ImGui.Button("重新連線##joinreconnect")) client.Reconnect();
        ImGui.EndDisabled();

        if (session.JoinError is { } error) ImGui.TextColored(Bad, error);
        var state = client.State switch
        {
            ClientState.Idle => "尚未連線",
            ClientState.Connecting => "連線中",
            ClientState.Connected => "已連線",
            _ => "已斷線",
        };
        ImGui.TextWrapped($"狀態：{state}　{client.LastMessage}");
        if (client.State == ClientState.Connected)
            ImGui.TextWrapped($"玩家編號 #{client.PlayerId}　RTT {(client.LastRttMs < 0 ? "—" : $"{client.LastRttMs:0} ms")}　收到 Pong {client.PongsReceived} 次");
    }
}
