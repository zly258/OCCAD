using OcctNet;

namespace OCCAD;

internal static class CadPathArray
{
    public const int MaximumCopies = 10000;
    public const int PreviewCopies = 200;

    public static CadEntity[] CreateCopies(IReadOnlyList<CadEntity> sources, CadArrayPath path,
        double spacing, OcctPoint3d reference, OcctVector3d forward, OcctVector3d up,
        bool align, int limit = MaximumCopies)
    {
        if (sources.Count == 0 || sources.Count > MaximumCopies) throw new ArgumentException("Select source objects.", nameof(sources));
        if (!reference.IsFinite || !forward.TryNormalize(out var direction) || !up.TryNormalize(out var normal) ||
            Math.Abs(direction.Dot(normal)) > 1e-8) throw new ArgumentException("Invalid path array reference frame.");
        if (limit < 1 || limit > MaximumCopies) throw new ArgumentOutOfRangeException(nameof(limit));
        var count = path.StationCount(spacing, MaximumCopies / sources.Count);
        var copies = new List<CadEntity>();
        for (var index = 0; index < count; index++)
        {
            var station = path.Evaluate(index * spacing);
            var dot = Math.Clamp(direction.Dot(station.Tangent), -1, 1);
            var cross = direction.Cross(station.Tangent);
            var axis = cross.TryNormalize(out var rotationAxis) ? rotationAxis : normal;
            var angle = Math.Acos(dot) * 180 / Math.PI;
            foreach (var source in sources)
            {
                var copy = source.Duplicate();
                if (align && angle > 1e-9) copy.RotatePlacement(reference, axis, angle);
                copy.TranslatePlacement(station.Point - reference);
                copies.Add(copy);
                if (copies.Count == limit) return copies.ToArray();
            }
        }
        return copies.ToArray();
    }
}
