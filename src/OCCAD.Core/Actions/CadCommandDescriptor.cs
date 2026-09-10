namespace OCCAD;

/// <summary>Shared metadata for action surfaces; behavior remains in CadAction/CadTool.</summary>
public sealed class CadCommandDescriptor
{
    internal CadCommandDescriptor(CadAction action)
    {
        Action = action;
        Aliases = CadCommandCatalog.Aliases.GetValueOrDefault(action.Id) ?? [];
        var caption = CadCommandCatalog.Captions.GetValueOrDefault(action.Id);
        DisplayNameKey = caption.Key ?? $"Cad.Text.{action.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)}";
        EnglishName = caption.Name ?? action.DisplayName;
    }

    public CadAction Action { get; }
    public string Id => Action.Id;
    public string EnglishName { get; }
    public string DisplayNameKey { get; }
    public IReadOnlyList<string> Aliases { get; }
    public string? Shortcut => Action.Shortcut;
    public bool Repeatable => Action.IsRepeatable;
    public string? ToolId => (Action as CadToolAction)?.ToolId;
    public bool CanExecute() => Action.CanExecute();
}

internal static class CadCommandCatalog
{
    // Keep aliases limited to Actions that CadCoreRegistration actually exposes.
    // Method-based drawing commands intentionally map the traditional short name
    // to the default method; every other method remains reachable by its Action ID.
    internal static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["draw.point"] = ["POINT", "PO"],
            ["draw.line"] = ["LINE", "L"],
            ["draw.polyline"] = ["POLYLINE", "PL"],
            ["draw.polygon"] = ["POLYGON", "PG"],
            ["draw.regularpolygon.inscribed"] = ["REGULARPOLYGON", "RPOLY"],
            ["draw.rectangle"] = ["RECTANGLE", "REC"],
            ["draw.circle.centerradius"] = ["CIRCLE", "C"],
            ["draw.circle.centerdiameter"] = ["CIRCLEDIAMETER"],
            ["draw.circle.twopoints"] = ["CIRCLE2P"],
            ["draw.circle.threepoints"] = ["CIRCLE3P"],
            ["draw.circle.pointcenter"] = ["CIRCLEPC"],
            ["draw.arc.threepoints"] = ["ARC", "A"],
            ["draw.ellipse.centermajor"] = ["ELLIPSE", "EL"],
            ["draw.spline"] = ["SPLINE", "SPL"],

            ["solid.box"] = ["BOX", "B"],
            ["solid.cylinder"] = ["CYLINDER", "CYL"],
            ["solid.cone"] = ["CONE", "CN"],
            ["solid.frustum"] = ["FRUSTUM", "FRU"],
            ["solid.sphere"] = ["SPHERE", "SPH"],
            ["solid.ellipsoid"] = ["ELLIPSOID", "ELLIP"],
            ["solid.torus"] = ["TORUS", "TOR"],
            ["curve.helix"] = ["HELIX", "HX"],

            ["feature.extrude"] = ["EXTRUDE", "EXT"],
            ["feature.revolve"] = ["REVOLVE", "REV"],
            ["feature.sweep"] = ["SWEEP", "SW"],
            ["feature.loft"] = ["LOFT"],

            ["edit.move"] = ["MOVE", "M"],
            ["edit.delete"] = ["DELETE", "ERASE"],

            ["view.fit"] = ["FIT", "ZE"],
            ["view.top"] = ["TOP"],
            ["view.bottom"] = ["BOTTOM"],
            ["view.front"] = ["FRONT"],
            ["view.back"] = ["BACK"],
            ["view.left"] = ["LEFT"],
            ["view.right"] = ["RIGHT"],
            ["view.iso.ne"] = ["ISO", "ISONE"],
            ["view.iso.nw"] = ["ISONW"],
            ["view.iso.se"] = ["ISOSE"],
            ["view.iso.sw"] = ["ISOSW"],
            ["display.wireframe"] = ["WIREFRAME", "WF"],
            ["display.shaded"] = ["SHADED", "SHADE"]
        };

    internal static readonly IReadOnlyDictionary<string, (string Key, string Name)> Captions =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["edit.delete"] = ("Cad.Text.Delete", "Delete"),
            ["edit.move"] = ("Cad.Text.Move", "Move"),

            ["draw.point"] = ("Cad.Text.Point", "Point"),
            ["draw.line"] = ("Cad.Text.Line", "Line"),
            ["draw.polyline"] = ("Cad.Text.Polyline", "Polyline"),
            ["draw.rectangle"] = ("Cad.Text.Rectangle", "Rectangle"),
            ["draw.polygon"] = ("Cad.Text.Polygon", "Polygon"),
            ["draw.spline"] = ("Cad.Text.Spline", "Spline"),
            ["draw.circle.centerradius"] = ("Cad.Parameter.circle.Method.CenterRadius", "Center + Radius"),
            ["draw.circle.centerdiameter"] = ("Cad.Parameter.circle.Method.CenterDiameter", "Center + Diameter"),
            ["draw.circle.twopoints"] = ("Cad.Parameter.circle.Method.TwoPoints", "Two Points"),
            ["draw.circle.threepoints"] = ("Cad.Parameter.circle.Method.ThreePoints", "Three Points"),
            ["draw.circle.pointcenter"] = ("Cad.Parameter.circle.Method.PointCenter", "Point + Center"),
            ["draw.arc.threepoints"] = ("Cad.Parameter.arc.Method.ThreePoints", "Three Points"),
            ["draw.arc.centerstartend"] = ("Cad.Parameter.arc.Method.CenterStartEnd", "Center + Start + End"),
            ["draw.arc.startcenterend"] = ("Cad.Parameter.arc.Method.StartCenterEnd", "Start + Center + End"),
            ["draw.arc.startendcenter"] = ("Cad.Parameter.arc.Method.StartEndCenter", "Start + End + Center"),
            ["draw.arc.startendpoint"] = ("Cad.Parameter.arc.Method.StartEndPoint", "Start + End + Point"),
            ["draw.arc.startendtangent"] = ("Cad.Parameter.arc.Method.StartEndTangent", "Start + End + Tangent"),
            ["draw.regularpolygon.inscribed"] = ("Cad.Parameter.regularpolygon.mode.Inscribed", "Inscribed"),
            ["draw.regularpolygon.circumscribed"] = ("Cad.Parameter.regularpolygon.mode.Circumscribed", "Circumscribed"),
            ["draw.ellipse.centermajor"] = ("Cad.Parameter.ellipse.Method.CenterMajorMinor", "Center + Axes"),
            ["draw.ellipse.axisendpoints"] = ("Cad.Parameter.ellipse.Method.AxisEndpointsMinor", "Axis Endpoints"),

            ["solid.box"] = ("Cad.Text.Box", "Box"),
            ["solid.cylinder"] = ("Cad.Text.Cylinder", "Cylinder"),
            ["solid.cone"] = ("Cad.Text.Cone", "Cone"),
            ["solid.frustum"] = ("Cad.Text.Frustum", "Frustum"),
            ["solid.sphere"] = ("Cad.Text.Sphere", "Sphere"),
            ["solid.ellipsoid"] = ("Cad.Text.Ellipsoid", "Ellipsoid"),
            ["solid.torus"] = ("Cad.Text.Torus", "Torus"),
            ["curve.helix"] = ("Cad.Text.Helix", "Helix"),

            ["feature.extrude"] = ("Cad.Text.Extrude", "Extrude"),
            ["feature.revolve"] = ("Cad.Text.Revolve", "Revolve"),
            ["feature.sweep"] = ("Cad.Text.Sweep", "Sweep"),
            ["feature.loft"] = ("Cad.Text.Loft", "Loft"),

            ["view.fit"] = ("Cad.Text.Fit", "Fit"),
            ["view.top"] = ("Cad.Text.Top", "Top"),
            ["view.bottom"] = ("Cad.Text.Bottom", "Bottom"),
            ["view.front"] = ("Cad.Text.Front", "Front"),
            ["view.back"] = ("Cad.Text.Back", "Back"),
            ["view.left"] = ("Cad.Text.Left", "Left"),
            ["view.right"] = ("Cad.Text.Right", "Right"),
            ["display.wireframe"] = ("Cad.Text.Wireframe", "Wireframe"),
            ["display.shaded"] = ("Cad.Text.Shaded", "Shaded")
        };
}
