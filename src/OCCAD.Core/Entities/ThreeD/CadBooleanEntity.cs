using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public enum CadBooleanKind
{
    Union,
    Cut,
    Common
}

public sealed class CadBooleanEntity : CadEntity
{
    private CadEntity _left;
    private CadEntity _right;
    private CadBooleanKind _operation;

    public CadBooleanEntity(
        CadEntity left,
        CadEntity right,
        CadBooleanKind operation) : base("Boolean")
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));

        _left = CadSolidFeatureGeometry.Snapshot(left);
        _right = CadSolidFeatureGeometry.Snapshot(right);
        _operation = operation;
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Geometry"), ReadOnly(true)]
    public CadBooleanKind Operation => _operation;

    [Browsable(false)]
    public string LeftType => _left.EntityType;

    [Browsable(false)]
    public string RightType => _right.EntityType;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var left = _left.BuildShape(engine);
        var right = _right.BuildShape(engine);

        try
        {
            return _operation switch
            {
                CadBooleanKind.Union =>
                    engine.Fuse(
                        left,
                        right,
                        hideInputs: true),
                CadBooleanKind.Cut =>
                    engine.Cut(
                        left,
                        right,
                        hideInputs: true),
                CadBooleanKind.Common =>
                    engine.Common(
                        left,
                        right,
                        hideInputs: true),
                _ => throw new InvalidOperationException()
            };
        }
        finally
        {
            if (engine.ContainsObject(left.Id))
                engine.Delete(left);
            if (engine.ContainsObject(right.Id))
                engine.Delete(right);
        }
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var center = OperandCenter();
        return
        [
            new(this, center, CadSnapType.Center, 0)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var center = OperandCenter();
        return
        [
            new(this, 0, center, Kind: CadGripKind.Center)
        ];
    }

    public override void MoveGrip(
        int index,
        OcctPoint3d targetPoint)
    {
        if (index != 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!targetPoint.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(targetPoint));

        var displacement =
            CadTransformMath.Between(
                OperandCenter(),
                targetPoint);
        Translate(displacement);
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadBooleanEntity(
                _left,
                _right,
                _operation));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadBooleanEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _left = value._left.Duplicate();
        _right = value._right.Duplicate();
        _operation = value._operation;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        _left.Translate(displacement);
        _right.Translate(displacement);
        RaiseGeometryChanged(nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        _left.Rotate(center, axis, angleDegrees);
        _right.Rotate(center, axis, angleDegrees);
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        _left.Scale(center, factor);
        _right.Scale(center, factor);
        RaiseGeometryChanged(nameof(Scale));
    }

    private OcctPoint3d OperandCenter()
    {
        var left =
            CadSolidFeatureGeometry.ApproximateCenter(_left);
        var right =
            CadSolidFeatureGeometry.ApproximateCenter(_right);
        return new OcctPoint3d(
            (left.X + right.X) * 0.5,
            (left.Y + right.Y) * 0.5,
            (left.Z + right.Z) * 0.5);
    }

    internal static JsonObject WriteGeometry(
        CadBooleanEntity entity) =>
        new()
        {
            ["operation"] = entity._operation.ToString(),
            ["left"] = CadSolidFeatureGeometry.Write(entity._left),
            ["right"] = CadSolidFeatureGeometry.Write(entity._right)
        };

    internal static CadBooleanEntity ReadGeometry(
        JsonObject data)
    {
        var operationText =
            data["operation"]?.GetValue<string>() ??
            throw new FormatException(
                "Boolean operation is missing.");

        if (!Enum.TryParse<CadBooleanKind>(
                operationText,
                ignoreCase: true,
                out var operation) ||
            !Enum.IsDefined(operation))
            throw new FormatException(
                $"Boolean operation '{operationText}' is invalid.");

        return new CadBooleanEntity(
            CadSolidFeatureGeometry.Read(
                data["left"] as JsonObject ??
                throw new FormatException(
                    "Boolean left operand is missing.")),
            CadSolidFeatureGeometry.Read(
                data["right"] as JsonObject ??
                throw new FormatException(
                    "Boolean right operand is missing.")),
            operation);
    }
}
