using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text;
using AnoMech.Core.Net;
using LiteNetLib.Utils;

internal static class NetChecks
{
    public static void Run()
    {
        Crc16MatchesCcittFalseVector();
        InviteCodeRoundTrips();
        InviteCodeRejectsBadInput();
        PacketHeaderRoundTripsAndRejectsGarbage();
        LoopbackConnectPingDisconnectReconnect();
        LoopbackRejectsStaleRoom();
        StunResponseParses();
        AddressesClassify();
        ReadinessVerdicts();
        Console.WriteLine("Net: invite code, packets, loopback connect/ping/reconnect, STUN and host readiness checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception($"NetChecks: {message}");
    }

    private static void Crc16MatchesCcittFalseVector()
    {
        Check(InviteCode.Crc16(Encoding.ASCII.GetBytes("123456789")) == 0x29B1, "CRC16-CCITT-FALSE of \"123456789\" is 0x29B1");
    }

    private static void InviteCodeRoundTrips()
    {
        var original = InviteCode.Create(IPAddress.Parse("203.0.113.7"), 42421, 0xDEADBEEF);
        var text = original.Encode();
        Check(text.StartsWith("AMP-") && text.Length == "AMP".Length + 6 * 5, $"encoded shape is AMP + six dash-separated groups of four: {text}");
        Check(InviteCode.TryDecode(text, out var decoded) == InviteError.None, "encoded code decodes");
        Check(decoded == original with { Address = decoded.Address } && decoded.Address.Equals(original.Address), "decoded fields match");
        var sloppy = " " + text.ToLowerInvariant().Replace("-", "").Replace("0", "o") + "\n";
        Check(InviteCode.TryDecode(sloppy, out var sloppyDecoded) == InviteError.None && sloppyDecoded.Port == 42421, "lower case, missing dashes, O for 0 and whitespace still decode");
    }

    private static void InviteCodeRejectsBadInput()
    {
        var text = InviteCode.Create(IPAddress.Parse("203.0.113.7"), 42421, 1).Encode();
        Check(InviteCode.TryDecode("", out _) == InviteError.Empty, "empty");
        Check(InviteCode.TryDecode("XYZ-0000", out _) == InviteError.BadPrefix, "wrong prefix");
        Check(InviteCode.TryDecode(text[..^2], out _) == InviteError.BadLength, "truncated");
        Check(InviteCode.TryDecode(text[..^1] + "U", out _) == InviteError.BadCharacter, "U is not in the alphabet");
        var flipped = text[..^1] + (text[^1] == '0' ? '1' : '0');
        Check(InviteCode.TryDecode(flipped, out _) == InviteError.ChecksumMismatch, "one changed character fails the checksum");
        var zeroPort = InviteCode.Create(IPAddress.Parse("203.0.113.7"), 0, 1).Encode();
        Check(InviteCode.TryDecode(zeroPort, out _) == InviteError.InvalidAddress, "port 0 is invalid");
        var future = new InviteCode((ushort)(NetProtocol.Version + 1), IPAddress.Parse("203.0.113.7"), 42421, 1).Encode();
        Check(InviteCode.TryDecode(future, out var futureDecoded) == InviteError.ProtocolMismatch, "other protocol version");
        Check(InviteCode.Describe(InviteError.ProtocolMismatch, futureDecoded).Contains($"v{NetProtocol.Version + 1}"), "mismatch message names the host version");
    }

    private static void PacketHeaderRoundTripsAndRejectsGarbage()
    {
        var writer = new NetDataWriter();
        new PacketHeader(MessageType.Ping, 7, 3, 1234.5).Write(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer);
        Check(PacketHeader.TryRead(reader, out var header) && header == new PacketHeader(MessageType.Ping, 7, 3, 1234.5), "header round trips");

        writer.Reset();
        writer.Put((byte)99);
        reader.SetSource(writer);
        Check(!PacketHeader.TryRead(reader, out _), "unknown message type is rejected");

        writer.Reset();
        new PacketHeader(MessageType.Ping, 1, NetProtocol.MaxPlayers, 0).Write(writer);
        reader.SetSource(writer);
        Check(!PacketHeader.TryRead(reader, out _), "sender id outside the party is rejected");

        writer.Reset();
        writer.Put((byte)MessageType.Ping);
        reader.SetSource(writer);
        Check(!PacketHeader.TryRead(reader, out _), "truncated header is rejected");

        writer.Reset();
        writer.Put(new byte[NetProtocol.MaxPacketBytes + 1]);
        reader.SetSource(writer);
        Check(!PacketHeader.TryRead(reader, out _), "oversized packet is rejected");

        writer.Reset();
        new HelloDto(NetProtocol.Version, 5, new string('名', 40)).Write(writer);
        reader.SetSource(writer);
        Check(HelloDto.TryRead(reader, out var hello) && hello.RoomId == 5 && hello.PlayerName.Length <= NetProtocol.MaxNameLength, "hello round trips and caps the name");
    }

    private static void PumpUntil(Func<bool> condition, NetHost host, NetClient client, string what)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            host.Poll();
            client.Poll();
            if (condition()) return;
            Thread.Sleep(10);
        }
        throw new Exception($"NetChecks: timed out waiting for {what}");
    }

    private static void LoopbackConnectPingDisconnectReconnect()
    {
        var logs = new List<string>();
        using var host = new NetHost(0xABCD1234, logs.Add);
        Check(host.Start(0, 1), "host binds an ephemeral UDP port");
        using var client = new NetClient(() => "Tester", logs.Add);

        client.Connect(InviteCode.Create(IPAddress.Loopback, (ushort)host.Port, host.RoomId));
        PumpUntil(() => client.State == ClientState.Connected, host, client, "welcome");
        Check(client.PlayerId == 1, "first joiner gets player id 1");
        Check(host.Players.Count == 1 && host.Players.First().Name == "Tester", "host lists the joiner by name");

        PumpUntil(() => client.LastRttMs >= 0 && host.Players.First().PingsReceived >= 1, host, client, "ping and pong");

        client.Disconnect();
        Check(client.State == ClientState.Disconnected, "client reports disconnected immediately");
        PumpUntil(() => host.Players.Count == 0, host, client, "host to notice the disconnect");

        client.Reconnect();
        PumpUntil(() => client.State == ClientState.Connected, host, client, "reconnect");
        Check(client.PlayerId == 1 && host.Players.Count == 1, "reconnect reuses the freed player id");
    }

    private static void LoopbackRejectsStaleRoom()
    {
        var logs = new List<string>();
        using var host = new NetHost(100, logs.Add);
        Check(host.Start(0, 1), "host binds an ephemeral UDP port");
        using var client = new NetClient(() => "Late", logs.Add);

        client.Connect(InviteCode.Create(IPAddress.Loopback, (ushort)host.Port, 101));
        PumpUntil(() => client.State == ClientState.Disconnected, host, client, "stale room rejection");
        Check(client.LastMessage.Contains("已關閉的房間"), $"rejection explains the stale room: {client.LastMessage}");
        Check(host.Players.Count == 0, "rejected client never becomes a player");
    }

    private static void StunResponseParses()
    {
        var request = StunClient.BuildBindingRequest(out var transactionId);
        Check(request.Length == 20 && request[0] == 0x00 && request[1] == 0x01, "binding request header");
        Check(BinaryPrimitives.ReadUInt32BigEndian(request.AsSpan(4)) == 0x2112A442, "request carries the magic cookie");

        var response = new byte[32];
        response[0] = 0x01; response[1] = 0x01;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2), 12);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(4), 0x2112A442);
        transactionId.CopyTo(response, 8);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(20), 0x0020);   // XOR-MAPPED-ADDRESS
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(22), 8);
        response[25] = 0x01;                                                   // IPv4
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(26), 42421 ^ 0x2112);
        BinaryPrimitives.WriteUInt32BigEndian(response.AsSpan(28), 0xCB007105u ^ 0x2112A442u); // 203.0.113.5

        Check(StunClient.TryParseBindingResponse(response, transactionId, out var ip) && ip!.Equals(IPAddress.Parse("203.0.113.5")), "XOR-MAPPED-ADDRESS decodes");
        var otherTransaction = (byte[])transactionId.Clone();
        otherTransaction[0] ^= 0xFF;
        Check(!StunClient.TryParseBindingResponse(response, otherTransaction, out _), "response for another transaction is ignored");
        Check(!StunClient.TryParseBindingResponse(response.AsSpan(0, 24), transactionId, out _), "truncated response is rejected");
    }

    private static void AddressesClassify()
    {
        Check(NetworkClassifier.Classify(IPAddress.Parse("203.0.113.5")) == AddressKind.Public, "public");
        Check(NetworkClassifier.Classify(IPAddress.Parse("192.168.1.10")) == AddressKind.Private, "192.168/16 private");
        Check(NetworkClassifier.Classify(IPAddress.Parse("172.20.0.1")) == AddressKind.Private, "172.16/12 private");
        Check(NetworkClassifier.Classify(IPAddress.Parse("10.1.2.3")) == AddressKind.Private, "10/8 private");
        Check(NetworkClassifier.Classify(IPAddress.Parse("100.64.0.1")) == AddressKind.CarrierGradeNat, "100.64/10 CGNAT");
        Check(NetworkClassifier.Classify(IPAddress.Parse("100.128.0.1")) == AddressKind.Public, "100.128 is outside CGNAT");
        Check(NetworkClassifier.Classify(IPAddress.Parse("0.0.0.0")) == AddressKind.Invalid, "0.0.0.0 invalid");
        Check(NetworkClassifier.Classify(null) == AddressKind.Invalid, "null invalid");
    }

    private static void ReadinessVerdicts()
    {
        var publicIp = IPAddress.Parse("203.0.113.7");

        var ok = HostReadiness.Evaluate(42420, publicIp, []);
        Check(ok.CanHost && ok.PublicIp!.Equals(publicIp) && ok.Problems.Count == 0, "a public address can host");
        Check(ok.Invite(9) is { } invite && InviteCode.TryDecode(invite, out var decoded) == InviteError.None && decoded.Port == 42420, "a passing report produces a valid invite");
        Check(ok.Steps.Any(s => s.Name == "路由器轉發" && s.Ok is null), "router forwarding is shown as not checkable");
        var direct = HostReadiness.Evaluate(42420, publicIp, [IPAddress.Parse("192.168.1.5"), publicIp]);
        Check(direct.CanHost && !direct.NeedsRouterForwarding && ok.NeedsRouterForwarding && direct.Steps.Any(s => s.Name == "路由器" && s.Ok == true), "a public address on this PC needs no router forwarding");

        var offline = HostReadiness.Evaluate(42420, null, []);
        Check(!offline.CanHost && offline.Problems.Any(p => p.Contains("查不到")) && offline.Invite(9) is null, "no public address cannot host and has no invite");

        var cgnat = HostReadiness.Evaluate(42420, IPAddress.Parse("100.72.1.2"), []);
        Check(!cgnat.CanHost && cgnat.Problems.Any(p => p.Contains("CGNAT")), "a carrier-grade NAT address cannot host");

        var local = HostReadiness.LocalOnly(42420);
        Check(local.CanHost && local.Invite(9) is { } localInvite && InviteCode.TryDecode(localInvite, out var localCode) == InviteError.None && localCode.Address.Equals(IPAddress.Loopback), "local-only report invites to 127.0.0.1");
        Check(HostReadiness.ManualForwardingHelp(42421).Contains("UDP") && HostReadiness.ManualForwardingHelp(42421).Contains("42421"), "router help names protocol and port");
    }
}
