using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

internal sealed class CadDynamicInputHud : Border
{
    private readonly CadWorkspace _workspace;
    private readonly TextBlock _promptText;
    private readonly TextBlock _firstLabel;
    private readonly TextBox _firstBox;
    private readonly TextBlock _firstLock;
    private readonly TextBlock _separator;
    private readonly TextBlock _secondLabel;
    private readonly TextBox _secondBox;
    private readonly TextBlock _secondLock;
    private readonly StackPanel _inputRow;

    private bool _firstEdited;
    private bool _secondEdited;
    private bool _isLengthAngleMode;
    private double? _lastComputedLength;
    private double? _lastComputedAngle;
    private double? _lastComputedX;
    private double? _lastComputedY;
    private OcctPoint3d? _currentReference;

    public CadDynamicInputHud(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        MinWidth = 120;
        MaxWidth = 320;
        Padding = new Thickness(6, 4);
        Background = new SolidColorBrush(Color.FromArgb(220, 32, 35, 38));
        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 80, 85, 90));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(2);
        IsVisible = false;

        _promptText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 205, 210)),
            FontSize = 10.0,
            Margin = new Thickness(0, 0, 0, 3),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        _firstLabel = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0)
        };

        _firstBox = CreateInputBox();
        _firstBox.KeyDown += OnFirstBoxKeyDown;
        _firstBox.GotFocus += (_, _) => _firstEdited = true;

        _firstLock = new TextBlock
        {
            Text = "🔒",
            FontSize = 9.0,
            Foreground = Brushes.Gold,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(1, 0, 3, 0)
        };

        _separator = new TextBlock
        {
            Text = " ",
            Foreground = Brushes.LightGray,
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0)
        };

        _secondLabel = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11.0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0)
        };

        _secondBox = CreateInputBox();
        _secondBox.KeyDown += OnSecondBoxKeyDown;
        _secondBox.GotFocus += (_, _) => _secondEdited = true;

        _secondLock = new TextBlock
        {
            Text = "🔒",
            FontSize = 9.0,
            Foreground = Brushes.Gold,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(1, 0, 0, 0)
        };

        _inputRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _firstLabel,
                _firstBox,
                _firstLock,
                _separator,
                _secondLabel,
                _secondBox,
                _secondLock
            }
        };

        var mainLayout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 1,
            Children = { _promptText, _inputRow }
        };

        Child = mainLayout;
    }

    public bool IsEditing => _firstBox.IsFocused || _secondBox.IsFocused || _firstEdited || _secondEdited;

    public void UpdateHud(
        CadResolvedPoint resolved,
        CadPointerPosition pointer,
        double renderScaling,
        Size viewportBounds)
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null ||
            tool.State != CadToolState.Drawing ||
            !tool.CurrentStep.RequiresPointer)
        {
            Reset();
            IsVisible = false;
            return;
        }

        var precision = tool.PrecisionInputs;
        _isLengthAngleMode = (precision & CadPrecisionInputKind.Length) != 0 ||
                             (precision & CadPrecisionInputKind.LengthAndAngle) != 0;

        _currentReference = tool.PrecisionReferencePoint ?? _workspace.WorkPlane.Origin;
        var refPoint = _currentReference.Value;
        var refLocal = _workspace.WorkPlane.WorldToLocal(refPoint);
        var ptLocal = _workspace.WorkPlane.WorldToLocal(resolved.Point);

        var dx = ptLocal.X - refLocal.X;
        var dy = ptLocal.Y - refLocal.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        if (angle < 0.0) angle += 360.0;

        _lastComputedLength = length;
        _lastComputedAngle = angle;
        _lastComputedX = ptLocal.X;
        _lastComputedY = ptLocal.Y;

        var promptLines = new List<string>(2);
        if (resolved.Snap is { } snap)
            promptLines.Add($"SNAP {snap.Type}");
        else if (resolved.Tracking is { } tracking)
            promptLines.Add($"{tracking.Kind.ToString().ToUpperInvariant()} {tracking.AngleDegrees:F1}°");

        var promptMsg = tool.Prompt?.Message;
        if (!string.IsNullOrWhiteSpace(promptMsg))
            promptLines.Add(promptMsg);

        _promptText.Text = string.Join(" · ", promptLines);
        _promptText.IsVisible = !string.IsNullOrEmpty(_promptText.Text);

        if (_isLengthAngleMode)
        {
            _firstLabel.Text = "L:";
            _secondLabel.Text = "<";
            _firstLock.IsVisible = _workspace.Drafting.LengthLockEnabled;
            _secondLock.IsVisible = _workspace.Drafting.AngleLockEnabled;

            if (!_firstEdited)
                _firstBox.Text = length.ToString("F3", CultureInfo.InvariantCulture);

            if (!_secondEdited)
                _secondBox.Text = angle.ToString("F1", CultureInfo.InvariantCulture);
        }
        else
        {
            _firstLabel.Text = "X:";
            _secondLabel.Text = "Y:";
            _firstLock.IsVisible = false;
            _secondLock.IsVisible = false;

            if (!_firstEdited)
                _firstBox.Text = ptLocal.X.ToString("F3", CultureInfo.InvariantCulture);

            if (!_secondEdited)
                _secondBox.Text = ptLocal.Y.ToString("F1", CultureInfo.InvariantCulture);
        }

        IsVisible = true;

        var x = pointer.X / renderScaling;
        var y = pointer.Y / renderScaling;
        const double offset = 18.0;
        const double estimatedWidth = 190.0;
        const double estimatedHeight = 44.0;
        const double margin = 6.0;

        var maxLeft = Math.Max(margin, viewportBounds.Width - estimatedWidth - margin);
        var maxTop = Math.Max(margin, viewportBounds.Height - estimatedHeight - margin);

        Canvas.SetLeft(this, Math.Clamp(x + offset, margin, maxLeft));
        Canvas.SetTop(this, Math.Clamp(y + offset, margin, maxTop));
    }

    public bool HandleViewportKeyDown(KeyEventArgs e)
    {
        if (!IsVisible)
            return false;

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            _firstBox.Focus();
            _firstBox.SelectAll();
            return true;
        }

        if (e.Key == Key.Space)
        {
            e.Handled = true;
            ToggleAxisLock();
            return true;
        }

        return false;
    }

    public bool HandleViewportTextInput(TextInputEventArgs e)
    {
        if (!IsVisible || string.IsNullOrEmpty(e.Text))
            return false;

        var ch = e.Text[0];
        if (char.IsDigit(ch) || ch == '-' || ch == '.')
        {
            e.Handled = true;
            _firstBox.Focus();
            _firstBox.Text = e.Text;
            _firstBox.CaretIndex = _firstBox.Text.Length;
            _firstEdited = true;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        _firstEdited = false;
        _secondEdited = false;
        _firstBox.Text = string.Empty;
        _secondBox.Text = string.Empty;
        _firstLock.IsVisible = false;
        _secondLock.IsVisible = false;
    }

    private static TextBox CreateInputBox() =>
        new()
        {
            Width = 64,
            Height = 20,
            Padding = new Thickness(3, 1),
            FontSize = 11.0,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(200, 20, 22, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 95, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment = VerticalAlignment.Center
        };

    private void OnFirstBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            ApplyFirstLock();
            _secondBox.Focus();
            _secondBox.SelectAll();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitDynamicInput();
        }
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
            ToggleAxisLock();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelAndReturnFocus();
        }
    }

    private void OnSecondBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            ApplySecondLock();
            _firstBox.Focus();
            _firstBox.SelectAll();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitDynamicInput();
        }
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
            ToggleAxisLock();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelAndReturnFocus();
        }
    }

    private void ApplyFirstLock()
    {
        if (_isLengthAngleMode)
        {
            if (double.TryParse(_firstBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var length) &&
                length > 1e-9)
            {
                var tool = _workspace.Tools.ActiveTool;
                _workspace.Precision.Apply(tool, new CadPrecisionInput(Length: length));
                _firstLock.IsVisible = true;
            }
        }
    }

    private void ApplySecondLock()
    {
        if (_isLengthAngleMode)
        {
            if (double.TryParse(_secondBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle))
            {
                var tool = _workspace.Tools.ActiveTool;
                _workspace.Precision.Apply(tool, new CadPrecisionInput(AngleDegrees: angle));
                _secondLock.IsVisible = true;
            }
        }
    }

    private void CommitDynamicInput()
    {
        var tool = _workspace.Tools.ActiveTool;
        if (tool is null)
            return;

        if (_isLengthAngleMode)
        {
            var hasLength = double.TryParse(_firstBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var len) && len > 1e-9;
            var hasAngle = double.TryParse(_secondBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ang);

            var length = hasLength ? len : _lastComputedLength ?? 0.0;
            var angle = hasAngle ? ang : _lastComputedAngle ?? 0.0;
            var reference = _currentReference ?? _workspace.WorkPlane.Origin;

            if (length > 1e-9 && CadExactInputGeometry.TryResolveAnglePoint(_workspace, reference, angle, length, out var exactPoint))
            {
                Reset();
                _workspace.Tools.CommitPoint(exactPoint);
            }
            else
            {
                _workspace.Precision.Apply(tool, new CadPrecisionInput(
                    Length: hasLength ? len : null,
                    AngleDegrees: hasAngle ? ang : null));
                Reset();
                _workspace.Tools.SubmitCurrent();
            }
        }
        else
        {
            var hasX = double.TryParse(_firstBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var px);
            var hasY = double.TryParse(_secondBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var py);

            var x = hasX ? px : _lastComputedX ?? 0.0;
            var y = hasY ? py : _lastComputedY ?? 0.0;
            var local = new CadPlanePoint(x, y);
            var worldPoint = _workspace.WorkPlane.LocalToWorld(local);

            Reset();
            _workspace.Tools.CommitPoint(worldPoint);
        }
    }

    private void ToggleAxisLock()
    {
        _workspace.Drafting.OrthogonalTrackingEnabled = !_workspace.Drafting.OrthogonalTrackingEnabled;
        _workspace.Tracking.Clear();
    }

    private void CancelAndReturnFocus()
    {
        Reset();
        _workspace.Drafting.ResetTransientLocks();
        _workspace.Precision.ResetFactor();
        _workspace.Tools.CancelCurrent();
    }
}
