using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;
using DrawingColor = System.Drawing.Color;
using MediaColor = Avalonia.Media.Color;

namespace OCCAD.Avalonia;

internal sealed class CadPropertyInspectorController : IDisposable
{
    private readonly Window _owner;
    private readonly CadWorkspace _workspace;
    private readonly StackPanel _host;
    private CadEntity[] _entities = [];
    private CadLayer? _layer;
    private bool _disposed;
    private bool _refreshing;

    public CadPropertyInspectorController(
        Window owner,
        CadWorkspace workspace,
        StackPanel host)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsInspectingLayer => _layer is not null;

    public void InspectEntities(IReadOnlyList<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureNotDisposed();

        _layer = null;
        _entities = entities
            .Distinct()
            .Where(_workspace.Document.Entities.Contains)
            .ToArray();
        Rebuild();
    }

    public void InspectLayer(CadLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        EnsureNotDisposed();

        _entities = [];
        _layer = layer;
        Rebuild();
    }

    public void Refresh()
    {
        EnsureNotDisposed();
        Rebuild();
    }

    public void RefreshLanguage() => Refresh();

    public void ApplyDocumentChangeSet(CadDocumentChangeSetEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureNotDisposed();

        if (_layer is not null)
        {
            if (args.Contains(CadDocumentChangeKind.LayerChanged) ||
                args.Contains(CadDocumentChangeKind.Reset))
                Rebuild();
            return;
        }

        if (args.Contains(CadDocumentChangeKind.Reset))
        {
            InspectEntities(_workspace.Selection.Selected.ToArray());
            return;
        }

        if (args.Entities.Any(entity => _entities.Contains(entity)))
            Rebuild();
    }

    public void ApplyLayerManagerChange(CadLayerManagerChangedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        EnsureNotDisposed();

        if (_layer is null) return;

        if (args.Kind == CadLayerManagerChangeKind.CurrentChanged)
            InspectLayer(_workspace.Layers.Current);
        else
            Rebuild();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Children.Clear();
        _entities = [];
        _layer = null;
    }

    private void Rebuild()
    {
        if (_disposed || _refreshing)
            return;

        _refreshing = true;
        try
        {
            _host.Children.Clear();
            var targets = Targets();
            if (targets.Count == 0)
            {
                _host.Children.Add(new TextBlock
                {
                    Text = CadLanguageManager.Text(
                        "Cad.Text.SelectionInitial",
                        "Selection: 0"),
                    Foreground = CadTheme.Muted,
                    Margin = new Thickness(8)
                });
                return;
            }

            var title = _layer is not null
                ? $"{CadLanguageManager.Text("Cad.Text.Layers", "Layers")}: {_layer.Name}"
                : _entities.Length == 1
                    ? LocalizeEntityName(_entities[0])
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        CadLanguageManager.Text(
                            "Cad.Text.SelectionWithCount",
                            "Selection [{0}]"),
                        _entities.Length);

            _host.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                Foreground = CadTheme.Text,
                Margin = new Thickness(8, 7, 8, 8)
            });

            var groups = CommonProperties(targets)
                .GroupBy(slot => LocalizeCategory(slot.Descriptor.Category))
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var group in groups)
            {
                _host.Children.Add(new Border
                {
                    Background = CadTheme.PanelAlt,
                    Padding = new Thickness(8, 5),
                    Margin = new Thickness(0, 2, 0, 2),
                    Child = new TextBlock
                    {
                        Text = group.Key,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = CadTheme.Text
                    }
                });

                foreach (var slot in group)
                    _host.Children.Add(CreatePropertyRow(slot));
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private IReadOnlyList<object> Targets() =>
        _layer is not null
            ? [_layer]
            : _entities.Cast<object>().ToArray();

    private static IReadOnlyList<PropertySlot> CommonProperties(
        IReadOnlyList<object> targets)
    {
        if (targets.Count == 0)
            return [];

        var first = BrowsableProperties(targets[0])
            .ToDictionary(descriptor => descriptor.Name, StringComparer.Ordinal);

        foreach (var target in targets.Skip(1))
        {
            var current = BrowsableProperties(target)
                .ToDictionary(descriptor => descriptor.Name, StringComparer.Ordinal);

            foreach (var name in first.Keys.ToArray())
            {
                if (!current.TryGetValue(name, out var descriptor) ||
                    descriptor.PropertyType != first[name].PropertyType)
                    first.Remove(name);
            }
        }

        return first.Values
            .OrderBy(descriptor => descriptor.Category)
            .ThenBy(descriptor => descriptor.DisplayName)
            .Select(descriptor => new PropertySlot(descriptor, targets))
            .ToArray();
    }

    private static IEnumerable<PropertyDescriptor> BrowsableProperties(object target)
    {
        var hideOrientation = target is
            CadCircleEntity or
            CadArcEntity or
            CadEllipseEntity or
            CadRectangleEntity;

        return TypeDescriptor
            .GetProperties(target, true)
            .Cast<PropertyDescriptor>()
            .Where(descriptor =>
                descriptor.IsBrowsable &&
                (!hideOrientation ||
                 !string.Equals(
                     descriptor.Category,
                     "Orientation",
                     StringComparison.OrdinalIgnoreCase)));
    }

    private Control CreatePropertyRow(PropertySlot slot)
    {
        var row = new Grid
        {
            ColumnSpacing = 8,
            Margin = new Thickness(8, 2)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(118)));
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var label = new TextBlock
        {
            Text = CadLanguageManager.Text(
                $"Cad.Property.{slot.Descriptor.Name}",
                slot.Descriptor.DisplayName),
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (!string.IsNullOrWhiteSpace(slot.Descriptor.Description))
        {
            ToolTip.SetTip(
                label,
                CadLanguageManager.Text(
                    $"Cad.Description.{slot.Descriptor.Name}",
                    slot.Descriptor.Description));
        }

        row.Children.Add(label);
        var editor = CreateEditor(slot);
        Grid.SetColumn(editor, 1);
        row.Children.Add(editor);
        return row;
    }

    private Control CreateEditor(PropertySlot slot)
    {
        var descriptor = slot.Descriptor;
        var value = slot.CommonValue(out var isMixed);
        var isLayer =
            descriptor.Name == nameof(CadEntity.Layer) &&
            _entities.Length > 0;

        if (isLayer)
            return CreateLayerEditor(value as string, isMixed);
        if (descriptor.IsReadOnly)
            return ReadOnlyValue(descriptor, value, isMixed);
        if (descriptor.PropertyType == typeof(bool))
            return CreateBooleanEditor(slot, value, isMixed);
        if (descriptor.PropertyType.IsEnum)
            return CreateEnumEditor(slot, value, isMixed);
        if (descriptor.PropertyType == typeof(DrawingColor))
            return CreateColorEditor(slot, value);
        if (CanEditAsText(descriptor))
            return CreateTextEditor(slot, value, isMixed);

        return ReadOnlyValue(descriptor, value, isMixed);
    }

    private Control CreateLayerEditor(string? value, bool isMixed)
    {
        var combo = new ComboBox
        {
            ItemsSource = _workspace.Layers.Layers
                .Select(static layer => layer.Name)
                .ToArray(),
            SelectedItem = isMixed ? null : value,
            PlaceholderText = isMixed ? "—" : null
        };
        combo.Classes.Add("cad-input");
        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedItem is not string layerName)
                return;
            ApplyLayer(layerName);
        };
        return combo;
    }

    private Control CreateBooleanEditor(
        PropertySlot slot,
        object? value,
        bool isMixed)
    {
        var editor = new CheckBox
        {
            IsThreeState = isMixed,
            IsChecked = isMixed ? null : value is bool flag && flag
        };
        editor.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing || editor.IsChecked is not { } next)
                return;
            ApplyValue(slot, next);
        };
        return editor;
    }

    private Control CreateEnumEditor(
        PropertySlot slot,
        object? value,
        bool isMixed)
    {
        var type = slot.Descriptor.PropertyType;
        var items = Enum.GetValues(type)
            .Cast<object>()
            .Select(item => new EnumChoice(
                item,
                CadLanguageManager.Text(
                    $"Cad.Value.{type.Name}.{item}",
                    item.ToString() ?? string.Empty)))
            .ToArray();

        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedItem = isMixed
                ? null
                : items.FirstOrDefault(item => Equals(item.Value, value)),
            PlaceholderText = isMixed ? "—" : null
        };
        combo.Classes.Add("cad-input");
        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedItem is not EnumChoice choice)
                return;
            ApplyValue(slot, choice.Value);
        };
        return combo;
    }

    private Control CreateColorEditor(PropertySlot slot, object? value)
    {
        var drawing = value is DrawingColor color
            ? color
            : DrawingColor.LightGray;
        var button = new Button
        {
            MinHeight = 24,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 2),
            Background = new SolidColorBrush(ToMediaColor(drawing)),
            Content = $"#{drawing.R:X2}{drawing.G:X2}{drawing.B:X2}"
        };
        button.Classes.Add("cad-compact");
        ToolTip.SetTip(
            button,
            CadLanguageManager.Text("Cad.Text.Color", "Color"));
        button.Click += async (_, _) =>
        {
            if (_refreshing)
                return;

            var selected = await CadColorDialog.ShowAsync(_owner, drawing);
            if (selected is { } next)
                ApplyValue(slot, next);
        };
        return button;
    }

    private Control CreateTextEditor(
        PropertySlot slot,
        object? value,
        bool isMixed)
    {
        var descriptor = slot.Descriptor;
        var editor = new TextBox
        {
            Text = isMixed ? string.Empty : ConvertToText(descriptor, value),
            PlaceholderText = isMixed ? "—" : null
        };
        editor.Classes.Add("cad-input");

        void Commit()
        {
            if (_refreshing) return;
            if (!TryConvertFromText(
                    descriptor,
                    editor.Text ?? string.Empty,
                    out var converted))
            {
                Rebuild();
                return;
            }
            ApplyValue(slot, converted);
        }

        editor.KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            Commit();
            e.Handled = true;
        };
        editor.LostFocus += (_, _) => Commit();
        return editor;
    }

    private static Control ReadOnlyValue(
        PropertyDescriptor descriptor,
        object? value,
        bool isMixed) =>
        new TextBlock
        {
            Text = isMixed ? "—" : ConvertToText(descriptor, value),
            Foreground = CadTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

    private static bool CanEditAsText(PropertyDescriptor descriptor)
    {
        var converter = descriptor.Converter;
        return descriptor.PropertyType == typeof(string) ||
               descriptor.PropertyType == typeof(double) ||
               descriptor.PropertyType == typeof(float) ||
               descriptor.PropertyType == typeof(decimal) ||
               descriptor.PropertyType == typeof(int) ||
               descriptor.PropertyType == typeof(long) ||
               converter.CanConvertFrom(typeof(string));
    }

    private static string ConvertToText(
        PropertyDescriptor descriptor,
        object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is double number)
            return number.ToString("0.000", CultureInfo.CurrentCulture);

        try
        {
            return descriptor.Converter.ConvertToString(
                       null,
                       CultureInfo.CurrentCulture,
                       value) ??
                   value.ToString() ??
                   string.Empty;
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static bool TryConvertFromText(
        PropertyDescriptor descriptor,
        string text,
        out object? value)
    {
        try
        {
            if (descriptor.PropertyType == typeof(double))
            {
                if (!double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out var number) &&
                    !double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out number))
                {
                    value = null;
                    return false;
                }

                value = number;
                return double.IsFinite(number);
            }

            value = descriptor.Converter.ConvertFromString(
                null,
                CultureInfo.CurrentCulture,
                text);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private void ApplyLayer(string layerName)
    {
        if (_entities.Length == 0)
            return;

        var wasModified = _workspace.IsModified;
        var historyState = _workspace.History.CurrentStateId;
        try
        {
            _workspace.AssignEntitiesToLayer(_entities, layerName);
        }
        catch
        {
            if (!wasModified &&
                _workspace.History.CurrentStateId == historyState)
                _workspace.MarkSaved();
        }

        Rebuild();
    }

    private void ApplyValue(PropertySlot slot, object? value)
    {
        if (_refreshing)
            return;

        if (_entities.Length > 0)
        {
            _ = CadPropertyTransaction.TryApply(
                _workspace,
                _entities,
                slot.Descriptor.Name,
                value,
                out _);
            Rebuild();
            return;
        }

        if (_layer is not null)
            ApplyLayerValue(slot, value);
    }

    private void ApplyLayerValue(PropertySlot slot, object? value)
    {
        if (_layer is null)
            return;

        var before = _workspace.CaptureLayerState(_layer);
        var wasModified = _workspace.IsModified;
        var historyState = _workspace.History.CurrentStateId;
        try
        {
            slot.Descriptor.SetValue(_layer, value);
            _workspace.RecordLayerStateChange(
                _layer,
                before,
                $"Property {slot.Descriptor.Name}");
        }
        catch
        {
            try
            {
                _layer.RestoreState(before);
            }
            catch
            {
            }

            if (!wasModified &&
                _workspace.History.CurrentStateId == historyState)
                _workspace.MarkSaved();
        }

        Rebuild();
    }

    private string LocalizeEntityName(CadEntity entity) =>
        CadLanguageManager.Text(
            $"Cad.Text.{entity.EntityType.Replace(" ", string.Empty, StringComparison.Ordinal)}",
            entity.Name);

    private static string LocalizeCategory(string category) =>
        CadLanguageManager.Text($"Cad.Category.{category}", category);

    private static MediaColor ToMediaColor(DrawingColor value) =>
        MediaColor.FromArgb(value.A, value.R, value.G, value.B);

    private void EnsureNotDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record PropertySlot(
        PropertyDescriptor Descriptor,
        IReadOnlyList<object> Targets)
    {
        public object? CommonValue(out bool isMixed)
        {
            var first = Descriptor.GetValue(Targets[0]);
            for (var index = 1; index < Targets.Count; index++)
            {
                var current = TypeDescriptor
                    .GetProperties(Targets[index], true)
                    .Find(Descriptor.Name, ignoreCase: false)
                    ?.GetValue(Targets[index]);
                if (!Equals(first, current))
                {
                    isMixed = true;
                    return null;
                }
            }

            isMixed = false;
            return first;
        }
    }

    private sealed record EnumChoice(object Value, string Label)
    {
        public override string ToString() => Label;
    }
}
