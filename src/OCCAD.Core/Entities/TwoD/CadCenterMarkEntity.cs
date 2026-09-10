using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Associative center mark hosted by a circle. Geometry and editable extension
/// parameters follow OCCTBIM-Source CenterMarkEntity while dependency refresh is
/// handled through OCCAD's document dependency contract.
/// </summary>
public sealed class CadCenterMarkEntity : CadEntity, ICadSourceDependentEntity
{
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private double _radius;
    private double _crossSizeFactor;
    private double _crossSpacingFactor;
    private double _leftExtend;
    private double _rightExtend;
    private double _topExtend;
    private double _bottomExtend;
    private bool _showExtend;
    private Guid? _hostCircleId;

    public CadCenterMarkEntity(
        OcctPoint3d center,
        OcctVector3d normal,
        double radius,
        double crossSizeFactor = 1.0,
        double crossSpacingFactor = 0.05,
        double leftExtend = 0.0,
        double rightExtend = 0.0,
        double topExtend = 0.0,
        double bottomExtend = 0.0,
        bool showExtend = false,
        Guid? hostCircleId = null)
        : base("Center Mark")
    {
        ValidatePoint(center, nameof(center));
        if (!normal.TryNormalize(out _normal))
            throw new ArgumentOutOfRangeException(nameof(normal));
        ValidatePositive(radius, nameof(radius));
        ValidateFactor(crossSizeFactor, nameof(crossSizeFactor));
        ValidateFactor(crossSpacingFactor, nameof(crossSpacingFactor));
        ValidateExtension(leftExtend, nameof(leftExtend));
        ValidateExtension(rightExtend, nameof(rightExtend));
        ValidateExtension(topExtend, nameof(topExtend));
        ValidateExtension(bottomExtend, nameof(bottomExtend));
        if (hostCircleId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(hostCircleId));

        _center = center;
        _radius = radius;
        _crossSizeFactor = crossSizeFactor;
        _crossSpacingFactor = crossSpacingFactor;
        _leftExtend = leftExtend;
        _rightExtend = rightExtend;
        _topExtend = topExtend;
        _bottomExtend = bottomExtend;
        _showExtend = showExtend;
        _hostCircleId = hostCircleId;

        DisplayMode = OcctDisplayMode.Wireframe;
        LineStyleByLayer = false;
        LineStyle = OcctLineStyle.DotDash;
    }

    [Browsable(false)]
    public OcctPoint3d Center => _center;

    [Browsable(false)]
    public OcctVector3d Normal => _normal;

    [Category("Geometry")]
    public double CenterX { get => _center.X; set => SetCenter(value, _center.Y, _center.Z); }

    [Category("Geometry")]
    public double CenterY { get => _center.Y; set => SetCenter(_center.X, value, _center.Z); }

    [Category("Geometry")]
    public double CenterZ { get => _center.Z; set => SetCenter(_center.X, _center.Y, value); }

    [Category("Geometry"), ReadOnly(true)]
    public double Radius => _radius;

    [Category("Geometry")]
    public double CrossSizeFactor
    {
        get => _crossSizeFactor;
        set
        {
            ValidateFactor(value, nameof(value));
            SetGeometry(ref _crossSizeFactor, value);
        }
    }

    [Category("Geometry")]
    public double CrossSpacingFactor
    {
        get => _crossSpacingFactor;
        set
        {
            ValidateFactor(value, nameof(value));
            SetGeometry(ref _crossSpacingFactor, value);
        }
    }

    [Category("Geometry")]
    public bool ShowExtend
    {
        get => _showExtend;
        set => SetGeometry(ref _showExtend, value);
    }

    [Category("Geometry")]
    public double LeftExtend
    {
        get => _leftExtend;
        set => SetExtension(ref _leftExtend, value, nameof(LeftExtend));
    }

    [Category("Geometry")]
    public double RightExtend
    {
        get => _rightExtend;
        set => SetExtension(ref _rightExtend, value, nameof(RightExtend));
    }

    [Category("Geometry")]
    public double TopExtend
    {
        get => _topExtend;
        set => SetExtension(ref _topExtend, value, nameof(TopExtend));
    }

    [Category("Geometry")]
    public double BottomExtend
    {
        get => _bottomExtend;
        set => SetExtension(ref _bottomExtend, value, nameof(BottomExtend));
    }

    [Category("Measurement"), ReadOnly(true)]
    public double Width => 2.0 * BaseHalfSize + _leftExtend + _rightExtend;

    [Category("Measurement"), ReadOnly(true)]
    public double Height => 2.0 * BaseHalfSize + _topExtend + _bottomExtend;

    [Browsable(false)]
    public Guid? HostCircleId => _hostCircleId;

    [Browsable(false)]
    public IReadOnlyCollection<Guid> SourceEntityIds =>
        _hostCircleId is { } id ? [id] : Array.Empty<Guid>();

    private double BaseHalfSize =>
        Math.Max(1e-6, _radius * Math.Max(0.05, _crossSizeFactor));

    public static CadCenterMarkEntity CreateAssociative(CadCircleEntity circle)
    {
        ArgumentNullException.ThrowIfNull(circle);
        return new CadCenterMarkEntity(
            circle.Center,
            circle.Normal,
            circle.Radius,
            hostCircleId: circle.Id)
        {
            LayerId = circle.LayerId,
            ColorByLayer = circle.ColorByLayer,
            Color = circle.Color,
            LineWidthByLayer = circle.LineWidthByLayer,
            LineWidth = circle.LineWidth,
            LineStyleByLayer = false,
            LineStyle = OcctLineStyle.DotDash
        };
    }

    public bool RefreshFromSources(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_hostCircleId is not { } hostId ||
            document.FindById(hostId) is not CadCircleEntity circle)
            return false;

        if (_center == circle.Center &&
            _normal == circle.Normal &&
            Math.Abs(_radius - circle.Radius) <= 1e-12)
            return false;

        RaiseGeometryChanging(nameof(RefreshFromSources));
        _center = circle.Center;
        _normal = circle.Normal;
        _radius = circle.Radius;
        RaiseGeometryChanged(nameof(RefreshFromSources));
        return true;
    }

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var segments = DisplaySegments();
        using var model = new OcctModelingSession();
        var edges = segments
            .Select(segment => model.MakeLine(segment.Start, segment.End))
            .ToArray();
        var compound = model.MakeCompound(edges);
        return engine.CreateShapeFromModel(model, compound);
    }

    internal override IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        var curves = new List<CadSnapCurve>();
        foreach (var segment in DisplaySegments())
        {
            if (CadPrecisionSnapGeometry.TryCreateSegment(
                    this,
                    curves.Count,
                    segment.Start,
                    segment.End,
                    workPlane,
                    out var curve))
                curves.Add(curve);
        }
        return curves;
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var (xAxis, yAxis) = CadCircleEntity.PlaneAxes(_normal);
        var plane = new CadSnapWorkPlane(_center, xAxis, yAxis);
        var l = BaseHalfSize;
        return
        [
            new(this, _center, CadSnapType.Center, 0, plane),
            new(this, _center - xAxis * (l + _leftExtend), CadSnapType.Endpoint, 1, plane),
            new(this, _center + xAxis * (l + _rightExtend), CadSnapType.Endpoint, 2, plane),
            new(this, _center + yAxis * (l + _topExtend), CadSnapType.Endpoint, 3, plane),
            new(this, _center - yAxis * (l + _bottomExtend), CadSnapType.Endpoint, 4, plane)
        ];
    }

    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var (xAxis, yAxis) = CadCircleEntity.PlaneAxes(_normal);
        var l = BaseHalfSize;
        return
        [
            new(this, 0, _center, new CadGripWorkPlane(_center, xAxis, yAxis), Kind: CadGripKind.Center),
            new(this, 1, _center - xAxis * (l + _leftExtend), new CadGripWorkPlane(_center, xAxis, yAxis, true, 180.0), _center, CadPrecisionInputKind.Length, CadGripKind.Vertex),
            new(this, 2, _center + xAxis * (l + _rightExtend), new CadGripWorkPlane(_center, xAxis, yAxis, true, 0.0), _center, CadPrecisionInputKind.Length, CadGripKind.Vertex),
            new(this, 3, _center + yAxis * (l + _topExtend), new CadGripWorkPlane(_center, xAxis, yAxis, true, 90.0), _center, CadPrecisionInputKind.Length, CadGripKind.Vertex),
            new(this, 4, _center - yAxis * (l + _bottomExtend), new CadGripWorkPlane(_center, xAxis, yAxis, true, -90.0), _center, CadPrecisionInputKind.Length, CadGripKind.Vertex)
        ];
    }

    public override void MoveGrip(int index, OcctPoint3d targetPoint)
    {
        ValidatePoint(targetPoint, nameof(targetPoint));
        if (index == 0)
        {
            if (_center == targetPoint)
                return;
            SetCenter(targetPoint.X, targetPoint.Y, targetPoint.Z);
            return;
        }

        var (xAxis, yAxis) = CadCircleEntity.PlaneAxes(_normal);
        var delta = targetPoint - _center;
        var l = BaseHalfSize;
        var extension = index switch
        {
            1 => Math.Max(0.0, -delta.Dot(xAxis) - l),
            2 => Math.Max(0.0, delta.Dot(xAxis) - l),
            3 => Math.Max(0.0, delta.Dot(yAxis) - l),
            4 => Math.Max(0.0, -delta.Dot(yAxis) - l),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        switch (index)
        {
            case 1: LeftExtend = extension; break;
            case 2: RightExtend = extension; break;
            case 3: TopExtend = extension; break;
            case 4: BottomExtend = extension; break;
        }
    }

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(new CadCenterMarkEntity(
            _center,
            _normal,
            _radius,
            _crossSizeFactor,
            _crossSpacingFactor,
            _leftExtend,
            _rightExtend,
            _topExtend,
            _bottomExtend,
            _showExtend,
            _hostCircleId));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadCenterMarkEntity value)
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        _center = value._center;
        _normal = value._normal;
        _radius = value._radius;
        _crossSizeFactor = value._crossSizeFactor;
        _crossSpacingFactor = value._crossSpacingFactor;
        _leftExtend = value._leftExtend;
        _rightExtend = value._rightExtend;
        _topExtend = value._topExtend;
        _bottomExtend = value._bottomExtend;
        _showExtend = value._showExtend;
        _hostCircleId = value._hostCircleId;
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        if (displacement == OcctVector3d.Zero)
            return;
        SetCenter(
            _center.X + displacement.X,
            _center.Y + displacement.Y,
            _center.Z + displacement.Z);
    }

    public override void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees)
    {
        RaiseGeometryChanging(nameof(Rotate));
        _center = CadTransformMath.RotatePoint(_center, center, axis, angleDegrees);
        _normal = CadTransformMath.RotateVector(_normal, axis, angleDegrees).Normalized();
        RaiseGeometryChanged(nameof(Rotate));
    }

    public override void Scale(OcctPoint3d center, double factor)
    {
        CadTransformMath.ValidateScale(factor);
        RaiseGeometryChanging(nameof(Scale));
        _center = CadTransformMath.ScalePoint(_center, center, factor);
        _radius *= factor;
        _leftExtend *= factor;
        _rightExtend *= factor;
        _topExtend *= factor;
        _bottomExtend *= factor;
        RaiseGeometryChanged(nameof(Scale));
    }

    private IReadOnlyList<(OcctPoint3d Start, OcctPoint3d End)> DisplaySegments()
    {
        var (xAxis, yAxis) = CadCircleEntity.PlaneAxes(_normal);
        var l = BaseHalfSize;
        var gap = Math.Max(0.0, _radius * Math.Max(0.0, _crossSpacingFactor));
        var halfGap = Math.Min(gap * 0.5, l * 0.49);
        var leftEnd = _showExtend ? l + _leftExtend : l;
        var rightEnd = _showExtend ? l + _rightExtend : l;
        var topEnd = _showExtend ? l + _topExtend : l;
        var bottomEnd = _showExtend ? l + _bottomExtend : l;

        return
        [
            (_center - xAxis * leftEnd, _center - xAxis * halfGap),
            (_center + xAxis * halfGap, _center + xAxis * rightEnd),
            (_center - yAxis * bottomEnd, _center - yAxis * halfGap),
            (_center + yAxis * halfGap, _center + yAxis * topEnd)
        ];
    }

    private void SetCenter(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        SetGeometry(ref _center, new OcctPoint3d(x, y, z));
    }

    private void SetExtension(ref double field, double value, string propertyName)
    {
        ValidateExtension(value, propertyName);
        SetGeometry(ref field, value, propertyName);
    }

    private static void ValidatePoint(OcctPoint3d point, string name)
    {
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(name, "Point must be finite.");
    }

    private static void ValidateFactor(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new ArgumentOutOfRangeException(name, "Factor must be finite and non-negative.");
    }

    private static void ValidateExtension(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new ArgumentOutOfRangeException(name, "Extension must be finite and non-negative.");
    }

    internal static JsonObject WriteGeometry(CadCenterMarkEntity entity) =>
        new()
        {
            ["center"] = CadEntityJson.Point(entity.Center),
            ["normal"] = CadEntityJson.Vector(entity.Normal),
            ["radius"] = entity.Radius,
            ["crossSizeFactor"] = entity.CrossSizeFactor,
            ["crossSpacingFactor"] = entity.CrossSpacingFactor,
            ["leftExtend"] = entity.LeftExtend,
            ["rightExtend"] = entity.RightExtend,
            ["topExtend"] = entity.TopExtend,
            ["bottomExtend"] = entity.BottomExtend,
            ["showExtend"] = entity.ShowExtend,
            ["hostCircleId"] = entity.HostCircleId
        };

    internal static CadCenterMarkEntity ReadGeometry(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new CadCenterMarkEntity(
            CadEntityJson.ReadPoint(data, "center"),
            CadEntityJson.ReadVector(data, "normal"),
            CadEntityJson.ReadDouble(data, "radius"),
            data["crossSizeFactor"]?.GetValue<double>() ?? 1.0,
            data["crossSpacingFactor"]?.GetValue<double>() ?? 0.05,
            data["leftExtend"]?.GetValue<double>() ?? 0.0,
            data["rightExtend"]?.GetValue<double>() ?? 0.0,
            data["topExtend"]?.GetValue<double>() ?? 0.0,
            data["bottomExtend"]?.GetValue<double>() ?? 0.0,
            data["showExtend"]?.GetValue<bool>() ?? false,
            ReadGuid(data, "hostCircleId"));
    }

    private static Guid? ReadGuid(JsonObject data, string name)
    {
        var node = data[name];
        if (node is null)
            return null;
        var value = node.GetValue<Guid>();
        return value == Guid.Empty ? null : value;
    }
}
