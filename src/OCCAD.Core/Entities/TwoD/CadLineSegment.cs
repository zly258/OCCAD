using OcctNet;

namespace OCCAD;

public sealed record CadLineSegment : CadPathSegment
{
    private const double Tolerance = 1e-12;

    public CadLineSegment(
        OcctPoint3d start,
        OcctPoint3d end)
    {
        if (!start.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (!end.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(end));
        if (start.DistanceTo(end) <= Tolerance)
            throw new ArgumentException(
                "Path line segment must have positive length.",
                nameof(end));

        Start = start;
        End = end;
    }

    public override CadPathSegmentType Type =>
        CadPathSegmentType.Line;
    public override OcctPoint3d Start { get; }
    public override OcctPoint3d End { get; }
    public override double Length => Start.DistanceTo(End);

    public override OcctPoint3d Evaluate(double parameter)
    {
        var value = ValidateParameter(parameter);
        return Start +
               CadTransformMath.Between(Start, End) * value;
    }

    public override bool TryClosestParameter(
        OcctPoint3d point,
        out double parameter)
    {
        if (!point.IsFinite)
        {
            parameter = 0.0;
            return false;
        }

        var direction =
            CadTransformMath.Between(Start, End);
        var lengthSquared = direction.LengthSquared;
        if (lengthSquared <= Tolerance * Tolerance)
        {
            parameter = 0.0;
            return false;
        }

        parameter = Math.Clamp(
            CadTransformMath.Between(Start, point)
                .Dot(direction) /
            lengthSquared,
            0.0,
            1.0);
        return true;
    }

    public override IReadOnlyList<CadPathSegment> Split(
        double parameter)
    {
        var value = ValidateParameter(parameter);
        if (value <= 1e-7 ||
            value >= 1.0 - 1e-7)
            throw new InvalidOperationException(
                "Split parameter must be inside the segment.");

        var point = Evaluate(value);
        return
        [
            new CadLineSegment(Start, point),
            new CadLineSegment(point, End)
        ];
    }

    public override CadPathSegment Reverse() =>
        new CadLineSegment(End, Start);

    public override CadPathSegment Translate(
        OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));
        return new CadLineSegment(
            Start + displacement,
            End + displacement);
    }

    public override CadPathSegment Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees) =>
        new CadLineSegment(
            CadTransformMath.RotatePoint(
                Start,
                center,
                axis,
                angleDegrees),
            CadTransformMath.RotatePoint(
                End,
                center,
                axis,
                angleDegrees));

    public override CadPathSegment Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        return new CadLineSegment(
            CadTransformMath.ScalePoint(
                Start,
                center,
                factor),
            CadTransformMath.ScalePoint(
                End,
                center,
                factor));
    }

    internal override CadEntity ToEntity() =>
        new CadLineEntity(Start, End);
}
