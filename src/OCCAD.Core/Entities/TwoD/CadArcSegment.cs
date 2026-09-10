using OcctNet;

namespace OCCAD;

public sealed record CadArcSegment : CadPathSegment
{
    public CadArcSegment(
        OcctPoint3d start,
        OcctPoint3d middle,
        OcctPoint3d end)
    {
        var arc = new CadArcEntity(
            start,
            middle,
            end);
        Start = arc.Start;
        Middle = arc.Middle;
        End = arc.End;
        Center = arc.Center;
        Normal = arc.Normal;
        Radius = arc.Radius;
        ArcLength = arc.ArcLength;
    }

    public override CadPathSegmentType Type =>
        CadPathSegmentType.Arc;
    public override OcctPoint3d Start { get; }
    public OcctPoint3d Middle { get; }
    public override OcctPoint3d End { get; }
    public OcctPoint3d Center { get; }
    public OcctVector3d Normal { get; }
    public double Radius { get; }
    public double ArcLength { get; }
    public override double Length => ArcLength;

    public override OcctPoint3d Evaluate(double parameter) =>
        AsArc().PointAtParameter(
            ValidateParameter(parameter));

    public override bool TryClosestParameter(
        OcctPoint3d point,
        out double parameter) =>
        AsArc().TryClosestParameter(
            point,
            out parameter);

    public override IReadOnlyList<CadPathSegment> Split(
        double parameter)
    {
        var value = ValidateParameter(parameter);
        if (value <= 1e-7 ||
            value >= 1.0 - 1e-7)
            throw new InvalidOperationException(
                "Split parameter must be inside the segment.");

        var arc = AsArc();
        return
        [
            FromEntity(
                arc.CopyWithParameterRange(0.0, value),
                applyPlacement: false),
            FromEntity(
                arc.CopyWithParameterRange(value, 1.0),
                applyPlacement: false)
        ];
    }

    public override CadPathSegment Reverse() =>
        new CadArcSegment(
            End,
            Middle,
            Start);

    public override CadPathSegment Translate(
        OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));
        return new CadArcSegment(
            Start + displacement,
            Middle + displacement,
            End + displacement);
    }

    public override CadPathSegment Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees) =>
        new CadArcSegment(
            CadTransformMath.RotatePoint(
                Start,
                center,
                axis,
                angleDegrees),
            CadTransformMath.RotatePoint(
                Middle,
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
        return new CadArcSegment(
            CadTransformMath.ScalePoint(
                Start,
                center,
                factor),
            CadTransformMath.ScalePoint(
                Middle,
                center,
                factor),
            CadTransformMath.ScalePoint(
                End,
                center,
                factor));
    }

    internal override CadEntity ToEntity() =>
        AsArc();

    internal CadArcEntity AsArc() =>
        new(Start, Middle, End);
}
