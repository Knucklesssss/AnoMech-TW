using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AnoMech.Core.Net;

// Asks a public STUN server which address our packets arrive from. Only this one query is
// made; no game traffic ever goes through the server.
public static class StunClient
{
    private const uint MagicCookie = 0x2112A442;
    private const ushort XorMappedAddress = 0x0020;
    private const ushort MappedAddress = 0x0001;

    private static readonly (string Host, int Port)[] Servers =
    [
        ("stun.l.google.com", 19302),
        ("stun.cloudflare.com", 3478),
    ];

    public static async Task<IPAddress?> QueryPublicIpAsync(TimeSpan timeout, CancellationToken ct)
    {
        foreach (var (host, port) in Servers)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, ct);
                if (addresses.Length == 0) continue;
                using var udp = new UdpClient(AddressFamily.InterNetwork);
                var request = BuildBindingRequest(out var transactionId);
                await udp.SendAsync(request, new IPEndPoint(addresses[0], port), ct);

                using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
                window.CancelAfter(timeout);
                var result = await udp.ReceiveAsync(window.Token);
                if (TryParseBindingResponse(result.Buffer, transactionId, out var address)) return address;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            catch (SocketException) { }
        }
        return null;
    }

    internal static byte[] BuildBindingRequest(out byte[] transactionId)
    {
        transactionId = RandomNumberGenerator.GetBytes(12);
        var packet = new byte[20];
        packet[1] = 0x01; // Binding Request, message length 0
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), MagicCookie);
        transactionId.CopyTo(packet, 8);
        return packet;
    }

    internal static bool TryParseBindingResponse(ReadOnlySpan<byte> data, ReadOnlySpan<byte> transactionId, out IPAddress? address)
    {
        address = null;
        if (data.Length < 20 || data[0] != 0x01 || data[1] != 0x01) return false;
        if (BinaryPrimitives.ReadUInt32BigEndian(data[4..]) != MagicCookie || !data.Slice(8, 12).SequenceEqual(transactionId)) return false;
        var end = 20 + BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        if (end > data.Length) return false;

        IPAddress? mapped = null;
        var offset = 20;
        while (offset + 4 <= end)
        {
            var type = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);
            var value = offset + 4;
            if (value + length > end) return false;
            if (length >= 8 && data[value + 1] == 0x01)
            {
                if (type == XorMappedAddress)
                {
                    var bytes = new byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(bytes, BinaryPrimitives.ReadUInt32BigEndian(data[(value + 4)..]) ^ MagicCookie);
                    address = new IPAddress(bytes);
                    return true;
                }
                if (type == MappedAddress) mapped = new IPAddress(data.Slice(value + 4, 4));
            }
            offset = value + (length + 3) / 4 * 4;
        }
        address = mapped;
        return mapped != null;
    }
}
