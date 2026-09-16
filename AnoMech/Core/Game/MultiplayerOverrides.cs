using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AnoMech.Core.Game;

// Carries the host's scenario settings to clients. Settings that single out "the local player"
// mean a different person on every machine, so they are reset to their defaults for everyone.
// Values are plain bools, ints, enums, or one of the type's own static instances (Direction.E,
// GlitchType.Mid, ...), which are sent by field name so clients get the identical instance back.
public static class MultiplayerOverrides
{
    private static readonly HashSet<string> PlayerBound =
    [
        "PlayerSlot", "TransitionPlayerSlot", "PlayerMonitor",
        "HelloWorld", "HelloWorldOrder", "HelloWorldType",
        "Dynamis", "ExtraDynamis",
        "TetherAssignment", "Monitor", "BeyondDefence", "PlayerSign",
    ];

    private const byte KindNull = 0;
    private const byte KindBool = 1;
    private const byte KindInt = 2;
    private const byte KindStatic = 3;

    public static T Resolve<T>(T local) where T : class, new()
    {
        switch (MultiplayerContext.Role)
        {
            case MultiplayerRole.Host:
                var sanitized = Sanitize(local);
                MultiplayerContext.OverridePayload = Serialize(sanitized);
                return sanitized;
            case MultiplayerRole.Client:
                return Deserialize<T>(MultiplayerContext.OverridePayload);
            default:
                return local;
        }
    }

    public static T Sanitize<T>(T source) where T : class, new()
    {
        var fresh = new T();
        foreach (var property in Properties(typeof(T)))
            if (!PlayerBound.Contains(property.Name))
                property.SetValue(fresh, property.GetValue(source));
        return fresh;
    }

    public static byte[] Serialize(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            var properties = Properties(value.GetType());
            writer.Write((byte)properties.Count);
            foreach (var property in properties)
            {
                writer.Write(property.Name);
                WriteValue(writer, property.GetValue(value));
            }
        }
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[]? data) where T : class, new()
    {
        var result = new T();
        if (data is null || data.Length == 0) return result;
        var properties = Properties(typeof(T)).ToDictionary(p => p.Name);
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var kind = reader.ReadByte();
                object? raw = kind switch
                {
                    KindNull => null,
                    KindBool => reader.ReadBoolean(),
                    KindInt => reader.ReadInt32(),
                    KindStatic => reader.ReadString(),
                    _ => throw new InvalidDataException($"unknown kind {kind}"),
                };
                if (properties.TryGetValue(name, out var property) && !PlayerBound.Contains(name)
                    && TryConvert(property.PropertyType, kind, raw, out var converted))
                    property.SetValue(result, converted);
            }
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException or IOException)
        {
        }
        return result;
    }

    private static void WriteValue(BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.Write(KindNull);
                break;
            case bool flag:
                writer.Write(KindBool);
                writer.Write(flag);
                break;
            case int or Enum:
                writer.Write(KindInt);
                writer.Write(Convert.ToInt32(value));
                break;
            default:
                if (StaticName(value) is { } name)
                {
                    writer.Write(KindStatic);
                    writer.Write(name);
                }
                else
                {
                    writer.Write(KindNull);
                }
                break;
        }
    }

    private static bool TryConvert(Type propertyType, byte kind, object? raw, out object? value)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        value = null;
        switch (kind)
        {
            case KindNull:
                return !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null;
            case KindBool when type == typeof(bool):
                value = raw;
                return true;
            case KindInt when type.IsEnum:
                value = Enum.ToObject(type, (int)raw!);
                return true;
            case KindInt when type == typeof(int):
                value = raw;
                return true;
            case KindStatic:
                var field = type.GetField((string)raw!, BindingFlags.Public | BindingFlags.Static);
                if (field is null || field.FieldType != type) return false;
                value = field.GetValue(null);
                return true;
            default:
                return false;
        }
    }

    private static string? StaticName(object value)
    {
        var type = value.GetType();
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
                   .Where(f => f.FieldType == type)
                   .FirstOrDefault(f => Equals(f.GetValue(null), value))?.Name;
    }

    private static List<PropertyInfo> Properties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .Where(p => p.CanRead && p.CanWrite)
               .OrderBy(p => p.MetadataToken)
               .ToList();
}
