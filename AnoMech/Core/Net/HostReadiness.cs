using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Core.Net;

// Ok: true = passed, false = failed, null = cannot be checked from this machine.
public sealed record ReadinessStep(string Name, bool? Ok, string Detail);

public sealed record HostReadinessReport(
    int Port,
    IPAddress? PublicIp,
    bool CanHost,
    IReadOnlyList<ReadinessStep> Steps,
    IReadOnlyList<string> Problems)
{
    public string? Invite(uint roomId)
        => CanHost && PublicIp is not null ? InviteCode.Create(PublicIp, (ushort)Port, roomId).Encode() : null;
}

// The player forwards the port on their router by hand. This machine can confirm the port is
// open locally and what address the internet sees; whether the router forwards it is only proven
// when a friend actually connects.
public static class HostReadiness
{
    public static async Task<HostReadinessReport> RunAsync(int port, CancellationToken ct)
        => Evaluate(port, await StunClient.QueryPublicIpAsync(TimeSpan.FromSeconds(3), ct));

    public static HostReadinessReport LocalOnly(int port)
        => new(port, IPAddress.Loopback, true,
               [new ReadinessStep("只在這台電腦測試", true, "這個邀請碼只能在同一台電腦上使用，朋友無法加入。")], []);

    public static HostReadinessReport Evaluate(int port, IPAddress? publicIp)
    {
        var problems = new List<string>();
        var kind = NetworkClassifier.Classify(publicIp);
        var steps = new List<ReadinessStep>
        {
            new("連線埠", true, $"已開啟 {port} 號埠（UDP）"),
            new("你的外部網路位址", kind == AddressKind.Public, publicIp?.ToString() ?? "查不到"),
        };

        if (kind == AddressKind.CarrierGradeNat)
            problems.Add("你的外部網路位址是電信商共用的位址（CGNAT），朋友連不進來，請改由其他人當房主。");
        else if (kind != AddressKind.Public)
            problems.Add("查不到你的外部網路位址，請確認這台電腦可以上網。");

        steps.Add(new ReadinessStep("路由器轉發", null, $"插件無法自動確認，請先在路由器把 UDP {port} 轉發到這台電腦。"));
        var canHost = problems.Count == 0;
        steps.Add(new ReadinessStep("邀請碼", canHost, canHost ? "已產生" : "沒有產生"));
        return new HostReadinessReport(port, canHost ? publicIp : null, canHost, steps, problems);
    }

    public static string ManualForwardingHelp(int port) =>
        "路由器設定步驟（第一次開房才需要做）：\n" +
        "1. 用瀏覽器打開路由器管理頁面（常見位址 192.168.0.1 或 192.168.1.1），帳號密碼通常貼在路由器背面。\n" +
        "2. 找到「虛擬伺服器」「通訊埠轉送」或「Port Forwarding」。\n" +
        $"3. 新增一條規則：協定選 UDP，外部埠和內部埠都填 {port}，內部 IP 填這台電腦的區網 IP（通常是 192.168.x.x）。\n" +
        "4. 儲存後回到這裡按「建立房間」。\n" +
        "不玩的時候可以把這條規則停用；如果家裡是數據機再接一台路由器，兩台可能都要設定。";
}
