using System.Net;
using System.Net.Sockets;

namespace AnoMech.Core.Net;

public enum AddressKind
{
    Public,
    Private,
    CarrierGradeNat,
    Invalid,
}

public static class NetworkClassifier
{
    public static AddressKind Classify(IPAddress? address)
    {
        if (address is null || address.AddressFamily != AddressFamily.InterNetwork) return AddressKind.Invalid;
        var b = address.GetAddressBytes();
        if (b[0] == 0 || b[0] == 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254)) return AddressKind.Invalid;
        if (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168)) return AddressKind.Private;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return AddressKind.CarrierGradeNat;
        return AddressKind.Public;
    }
}
