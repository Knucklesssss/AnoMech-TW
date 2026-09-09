using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Runtime.InteropServices;

namespace AnoMech.Pointers;

// The API13 ClientStructs snapshot predates SpawnObjectPacket, so the layout is
// carried here alongside the other hand-written packet structs. Offsets follow
// the upstream CS definition; field names follow this project's call sites.
// TimelineState (0x2C) is still written by raw pointer in SimEventObject.ToPacket.
[StructLayout(LayoutKind.Explicit, Size = 0x40)]
public partial struct SpawnObjectPacket
{
    [FieldOffset(0x00)] public byte ObjectIndex;
    [FieldOffset(0x01)] public byte ObjectKind;
    [FieldOffset(0x02)] public byte TargetableStatus;
    [FieldOffset(0x03)] public byte Visibility;
    [FieldOffset(0x04)] public uint BaseId;
    [FieldOffset(0x08)] public uint EntityId;
    [FieldOffset(0x0C)] public uint LayoutId;
    [FieldOffset(0x10)] public EventId EventId;
    [FieldOffset(0x14)] public uint OwnerId;
    [FieldOffset(0x18)] public uint GimmickId;
    [FieldOffset(0x1C)] public float Radius;
    [FieldOffset(0x22)] public ushort Rotation;
    [FieldOffset(0x24)] public ushort FateId;
    [FieldOffset(0x26)] public byte EventState;
    [FieldOffset(0x34)] public float PositionX;
    [FieldOffset(0x38)] public float PositionY;
    [FieldOffset(0x3C)] public float PositionZ;
}

[StructLayout(LayoutKind.Explicit, Size = 0x20)]
public partial struct ActorCastPacket
{
    [FieldOffset(0x00)] public ushort ActionId;
    [FieldOffset(0x02)] public byte ActionType;
    [FieldOffset(0x03)] public byte OmenDelay; // The value gets divided by 10.0f
    [FieldOffset(0x04)] public uint ActionId_2;
    [FieldOffset(0x08)] public float CastTime;
    [FieldOffset(0x0C)] public uint TargetEntityId;
    [FieldOffset(0x10)] public ushort RotationInt; // Quantized Rotation
    [FieldOffset(0x12)] public bool Interruptible;
    [FieldOffset(0x14)] public uint BallistaEntityId;
    [FieldOffset(0x18)] public ushort PositionX; // Quantized Position
    [FieldOffset(0x1A)] public ushort PositionY; // Quantized Position
    [FieldOffset(0x1C)] public ushort PositionZ; // Quantized Position
}

[StructLayout(LayoutKind.Explicit, Size = 0x1)]
public partial struct DespawnCharacterPacket
{
    [FieldOffset(0x0)] public byte Index;
}

[StructLayout(LayoutKind.Explicit, Size = 0x10)]
public partial struct UpdateClassInfoPacket
{
    [FieldOffset(0x0)] public byte ClassJobId;
    [FieldOffset(0x2)] public ushort CurrentLevel;
    [FieldOffset(0x4)] public ushort ClassJobLevel;
    [FieldOffset(0x6)] public ushort SyncedLevel;
    [FieldOffset(0x8)] public ushort ClassJobExp;
    [FieldOffset(0xC)] public uint BaseRestedExperience;
}

internal unsafe class PacketDispatcherPointers
{
    [Signature("40 53 57 48 81 EC ?? ?? ?? ?? 48 8B FA 8B", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleActorCastPacketDelegate HandleActorCastPacket { get; private set; } = null!;

    [Signature("40 53 48 83 EC 20 48 8B DA 48 8D 0D ?? ?? ?? ?? 0F", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleDespawnObjectPacketDelegate HandleDespawnObjectPacket { get; private set; } = null!;

    [Signature("48 89 5C 24 ?? 57 48 83 EC 40 0F B6 1A", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleDespawnCharacterPacketDelegate HandleDespawnCharacterPacket { get; private set; } = null!;

    // Technically the real HandleUpdateClassInfoPacket is a wrapper to this sig... but this is still close to other HandleX methods, so it fits here
    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B DA 48 8D 0D ?? ?? ?? ?? 33", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleUpdateClassInfoPacketDelegate HandleUpdateClassInfoPacket { get; private set; } = null!;

    // Absent from the API13 ClientStructs snapshot, so it is scanned here rather
    // than called through CS. Signature is upstream's; whether it resolves on the
    // TC binary is one of the things the startup signature report answers.
    [Signature("40 53 57 48 83 EC ?? F6 42", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleSpawnObjectPacketDelegate HandleSpawnObjectPacket { get; private set; } = null!;

    [Signature("40 55 53 57 41 54 41 56 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 8B 85", UseFlags = SignatureUseFlags.Pointer, ScanType = ScanType.Text, Fallibility = Fallibility.Fallible)]
    public static HandleActorControlPacketDelegate HandleActorControlPacket { get; private set; } = null!;

    public delegate void HandleActorControlPacketDelegate(uint entityId, uint category, uint arg1, uint arg2, uint arg3, uint arg4, uint arg5, uint arg6, uint arg7, uint arg8, GameObjectId targetId, bool isRecorded);
    public delegate void HandleSpawnObjectPacketDelegate(uint targetId, SpawnObjectPacket* packet);
    public delegate void HandleActorCastPacketDelegate(uint entityId, ActorCastPacket* packet);
    public delegate void HandleDespawnObjectPacketDelegate(uint unused, byte* packet);
    public delegate void HandleDespawnCharacterPacketDelegate(ulong unused, DespawnCharacterPacket* packet);
    public delegate void HandleUpdateClassInfoPacketDelegate(ulong unused, UpdateClassInfoPacket* packet);

    public static void Initialize()
    {
        Plugin.GameInterop.InitializeFromAttributes(new PacketDispatcherPointers());
    }
}
