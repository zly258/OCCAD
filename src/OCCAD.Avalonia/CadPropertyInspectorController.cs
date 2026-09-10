using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;
using OcctNet;
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
    private CadSubobjectSelection? _subobject;
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
        _subobject = null;
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
        _subobject = null;
        _layer = layer;
        Rebuild();
    }

    public void InspectSubobject(CadSubobjectSelection selection)
    {
        EnsureNotDisposed();

        if (!selection.IsValid ||
            !_workspace.Document.Entities.Contains(selection.Entity) ||
            !_workspace.Document.IsEntitySelectable(selection.Entity))
        {
            InspectEntities([]);
            return;
        }

        _layer = null;
        _subobject = selection;
        _entities = [selection.Entity];
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
            if (_workspace.Subobjects.Primary is { } primary)
                InspectSubobject(primary);
            else
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
        _subobject = null;
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
                    Margin = new Thickness(5)
                });
                return;
            }

            var title = _layer is not null
                ? $"{CadLanguageManager.Text("Cad.Text.Layers", "Layers")}: {_layer.Name}"
                : _subobject is { } selectedSubobject
                    ? LocalizeSubobjectTitle(selectedSubobject)
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
                Margin = new Thickness(5, 3, 5, 2)
            });

            if (_subobject is { } subobject)
                AddSubobjectDetails(subobject);

            var groups = CommonProperties(targets)
                .GroupBy(slot => slot.Descriptor.Category);

            foreach (var group in groups)
            {
                var groupTitle = LocalizeCategory(group.Key);
                _host.Children.Add(new Border
                {
                    Background = CadTheme.Toolbar,
                    BorderBrush = CadTheme.Border,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(4, 1),
                    Margin = new Thickness(0),
                    Child = new TextBlock
                    {
                        Text = groupTitle,
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

        var common = BrowsableProperties(targets[0])
            .ToList();

        foreach (var target in targets.Skip(1))
        {
            var current = BrowsableProperties(target)
                .ToDictionary(
                    descriptor => descriptor.Name,
                    StringComparer.Ordinal);

            common.RemoveAll(descriptor =>
                !current.TryGetValue(descriptor.Name, out var other) ||
                other.PropertyType != descriptor.PropertyType);
        }

        return common
            .Select(descriptor =>
                new PropertySlot(descriptor, targets))
            .ToArray();
    }

    private static IEnumerable<CadPropertyDescriptor> BrowsableProperties(
        object target) =>
        CadPropertyCatalog.Describe(target);

    private void AddSubobjectDetails(
        CadSubobjectSelection selection)
    {
        var rows = new List<(string Label, string Value)>();

        if (selection.TryGetPathSegment(out var segment))
        {
            rows.Add((
                Text("Cad.Property.SubobjectIndex", "Index"),
                (segment.Index + 1).ToString(CultureInfo.CurrentCulture)));
            rows.Add((
                Text("Cad.Property.SubobjectType", "Type"),
                segment.Type == CadPathSegmentType.Line
                    ? Text("Cad.Text.PathSegmentLine", "Line")
                    : Text("Cad.Text.PathSegmentArc", "Arc")));
            rows.Add((
                Text("Cad.Property.Length", "Length"),
                FormatNumber(segment.Length)));
            rows.Add((
                Text("Cad.Property.StartPoint", "Start"),
                FormatPoint(segment.Start)));
            rows.Add((
                Text("Cad.Property.EndPoint", "End"),
                FormatPoint(segment.End)));
            if (segment.Radius is { } radius)
            {
                rows.Add((
                    Text("Cad.Property.Radius", "Radius"),
                    FormatNumber(radius)));
            }

            AddReadOnlyGroup(
                Text("Cad.Category.Subobject", "Subobject"),
                rows);
            return;
        }

        var engine = _workspace.Engine;
        if (engine is not { IsInitialized: true } ||
            selection.Entity.ViewerShape is not { } owner)
            return;

        OcctShape? temporary = null;
        try
        {
            rows.Add((
                Text("Cad.Property.SubobjectIndex", "Index"),
                (selection.SubshapeIndex + 1)
                    .ToString(CultureInfo.CurrentCulture)));

            switch (selection.SubshapeType)
            {
                case OcctShapeType.Vertex:
                {
                    rows.Add((
                        Text("Cad.Property.SubobjectType", "Type"),
                        Text("Cad.Text.SubobjectVertex", "Vertex")));
                    rows.Add((
                        Text("Cad.Property.Position", "Position"),
                        FormatPoint(
                            engine.GetVertexPoint(
                                owner,
                                selection.SubshapeIndex))));
                    break;
                }

                case OcctShapeType.Edge:
                {
                    var edgeShape = engine.GetSubshapeAt(
                        owner,
                        OcctShapeType.Edge,
                        selection.SubshapeIndex);
                    temporary = edgeShape;
                    var curveType =
                        engine.GetEdgeCurveType(edgeShape);
                    var endpoints =
                        engine.GetEdgeEndpoints(edgeShape);
                    var properties =
                        engine.GetShapeLinearProperties(edgeShape);

                    rows.Add((
                        Text("Cad.Property.SubobjectType", "Type"),
                        Text(
                            $"Cad.Value.OcctCurveType.{curveType}",
                            curveType.ToString())));
                    rows.Add((
                        Text("Cad.Property.Length", "Length"),
                        FormatNumber(properties.Mass)));
                    rows.Add((
                        Text("Cad.Property.StartPoint", "Start"),
                        FormatPoint(endpoints.Start)));
                    rows.Add((
                        Text("Cad.Property.EndPoint", "End"),
                        FormatPoint(endpoints.End)));
                    rows.Add((
                        Text("Cad.Property.Center", "Center"),
                        FormatPoint(properties.CenterOfMass)));
                    break;
                }

                case OcctShapeType.Face:
                {
                    var faceShape = engine.GetSubshapeAt(
                        owner,
                        OcctShapeType.Face,
                        selection.SubshapeIndex);
                    temporary = faceShape;
                    var surfaceType =
                        engine.GetFaceSurfaceType(faceShape);
                    var properties =
                        engine.GetShapeSurfaceProperties(faceShape);
                    rows.Add((
                        Text("Cad.Property.SubobjectType", "Type"),
                        Text(
                            $"Cad.Value.OcctSurfaceType.{surfaceType}",
                            surfaceType.ToString())));
                    rows.Add((
                        Text("Cad.Property.Area", "Area"),
                        FormatNumber(properties.Mass)));
                    rows.Add((
                        Text("Cad.Property.Center", "Center"),
                        FormatPoint(properties.CenterOfMass)));

                    try
                    {
                        var bounds =
                            engine.GetFaceUvBounds(faceShape);
                        var u =
                            (bounds.UMin + bounds.UMax) * 0.5;
                        var v =
                            (bounds.VMin + bounds.VMax) * 0.5;
                        var evaluation =
                            engine.EvaluateFace(
                                faceShape,
                                u,
                                v);
                        rows.Add((
                            Text("Cad.Property.Normal", "Normal"),
                            FormatVector(evaluation.Normal)));
                    }
                    catch (Exception exception)
                        when (IsRecoverable(exception))
                    {
                    }

                    break;
                }

                default:
                    rows.Add((
                        Text("Cad.Property.SubobjectType", "Type"),
                        Text(
                            $"Cad.Text.Subobject{selection.SubshapeType}",
                            selection.SubshapeType.ToString())));
                    break;
            }
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
            rows.Add((
                Text("Cad.Property.Details", "Details"),
                exception.GetBaseException().Message));
        }
        finally
        {
            if (temporary is { } temporaryShape)
                TryDeleteTemporaryShape(
                    engine,
                    temporaryShape);
        }

        AddReadOnlyGroup(
            Text("Cad.Category.Subobject", "Subobject"),
            rows);
    }

    private void AddReadOnlyGroup(
        string title,
        IReadOnlyList<(string Label, string Value)> rows)
    {
        if (rows.Count == 0)
            return;

        _host.Children.Add(new Border
        {
            Background = CadTheme.Toolbar,
            BorderBrush = CadTheme.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(4, 1),
            Margin = new Thickness(0),
            Child = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                Foreground = CadTheme.Text
            }
        });

        foreach (var row in rows)
            _host.Children.Add(
                CreateReadOnlyRow(
                    row.Label,
                    row.Value));
    }

    private static Control CreateReadOnlyRow(
        string label,
        string value)
    {
        var row = new Grid
        {
            ColumnSpacing = 4,
            Margin = new Thickness(4, 0)
        };
        row.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(CadTheme.PropertyLabelWidth)));
        row.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));

        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = CadTheme.Text,
            VerticalAlignment =
                VerticalAlignment.Center,
            TextTrimming =
                TextTrimming.CharacterEllipsis
        });

        var text = new TextBlock
        {
            Text = value,
            Foreground = CadTheme.Muted,
            VerticalAlignment =
                VerticalAlignment.Center,
            TextTrimming =
                TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private static string Text(
        string key,
        string fallback) =>
        CadLanguageManager.Text(key, fallback);

    private static string FormatNumber(double value) =>
        value.ToString(
            "0.######",
            CultureInfo.CurrentCulture);

    private static string FormatPoint(OcctPoint3d point) =>
        string.Format(
            CultureInfo.CurrentCulture,
            "({0:0.######}, {1:0.######}, {2:0.######})",
            point.X,
            point.Y,
            point.Z);

    private static string FormatVector(OcctVector3d vector) =>
        FormatPoint(
            new OcctPoint3d(
                vector.X,
                vector.Y,
                vector.Z));

    private static void TryDeleteTemporaryShape(
        OcctEngine engine,
        OcctShape shape)
    {
        try
        {
            if (engine.ContainsObject(shape.Id))
                engine.Delete(shape);
        }
        catch (Exception exception)
            when (IsRecoverable(exception))
        {
        }
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private Control CreatePropertyRow(PropertySlot slot)
    {
        var row = new Grid
        {
            ColumnSpacing = 4,
            Margin = new Thickness(4, 0)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CadTheme.PropertyLabelWidth)));
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var label = new TextBlock
        {
            Text = CadLanguageManager.Text(
                slot.Descriptor.Value.DisplayKey,
                slot.Descriptor.DisplayName),
            Foreground = CadTheme.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = CadTheme.FontSize
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
        var semantic = descriptor.Value.Semantic;
        var isLayer =
            semantic == CadValueSemantic.Layer &&
            _entities.Length > 0;

        if (isLayer)
            return CreateLayerEditor(value as string, isMixed);
        if (descriptor.IsReadOnly)
            return ReadOnlyValue(descriptor, value, isMixed);

        var byLayerProperty = descriptor.Name switch
        {
            nameof(CadEntity.Color) =>
                nameof(CadEntity.ColorByLayer),
            nameof(CadEntity.LineStyle) =>
                nameof(CadEntity.LineStyleByLayer),
            nameof(CadEntity.LineWidth) =>
                nameof(CadEntity.LineWidthByLayer),
            _ => null
        };
        if (byLayerProperty is not null &&
            _entities.Length > 0)
        {
            return CreateByLayerEditor(
                slot,
                byLayerProperty,
                value,
                isMixed);
        }

        if (semantic == CadValueSemantic.Boolean)
            return CreateBooleanEditor(slot, value, isMixed);
        if (semantic is CadValueSemantic.Enum or CadValueSemantic.Choice)
            return CreateEnumEditor(slot, value, isMixed);
        if (semantic == CadValueSemantic.Color)
            return CreateColorEditor(slot, value);
        if (CanEditAsText(descriptor))
            return CreateTextEditor(slot, value, isMixed);

        return ReadOnlyValue(descriptor, value, isMixed);
    }

    private Control CreateByLayerEditor(
        PropertySlot slot,
        string byLayerProperty,
        object? value,
        bool isMixed)
    {
        var flags = _entities
            .Select(entity =>
                TypeDescriptor
                    .GetProperties(entity, true)
                    .Find(
                        byLayerProperty,
                        ignoreCase: false)?
                    .GetValue(entity) as bool?)
            .ToArray();

        var first = flags.FirstOrDefault();
        var mixedByLayer =
            flags.Any(flag => flag != first);

        var byLayer = new CheckBox
        {
            Content = CadLanguageManager.Text(
                "Cad.Text.ByLayer",
                "ByLayer"),
            IsThreeState = mixedByLayer,
            IsChecked = mixedByLayer
                ? null
                : first == true,
            VerticalAlignment =
                VerticalAlignment.Center
        };
        ToolTip.SetTip(
            byLayer,
            CadLanguageManager.Text(
                "Cad.Text.ByLayer",
                "ByLayer"));
        byLayer.IsCheckedChanged += (_, _) =>
        {
            if (_refreshing ||
                byLayer.IsChecked is not { } next)
                return;

            ApplyHiddenEntityProperty(
                byLayerProperty,
                next);
        };

        Control valueEditor =
            slot.Descriptor.Value.Semantic switch
            {
                CadValueSemantic.Color =>
                    CreateColorEditor(
                        slot,
                        value,
                        showByLayerText: false),
                CadValueSemantic.Enum or
                CadValueSemantic.Choice =>
                    CreateEnumEditor(
                        slot,
                        value,
                        isMixed),
                _ when CanEditAsText(slot.Descriptor) =>
                    CreateTextEditor(
                        slot,
                        value,
                        isMixed),
                _ =>
                    ReadOnlyValue(
                        slot.Descriptor,
                        value,
                        isMixed)
            };

        var grid = new Grid
        {
            ColumnSpacing = 4
        };
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(
            new ColumnDefinition(
                new GridLength(
                    1,
                    GridUnitType.Star)));
        grid.Children.Add(byLayer);
        Grid.SetColumn(valueEditor, 1);
        grid.Children.Add(valueEditor);
        return grid;
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

    private Control CreateColorEditor(
        PropertySlot slot,
        object? value,
        bool showByLayerText = true)
    {
        var entityTargets = slot.Targets
            .OfType<CadEntity>()
            .ToArray();
        var byLayer =
            entityTargets.Length > 0 &&
            entityTargets.All(entity => entity.ColorByLayer);
        var drawing =
            byLayer && entityTargets.Length > 0
                ? _workspace.Document.ResolveAppearance(
                    entityTargets[0]).Color
                : value is DrawingColor color
                    ? color
                    : DrawingColor.LightGray;
        var button = new Button
        {
            MinHeight = CadTheme.ControlHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(4, 0),
            Background = new SolidColorBrush(ToMediaColor(drawing)),
            Foreground = ColorTextBrush(drawing),
            Content = byLayer && showByLayerText
                ? $"{CadLanguageManager.Text("Cad.Text.ByLayer", "ByLayer")} · #{drawing.R:X2}{drawing.G:X2}{drawing.B:X2}"
                : $"#{drawing.R:X2}{drawing.G:X2}{drawing.B:X2}"
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
            if (e.Key != global::Avalonia.Input.Key.Enter) return;
            Commit();
            e.Handled = true;
        };
        editor.LostFocus += (_, _) => Commit();
        return editor;
    }

    private static Control ReadOnlyValue(
        CadPropertyDescriptor descriptor,
        object? value,
        bool isMixed) =>
        new TextBlock
        {
            Text = isMixed ? "—" : ConvertToText(descriptor, value),
            Foreground = CadTheme.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

    private static bool CanEditAsText(CadPropertyDescriptor descriptor)
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
        CadPropertyDescriptor descriptor,
        object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is double number)
            return number.ToString("0.######", CultureInfo.CurrentCulture);

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
        CadPropertyDescriptor descriptor,
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

                if (!double.IsFinite(number) ||
                    descriptor.Value.Minimum is { } minimum &&
                    number < minimum ||
                    descriptor.Value.Maximum is { } maximum &&
                    number > maximum)
                {
                    value = null;
                    return false;
                }

                value = number;
                return true;
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

    private void ApplyHiddenEntityProperty(
        string propertyName,
        object value)
    {
        if (_entities.Length == 0)
            return;

        if (!CadPropertyTransaction.TryApply(
                _workspace,
                _entities,
                propertyName,
                value,
                out var error) &&
            error is not null)
        {
            ShowPropertyError(error);
        }

        Rebuild();
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
        catch (Exception exception)
        {
            if (!wasModified &&
                _workspace.History.CurrentStateId == historyState)
                _workspace.MarkSaved();
            ShowPropertyError(exception);
        }

        Rebuild();
    }

    private void ApplyValue(PropertySlot slot, object? value)
    {
        if (_refreshing)
            return;

        if (_entities.Length > 0)
        {
            if (!CadPropertyTransaction.TryApply(
                    _workspace,
                    _entities,
                    slot.Descriptor.Name,
                    value,
                    out var error) &&
                error is not null)
            {
                ShowPropertyError(error);
            }
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
        catch (Exception exception)
        {
            try
            {
                _layer.RestoreState(before);
            }
            catch (Exception restoreFailure)
            {
                exception = new AggregateException(
                    "Property apply and rollback both failed.",
                    exception,
                    restoreFailure);
            }

            if (!wasModified &&
                _workspace.History.CurrentStateId == historyState)
                _workspace.MarkSaved();
            ShowPropertyError(exception);
        }

        Rebuild();
    }

    private void ShowPropertyError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var messages = error is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions
                .Select(static item => item.GetBaseException().Message)
                .Where(static message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.CurrentCulture)
                .ToArray()
            : [error.GetBaseException().Message];

        _ = CadMessageDialog.ShowAsync(
            _owner,
            CadLanguageManager.Text(
                "Cad.Text.PropertyUpdateFailed",
                "Property Update Failed"),
            string.Join(Environment.NewLine, messages),
            kind: CadMessageDialogKind.Error);
    }

    private string LocalizeSubobjectTitle(
        CadSubobjectSelection selection)
    {
        var entityName = LocalizeEntityName(selection.Entity);
        if (selection.TryGetPathSegment(out var segment))
        {
            var type = segment.Type == CadPathSegmentType.Line
                ? CadLanguageManager.Text(
                    "Cad.Text.PathSegmentLine",
                    "Line")
                : CadLanguageManager.Text(
                    "Cad.Text.PathSegmentArc",
                    "Arc");
            var segmentText = string.Format(
                CultureInfo.CurrentCulture,
                CadLanguageManager.Text(
                    "Cad.Text.PathSegmentStatus",
                    "Path segment {0}: {1}"),
                segment.Index + 1,
                type);
            return $"{entityName} · {segmentText}";
        }

        var typeName = CadLanguageManager.Text(
            $"Cad.Text.Subobject{selection.SubshapeType}",
            selection.SubshapeType.ToString());
        return string.Format(
            CultureInfo.CurrentCulture,
            CadLanguageManager.Text(
                "Cad.Text.SubobjectTitle",
                "{0} · {1} {2}"),
            entityName,
            typeName,
            selection.SubshapeIndex + 1);
    }

    private string LocalizeEntityName(CadEntity entity) =>
        CadLanguageManager.Text(
            $"Cad.Text.{entity.EntityType.Replace(" ", string.Empty, StringComparison.Ordinal)}",
            entity.Name);

    private static string LocalizeCategory(string category) =>
        CadLanguageManager.Text($"Cad.Category.{category}", category);

    private static MediaColor ToMediaColor(DrawingColor value) =>
        MediaColor.FromArgb(value.A, value.R, value.G, value.B);

    private static IBrush ColorTextBrush(DrawingColor value)
    {
        var luminance =
            0.2126 * value.R +
            0.7152 * value.G +
            0.0722 * value.B;
        return luminance < 140.0
            ? Brushes.White
            : CadTheme.Text;
    }

    private void EnsureNotDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record PropertySlot(
        CadPropertyDescriptor Descriptor,
        IReadOnlyList<object> Targets)
    {
        public object? CommonValue(out bool isMixed)
        {
            var first = Descriptor.GetValue(Targets[0]);
            for (var index = 1; index < Targets.Count; index++)
            {
                var current = CadPropertyCatalog
                    .Describe(Targets[index])
                    .FirstOrDefault(property =>
                        string.Equals(
                            property.Name,
                            Descriptor.Name,
                            StringComparison.Ordinal))
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
