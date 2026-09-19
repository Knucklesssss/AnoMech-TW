using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Text;

namespace AnoMech.Helpers;

public static unsafe class GameObjectHelper
{
    private const int NameBytes = 63;

    // The game reads Name[] as UTF-8; one byte per char turned every Chinese name into "Ps=M".
    public static void WriteName(GameObject* obj, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        var length = Math.Min(bytes.Length, NameBytes);
        while (length > 0 && length < bytes.Length && (bytes[length] & 0xC0) == 0x80) length--;
        for (var i = 0; i < length; i++) obj->Name[i] = bytes[i];
        obj->Name[length] = 0;
    }
}
