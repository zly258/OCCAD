using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OCCAD;

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
        Width = 220;
        MinWidth = 170;
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.Parse("#D3D8DE"));
        BorderThickness = new Thickness(0, 0, 1, 0);

        _filter.PlaceholderText = "筛选模型";
        _filter.Margin = new Thickness(6);
        _filter.TextChanged += (_, _) => Refresh();

        _entities.Margin = new Thickness(4, 0, 4, 4);
        _entities.SelectionChanged += (_, _) => SelectionFromPanel();

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.Children.Add(Header("模型"));
        Grid.SetRow(_filter, 1);
        root.Children.Add(_filter);
        Grid.SetRow(_entities, 2);
        root.Children.Add(_entities);
        Child = root;

        _workspace.Events.Changed += WorkspaceChanged;
        Refresh();
    }

    private static Control Header(string text) =>
        new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F1F3F5")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D3D8DE")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6),
            Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            }
        };

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
        if (_refreshing) return;
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
        Width = 280;
        MinWidth = 230;
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.Parse("#D3D8DE"));
        BorderThickness = new Thickness(1, 0, 0, 0);

        var propertiesScroll = new ScrollViewer
        {
            Content = _propertyHost,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        var layersScroll = new ScrollViewer
        {
            Content = _layerHost,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        _tabs = new TabControl
        {
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
            CadDomainEventKind.DocumentReset)
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
            Margin = new Thickness(8, 1),
            ColumnDefinitions = new ColumnDefinitions("100,*")
        };
        grid.Children.Add(new TextBlock
        {
            Text = PropertyName(property.DisplayName),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#495057")),
            FontSize = 11
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
                if (_refreshing) return;
                Apply(entity, property.Name, check.IsChecked == true);
            };
            return check;
        }

        if (property.EditorType == CadPropertyEditorKind.ByLayer)
            return ByLayerEditor(entity, property);

        return DirectValueEditor(entity, property);
    }

    private Control LayerEditor(CadEntity entity, CadEntityProperty property)
    {
        var combo = new ComboBox
        {
            ItemsSource = _workspace.Layers.Layers.Select(layer => layer.Name).ToArray(),
            SelectedItem = property.Value?.ToString(),
            MinHeight = 26
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (_refreshing || combo.SelectedItem is not string value) return;
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

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };
        var toggle = new CheckBox
        {
            Content = "随层",
            IsChecked = byLayer,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var direct = DirectValueEditor(entity, property);
        direct.IsEnabled = !byLayer;

        toggle.Click += (_, _) =>
        {
            if (_refreshing) return;
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
            var combo = new ComboBox
            {
                ItemsSource = choices,
                SelectedItem = property.Value?.ToString(),
                MinHeight = 26
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (_refreshing || combo.SelectedItem is not string text) return;
                if (TryConvert(property, text, out var value))
                    Apply(entity, property.Name, value);
            };
            return combo;
        }

        if (property.EditorParams.TryGetValue("valueType", out var rawType) &&
            rawType is Type type && type.IsEnum)
        {
            var values = Enum.GetNames(type);
            var combo = new ComboBox
            {
                ItemsSource = values,
                SelectedItem = property.Value?.ToString(),
                MinHeight = 26
            };
            combo.SelectionChanged += (_, _) =>
            {
                if (_refreshing || combo.SelectedItem is not string text) return;
                if (TryConvert(property, text, out var value))
                    Apply(entity, property.Name, value);
            };
            return combo;
        }

        var box = new TextBox
        {
            Text = FormatValue(property.Value),
            MinHeight = 26
        };
        void Commit()
        {
            if (_refreshing || !TryConvert(property, box.Text, out var value)) return;
            Apply(entity, property.Name, value);
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Commit();
            e.Handled = true;
        };
        box.LostFocus += (_, _) => Commit();
        return box;
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
            if (target == typeof(string)) value = text;
            else if (target == typeof(System.Drawing.Color))
            {
                if (!TryParseColor(text, out var color))
                {
                    value = null;
                    return false;
                }
                value = color;
            }
            else if (target.IsEnum) value = Enum.Parse(target, text, true);
            else if (target == typeof(double)) value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (target == typeof(float)) value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (target == typeof(int)) value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else if (target == typeof(long)) value = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else value = Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
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

        if (hex.Length == 6)
        {
            color = System.Drawing.Color.FromArgb(
                255,
                (int)((raw >> 16) & 0xFF),
                (int)((raw >> 8) & 0xFF),
                (int)(raw & 0xFF));
        }
        else
        {
            color = System.Drawing.Color.FromArgb(
                (int)((raw >> 24) & 0xFF),
                (int)((raw >> 16) & 0xFF),
                (int)((raw >> 8) & 0xFF),
                (int)(raw & 0xFF));
        }
        return true;
    }

    private void RefreshLayers()
    {
        _refreshing = true;
        try
        {
            _layerHost.Children.Clear();
            _layerHost.Margin = new Thickness(8, 8, 8, 12);

            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var current = new ComboBox
            {
                ItemsSource = _workspace.Layers.Layers.ToArray(),
                SelectedItem = _workspace.Layers.Current,
                MinHeight = 28
            };
            current.SelectionChanged += (_, _) =>
            {
                if (_refreshing || current.SelectedItem is not CadLayer layer) return;
                _workspace.SetCurrentLayer(layer);
            };
            top.Children.Add(current);

            var add = new Button
            {
                Content = "新建",
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = 52,
                MinHeight = 28
            };
            add.Click += (_, _) =>
            {
                _workspace.AddNextLayer();
                RefreshLayers();
            };
            Grid.SetColumn(add, 1);
            top.Children.Add(add);
            _layerHost.Children.Add(top);

            foreach (var layer in _workspace.Layers.Layers)
                _layerHost.Children.Add(LayerRow(layer));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private Control LayerRow(CadLayer layer)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 6, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto")
        };
        row.Children.Add(new TextBlock
        {
            Text = layer.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = ReferenceEquals(layer, _workspace.Layers.Current)
                ? FontWeight.SemiBold
                : FontWeight.Normal
        });

        var visible = new CheckBox
        {
            Content = "可见",
            IsChecked = layer.Visible,
            VerticalAlignment = VerticalAlignment.Center
        };
        visible.Click += (_, _) =>
            _workspace.SetLayerVisible(layer, visible.IsChecked == true);
        Grid.SetColumn(visible, 1);
        row.Children.Add(visible);

        var locked = new CheckBox
        {
            Content = "锁定",
            IsChecked = layer.Locked,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        locked.Click += (_, _) =>
            _workspace.SetLayerLocked(layer, locked.IsChecked == true);
        Grid.SetColumn(locked, 2);
        row.Children.Add(locked);
        return row;
    }

    private static Control GroupHeader(string text) =>
        new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F3F5F7")),
            Margin = new Thickness(0, 6, 0, 2),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = text,
                FontWeight = FontWeight.SemiBold,
                FontSize = 11
            }
        };

    private static TextBlock Empty(string text) =>
        new()
        {
            Text = text,
            Foreground = Brushes.Gray,
            Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap
        };

    private static TextBlock ValueText(object? value) =>
        new()
        {
            Text = FormatValue(value),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 11
        };

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
