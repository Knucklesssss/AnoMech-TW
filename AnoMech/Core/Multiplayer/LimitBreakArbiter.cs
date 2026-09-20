namespace AnoMech.Core.Multiplayer;

// The party shares one limit break bar, so someone has to decide who got it. Kept free of Game and
// networking types: MultiplayerSession needs both, and the tests can reach neither.
public sealed class LimitBreakArbiter
{
    public const byte Nobody = 255;

    private readonly uint[] acks = new uint[Wire.Slots];

    public byte Holder { get; private set; } = Nobody;
    public uint[] Acks => acks;

    // Acknowledges the request either way: a client that never hears back cannot tell a lost race
    // from a lost packet, and would wait forever.
    public bool TryClaim(byte role, uint requestId)
    {
        if (role >= Wire.Slots) return false;
        var fresh = requestId > acks[role];
        acks[role] = requestId > acks[role] ? requestId : acks[role];
        if (!fresh || Holder != Nobody) return false;
        Holder = role;
        return true;
    }

    public void Release() => Holder = Nobody;

    public void Reset()
    {
        Holder = Nobody;
        System.Array.Clear(acks);
    }
}
