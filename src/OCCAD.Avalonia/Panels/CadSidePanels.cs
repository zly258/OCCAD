using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using OCCAD;
using OcctNet;

namespace OCCAD.Avalonia;

internal sealed class CadModelPanel : Border
{
    private readonly CadWorkspace _workspace;
    private readonly TextBox _filter = new();
    private readonly ListBox _entities = new();
    private bool _refreshing;

    public CadModelPanel(CadWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        Width = CadUi.ModelPanelWidth;
        MinWidth = 170;
        Background = CadUi.Panel;
        BorderBrush = CadUi.Border;
        BorderThickness = new Thickness(0, 0, 1, 0);

        _filter.PlaceholderText = "筛选模型";
        _filter.Margin = new Thickness(6);
        _filter.MinHeight = CadUi.CompactControlHeight;
        _filter.FontSize = CadUi.UiFontSize;
        _filter.TextChanged += (_, _) => Refresh();

        _entities.Margin = new Thickness(4, 0, 4, 4);
        _entities.FontSize = CadUi.UiFontSize;
        _entities.SelectionChanged += (_, _) => SelectionFromPanel();

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*")
        };
        root.Children.Add(CadUi.CreatePanelHeader("模型"));
        Grid.SetRow(_filter, 1);
        root.Children.Add(_filter);
        Grid.SetRow(_entities, 2);
        root.Children.Add(_entities);
        Child = root;

        _workspace.Events.Changed += WorkspaceChanged;
        Refresh();
    }

    private void WorkspaceChanged(object? sender, CadDomainEventArgs args)
    {
        if (args.Kind is CadDomainEventKind.EntityAdded or
            CadDomainEventKind.EntityRemoved or
            CadDomainEventKind.EntityMetadataChanged or
            CadDomainEventKind.DocumentReset or
            CadDomainEventKind.SelectionChanged or
            CadDomainEventKind.LayerChanged)
            Refresh();
    }

    private void Refresh()
    {
        _refreshing = true;
        try
        {
            var query = _filter.Text?.Trim();
            var items = _workspace.Document.Entities
                .Where(entity => string.IsNullOrWhiteSpace(query) ||
                    entity.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    entity.EntityType.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(entity => new ModelItem(entity))
                .ToArray();
            _entities.ItemsSource = items;

            var primary = _workspace.Selection.Primary;
            _entities.SelectedItem = primary is null
                ? null
                : items.FirstOrDefault(item => ReferenceEquals(item.Entity, primary));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SelectionFromPanel()
    {
        if (_refreshing)
            return;

        if (_entities.SelectedItem is ModelItem item)
            _workspace.Selection.Select(item.Entity);
        else
            _workspace.Selection.Clear();
    }

    private sealed record ModelItem(CadEntity Entity)
    {
        public override string ToString() =>
            string.Equals(Entity.Name, Entity.EntityType, StringComparison.OrdinalIgnoreCase)
                ? Entity.EntityType
                : $"{Entity.Name}  ·  {Entity.EntityType}";
    }
}

internal sealed class CadInspectorPanel : Border
{
    private readonly CadWorkspace _workspace;
    private readonly Action<string> _feedback;
    private readonly StackPanel _propertyHost = new();
    private readonly StackPanel _layerHost = new();
    private readonly TabControl _tabs;
    private bool _refreshing;

    public CadInspectorPanel(CadWorkspace workspace, Action<string> feedback)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
        Width = CadUi.InspectorPanelWidth;
        MinWidth = 230;
        Background = CadUi.Panel;
        BorderBrush = CadUi.Border;
        BorderThickness = new Thickness(1, 0, 0, 0);

        var propertiesScroll = new ScrollViewer
        {
            Content = _propertyHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var layersScroll = new ScrollViewer
        {
            Content = _layerHost,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _tabs = new TabControl
        {
            FontSize = CadUi.UiFontSize,
            ItemsSource = new[]
            {
                new TabItem { Header = "属性", Content = propertiesScroll },
                new TabItem { Header = "图层", Content = layersScroll }
            }
        };
        Child = _tabs;

        _workspace.Events.Changed += WorkspaceChanged;
        RefreshAll();
    }

    public void ShowProperties() => _tabs.SelectedIndex = 0;
    public void ShowLayers() => _tabs.SelectedIndex = 1;

    private void WorkspaceChanged(object? sender, CadDomainEventArgs args)
    {
        if (args.Kind is CadDomainEventKind.SelectionChanged or
            CadDomainEventKind.EntityGeometryChanged or
            CadDomainEventKind.EntityAppearanceChanged or
            CadDomainEventKind.EntityMetadataChanged or
            CadDomainEventKind.EntityAdded or
            CadDomainEventKind.EntityRemoved or
            CadDomainEventKind.DocumentReset or
            CadDomainEventKind.LayerChanged)
            RefreshProperties();

        if (args.Kind is CadDomainEventKind.LayerChanged or
            CadDomainEventKind.DocumentReset or
            CadDomainEventKind.EntityAdded or
            CadDomainEventKind.EntityRemoved)
            RefreshLayers();
    }

    private void RefreshAll()
    {
        RefreshProperties();
        RefreshLayers();
    }

    private void RefreshProperties()
    {
        _refreshing = true;
        try
        {
            _propertyHost.Children.Clear();
            _propertyHost.Margin = new Thickness(0, 4, 0, 8);

            var selected = _workspace.Selection.Selected;
            if (selected.Count == 0)
            {
                _propertyHost.Children.Add(Empty("未选择对象"));
                return;
            }
            if (selected.Count > 1)
            {
                _propertyHost.Children.Add(Empty($"已选择 {selected.Count} 个对象"));
                return;
            }

            var entity = selected[0];
            string? group = null;
            foreach (var property in CadPropertyService.Describe(_workspace, entity))
            {
                if (string.Equals(property.Name, nameof(CadEntity.Id), StringComparison.Ordinal))
                    continue;

                if (!string.Equals(group, property.Group, StringComparison.Ordinal))
                {
                    group = property.Group;
                    _propertyHost.Children.Add(GroupHeader(GroupName(group)));
                }
                _propertyHost.Children.Add(PropertyRow(entity, property));
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Control PropertyRow(CadEntity entity, CadEntityProperty property)
    {
        var grid = new Grid
        {
            Margin = new Thickness(7, 1),
            ColumnDefinitions = new ColumnDefinitions("96,*")
        };
        grid.Children.Add(new TextBlock
        {
            Text = PropertyName(property.DisplayName),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = CadUi.Muted,
            FontSize = CadUi.UiFontSize
        });

        var editor = CreateEditor(entity, property);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private Control CreateEditor(CadEntity entity, CadEntityProperty property)
    {
        if (property.ReadOnly || property.EditorType == CadPropertyEditorKind.ReadOnly)
            return ValueText(property.Value);
        if (property.EditorType == CadPropertyEditorKind.Layer)
            return LayerEditor(entity, property);

        if (property.EditorType == CadPropertyEditorKind.Boolean && property.Value is bool boolean)
        {
            var check = new CheckBox
            {
                IsChecked = boolean,
                VerticalAlignment = VerticalAlignment.Center
            };
            check.Click += (_, _) =>
            {
                if (!_refreshing)
                    Apply(entity, property.Name, check.IsChecked == true);
            };
            return check;
        }

        return property.EditorType == CadPropertyEditorKind.ByLayer
            ? ByLayerEditor(entity, property)
            : DirectValueEditor(entity, property);
    }

    private Control LayerEditor(CadEntity entity, CadEntityProperty property)
    {
        var combo = new ComboBox
        {
            ItemsSource = _workspace.Layers.Layers.Select(layer => layer.Name).ToArray(),
            SelectedItem = property.Value?.ToString(),
            MinHeight = CadUi.CompactControlHeight,
            FontSize = CadUi.UiFontSize
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedItem is not string value)
                return;
            Apply(entity, property.Name, value);
        };
        return combo;
    }

    private Control ByLayerEditor(CadEntity entity, CadEntityProperty property)
    {
        if (!property.EditorParams.TryGetValue("byLayerProperty", out var rawName) ||
            rawName is not string byLayerName)
            return DirectValueEditor(entity, property);

        var descriptor = TypeDescriptor.GetProperties(entity, true)
            .Find(byLayerName, ignoreCase: false);
        var byLayer = descriptor?.GetValue(entity) as bool? ?? false;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var toggle = new CheckBox
        {
            Content = "随层",
            IsChecked = byLayer,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = CadUi.UiFontSize
        };
        var direct = DirectValueEditor(entity, property);
        direct.IsEnabled = !byLayer;

        toggle.Click += (_, _) =>
        {
            if (_refreshing)
                return;
            var enabled = toggle.IsChecked == true;
            if (CadPropertyService.TryApply(
                    _workspace,
                    [entity],
                    byLayerName,
                    enabled,
                    out var error))
            {
                direct.IsEnabled = !enabled;
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                _feedback(error);
            }
        };

        grid.Children.Add(toggle);
        Grid.SetColumn(direct, 1);
        grid.Children.Add(direct);
        return grid;
    }

    private Control DirectValueEditor(CadEntity entity, CadEntityProperty property)
    {
        if (property.EditorType == CadPropertyEditorKind.Choice &&
            property.EditorParams.TryGetValue("choices", out var rawChoices) &&
            rawChoices is string[] choices)
        {
            return ChoiceEditor(entity, property, choices);
        }

        if (property.EditorParams.TryGetValue("valueType", out var rawType) &&
            rawType is Type type && type.IsEnum)
        {
            return ChoiceEditor(entity, property, Enum.GetNames(type));
        }

        var box = CompactTextBox(FormatValue(property.Value));
        void Commit()
        {
            if (_refreshing || !TryConvert(property, box.Text, out var value))
                return;
            Apply(entity, property.Name, value);
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            Commit();
            e.Handled = true;
        };
        box.LostFocus += (_, _) => Commit();
        return box;
    }

    private Control ChoiceEditor(
        CadEntity entity,
        CadEntityProperty property,
        IReadOnlyList<string> choices)
    {
        var combo = new ComboBox
        {
            ItemsSource = choices,
            SelectedItem = property.Value?.ToString(),
            MinHeight = CadUi.CompactControlHeight,
            FontSize = CadUi.UiFontSize
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedItem is not string text)
                return;
            if (TryConvert(property, text, out var value))
                Apply(entity, property.Name, value);
        };
        return combo;
    }

    private void Apply(CadEntity entity, string propertyName, object? value)
    {
        if (CadPropertyService.TryApply(
                _workspace,
                [entity],
                propertyName,
                value,
                out var error))
        {
            _feedback($"已更新 {PropertyName(propertyName)}");
        }
        else if (!string.IsNullOrWhiteSpace(error))
        {
            _feedback(error);
        }
    }

    private void RefreshLayers()
    {
        _refreshing = true;
        try
        {
            _layerHost.Children.Clear();
            _layerHost.Margin = new Thickness(7, 7, 7, 10);

            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var current = new ComboBox
            {
                ItemsSource = _workspace.Layers.Layers.ToArray(),
                SelectedItem = _workspace.Layers.Current,
                MinHeight = CadUi.CompactControlHeight,
                FontSize = CadUi.UiFontSize
            };
            current.SelectionChanged += (_, _) =>
            {
                if (_refreshing || current.SelectedItem is not CadLayer layer)
                    return;
                _workspace.SetCurrentLayer(layer);
            };
            top.Children.Add(current);

            var add = CompactButton("新建");
            add.Margin = new Thickness(4, 0, 0, 0);
            add.Click += (_, _) =>
            {
                try
                {
                    _workspace.AddNextLayer();
                    RefreshLayers();
                }
                catch (Exception exception)
                {
                    _feedback(exception.Message);
                }
            };
            Grid.SetColumn(add, 1);
            top.Children.Add(add);
            _layerHost.Children.Add(top);

            foreach (var layer in _workspace.Layers.Layers)
                _layerHost.Children.Add(LayerCard(layer));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Control LayerCard(CadLayer layer)
    {
        var host = new StackPanel { Margin = new Thickness(0, 7, 0, 0), Spacing = 3 };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };

        var name = CompactTextBox(layer.Name);
        name.FontWeight = ReferenceEquals(layer, _workspace.Layers.Current)
            ? FontWeight.SemiBold
            : FontWeight.Normal;
        void CommitName()
        {
            if (_refreshing || string.Equals(name.Text?.Trim(), layer.Name, StringComparison.Ordinal))
                return;
            try
            {
                _workspace.RenameLayer(layer, name.Text ?? string.Empty);
                RefreshLayers();
            }
            catch (Exception exception)
            {
                name.Text = layer.Name;
                _feedback(exception.Message);
            }
        }
        name.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            CommitName();
            e.Handled = true;
        };
        name.LostFocus += (_, _) => CommitName();
        row.Children.Add(name);

        var visible = new CheckBox
        {
            Content = "显",
            IsChecked = layer.Visible,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = CadUi.UiFontSize
        };
        visible.Click += (_, _) =>
        {
            if (!_refreshing)
                _workspace.SetLayerVisible(layer, visible.IsChecked == true);
        };
        Grid.SetColumn(visible, 1);
        row.Children.Add(visible);

        var locked = new CheckBox
        {
            Content = "锁",
            IsChecked = layer.Locked,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = CadUi.UiFontSize
        };
        locked.Click += (_, _) =>
        {
            if (!_refreshing)
                _workspace.SetLayerLocked(layer, locked.IsChecked == true);
        };
        Grid.SetColumn(locked, 2);
        row.Children.Add(locked);

        var remove = CompactButton("删");
        remove.IsEnabled = _workspace.Layers.Layers.Count > 1;
        remove.Margin = new Thickness(4, 0, 0, 0);
        remove.Click += (_, _) =>
        {
            try
            {
                _workspace.RemoveLayer(layer);
                RefreshLayers();
            }
            catch (Exception exception)
            {
                _feedback(exception.Message);
            }
        };
        Grid.SetColumn(remove, 3);
        row.Children.Add(remove);
        host.Children.Add(row);

        var style = new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,64") };
        var color = CompactTextBox($"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}");
        color.PlaceholderText = "#RRGGBB";
        void CommitColor()
        {
            if (_refreshing || !TryParseColor(color.Text?.Trim() ?? string.Empty, out var parsed))
                return;
            try
            {
                _workspace.SetLayerColor(layer, parsed);
            }
            catch (Exception exception)
            {
                _feedback(exception.Message);
            }
        }
        color.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            CommitColor();
            e.Handled = true;
        };
        color.LostFocus += (_, _) => CommitColor();
        style.Children.Add(color);

        var lineStyle = new ComboBox
        {
            ItemsSource = Enum.GetValues<OcctLineStyle>(),
            SelectedItem = layer.LineStyle,
            MinHeight = CadUi.CompactControlHeight,
            FontSize = CadUi.UiFontSize,
            Margin = new Thickness(4, 0, 0, 0)
        };
        lineStyle.SelectionChanged += (_, _) =>
        {
            if (_refreshing || lineStyle.SelectedItem is not OcctLineStyle value)
                return;
            _workspace.SetLayerLineStyle(layer, value);
        };
        Grid.SetColumn(lineStyle, 1);
        style.Children.Add(lineStyle);

        var width = CompactTextBox(layer.LineWidth.ToString("0.###", CultureInfo.InvariantCulture));
        width.Margin = new Thickness(4, 0, 0, 0);
        void CommitWidth()
        {
            if (_refreshing ||
                !CadValueTextConverter.TryParseFiniteDouble(width.Text ?? string.Empty, out var value) ||
                value <= 0.0)
                return;
            try
            {
                _workspace.SetLayerLineWidth(layer, value);
            }
            catch (Exception exception)
            {
                _feedback(exception.Message);
            }
        }
        width.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            CommitWidth();
            e.Handled = true;
        };
        width.LostFocus += (_, _) => CommitWidth();
        Grid.SetColumn(width, 2);
        style.Children.Add(width);
        host.Children.Add(style);

        return new Border
        {
            Background = ReferenceEquals(layer, _workspace.Layers.Current)
                ? CadUi.Surface
                : CadUi.Panel,
            BorderBrush = CadUi.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5),
            Child = host
        };
    }

    private static TextBox CompactTextBox(string? text) =>
        new()
        {
            Text = text,
            MinHeight = CadUi.CompactControlHeight,
            FontSize = CadUi.UiFontSize,
            VerticalContentAlignment = VerticalAlignment.Center
        };

    private static Button CompactButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Foreground = CadUi.Text,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        CadUi.ConfigureCompactButton(button);
        return button;
    }

    private static Control GroupHeader(string text) =>
        new Border
        {
            Background = CadUi.Header,
            Margin = new Thickness(0, 5, 0, 2),
            Padding = new Thickness(7, 3),
            Child = new TextBlock
            {
                Text = text,
                Foreground = CadUi.Text,
                FontWeight = FontWeight.SemiBold,
                FontSize = CadUi.HeaderFontSize
            }
        };

    private static TextBlock Empty(string text) =>
        new()
        {
            Text = text,
            Foreground = CadUi.Muted,
            Margin = new Thickness(10),
            FontSize = CadUi.UiFontSize,
            TextWrapping = TextWrapping.Wrap
        };

    private static TextBlock ValueText(object? value) =>
        new()
        {
            Text = FormatValue(value),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = CadUi.Text,
            FontSize = CadUi.UiFontSize
        };

    private static bool TryConvert(
        CadEntityProperty property,
        string? text,
        out object? value)
    {
        text = text?.Trim() ?? string.Empty;
        if (!property.EditorParams.TryGetValue("valueType", out var rawType) ||
            rawType is not Type type)
        {
            value = text;
            return true;
        }

        try
        {
            var target = Nullable.GetUnderlyingType(type) ?? type;
            if (target == typeof(string))
                value = text;
            else if (target == typeof(System.Drawing.Color))
            {
                if (!TryParseColor(text, out var color))
                {
                    value = null;
                    return false;
                }
                value = color;
            }
            else if (target.IsEnum)
                value = Enum.Parse(target, text, true);
            else if (target == typeof(double))
                value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (target == typeof(float))
                value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (target == typeof(int))
                value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else if (target == typeof(long))
                value = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else
                value = Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static bool TryParseColor(string text, out System.Drawing.Color color)
    {
        color = default;
        var hex = text.StartsWith('#') ? text[1..] : text;
        if (hex.Length is not (6 or 8) ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
            return false;

        color = hex.Length == 6
            ? System.Drawing.Color.FromArgb(
                255,
                (int)((raw >> 16) & 0xFF),
                (int)((raw >> 8) & 0xFF),
                (int)(raw & 0xFF))
            : System.Drawing.Color.FromArgb(
                (int)((raw >> 24) & 0xFF),
                (int)((raw >> 16) & 0xFF),
                (int)((raw >> 8) & 0xFF),
                (int)(raw & 0xFF));
        return true;
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            double number => number.ToString("0.###", CultureInfo.InvariantCulture),
            float number => number.ToString("0.###", CultureInfo.InvariantCulture),
            System.Drawing.Color color => $"#{color.R:X2}{color.G:X2}{color.B:X2}",
            _ => value.ToString() ?? string.Empty
        };

    private static string GroupName(string group) => group switch
    {
        "General" => "常规",
        "Geometry" => "几何",
        "Position" => "位置",
        "Display" => "显示",
        "Data" => "数据",
        _ => group
    };

    private static string PropertyName(string name) => name switch
    {
        "Type" or "EntityType" => "类型",
        "Name" => "名称",
        "Layer" or "LayerId" => "图层",
        "Visible" => "可见",
        "Selectable" => "可选择",
        "Color" => "颜色",
        "LineWidth" => "线宽",
        "LineStyle" => "线型",
        "Transparency" => "透明度",
        "DisplayMode" => "显示模式",
        "Material" => "材质",
        "Length" => "长度",
        "Width" => "宽度",
        "Height" => "高度",
        "Radius" => "半径",
        "Diameter" => "直径",
        "Area" => "面积",
        "Volume" => "体积",
        _ => name
    };
}
