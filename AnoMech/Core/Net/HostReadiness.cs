using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Core.Net;

// Ok: true = passed, false = failed, null = skipped or not applicable.
public sealed record ReadinessStep(string Name, bool? Ok, string Detail);

public sealed record HostReadinessReport(
    int Port,
    UpnpOutcome? Upnp,
    IPAddress? StunIp,
    bool CanHost,
    IPAddress? PublicIp,
    IReadOnlyList<ReadinessStep> Steps,
    IReadOnlyList<string> Problems)
{
    public string? Invite(uint roomId)
        => CanHost && PublicIp is not null ? InviteCode.Create(PublicIp, (ushort)Port, roomId).Encode() : null;
}

// Passing means every check this machine can make succeeded. Whether a friend outside can
// actually reach the port is only proven when they connect with the invite code.
public static class HostReadiness
{
    public static async Task<HostReadinessReport> RunAsync(int port, bool manualForwarding, Action<string> log, CancellationToken ct)
    {
        var stunTask = StunClient.QueryPublicIpAsync(TimeSpan.FromSeconds(3), ct);
        var upnp = manualForwarding ? null : await UpnpClient.MapUdpPortAsync(port, log, ct);
        return Evaluate(port, upnp, await stunTask);
    }

    public static HostReadinessReport LocalOnly(int port)
        => new(port, null, null, true, IPAddress.Loopback,
               [new ReadinessStep("本機測試模式", true, "邀請碼使用 127.0.0.1，只能在同一台電腦上連線。")], []);

    public static HostReadinessReport Evaluate(int port, UpnpOutcome? upnp, IPAddress? stunIp)
    {
        var steps = new List<ReadinessStep> { new("UDP 埠", true, $"已綁定 UDP {port}") };
        var problems = new List<string>();
        var stunKind = NetworkClassifier.Classify(stunIp);
        IPAddress? publicIp;

        if (upnp is null)
        {
            steps.Add(new ReadinessStep("UPnP", null, "已略過（手動埠轉發）"));
            steps.Add(new ReadinessStep("公網 IP（STUN 查詢）", stunKind == AddressKind.Public, stunIp?.ToString() ?? "無法取得"));
            publicIp = stunKind == AddressKind.Public ? stunIp : null;
            if (publicIp is null) problems.Add("無法取得公網 IP：STUN 查詢失敗，請確認這台電腦可以連上外部網路。");
            steps.Add(new ReadinessStep("CGNAT 判斷", null, "手動轉發模式無法向路由器確認；若是電信商共用 IP，轉發也不會生效。"));
        }
        else
        {
            steps.Add(new ReadinessStep("UPnP 路由器", upnp.GatewayFound, upnp.GatewayFound ? "找到支援 UPnP 的路由器" : upnp.Error ?? "找不到"));
            if (!upnp.GatewayFound)
                problems.Add($"找不到支援 UPnP 的路由器：路由器可能沒有 UPnP 功能，或 UPnP 已被關閉（{upnp.Error}）。");

            steps.Add(new ReadinessStep("埠映射", upnp.GatewayFound ? upnp.Mapped : null, upnp.Mapped ? $"已映射 UDP {port}" : upnp.Error ?? "未執行"));
            if (upnp.GatewayFound && !upnp.Mapped)
                problems.Add($"路由器拒絕建立埠映射：{upnp.Error}。");

            var router = upnp.RouterExternalIp;
            var routerKind = NetworkClassifier.Classify(router);
            steps.Add(new ReadinessStep("公網 IP（路由器回報）", router is null ? null : routerKind == AddressKind.Public, router?.ToString() ?? "無法取得"));
            steps.Add(new ReadinessStep("公網 IP（STUN 查詢）", stunIp is null ? null : stunKind == AddressKind.Public, stunIp?.ToString() ?? "無法取得"));

            var nat = routerKind switch
            {
                AddressKind.CarrierGradeNat => $"路由器的對外 IP {router} 是電信商共用 IP（CGNAT），外部無法連入。",
                AddressKind.Private => $"路由器的對外 IP {router} 是內網位址，上游還有一層 NAT（例如數據機也在當路由器），外部無法連入。",
                _ when router is not null && stunIp is not null && !router.Equals(stunIp)
                    => $"路由器回報的 IP {router} 與實際對外 IP {stunIp} 不同，疑似 CGNAT 或多層 NAT，外部可能無法連入。",
                _ => null,
            };
            steps.Add(new ReadinessStep("CGNAT 判斷", router is null && stunIp is null ? null : nat is null, nat ?? "未發現共用 IP 或多層 NAT"));
            if (nat is not null) problems.Add(nat);

            publicIp = routerKind == AddressKind.Public ? router : stunKind == AddressKind.Public ? stunIp : null;
            if (publicIp is null && nat is null) problems.Add("無法取得公網 IP。");
        }

        var canHost = problems.Count == 0 && publicIp is not null && (upnp is null || upnp.Mapped);
        steps.Add(new ReadinessStep("邀請碼", canHost, canHost ? "已產生" : "無法開房，未產生邀請碼"));
        return new HostReadinessReport(port, upnp, stunIp, canHost, publicIp, steps, problems);
    }

    public static string ManualForwardingHelp(int port) =>
        "手動設定埠轉發（Port Forwarding）：\n" +
        "1. 用瀏覽器登入路由器管理頁面（常見位址 192.168.0.1 或 192.168.1.1）。\n" +
        "2. 找到「虛擬伺服器」「通訊埠轉送」或「Port Forwarding」。\n" +
        $"3. 新增規則：協定 UDP，外部埠 {port}，內部埠 {port}，內部 IP 填這台電腦的區網 IP。\n" +
        "4. 儲存後回到這裡，選「我已手動設定埠轉發」再按一次「建立房間」。\n" +
        "若是電信商共用 IP（CGNAT），手動轉發也不會生效，請改由其他玩家當房主。";
}
