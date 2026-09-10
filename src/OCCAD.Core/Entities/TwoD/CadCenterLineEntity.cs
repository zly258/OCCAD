using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Associative center line between two host line entities. The stored start/end
/// points are the logical center-line limits; optional end extensions affect
/// presentation geometry only, matching OCCTBIM-Source CenterLineEntity.
/// </summary>
public sealed class CadCenterLineEntity : CadEntity, ICadSourceDependentEntity
{
    private OcctPoint3d _start;
    private OcctPoint3d _end;
    private double _startExtend;
    private double _endExtend;
    private bool _showExtend;
    private Guid? _hostLine1Id;
    private Guid? _hostLine2Id;
    private bool _reverseHost2;

    public CadCenterLineEntity(
        OcctPoint3d start,
        OcctPoint3d end,
        double startExtend = 20.0,
        double endExtend = 20.0,
        bool showExtend = true,
        Guid? hostLine1Id = null,
        Guid? hostLine2Id = null,
        bool reverseHost2 = false)
        : base("Center Line")
    {
        ValidatePoint(start, nameof(start));
        ValidatePoint(end, nameof(end));
        ValidateExtension(startExtend, nameof(startExtend));
        ValidateExtension(endExtend, nameof(endExtend));
        ValidateSourceId(hostLine1Id, nameof(hostLine1Id));
        ValidateSourceId(hostLine2Id, nameof(hostLine2Id));
        if (start.DistanceTo(end) <= 1e-12)
            throw new ArgumentException(
                "Center-line endpoints must be distinct.",
                nameof(end));

        _start = start;
        _end = end;
        _startExtend = startExtend;
        _endExtend = endExtend;
        _showExtend = showExtend;
        _hostLine1Id = hostLine1Id;
        _hostLine2Id = hostLine2Id;
        _reverseHost2 = reverseHost2;

        DisplayMode = OcctDisplayMode.Wireframe;
        LineStyle = OcctLineStyle.DotDash;
    }

    [Browsable(false)]
    public OcctPoint3d Start => _start;

    [Browsable(false)]
    public OcctPoint3d End => _end;

    [Category("Geometry")]
    public double StartX
    {
        get => _start.X;
        set => SetStart(value, _start.Y, _start.Z);
    }

    [Category("Geometry")]
    public double StartY
    {
        get => _start.Y;
        set => SetStart(_start.X, value, _start.Z);
    }

    [Category("Geometry")]
    public double StartZ
    {
        get => _start.Z;
        set => SetStart(_start.X, _start.Y, value);
    }

    [Category("Geometry")]
    public double EndX
    {
        get => _end.X;
        set => SetEnd(value, _end.Y, _end.Z);
    }

    [Category("Geometry")]
    public double EndY
    {
        get => _end.Y;
        set => SetEnd(_end.X, value, _end.Z);
    }

    [Category("Geometry")]
    public double EndZ
    {
        get => _end.Z;
        set => SetEnd(_end.X, _end.Y, value);
    }

    [Category("Geometry")]
    public double StartExtend
    {
        get => _startExtend;
        set
        {
            ValidateExtension(value, nameof(value));
            SetGeometry(ref _startExtend, value);
        }
    }

    [Category("Geometry")]
    public double EndExtend
    {
        get => _endExtend;
        set
        {
            ValidateExtension(value, nameof(value));
            SetGeometry(ref _endExtend, value);
        }
    }

    [Category("Geometry")]
    public bool ShowExtend
    {
        get => _showExtend;
        set => SetGeometry(ref _showExtend, value);
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Length => _start.DistanceTo(_end);

    [Browsable(false)]
    public Guid? HostLine1Id => _hostLine1Id;

    [Browsable(false)]
    public Guid? HostLine2Id => _hostLine2Id;

    [Browsable(false)]
    public bool ReverseHost2 => _reverseHost2;

    [Browsable(false)]
    public IReadOnlyCollection<Guid> SourceEntityIds =>
        new[] { _hostLine1Id, _hostLine2Id }
            .Where(static id => id.HasValue)
            .Select(static id => id!.Value)
            .Distinct()
            .ToArray();

    public static CadCenterLineEntity CreateAssociative(
        CadLineEntity first,
        CadLineEntity second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (ReferenceEquals(first, second) || first.Id == second.Id)
            throw new ArgumentException(
                "Center line requires two distinct host lines.",
                nameof(second));

        var reverse =
            Direction(first).Dot(Direction(second)) < 0.0;
        var (start, end) = CalculateCenterPoints(
            first,
            second,
            reverse);

        var result = new CadCenterLineEntity(
            start,
            end,
            hostLine1Id: first.Id,
            hostLine2Id: second.Id,
            reverseHost2: reverse)
        {
            LayerId = first.LayerId,
            ColorByLayer = first.ColorByLayer,
            Color = first.Color,
            LineWidthByLayer = first.LineWidthByLayer,
            LineWidth = first.LineWidth,
            LineStyleByLayer = false,
            LineStyle = OcctLineStyle.DotDash
        };

        return result;
    }

    public bool RefreshFromSources(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_hostLine1Id is not { } firstId ||
            _hostLine2Id is not { } secondId)
            return false;

        if (document.FindById(firstId) is not CadLineEntity first ||
            document.FindById(secondId) is not CadLineEntity second)
            return false;

        var reverse = Direction(first).Dot(Direction(second)) < 0.0;
        var (start, end) = CalculateCenterPoints(
            first,
            second,
            reverse);

        if (_start == start &&
            _end == end &&
            _reverseHost2 == reverse)
            return false;

        RaiseGeometryChanging(nameof(RefreshFromSources));
        _start = start;
        _end = end;
        _reverseHost2 = reverse;
        RaiseGeometryChanged(nameof(RefreshFromSources));
        return true;
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var (start, end) = DisplayEndpoints();
        return engine.MakeLine(start, end);
    }

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var (start, end) = DisplayEndpoints();
        return CadPrecisionSnapGeometry.TryCreateSegment(
            this,
            0,
            start,
            end,
            workPlane,
            out var curve)
            ? [curve]
            : Array.Empty<CadSnapCurve>();
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
    [
        new(this, _start, CadSnapType.Endpoint, 0),
        new(this, Midpoint(_start, _end), CadSnapType.Midpoint, 0),
        new(this, _end, CadSnapType.Endpoint, 1)
    ];

    public override IReadOnlyList<CadGripPoint> GetGripPoints() =>
    [
        new(this, 0, _start, Kind: CadGripKind.Vertex),
        new(this, 1, _end, Kind: CadGripKind.Vertex)
    ];

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        ValidatePoint(targetPoint, nameof(targetPoint));
        switch (index)
        {
            case 0:
                if (targetPoint.DistanceTo(_end) <= 1e-12)
                    return;
                SetEndpoints(targetPoint, _end, nameof(MoveGrip));
                break;
            case 1:
                if (targetPoint.DistanceTo(_start) <= 1e-12)
                    return;
                SetEndpoints(_start, targetPoint, nameof(MoveGrip));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadCenterLineEntity(
                _start,
                _end,
                _startExtend,
                _endExtend,
                _showExtend,
                _hostLine1Id,
                _hostLine2Id,
                _reverseHost2));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadCenterLineEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _start = value._start;
        _end = value._end;
        _startExtend = value._startExtend;
        _endExtend = value._endExtend;
        _showExtend = value._showExtend;
        _hostLine1Id = value._hostLine1Id;
        _hostLine2Id = value._hostLine2Id;
        _reverseHost2 = value._reverseHost2;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement == OcctVector3d.Zero)
            return;

        SetEndpoints(
            Translated(_start, displacement),
            Translated(_end, displacement),
            nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees) =>
        SetEndpoints(
            CadTransformMath.RotatePoint(
                _start,
                center,
                axis,
                angleDegrees),
            CadTransformMath.RotatePoint(
                _end,
                center,
                axis,
                angleDegrees),
            nameof(Rotate));

    public override void Scale(
        OcctPoint3d center,
        double factor) =>
        SetEndpoints(
            CadTransformMath.ScalePoint(
                _start,
                center,
                factor),
            CadTransformMath.ScalePoint(
                _end,
                center,
                factor),
            nameof(Scale));

    private void SetStart(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        var value = new OcctPoint3d(x, y, z);
        if (value.DistanceTo(_end) <= 1e-12)
            throw new ArgumentException(
                "Center-line endpoints must be distinct.");
        SetEndpoints(value, _end, nameof(Start));
    }

    private void SetEnd(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        var value = new OcctPoint3d(x, y, z);
        if (value.DistanceTo(_start) <= 1e-12)
            throw new ArgumentException(
                "Center-line endpoints must be distinct.");
        SetEndpoints(_start, value, nameof(End));
    }

    private void SetEndpoints(
        OcctPoint3d start,
        OcctPoint3d end,
        string propertyName)
    {
        ValidatePoint(start, nameof(start));
        ValidatePoint(end, nameof(end));
        if (start.DistanceTo(end) <= 1e-12)
            throw new ArgumentException(
                "Center-line endpoints must be distinct.");
        if (_start == start && _end == end)
            return;

        RaiseGeometryChanging(propertyName);
        _start = start;
        _end = end;
        RaiseGeometryChanged(propertyName);
    }

    private (OcctPoint3d Start, OcctPoint3d End) DisplayEndpoints()
    {
        if (!_showExtend ||
            (_startExtend <= 0.0 && _endExtend <= 0.0))
            return (_start, _end);

        var direction = Direction(_start, _end);
        return (
            _start - direction * _startExtend,
            _end + direction * _endExtend);
    }

    private static (
        OcctPoint3d Start,
        OcctPoint3d End) CalculateCenterPoints(
        CadLineEntity first,
        CadLineEntity second,
        bool reverseSecond)
    {
        var secondStart = reverseSecond
            ? second.End
            : second.Start;
        var secondEnd = reverseSecond
            ? second.Start
            : second.End;

        var start = Midpoint(first.Start, secondStart);
        var end = Midpoint(first.End, secondEnd);
        if (start.DistanceTo(end) <= 1e-12)
            throw new InvalidOperationException(
                "Host lines do not define a valid center line.");
        return (start, end);
    }

    private static OcctVector3d Direction(CadLineEntity line) =>
        Direction(line.Start, line.End);

    private static OcctVector3d Direction(
        OcctPoint3d start,
        OcctPoint3d end)
    {
        var direction = end - start;
        if (!direction.TryNormalize(out var normalized))
            throw new InvalidOperationException(
                "Center-line direction is degenerate.");
        return normalized;
    }

    private static void ValidateExtension(
        double value,
        string name)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new ArgumentOutOfRangeException(
                name,
                "Center-line extension must be finite and non-negative.");
    }

    private static void ValidateSourceId(
        Guid? value,
        string name)
    {
        if (value == Guid.Empty)
            throw new ArgumentOutOfRangeException(name);
    }

    private static OcctPoint3d Midpoint(
        OcctPoint3d a,
        OcctPoint3d b) =>
        new(
            (a.X + b.X) * 0.5,
            (a.Y + b.Y) * 0.5,
            (a.Z + b.Z) * 0.5);

    internal static JsonObject WriteGeometry(
        CadCenterLineEntity entity) =>
        new()
        {
            ["start"] = CadEntityJson.Point(entity.Start),
            ["end"] = CadEntityJson.Point(entity.End),
            ["startExtend"] = entity.StartExtend,
            ["endExtend"] = entity.EndExtend,
            ["showExtend"] = entity.ShowExtend,
            ["hostLine1Id"] = entity.HostLine1Id,
            ["hostLine2Id"] = entity.HostLine2Id,
            ["reverseHost2"] = entity.ReverseHost2
        };

    internal static CadCenterLineEntity ReadGeometry(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new CadCenterLineEntity(
            CadEntityJson.ReadPoint(data, "start"),
            CadEntityJson.ReadPoint(data, "end"),
            data["startExtend"]?.GetValue<double>() ?? 20.0,
            data["endExtend"]?.GetValue<double>() ?? 20.0,
            data["showExtend"]?.GetValue<bool>() ?? true,
            ReadGuid(data, "hostLine1Id"),
            ReadGuid(data, "hostLine2Id"),
            data["reverseHost2"]?.GetValue<bool>() ?? false);
    }

    private static Guid? ReadGuid(
        JsonObject data,
        string name)
    {
        var node = data[name];
        if (node is null)
            return null;
        var value = node.GetValue<Guid>();
        return value == Guid.Empty
            ? null
            : value;
    }
}
