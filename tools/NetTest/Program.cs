using System.Text;
using AnoMech.Core.Net;

Console.OutputEncoding = Encoding.UTF8;

var name = OptionValue(args, "--name") ?? "NetTest";
using var session = new ConnectionTestSession(() => name, message => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}"));

return args.FirstOrDefault() switch
{
    "host" => await Host(session, args.Contains("--local") ? HostMode.LocalOnly : HostMode.Internet),
    "join" when args.Length >= 2 => await Join(session, args[1]),
    _ => Usage(),
};

static async Task<int> Host(ConnectionTestSession session, HostMode mode)
{
    session.StartHosting(mode);
    if (session.Host is not { } host)
    {
        Console.WriteLine(session.HostMessage);
        return 2;
    }
    host.PlayerJoined += p => Console.WriteLine($"玩家加入：#{p.Id} {p.Name}");
    host.PlayerLeft += (p, reason) => Console.WriteLine($"玩家離開：#{p.Id} {p.Name}（{reason}）");
    Console.WriteLine($"房間已在 UDP {host.Port} 啟動。按 Q 關閉房間。");

    var lastMessage = "";
    var reported = false;
    var nextStatus = 0.0;
    while (!QuitPressed())
    {
        session.Poll();
        if (session.HostMessage != lastMessage)
        {
            lastMessage = session.HostMessage;
            Console.WriteLine(lastMessage);
        }
        if (!reported && session.Report is { } report)
        {
            reported = true;
            PrintReport(report, session.InviteText);
        }
        if (host.Players.Count > 0 && NetProtocol.NowMs >= nextStatus)
        {
            nextStatus = NetProtocol.NowMs + 5000;
            foreach (var p in host.Players)
                Console.WriteLine($"  #{p.Id} {p.Name}  RTT {p.RoundTripMs} ms  收到 Ping {p.PingsReceived} 次");
        }
        await Task.Delay(15);
    }

    session.StopHosting();
    Console.WriteLine("房間已關閉。");
    return 0;
}

static async Task<int> Join(ConnectionTestSession session, string code)
{
    session.Join(code);
    if (session.JoinError is { } error)
    {
        Console.WriteLine(error);
        return 2;
    }
    Console.WriteLine("按 D 中斷、R 重新連線、Q 離開。");

    var client = session.Client;
    var lastMessage = "";
    var nextRtt = 0.0;
    while (true)
    {
        if (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Q) break;
            if (key == ConsoleKey.D) client.Disconnect();
            if (key == ConsoleKey.R) client.Reconnect();
        }
        session.Poll();
        if (client.LastMessage != lastMessage)
        {
            lastMessage = client.LastMessage;
            Console.WriteLine(lastMessage);
        }
        if (client.State == ClientState.Connected && client.LastRttMs >= 0 && NetProtocol.NowMs >= nextRtt)
        {
            nextRtt = NetProtocol.NowMs + 1000;
            Console.WriteLine($"  RTT {client.LastRttMs:0} ms（收到 Pong {client.PongsReceived} 次）");
        }
        await Task.Delay(15);
    }

    client.Disconnect();
    session.Poll();
    return 0;
}

static bool QuitPressed() => !Console.IsInputRedirected && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q;

static void PrintReport(HostReadinessReport report, string? invite)
{
    foreach (var step in report.Steps)
    {
        var mark = step.Ok switch { true => "通過", false => "失敗", null => "略過" };
        Console.WriteLine($"  [{mark}] {step.Name}：{step.Detail}");
    }
    Console.WriteLine(report.CanHost ? "結論：可以開房" : "結論：無法開房");
    foreach (var problem in report.Problems)
        Console.WriteLine($"  ・{problem}");
    if (!report.CanHost)
        Console.WriteLine(HostReadiness.ManualForwardingHelp(report.Port));
    if (invite is not null)
    {
        Console.WriteLine($"邀請碼：{invite}");
        Console.WriteLine("通過代表本機條件沒問題；請朋友實際貼上邀請碼連線，連進來才算真正成功。若 Windows 防火牆詢問，請允許存取。");
    }
}

static int Usage()
{
    Console.WriteLine("用法：");
    Console.WriteLine("  dotnet run --project tools/NetTest -- host [--local] [--name 名稱]");
    Console.WriteLine("  dotnet run --project tools/NetTest -- join <邀請碼> [--name 名稱]");
    return 1;
}

static string? OptionValue(string[] args, string option)
{
    var index = Array.IndexOf(args, option);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
