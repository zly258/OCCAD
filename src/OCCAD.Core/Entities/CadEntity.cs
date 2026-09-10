using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using OcctNet;

namespace OCCAD;

public enum CadEntityChangeKind
{
    Metadata,
    Geometry,
    Appearance
}

public sealed class CadEntityChangingEventArgs(CadEntityChangeKind kind, string? propertyName) : EventArgs
{
    public CadEntityChangeKind Kind { get; } = kind;
    public string? PropertyName { get; } = propertyName;
}

public sealed class CadEntityChangedEventArgs(CadEntityChangeKind kind, string? propertyName) : EventArgs
{
    public CadEntityChangeKind Kind { get; } = kind;
    public string? PropertyName { get; } = propertyName;
}

public abstract class CadEntity
{
    private readonly string _entityType;
    private string _name;
    private string _layerId = CadLayer.DefaultId;
    private bool _visible = true;
    private bool _selectable = true;
    private bool _colorByLayer = true;
    private bool _lineWidthByLayer = true;
    private bool _lineStyleByLayer = true;
    private Color _color = Color.FromArgb(220, 220, 220);
    private double _transparency;
    private double _lineWidth = 1.0;
    private OcctLineStyle _lineStyle = OcctLineStyle.Solid;
    private OcctDisplayMode _displayMode = OcctDisplayMode.Shaded;
    private OcctMaterial _material = OcctMaterial.Plastified;
    private CadPlacement _placement = CadPlacement.Identity;

    protected CadEntity(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _entityType = name.Trim();
        _name = _entityType;
    }

    [Category("General"), DisplayName("Type"), ReadOnly(true)]
    [CadProperty(CadValueSemantic.Text, Order = 0)]
    public string EntityType => _entityType;

    [Category("General"), DisplayName("ID"), ReadOnly(true)]
    [CadProperty(CadValueSemantic.Text, Order = 5)]
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Category("General")]
    [CadProperty(CadValueSemantic.Text, Order = 10)]
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetMetadata(ref _name, value);
        }
    }

    [Category("General"), DisplayName("Layer")]
    [CadProperty(CadValueSemantic.Layer, Order = 20)]
    public string LayerId
    {
        get => _layerId;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetMetadata(ref _layerId, value.Trim());
        }
    }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Boolean, Order = 70)]
    public bool Visible { get => _visible; set => SetAppearance(ref _visible, value); }

    [Browsable(false)]
    public bool Selectable { get => _selectable; set => SetAppearance(ref _selectable, value); }

    [Browsable(false)]
    public bool ColorByLayer { get => _colorByLayer; set => SetAppearance(ref _colorByLayer, value); }

    [Browsable(false)]
    public bool LineWidthByLayer { get => _lineWidthByLayer; set => SetAppearance(ref _lineWidthByLayer, value); }

    [Browsable(false)]
    public bool LineStyleByLayer { get => _lineStyleByLayer; set => SetAppearance(ref _lineStyleByLayer, value); }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Color, Order = 30)]
    public Color Color { get => _color; set => SetAppearance(ref _color, value); }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Transparency, Order = 60)]
    public double Transparency
    {
        get => _transparency;
        set
        {
            if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Transparency must be between 0 and 1.");
            SetAppearance(ref _transparency, value);
        }
    }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Length, Order = 50)]
    public double LineWidth
    {
        get => _lineWidth;
        set
        {
            ValidatePositive(value, nameof(value));
            SetAppearance(ref _lineWidth, value);
        }
    }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Enum, Order = 40)]
    public OcctLineStyle LineStyle
    {
        get => _lineStyle;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            SetAppearance(ref _lineStyle, value);
        }
    }

    [Browsable(false)]
    public OcctDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            SetAppearance(ref _displayMode, value);
        }
    }

    [Browsable(false)]
    public OcctPoint3d Position => _placement.Position;

    [Browsable(false)]
    public CadPlacement Placement => _placement;

    [Browsable(false)]
    public OcctMaterial Material
    {
        get => _material;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            SetAppearance(ref _material, value);
        }
    }

    [Browsable(false)]
    public IOcctObject? ViewerObject { get; internal set; }

    [Browsable(false)]
    public OcctShape? ViewerShape
    {
        get => ViewerObject is OcctShape shape ? shape : null;
        internal set => ViewerObject = value;
    }

    [Browsable(false)]
    public long? ViewerObjectId => ViewerObject?.Id;

    public event EventHandler<CadEntityChangingEventArgs>? Changing;
    public event EventHandler<CadEntityChangedEventArgs>? Changed;

    internal void RestoreIdentity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentOutOfRangeException(
                nameof(id),
                "Entity identity cannot be empty.");
        if (ViewerObject is not null)
            throw new InvalidOperationException(
                "Entity identity cannot change after presentation creation.");
        Id = id;
    }

    internal virtual IOcctObject BuildPresentation(OcctEngine engine) => BuildShape(engine);

    internal virtual OcctShape BuildShape(OcctEngine engine) =>
        throw new NotSupportedException($"{GetType().Name} does not provide a topology shape.");

    internal virtual IReadOnlyList<CadSnapCurve> GetPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        return Array.Empty<CadSnapCurve>();
    }

    public abstract IReadOnlyList<CadSnapPoint> GetSnapPoints();
    public abstract IReadOnlyList<CadGripPoint> GetGripPoints();
    public abstract void MoveGrip(int index, OcctPoint3d targetPoint);

    public CadEntity MirroredCopy(OcctPoint3d origin, OcctVector3d normal) =>
        CopyPropertiesTo(
            CadMirrorGeometry.Create(this, origin, normal),
            copyPlacement: false);

    public abstract CadEntity Duplicate();
    public abstract void RestoreGeometry(CadEntity snapshot);
    public abstract void Translate(OcctVector3d displacement);
    public abstract void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees);
    public abstract void Scale(OcctPoint3d center, double factor);

    public void TranslatePlacement(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        SetPlacement(
            _placement.TranslateWorld(displacement),
            nameof(Placement));
    }

    public void RotatePlacement(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        if (!center.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(center));
        if (!axis.TryNormalize(out var normal))
            throw new ArgumentOutOfRangeException(nameof(axis));
        ValidateFinite(angleDegrees, nameof(angleDegrees));

        SetPlacement(
            _placement.RotateWorld(
                center,
                normal,
                angleDegrees),
            nameof(Placement));
    }

    public void ScalePlacement(
        OcctPoint3d center,
        double factor)
    {
        if (!center.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(center));
        ValidatePositive(factor, nameof(factor));

        SetPlacement(
            _placement.ScaleWorld(center, factor),
            nameof(Placement));
    }

    public OcctPoint3d ToWorldPoint(OcctPoint3d point)
    {
        ValidatePoint(point, nameof(point));
        return _placement.Transform.TransformPoint(point);
    }

    public OcctVector3d ToWorldVector(OcctVector3d vector)
    {
        ValidateDisplacement(vector);
        return _placement.Transform.TransformVector(vector);
    }

    public OcctPoint3d ToLocalPoint(OcctPoint3d point)
    {
        ValidatePoint(point, nameof(point));
        return _placement.InverseTransform.TransformPoint(point);
    }

    public OcctVector3d ToLocalVector(OcctVector3d vector)
    {
        ValidateDisplacement(vector);
        return _placement.InverseTransform.TransformVector(vector);
    }

    public IReadOnlyList<CadSnapPoint> GetWorldSnapPoints()
    {
        var transform = _placement.Transform;
        return GetSnapPoints()
            .Select(snap => snap with
            {
                Position = transform.TransformPoint(snap.Position)
            })
            .ToArray();
    }

    internal IReadOnlyList<CadSnapCurve> GetWorldPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);

        if (_placement.IsIdentity)
            return GetPrecisionSnapCurves(workPlane);

        var snapshot = CreateWorldGeometrySnapshot();
        return snapshot.GetPrecisionSnapCurves(workPlane);
    }

    public IReadOnlyList<CadGripPoint> GetWorldGripPoints()
    {
        var transform = _placement.Transform;
        return GetGripPoints()
            .Select(grip => grip with
            {
                Position = transform.TransformPoint(grip.Position)
            })
            .ToArray();
    }

    public void MoveWorldGrip(int index, OcctPoint3d targetPoint)
    {
        ValidatePoint(targetPoint, nameof(targetPoint));
        MoveGrip(index, ToLocalPoint(targetPoint));
    }

    public CadEntity CreateWorldGeometrySnapshot()
    {
        var snapshot = Duplicate();
        if (_placement.IsIdentity)
            return snapshot;

        snapshot.ApplyPlacementToGeometry(_placement);
        snapshot.SetPlacementDirect(CadPlacement.Identity);
        return snapshot;
    }

    public T CreateWorldGeometrySnapshot<T>()
        where T : CadEntity =>
        CreateWorldGeometrySnapshot() is T value
            ? value
            : throw new InvalidOperationException(
                $"Expected world geometry snapshot of type {typeof(T).Name}.");

    public void ApplyPlacementToGeometry(CadPlacement placement)
    {
        if (placement.IsIdentity)
            return;

        var transform = placement.Transform;
        if (placement.Scale <= 0.0 ||
            !double.IsFinite(placement.Scale))
            throw new InvalidOperationException(
                "Entity placement contains an invalid scale.");

        Scale(OcctPoint3d.Origin, placement.Scale);

        if (CadTransformMath.TryGetAxisAngle(
                transform.TransformVector(OcctVector3d.UnitX),
                transform.TransformVector(OcctVector3d.UnitY),
                transform.TransformVector(OcctVector3d.UnitZ),
                out var axis,
                out var angleDegrees))
        {
            Rotate(OcctPoint3d.Origin, axis, angleDegrees);
        }

        Translate(placement.Position - OcctPoint3d.Origin);
    }

    internal void RestorePlacement(CadPlacement placement)
    {
        if (!placement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(placement));
        SetPlacement(placement, nameof(Placement));
    }

    protected T CopyPropertiesTo<T>(
        T target,
        bool copyPlacement = true)
        where T : CadEntity
    {
        ArgumentNullException.ThrowIfNull(target);
        target._name = _name;
        target._layerId = _layerId;
        target._visible = _visible;
        target._selectable = _selectable;
        target._colorByLayer = _colorByLayer;
        target._lineWidthByLayer = _lineWidthByLayer;
        target._lineStyleByLayer = _lineStyleByLayer;
        target._color = _color;
        target._transparency = _transparency;
        target._lineWidth = _lineWidth;
        target._lineStyle = _lineStyle;
        target._displayMode = _displayMode;
        target._material = _material;
        target._placement = copyPlacement
            ? _placement
            : CadPlacement.Identity;
        return target;
    }

    protected bool SetMetadata<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        RaiseChanging(CadEntityChangeKind.Metadata, propertyName);
        field = value;
        RaiseChanged(CadEntityChangeKind.Metadata, propertyName);
        return true;
    }

    protected bool SetGeometry<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        RaiseGeometryChanging(propertyName);
        field = value;
        RaiseGeometryChanged(propertyName);
        return true;
    }

    protected bool SetAppearance<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        RaiseChanging(CadEntityChangeKind.Appearance, propertyName);
        field = value;
        RaiseChanged(CadEntityChangeKind.Appearance, propertyName);
        return true;
    }

    protected void RaiseGeometryChanging(
        [CallerMemberName] string? propertyName = null) =>
        RaiseChanging(CadEntityChangeKind.Geometry, propertyName);

    protected void RaiseGeometryChanged(
        [CallerMemberName] string? propertyName = null) =>
        RaiseChanged(CadEntityChangeKind.Geometry, propertyName);

    protected static void ValidatePoint(
        OcctPoint3d point,
        string parameterName)
    {
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    protected static void ValidateDisplacement(OcctVector3d displacement)
    {
        if (!displacement.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(displacement));
    }

    protected static void ValidatePositive(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    protected static void ValidateFinite(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private void SetPlacement(
        CadPlacement value,
        string propertyName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_placement == value)
            return;

        RaiseGeometryChanging(propertyName);
        _placement = value;
        RaiseGeometryChanged(propertyName);
    }

    private void SetPlacementDirect(CadPlacement value) =>
        _placement = value;

    private void RaiseChanging(
        CadEntityChangeKind kind,
        string? propertyName) =>
        Changing?.Invoke(
            this,
            new CadEntityChangingEventArgs(kind, propertyName));

    private void RaiseChanged(
        CadEntityChangeKind kind,
        string? propertyName) =>
        Changed?.Invoke(
            this,
            new CadEntityChangedEventArgs(kind, propertyName));
}
