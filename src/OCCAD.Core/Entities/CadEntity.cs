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

    /// <summary>
    /// Compatibility alias for older OCCAD callers. Core code must use LayerId.
    /// </summary>
    [Browsable(false)]
    public string Layer
    {
        get => LayerId;
        set => LayerId = value;
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

    public void ResetPlacement() =>
        SetPlacement(
            CadPlacement.Identity,
            nameof(Placement));

    public OcctPoint3d ToWorldPoint(OcctPoint3d point) =>
        _placement.ToWorldPoint(point);

    public OcctVector3d ToWorldVector(OcctVector3d vector) =>
        _placement.ToWorldVector(vector);

    public OcctPoint3d ToLocalPoint(OcctPoint3d point) =>
        _placement.ToLocalPoint(point);

    public OcctVector3d ToLocalVector(OcctVector3d vector) =>
        _placement.ToLocalVector(vector);

    internal IReadOnlyList<CadSnapPoint> GetWorldSnapPoints()
    {
        var local = GetSnapPoints();
        if (_placement.IsIdentity)
            return local;

        return local
            .Select(TransformSnapPoint)
            .ToArray();
    }

    internal IReadOnlyList<CadGripPoint> GetWorldGripPoints()
    {
        var local = GetGripPoints();
        if (_placement.IsIdentity)
            return local;

        return local
            .Select(TransformGripPoint)
            .ToArray();
    }

    internal void MoveWorldGrip(
        int index,
        OcctPoint3d targetPoint) =>
        MoveGrip(
            index,
            _placement.ToLocalPoint(targetPoint));

    internal IReadOnlyList<CadSnapCurve> GetWorldPrecisionSnapCurves(
        CadWorkPlane workPlane)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (_placement.IsIdentity)
            return GetPrecisionSnapCurves(workPlane);

        var localPlane = new CadWorkPlane();
        localPlane.SetCustom(
            _placement.ToLocalPoint(workPlane.Origin),
            _placement.ToLocalVector(workPlane.XAxis),
            _placement.ToLocalVector(workPlane.YAxis));
        return GetPrecisionSnapCurves(localPlane);
    }

    internal void ScaleFromWorld(
        OcctPoint3d center,
        double factor) =>
        Scale(
            _placement.ToLocalPoint(center),
            factor);

    internal void RestoreGeometrySnapshot(
        CadEntity snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.GetType() != GetType())
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _placement = snapshot._placement;
        RestoreGeometry(snapshot);
    }

    internal void RestorePlacement(CadPlacement placement) =>
        SetPlacement(placement, nameof(Placement));

    internal CadEntity CreateWorldGeometrySnapshot()
    {
        var snapshot = Duplicate();
        snapshot.BakePlacementIntoGeometry();
        return snapshot;
    }

    internal T CreateWorldGeometrySnapshot<T>()
        where T : CadEntity =>
        (T)CreateWorldGeometrySnapshot();

    internal void BakePlacementIntoGeometry()
    {
        if (_placement.IsIdentity)
            return;

        var placement = _placement;
        var xAxis = placement.ToWorldVector(OcctVector3d.UnitX);
        var yAxis = placement.ToWorldVector(OcctVector3d.UnitY);
        var zAxis = placement.ToWorldVector(OcctVector3d.UnitZ);

        _placement = CadPlacement.Identity;

        if (CadTransformMath.TryGetAxisAngle(
                xAxis,
                yAxis,
                zAxis,
                out var axis,
                out var angleDegrees) &&
            Math.Abs(angleDegrees) > 1e-10)
        {
            Rotate(
                OcctPoint3d.Origin,
                axis,
                angleDegrees);
        }

        var transform = placement.Transform;
        var translation = new OcctVector3d(
            transform.M03,
            transform.M13,
            transform.M23);
        if (translation.LengthSquared > 1e-24)
            Translate(translation);
    }

    public void RestoreState(CadEntity snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.GetType() != GetType())
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        var previous = Duplicate();
        var metadataChanged = _name != snapshot._name || _layerId != snapshot._layerId;
        var appearanceChanged =
            _visible != snapshot._visible ||
            _selectable != snapshot._selectable ||
            _colorByLayer != snapshot._colorByLayer ||
            _lineWidthByLayer != snapshot._lineWidthByLayer ||
            _lineStyleByLayer != snapshot._lineStyleByLayer ||
            _color != snapshot._color ||
            !_transparency.Equals(snapshot._transparency) ||
            !_lineWidth.Equals(snapshot._lineWidth) ||
            _lineStyle != snapshot._lineStyle ||
            _displayMode != snapshot._displayMode ||
            _material != snapshot._material;

        try
        {
            ApplyBaseState(snapshot);
            RestoreGeometry(snapshot);

            if (metadataChanged)
                Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Metadata, nameof(RestoreState)));
            if (appearanceChanged)
                Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Appearance, nameof(RestoreState)));
        }
        catch (Exception failure)
        {
            try
            {
                ApplyBaseState(previous);
                RestoreGeometry(previous);

                if (metadataChanged)
                    Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Metadata, nameof(RestoreState)));
                if (appearanceChanged)
                    Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Appearance, nameof(RestoreState)));
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Entity state apply and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    protected T CopyPropertiesTo<T>(
        T target,
        bool copyPlacement = true)
        where T : CadEntity
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Name = Name;
        target.LayerId = LayerId;
        target.Visible = Visible;
        target.Selectable = Selectable;
        target.ColorByLayer = ColorByLayer;
        target.LineWidthByLayer = LineWidthByLayer;
        target.LineStyleByLayer = LineStyleByLayer;
        target.Color = Color;
        target.Transparency = Transparency;
        target.LineWidth = LineWidth;
        target.LineStyle = LineStyle;
        target.DisplayMode = DisplayMode;
        target.Material = Material;
        if (copyPlacement)
            target._placement = _placement;
        return target;
    }

    private CadSnapPoint TransformSnapPoint(CadSnapPoint point)
    {
        CadSnapWorkPlane? workPlane = point.WorkPlane is { } plane
            ? new CadSnapWorkPlane(
                _placement.ToWorldPoint(plane.Origin),
                _placement.ToWorldVector(plane.XAxis).Normalized(),
                _placement.ToWorldVector(plane.YAxis).Normalized(),
                plane.LockPlane,
                plane.LockedAngleDegrees)
            : null;

        return point with
        {
            Position = _placement.ToWorldPoint(point.Position),
            WorkPlane = workPlane
        };
    }

    private CadGripPoint TransformGripPoint(CadGripPoint point)
    {
        CadGripWorkPlane? workPlane = point.WorkPlane is { } plane
            ? new CadGripWorkPlane(
                _placement.ToWorldPoint(plane.Origin),
                _placement.ToWorldVector(plane.XAxis).Normalized(),
                _placement.ToWorldVector(plane.YAxis).Normalized(),
                plane.LockPlane,
                plane.LockedAngleDegrees)
            : null;

        return point with
        {
            Position = _placement.ToWorldPoint(point.Position),
            WorkPlane = workPlane,
            ConstraintOrigin = point.ConstraintOrigin is { } origin
                ? _placement.ToWorldPoint(origin)
                : null
        };
    }

    private void SetPlacement(
        CadPlacement value,
        string propertyName)
    {
        if (_placement == value)
            return;

        Changing?.Invoke(
            this,
            new CadEntityChangingEventArgs(
                CadEntityChangeKind.Geometry,
                propertyName));

        var previous = _placement;
        _placement = value;
        try
        {
            RaiseGeometryChanged(propertyName);
        }
        catch
        {
            _placement = previous;
            throw;
        }
    }

    protected static void ValidateDisplacement(OcctVector3d displacement)
    {
        if (!double.IsFinite(displacement.X) ||
            !double.IsFinite(displacement.Y) ||
            !double.IsFinite(displacement.Z))
            throw new ArgumentOutOfRangeException(nameof(displacement), "Displacement must be finite.");
    }

    protected static OcctPoint3d Translated(OcctPoint3d point, OcctVector3d displacement) =>
        new(point.X + displacement.X, point.Y + displacement.Y, point.Z + displacement.Z);

    protected void RaiseGeometryChanging([CallerMemberName] string? propertyName = null) =>
        Changing?.Invoke(this, new CadEntityChangingEventArgs(CadEntityChangeKind.Geometry, propertyName));

    protected void RaiseGeometryChanged([CallerMemberName] string? propertyName = null) =>
        Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Geometry, propertyName));

    protected bool SetGeometry<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        Changing?.Invoke(
            this,
            new CadEntityChangingEventArgs(
                CadEntityChangeKind.Geometry,
                propertyName));

        var previous = field;
        field = value;

        try
        {
            RaiseGeometryChanged(propertyName);
            return true;
        }
        catch
        {
            field = previous;
            throw;
        }
    }

    protected static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Value must be finite.");
    }

    protected static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(name, "Value must be finite and greater than zero.");
    }

    private bool SetMetadata<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        Changing?.Invoke(
            this,
            new CadEntityChangingEventArgs(
                CadEntityChangeKind.Metadata,
                propertyName));

        var previous = field;
        field = value;
        try
        {
            Changed?.Invoke(
                this,
                new CadEntityChangedEventArgs(
                    CadEntityChangeKind.Metadata,
                    propertyName));
            return true;
        }
        catch (Exception failure)
        {
            field = previous;
            try
            {
                Changed?.Invoke(
                    this,
                    new CadEntityChangedEventArgs(
                        CadEntityChangeKind.Metadata,
                        propertyName));
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Entity metadata apply and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    private bool SetAppearance<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        Changing?.Invoke(
            this,
            new CadEntityChangingEventArgs(
                CadEntityChangeKind.Appearance,
                propertyName));

        var previous = field;
        field = value;
        try
        {
            Changed?.Invoke(
                this,
                new CadEntityChangedEventArgs(
                    CadEntityChangeKind.Appearance,
                    propertyName));
            return true;
        }
        catch (Exception failure)
        {
            field = previous;
            try
            {
                Changed?.Invoke(
                    this,
                    new CadEntityChangedEventArgs(
                        CadEntityChangeKind.Appearance,
                        propertyName));
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Entity appearance apply and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    private void ApplyBaseState(CadEntity snapshot)
    {
        _name = snapshot._name;
        _layerId = snapshot._layerId;
        _visible = snapshot._visible;
        _selectable = snapshot._selectable;
        _colorByLayer = snapshot._colorByLayer;
        _lineWidthByLayer = snapshot._lineWidthByLayer;
        _lineStyleByLayer = snapshot._lineStyleByLayer;
        _color = snapshot._color;
        _transparency = snapshot._transparency;
        _lineWidth = snapshot._lineWidth;
        _lineStyle = snapshot._lineStyle;
        _displayMode = snapshot._displayMode;
        _material = snapshot._material;
        _placement = snapshot._placement;
    }
}
