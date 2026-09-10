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
    private string _layer = "0";
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

    protected CadEntity(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _entityType = name.Trim();
        _name = _entityType;
    }

    [Category("General"), DisplayName("Type"), ReadOnly(true)]
    public string EntityType => _entityType;

    [Category("General"), ReadOnly(true)]
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Category("General")]
    public string Name
    {
        get => _name;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetMetadata(ref _name, value);
        }
    }

    [Category("General"), ReadOnly(true)]
    public string Layer
    {
        get => _layer;
        internal set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            SetMetadata(ref _layer, value.Trim());
        }
    }

    [Category("Display")]
    public bool Visible { get => _visible; set => SetAppearance(ref _visible, value); }

    [Category("Display")]
    public bool Selectable { get => _selectable; set => SetAppearance(ref _selectable, value); }

    [Category("Display")]
    public bool ColorByLayer { get => _colorByLayer; set => SetAppearance(ref _colorByLayer, value); }

    [Category("Display")]
    public bool LineWidthByLayer { get => _lineWidthByLayer; set => SetAppearance(ref _lineWidthByLayer, value); }

    [Category("Display")]
    public bool LineStyleByLayer { get => _lineStyleByLayer; set => SetAppearance(ref _lineStyleByLayer, value); }

    [Category("Display")]
    public Color Color { get => _color; set => SetAppearance(ref _color, value); }

    [Category("Display")]
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

    [Category("Display")]
    public OcctDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            SetAppearance(ref _displayMode, value);
        }
    }

    [Category("Display")]
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
        CopyPropertiesTo(CadMirrorGeometry.Create(this, origin, normal));

    public abstract CadEntity Duplicate();
    public abstract void RestoreGeometry(CadEntity snapshot);
    public abstract void Translate(OcctVector3d displacement);
    public abstract void Rotate(OcctPoint3d center, OcctVector3d axis, double angleDegrees);
    public abstract void Scale(OcctPoint3d center, double factor);

    public void RestoreState(CadEntity snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.GetType() != GetType())
            throw new ArgumentException("Snapshot type does not match.", nameof(snapshot));

        var metadataChanged = _name != snapshot._name || _layer != snapshot._layer;
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

        _name = snapshot._name;
        _layer = snapshot._layer;
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

        RestoreGeometry(snapshot);

        if (metadataChanged)
            Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Metadata, nameof(RestoreState)));
        if (appearanceChanged)
            Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Appearance, nameof(RestoreState)));
    }

    protected T CopyPropertiesTo<T>(T target) where T : CadEntity
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Name = Name;
        target.Layer = Layer;
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
        return target;
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

    protected bool SetGeometry<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        Changing?.Invoke(this, new CadEntityChangingEventArgs(CadEntityChangeKind.Geometry, propertyName));
        field = value;
        RaiseGeometryChanged(propertyName);
        return true;
    }

    protected static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Value must be finite.");
    }

    protected static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(name, "Value must be finite and greater than zero.");
    }

    private bool SetMetadata<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        Changing?.Invoke(this, new CadEntityChangingEventArgs(CadEntityChangeKind.Metadata, propertyName));
        field = value;
        Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Metadata, propertyName));
        return true;
    }

    private bool SetAppearance<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        Changing?.Invoke(this, new CadEntityChangingEventArgs(CadEntityChangeKind.Appearance, propertyName));
        field = value;
        Changed?.Invoke(this, new CadEntityChangedEventArgs(CadEntityChangeKind.Appearance, propertyName));
        return true;
    }
}
