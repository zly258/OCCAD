using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

internal static class CadEntityJson
{
    internal static JsonObject Point(OcctPoint3d value) =>
        new()
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z
        };

    internal static JsonObject Vector(OcctVector3d value) =>
        new()
        {
            ["x"] = value.X,
            ["y"] = value.Y,
            ["z"] = value.Z
        };

    internal static JsonArray Points(IEnumerable<OcctPoint3d> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(Point(value));
        return array;
    }

    internal static OcctPoint3d ReadPoint(JsonObject data, string name)
    {
        var value = ReadObject(data, name);
        return new OcctPoint3d(
            ReadDouble(value, "x"),
            ReadDouble(value, "y"),
            ReadDouble(value, "z"));
    }

    internal static OcctVector3d ReadVector(JsonObject data, string name)
    {
        var value = ReadObject(data, name);
        return new OcctVector3d(
            ReadDouble(value, "x"),
            ReadDouble(value, "y"),
            ReadDouble(value, "z"));
    }

    internal static IReadOnlyList<OcctPoint3d> ReadPoints(
        JsonObject data,
        string name)
    {
        if (data[name] is not JsonArray array)
            throw Invalid(name, "array");

        var values = new OcctPoint3d[array.Count];
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonObject point)
                throw Invalid($"{name}[{index}]", "point object");
            values[index] = new OcctPoint3d(
                ReadDouble(point, "x"),
                ReadDouble(point, "y"),
                ReadDouble(point, "z"));
        }
        return values;
    }

    private static JsonObject ReadObject(JsonObject data, string name) =>
        data[name] as JsonObject ??
        throw Invalid(name, "object");

    internal static double ReadDouble(JsonObject data, string name)
    {
        if (data[name] is not JsonValue value ||
            !value.TryGetValue<double>(out var result) ||
            !double.IsFinite(result))
            throw Invalid(name, "finite number");
        return result;
    }

    internal static int ReadInt(JsonObject data, string name)
    {
        if (data[name] is not JsonValue value ||
            !value.TryGetValue<int>(out var result))
            throw Invalid(name, "integer");
        return result;
    }

    internal static bool ReadBool(JsonObject data, string name)
    {
        if (data[name] is not JsonValue value ||
            !value.TryGetValue<bool>(out var result))
            throw Invalid(name, "boolean");
        return result;
    }

    private static InvalidDataException Invalid(
        string name,
        string expected) =>
        new($"CAD entity field '{name}' must be a valid {expected}.");
}
