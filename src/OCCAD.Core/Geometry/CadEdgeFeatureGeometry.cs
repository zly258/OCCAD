using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

internal static class CadEdgeFeatureGeometry
{
    internal static int[] NormalizeIndices(
        IEnumerable<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        var result = indices
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();

        if (result.Length == 0 ||
            result.Any(static value => value < 0))
            throw new ArgumentException(
                "Edge feature requires one or more non-negative edge indices.",
                nameof(indices));

        return result;
    }

    internal static JsonArray WriteIndices(
        IReadOnlyList<int> indices)
    {
        var result = new JsonArray();
        foreach (var index in indices)
            result.Add(index);
        return result;
    }

    internal static int[] ReadIndices(
        JsonObject data)
    {
        var values =
            data["edgeIndices"] as JsonArray ??
            throw new FormatException(
                "Edge indices are missing.");

        return NormalizeIndices(
            values.Select(node =>
                node?.GetValue<int>() ??
                throw new FormatException(
                    "Edge index is invalid.")));
    }

    internal static IReadOnlyList<CadSubobjectSelection> CurrentEdges(
        CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var items = workspace.Subobjects.Selected;
        if (items.Count == 0)
            return Array.Empty<CadSubobjectSelection>();

        var source = items[0].Entity;
        if (!CadSolidFeatureGeometry.IsSolid(source))
            return Array.Empty<CadSubobjectSelection>();

        if (items.Any(item =>
                !ReferenceEquals(item.Entity, source) ||
                item.SubshapeType != OcctShapeType.Edge ||
                item.SubshapeIndex < 0))
            return Array.Empty<CadSubobjectSelection>();

        return items;
    }
}
