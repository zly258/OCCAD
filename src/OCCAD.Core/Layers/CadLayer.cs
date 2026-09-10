using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using OcctNet;

namespace OCCAD;

public enum CadLayerChangeKind
{
    Metadata,
    Appearance,
    State
}

public sealed class CadLayerChangedEventArgs(
    CadLayerChangeKind kind,
    string? propertyName,
    string? previousName = null) : EventArgs
{
    public CadLayerChangeKind Kind { get; } = kind;
    public string? PropertyName { get; } = propertyName;
    public string? PreviousName { get; } = previousName;
}

public readonly record struct CadLayerState(
    string Name,
    Color Color,
    double LineWidth,
    OcctLineStyle LineStyle,
    bool Visible,
    bool Locked);

public sealed class CadLayer
{
    private readonly CadLayerManager _owner;
    private string _name;
    private Color _color = Color.FromArgb(220, 220, 220);
    private double _lineWidth = 1.0;
    private OcctLineStyle _lineStyle = OcctLineStyle.Solid;
    private bool _visible = true;
    private bool _locked;

    internal CadLayer(CadLayerManager owner, string name)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name.Trim();
    }

    [Category("General"), ReadOnly(true)]
    [CadProperty(CadValueSemantic.Text, Order = 0)]
    public string Name => _name;

    [Browsable(false)]
    public bool IsDefault => string.Equals(_name, "0", StringComparison.OrdinalIgnoreCase);

    [Category("Display")]
    [CadProperty(CadValueSemantic.Color, Order = 10)]
    public Color Color
    {
        get => _color;
        set => Set(ref _color, value, CadLayerChangeKind.Appearance);
    }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Length, Order = 30)]
    public double LineWidth
    {
        get => _lineWidth;
        set
        {
            if (!double.IsFinite(value) || value <= 0.0)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Line width must be finite and greater than zero.");
            Set(ref _lineWidth, value, CadLayerChangeKind.Appearance);
        }
    }

    [Category("Display")]
    [CadProperty(CadValueSemantic.Enum, Order = 20)]
    public OcctLineStyle LineStyle
    {
        get => _lineStyle;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            Set(ref _lineStyle, value, CadLayerChangeKind.Appearance);
        }
    }

    [Category("State")]
    [CadProperty(CadValueSemantic.Boolean, Order = 10)]
    public bool Visible
    {
        get => _visible;
        set => Set(ref _visible, value, CadLayerChangeKind.State);
    }

    [Category("State")]
    [CadProperty(CadValueSemantic.Boolean, Order = 20)]
    public bool Locked
    {
        get => _locked;
        set => Set(ref _locked, value, CadLayerChangeKind.State);
    }

    public event EventHandler<CadLayerChangedEventArgs>? Changing;
    public event EventHandler<CadLayerChangedEventArgs>? Changed;

    // Internal state propagation remains strict so the layer manager/document
    // can reject and roll back a layer mutation when native synchronization
    // fails. Public Changed is post-state notification only.
    internal event EventHandler<CadLayerChangedEventArgs>? StateChanged;

    public CadLayerState CaptureState() =>
        new(Name, Color, LineWidth, LineStyle, Visible, Locked);

    public void RestoreState(CadLayerState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state.Name);
        if (!double.IsFinite(state.LineWidth) || state.LineWidth <= 0.0 ||
            !Enum.IsDefined(state.LineStyle))
            throw new ArgumentOutOfRangeException(nameof(state));

        var previous = CaptureState();
        var nameChanged =
            !string.Equals(_name, state.Name, StringComparison.Ordinal);
        var appearanceChanged =
            _color != state.Color ||
            !_lineWidth.Equals(state.LineWidth) ||
            _lineStyle != state.LineStyle;
        var stateChanged =
            _visible != state.Visible ||
            _locked != state.Locked;

        try
        {
            if (nameChanged)
                _owner.Rename(this, state.Name);

            _color = state.Color;
            _lineWidth = state.LineWidth;
            _lineStyle = state.LineStyle;
            _visible = state.Visible;
            _locked = state.Locked;

            if (appearanceChanged)
                PublishStateAndChanged(
                    new CadLayerChangedEventArgs(
                        CadLayerChangeKind.Appearance,
                        nameof(RestoreState)));

            if (stateChanged)
                PublishStateAndChanged(
                    new CadLayerChangedEventArgs(
                        CadLayerChangeKind.State,
                        nameof(RestoreState)));
        }
        catch (Exception failure)
        {
            try
            {
                if (!string.Equals(_name, previous.Name, StringComparison.Ordinal))
                    _owner.Rename(this, previous.Name);

                _color = previous.Color;
                _lineWidth = previous.LineWidth;
                _lineStyle = previous.LineStyle;
                _visible = previous.Visible;
                _locked = previous.Locked;

                if (appearanceChanged)
                    PublishStateAndChanged(
                        new CadLayerChangedEventArgs(
                            CadLayerChangeKind.Appearance,
                            nameof(RestoreState)));

                if (stateChanged)
                    PublishStateAndChanged(
                        new CadLayerChangedEventArgs(
                            CadLayerChangeKind.State,
                            nameof(RestoreState)));
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Layer state apply and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    internal void RenameCore(string name)
    {
        var normalized = name.Trim();
        if (string.Equals(_name, normalized, StringComparison.Ordinal))
            return;

        var previousName = _name;
        var args = new CadLayerChangedEventArgs(
            CadLayerChangeKind.Metadata,
            nameof(Name),
            previousName);

        // Changing is a strict pre-state veto hook.
        Changing?.Invoke(this, args);
        _name = normalized;
        try
        {
            PublishStateAndChanged(args);
        }
        catch (Exception failure)
        {
            _name = previousName;
            try
            {
                PublishStateAndChanged(
                    new CadLayerChangedEventArgs(
                        CadLayerChangeKind.Metadata,
                        nameof(Name),
                        normalized));
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Layer rename and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    private bool Set<T>(
        ref T field,
        T value,
        CadLayerChangeKind kind,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        var args = new CadLayerChangedEventArgs(kind, propertyName);

        Changing?.Invoke(this, args);
        var previous = field;
        field = value;
        try
        {
            PublishStateAndChanged(args);
            return true;
        }
        catch (Exception failure)
        {
            field = previous;
            try
            {
                PublishStateAndChanged(args);
            }
            catch (Exception restoreFailure)
            {
                throw new AggregateException(
                    "Layer property apply and rollback both failed.",
                    failure,
                    restoreFailure);
            }

            throw;
        }
    }

    private void PublishStateAndChanged(CadLayerChangedEventArgs args)
    {
        StateChanged?.Invoke(this, args);
        PublishChanged(args);
    }

    private void PublishChanged(CadLayerChangedEventArgs args)
    {
        var handlers = Changed;
        if (handlers is null)
            return;

        foreach (EventHandler<CadLayerChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception) when (IsRecoverableObserverFailure(exception))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"CadLayer Changed observer failed after state changed: {exception}");
            }
        }
    }

    private static bool IsRecoverableObserverFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    public override string ToString() => Name;
}
