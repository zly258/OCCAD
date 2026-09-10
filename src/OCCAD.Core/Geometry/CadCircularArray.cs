using OcctNet;

namespace OCCAD;

internal static class CadCircularArray
{
    public const int MaximumCopies = 10000;
    public const int PreviewCopies = 200;
    public static bool IsValid(int sourceCount, int count, double sweep) =>
        sourceCount > 0 && count is >= 2 and <= 1000 && (long)sourceCount * (count - 1) <= MaximumCopies &&
        double.IsFinite(sweep) && Math.Abs(sweep) > 1e-9 && Math.Abs(sweep) <= 360;

    public static CadEntity[] CreateCopies(IReadOnlyList<CadEntity> sources, OcctPoint3d center,
        OcctVector3d axis, int count, double sweep, bool rotateItems, OcctPoint3d? reference,
        int limit = MaximumCopies)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (!center.IsFinite || !axis.TryNormalize(out var normal) || !IsValid(sources.Count, count, sweep))
            throw new ArgumentException("Invalid circular array center, axis, count or angle.");
        if (!rotateItems && (reference is not { IsFinite: true } point ||
            ((point - center) - normal * (point - center).Dot(normal)).Length <= 1e-9))
            throw new ArgumentException("A reference point away from the array axis is required to preserve orientation.");
        if (limit <= 0 || limit > MaximumCopies) throw new ArgumentOutOfRangeException(nameof(limit));
        var divisor = Math.Abs(Math.Abs(sweep) - 360) <= 1e-9 ? count : count - 1;
        var copies = new List<CadEntity>();
        for (var index = 1; index < count; index++)
        {
            var angle = sweep * index / divisor;
            foreach (var source in sources)
            {
                var copy = source.Duplicate();
                if (rotateItems) copy.RotatePlacement(center, normal, angle);
                else copy.TranslatePlacement(CadTransformMath.RotatePoint(reference!.Value, center, normal, angle) - reference.Value);
                copies.Add(copy);
                if (copies.Count == limit) return copies.ToArray();
            }
        }
        return copies.ToArray();
    }
}
