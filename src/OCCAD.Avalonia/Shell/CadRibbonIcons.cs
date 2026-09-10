using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace OCCAD.Avalonia;

internal static class CadRibbonIcons
{
    public static Control? CreateIcon(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var (pathData, isFill) = GetIconData(id);
        if (pathData is null)
            return null;

        try
        {
            if (isFill)
            {
                return new ShapePath
                {
                    Data = Geometry.Parse(pathData),
                    Fill = CadTheme.Accent,
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
            else
            {
                return new ShapePath
                {
                    Data = Geometry.Parse(pathData),
                    Stroke = CadTheme.Accent,
                    StrokeThickness = 1.25,
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
        }
        catch
        {
            return null;
        }
    }

    private static (string? PathData, bool IsFill) GetIconData(string id) => id.Trim().ToLowerInvariant() switch
    {
        // File
        "new" or "file.new" or "cad.text.new" => ("M 3,1 L 9,1 L 13,5 L 13,15 L 3,15 Z M 9,1 L 9,5 L 13,5", false),
        "open" or "file.open" or "cad.text.open" => ("M 1,3 L 6,3 L 8,5 L 15,5 L 15,13 L 1,13 Z", false),
        "save" or "file.save" or "cad.text.save" => ("M 2,1 L 12,1 L 14,3 L 14,15 L 2,15 Z M 4,1 V 5 H 10 V 1 M 4,9 H 12 V 15 H 4", false),
        "saveas" or "file.saveas" or "cad.text.saveas" => ("M 2,1 L 10,1 L 12,3 L 12,13 L 2,13 Z M 4,1 V 5 H 9 V 1 M 4,8 H 10 V 13 H 4 M 14,7 L 14,15 M 11,12 L 14,15 L 17,12", false),
        "import" or "file.import" or "cad.text.import" => ("M 2,6 V 14 H 14 V 6 M 8,1 V 10 M 5,7 L 8,10 L 11,7", false),
        "export" or "file.export" or "cad.text.export" => ("M 2,6 V 14 H 14 V 6 M 8,10 V 1 M 5,4 L 8,1 L 11,4", false),
        "settings" or "preferences" or "cad.text.preferences" => ("M 8,5 A 3,3 0 1 0 8,11 A 3,3 0 1 0 8,5 M 8,1 V 3 M 8,13 V 15 M 1,8 H 3 M 13,8 H 15 M 3,3 L 4.5,4.5 M 11.5,11.5 L 13,13 M 3,13 L 4.5,11.5 M 11.5,4.5 L 13,3", false),
        "file.clear" or "cad.text.clearmodel" => ("M 2,4 H 14 M 5,4 V 14 H 11 V 4 M 6,2 H 10", false),
        "exit" or "cad.text.exit" => ("M 6,2 H 2 V 14 H 6 M 9,5 L 13,8 L 9,11 M 4,8 H 13", false),

        // Edit
        "undo" or "edit.undo" or "cad.text.undo" => ("M 3,6 L 7,2 L 7,5 C 12,5 14,8 14,13 C 12.5,9.5 9.5,8.5 7,8.5 L 7,11 Z", true),
        "redo" or "edit.redo" or "cad.text.redo" => ("M 13,6 L 9,2 L 9,5 C 4,5 2,8 2,13 C 3.5,9.5 6.5,8.5 9,8.5 L 9,11 Z", true),
        "delete" or "edit.delete" or "cad.text.delete" => ("M 3,3 L 13,13 M 13,3 L 3,13", false),

        // Selection
        "select" or "cad.text.select" => ("M 2,1 L 2,14 L 6,10 L 10,14 L 12,12 L 8,8 L 13,8 Z", true),
        "select.all" or "cad.text.selectall" => ("M 2,2 H 14 V 14 H 2 Z M 5,5 H 11 V 11 H 5 Z", false),
        "select.invert" or "cad.text.selectinvert" => ("M 2,2 H 14 V 14 H 2 Z M 2,2 L 14,14", false),
        "layers" or "cad.text.layers" => ("M 8,2 L 15,6 L 8,10 L 1,6 Z M 1,9 L 8,13 L 15,9 M 1,12 L 8,16 L 15,12", false),

        // 2D Draw
        "draw.point" or "cad.text.point" => ("M 8,2 V 6 M 8,10 V 14 M 2,8 H 6 M 10,8 H 14 M 7,8 A 1,1 0 1 0 9,8 A 1,1 0 1 0 7,8", false),
        "draw.line" or "cad.text.line" => ("M 2,14 L 14,2 M 1,14 A 1.5,1.5 0 1 0 4,14 A 1.5,1.5 0 1 0 1,14 M 12,2 A 1.5,1.5 0 1 0 15,2 A 1.5,1.5 0 1 0 12,2", false),
        "draw.polyline" or "cad.text.polyline" => ("M 2,13 L 6,4 L 10,11 L 14,3", false),
        "draw.rectangle" or "cad.text.rectangle" => ("M 2,3 H 14 V 13 H 2 Z", false),
        "draw.polygon" or "regularpolygon" or "cad.text.polygon" or "cad.text.regularpolygon" => ("M 8,1 L 15,6 L 12,14 L 4,14 L 1,6 Z", false),
        "draw.spline" or "cad.text.spline" => ("M 2,13 C 4,2 11,14 14,3", false),
        "circle" or "cad.text.circle" or "draw.circle" or "draw.circle.centerradius" => ("M 8,2 A 6,6 0 1 0 8,14 A 6,6 0 1 0 8,2", false),
        "arc" or "cad.text.arcfamily" or "draw.arc" or "draw.arc.threepoints" => ("M 2,13 A 8,8 0 0 1 13,3", false),
        "ellipse" or "cad.text.ellipse" or "draw.ellipse" or "draw.ellipse.centermajor" => ("M 8,3 C 12,3 15,5.2 15,8 C 15,10.8 12,13 8,13 C 4,13 1,10.8 1,8 C 1,5.2 4,3 8,3", false),

        // 3D Modeling
        "primitives" or "cad.text.primitives" or "solid.box" or "cad.text.box" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15", false),
        "curve.helix" or "cad.text.helix" => ("M 8,2 C 14,2 14,6 8,6 C 2,6 2,10 8,10 C 14,10 14,14 8,14", false),
        "feature.extrude" or "cad.text.extrude" => ("M 3,11 L 8,8 L 13,11 L 8,14 Z M 3,5 L 8,2 L 13,5 L 8,8 Z M 3,5 V 11 M 13,5 V 11 M 8,8 V 14", false),
        "feature.revolve" or "cad.text.revolve" => ("M 4,2 V 14 M 7,4 C 12,4 14,6 14,8 C 14,10 12,12 7,12 M 7,10 L 9,12 L 7,14", false),
        "feature.sweep" or "cad.text.sweep" => ("M 3,13 C 3,5 11,13 11,5 M 9,3 L 13,3 L 13,7", false),
        "feature.loft" or "cad.text.loft" => ("M 4,2 H 12 M 2,8 H 14 M 4,14 H 12 M 4,2 L 2,8 L 4,14 M 12,2 L 14,8 L 12,14", false),

        // Modify
        "modify.move" or "cad.text.move" => ("M 8,1 V 15 M 5,4 L 8,1 L 11,4 M 5,12 L 8,15 L 11,12 M 1,8 H 15 M 4,5 L 1,8 L 4,11 M 12,5 L 15,8 L 12,11", false),
        "modify.copy" or "cad.text.copy" => ("M 4,2 H 14 V 11 H 4 Z M 1,5 H 3 V 14 H 11 V 15 H 1 Z", false),
        "modify.rotate" or "cad.text.rotate" => ("M 13,8 A 5.5,5.5 0 1 1 8,2.5 V 0.5 L 11,3.5 L 8,6.5 V 4.5", false),
        "modify.scale" or "cad.text.scale" => ("M 2,14 V 8 H 8 V 14 Z M 6,10 L 13,3 M 9,3 H 13 V 7 M 4,5 H 12 V 13", false),
        "modify.mirror" or "cad.text.mirror" => ("M 8,1 V 15 M 8,3 L 2,13 H 8 M 8,3 L 14,13 H 8", false),
        "modify.array" or "cad.text.array" => ("M 2,2 H 6 V 6 H 2 Z M 10,2 H 14 V 6 H 10 Z M 2,10 H 6 V 14 H 2 Z M 10,10 H 14 V 14 H 10 Z", false),
        "modify.offset" or "cad.text.offset" => ("M 2,4 H 12 M 2,8 H 14 M 2,12 H 14", false),
        "modify.trim" or "cad.text.trim" => ("M 2,2 L 14,14 M 2,14 L 14,2 M 8,2 V 14", false),
        "modify.extend" or "cad.text.extend" => ("M 14,2 V 14 M 2,8 H 12 M 9,5 L 12,8 L 9,11", false),
        "modify.fillet" or "cad.text.fillet" => ("M 2,14 V 7 A 5,5 0 0 1 7,2 H 14", false),
        "modify.chamfer" or "cad.text.chamfer" => ("M 2,14 V 7 L 7,2 H 14", false),

        // Annotate
        "annotate.text" or "cad.text.text" => ("M 8,1 L 3,14 H 5.5 L 6.8,10.5 H 9.2 L 10.5,14 H 13 Z M 7.4,8.5 L 8,6.5 L 8.6,8.5 Z", true),
        "annotate.length" or "cad.text.lengthdimension" => ("M 2,3 V 13 M 14,3 V 13 M 2,8 H 14 M 4,6 L 2,8 L 4,10 M 12,6 L 14,8 L 12,10", false),
        "annotate.angle" or "cad.text.angledimension" => ("M 2,14 L 14,14 M 2,14 L 11,3 M 7,14 A 5,5 0 0 0 6,9", false),
        "annotate.radius" or "cad.text.radiusdimension" => ("M 8,2 A 6,6 0 1 0 8,14 A 6,6 0 1 0 8,2 M 8,8 L 13,4 M 11,4 H 13 V 6", false),
        "annotate.diameter" or "cad.text.diameterdimension" => ("M 8,2 A 6,6 0 1 0 8,14 A 6,6 0 1 0 8,2 M 3,13 L 13,3", false),
        "measure.distance" or "cad.text.distance" => ("M 2,11 L 11,2 L 14,5 L 5,14 Z M 5,8 L 7,10 M 7,6 L 9,8 M 9,4 L 11,6", false),

        // View
        "view.fit" or "cad.text.fit" => ("M 2,6 V 2 H 6 M 10,2 H 14 V 6 M 14,10 V 14 H 10 M 6,14 H 2 V 10", false),
        "view.isometric" or "cad.text.isometric" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15", false),
        "view.top" or "cad.text.top" => ("M 3,3 H 13 V 13 H 3 Z M 8,3 V 13 M 3,8 H 13", false),
        "view.bottom" or "cad.text.bottom" => ("M 3,3 H 13 V 13 H 3 Z M 6,8 H 10", false),
        "view.front" or "cad.text.front" => ("M 3,5 H 13 V 13 H 3 Z", false),
        "view.back" or "cad.text.back" => ("M 3,5 H 13 V 13 H 3 Z M 5,7 H 11", false),
        "view.left" or "cad.text.left" => ("M 5,3 H 13 V 13 H 5 Z", false),
        "view.right" or "cad.text.right" => ("M 3,3 H 11 V 13 H 3 Z", false),
        "display.wireframe" or "cad.text.wireframe" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15", false),
        "display.shaded" or "cad.text.shaded" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15", true),
        "display.transparent" or "cad.text.transparent" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15 M 1,5 L 8,9 L 15,5", false),
        "display.hiddenline" or "cad.text.hiddenline" => ("M 8,1 L 15,5 L 8,9 L 1,5 Z M 1,5 V 11 L 8,15 V 9 M 15,5 V 11 L 8,15", false),
        "view.hide" or "cad.text.hide" => ("M 2,8 C 4,4 12,4 14,8 C 12,12 4,12 2,8 Z M 2,2 L 14,14", false),
        "view.isolate" or "cad.text.isolate" => ("M 5,5 H 11 V 11 H 5 Z M 1,1 H 15 V 15 H 1 Z", false),
        "view.showall" or "cad.text.showall" => ("M 2,8 C 4,4 12,4 14,8 C 12,12 4,12 2,8 Z M 8,6 A 2,2 0 1 0 8,10 A 2,2 0 1 0 8,6", false),

        // Panels
        "model" or "cad.text.model" => ("M 2,2 H 6 V 14 H 2 Z M 8,4 H 14 M 8,8 H 14 M 8,12 H 14", false),
        "properties" or "cad.text.properties" => ("M 2,2 H 14 V 14 H 2 Z M 6,2 V 14 M 2,6 H 14 M 2,10 H 14", false),
        "tool" or "cad.text.toolparameters" => ("M 11,2 L 14,5 L 5,14 H 2 V 11 Z", false),

        _ => (null, false)
    };
}
