using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Core.Net;

public enum HostMode
{
    Internet,
    LocalOnly,
}

// Shared by the in-game window and the console tool so both test exactly the same code path.
public sealed class ConnectionTestSession : IDisposable
{
    private readonly Action<string> log;
    private CancellationTokenSource? readinessCts;
    private Task<HostReadinessReport>? readinessTask;

    public ConnectionTestSession(Func<string> playerName, Action<string> log)
    {
        this.log = log;
        Client = new NetClient(playerName, log);
    }

    public NetClient Client { get; }
    public NetHost? Host { get; private set; }
    public HostReadinessReport? Report { get; private set; }
    public string? InviteText { get; private set; }
    public string HostMessage { get; private set; } = "";
    public string? JoinError { get; private set; }

    public void StartHosting(HostMode mode)
    {
        StopHosting();
        var roomId = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        var host = new NetHost(roomId, log);
        if (!host.Start())
        {
            host.Dispose();
            HostMessage = $"無法開啟 {NetProtocol.DefaultPort}～{NetProtocol.DefaultPort + NetProtocol.PortCandidates - 1} 號埠，可能被其他程式占用。";
            return;
        }
        Host = host;

        if (mode == HostMode.LocalOnly)
        {
            Report = HostReadiness.LocalOnly(host.Port);
            InviteText = Report.Invite(roomId);
            HostMessage = "只在這台電腦測試：可以用同一台電腦加入。";
            return;
        }

        readinessCts = new CancellationTokenSource();
        readinessTask = HostReadiness.RunAsync(host.Port, readinessCts.Token);
        HostMessage = "正在查詢你的外部網路位址…";
    }

    public void StopHosting()
    {
        readinessCts?.Cancel();
        readinessCts?.Dispose();
        readinessCts = null;
        readinessTask = null;
        Host?.Dispose();
        Host = null;
        Report = null;
        InviteText = null;
        HostMessage = "";
    }

    public void Join(string inviteText)
    {
        var error = InviteCode.TryDecode(inviteText, out var code);
        if (error != InviteError.None)
        {
            JoinError = InviteCode.Describe(error, code);
            return;
        }
        JoinError = null;
        Client.Connect(code);
    }

    public void Poll()
    {
        Host?.Poll();
        Client.Poll();
        if (readinessTask is not { IsCompleted: true } finished) return;

        readinessTask = null;
        if (finished.IsCompletedSuccessfully && Host is not null)
        {
            Report = finished.Result;
            InviteText = Report.Invite(Host.RoomId);
            HostMessage = Report.CanHost ? "可以開房：把邀請碼傳給朋友" : "無法開房";
        }
        else if (finished.IsFaulted)
        {
            HostMessage = $"無法開房：查詢網路時發生錯誤（{finished.Exception?.GetBaseException().Message}）";
        }
    }

    public void Dispose()
    {
        StopHosting();
        Client.Dispose();
    }
}
