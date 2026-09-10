using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace OCCAD.Avalonia;

/// <summary>
/// Presentation-only helpers for OCCAD's compact three-row industrial Ribbon.
/// Adapted from the ModelScript Avalonia Ribbon structure; it deliberately knows
/// nothing about CAD actions, tools, documents or selection state.
/// </summary>
internal static class CadRibbonBar
{
    public static Border CreateShell(
        TabControl tabs,
        Control? rightContent = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        var container = new Grid();
        container.Children.Add(tabs);

        if (rightContent is not null)
        {
            rightContent.HorizontalAlignment = HorizontalAlignment.Right;
            rightContent.VerticalAlignment = VerticalAlignment.Top;
            rightContent.Margin = new Thickness(0, 2, 8, 0);
            container.Children.Add(rightContent);
        }

        return new Border
        {
            Classes = { "cad-ribbon-shell" },
            Background = CadUi.Surface,
            BorderBrush = CadUi.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = container
        };
    }

    public static TabItem CreateTab(
        string title,
        params Control[] groups)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2, 1, 2, 0)
        };

        foreach (var group in groups.Where(static value => value is not null))
            panel.Children.Add(group);

        return new TabItem
        {
            Header = title,
            Classes = { "cad-ribbon-tab" },
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = panel
            }
        };
    }

    public static Border CreateGroup(
        string title,
        IEnumerable<Control> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        var list = controls
            .Where(static value => value is not null)
            .ToList();
        var buttons = new Grid();
        var columnCount = Math.Max(1, (list.Count + 2) / 3);

        for (var row = 0; row < 3; row++)
            buttons.RowDefinitions.Add(
                new RowDefinition(new GridLength(CadUi.RibbonRowHeight)));
        for (var column = 0; column < columnCount; column++)
            buttons.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Auto));

        for (var index = 0; index < list.Count; index++)
        {
            var control = list[index];
            control.Classes.Add("cad-ribbon-button");
            control.HorizontalAlignment = HorizontalAlignment.Left;
            control.Margin = new Thickness(0, 0, 2, 0);
            Grid.SetRow(control, index % 3);
            Grid.SetColumn(control, index / 3);
            buttons.Children.Add(control);
        }

        var caption = new TextBlock
        {
            Text = title,
            Classes = { "cad-ribbon-caption" },
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = CadUi.Muted,
            FontFamily = CadUi.UiFontFamily,
            FontSize = 10
        };

        var stack = new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(4, 1)
        };
        stack.Children.Add(buttons);
        stack.Children.Add(caption);

        return new Border
        {
            BorderBrush = CadUi.Border,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Margin = new Thickness(0, 2, 2, 2),
            Child = stack
        };
    }

    public static Button CreateButton(
        string text,
        Action action,
        string? tooltip = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(action);

        var button = new Button
        {
            Content = text,
            Classes = { "cad-ribbon-button" },
            Height = CadUi.RibbonRowHeight,
            MinWidth = 0,
            Padding = new Thickness(6, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = CadUi.UiFontFamily,
            FontSize = CadUi.UiFontSize,
            Foreground = CadUi.Text
        };
        ToolTip.SetTip(button, tooltip ?? text);
        button.Click += (_, _) => action();
        return button;
    }
}
