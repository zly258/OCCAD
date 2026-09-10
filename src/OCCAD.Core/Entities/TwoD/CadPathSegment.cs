using OcctNet;

namespace OCCAD;

/// <summary>
/// Geometry-only child of a CadPathEntity. A path segment has no document
/// identity, layer, appearance, viewer object, events, or serialization identity.
/// </summary>
public abstract record CadPathSegment
{
    public abstract CadPathSegmentType Type { get; }
    public abstract OcctPoint3d Start { get; }
    public abstract OcctPoint3d End { get; }
    public abstract double Length { get; }

    public abstract OcctPoint3d Evaluate(double parameter);
    public abstract bool TryClosestParameter(
        OcctPoint3d point,
        out double parameter);
    public abstract IReadOnlyList<CadPathSegment> Split(
        double parameter);
    public abstract CadPathSegment Reverse();
    public abstract CadPathSegment Translate(
        OcctVector3d displacement);
    public abstract CadPathSegment Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees);
    public abstract CadPathSegment Scale(
        OcctPoint3d center,
        double factor);

    internal abstract CadEntity ToEntity();

    internal static CadPathSegment FromEntity(
        CadEntity entity,
        bool applyPlacement = true)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return entity switch
        {
            CadLineEntity line => new CadLineSegment(
                applyPlacement
                    ? line.ToWorldPoint(line.Start)
                    : line.Start,
                applyPlacement
                    ? line.ToWorldPoint(line.End)
                    : line.End),

            CadArcEntity arc => new CadArcSegment(
                applyPlacement
                    ? arc.ToWorldPoint(arc.Start)
                    : arc.Start,
                applyPlacement
                    ? arc.ToWorldPoint(arc.Middle)
                    : arc.Middle,
                applyPlacement
                    ? arc.ToWorldPoint(arc.End)
                    : arc.End),

            _ => throw new ArgumentException(
                "Path segments must be lines or arcs.",
                nameof(entity))
        };
    }

    protected static double ValidateParameter(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        return Math.Clamp(value, 0.0, 1.0);
    }
}
