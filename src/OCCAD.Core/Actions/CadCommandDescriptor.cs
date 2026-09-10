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
    internal static readonly IReadOnlyDictionary<string, string[]> Aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["measure.distance"] = ["DISTANCE", "DIST", "DI"],
        ["draw.point"] = ["POINT", "PO"],
        ["draw.line"] = ["LINE", "L"],
        ["draw.polyline"] = ["POLYLINE", "PL"],
        ["draw.rectangle"] = ["RECTANGLE", "REC"],
        ["draw.polygon"] = ["POLYGON", "PG"],
        ["draw.regularpolygon"] = ["REGULARPOLYGON", "RPOLY"],
        ["draw.circle"] = ["CIRCLE", "C"],
        ["draw.arc"] = ["ARC", "A"],
        ["draw.ellipse"] = ["ELLIPSE", "EL"],
        ["draw.spline"] = ["SPLINE", "SPL"],
        ["annotate.text"] = ["TEXT", "DTEXT"],
        ["annotate.length"] = ["DIMLINEAR", "DLI"],
        ["annotate.angle"] = ["DIMANGULAR", "DAN"],
        ["annotate.radius"] = ["DIMRADIUS", "DRA"],
        ["annotate.diameter"] = ["DIMDIAMETER", "DDI"],
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
        ["modify.move"] = ["MOVE", "M"],
        ["modify.copy"] = ["COPY", "CO"],
        ["modify.rotate"] = ["ROTATE", "RO"],
        ["modify.scale"] = ["SCALE", "SC"],
        ["modify.mirror"] = ["MIRROR", "MI"],
        ["modify.array"] = ["ARRAY", "AR"],
        ["modify.offset"] = ["OFFSET", "O"],
        ["modify.trim"] = ["TRIM", "TR"],
        ["modify.extend"] = ["EXTEND", "EX"],
        ["modify.fillet"] = ["FILLET", "F"],
        ["modify.chamfer"] = ["CHAMFER", "CHA"],
        ["edit.delete"] = ["DELETE", "ERASE"],
        ["edit.undo"] = ["UNDO", "U"],
        ["edit.redo"] = ["REDO"],
        ["select"] = ["SELECT"],
        ["select.all"] = ["SELECTALL"],
        ["select.invert"] = ["SELECTINVERT"],
        ["view.fit"] = ["FIT", "ZE"],
        ["view.isometric"] = ["ISOMETRIC", "ISO"],
        ["view.top"] = ["TOP"],
        ["view.bottom"] = ["BOTTOM"],
        ["view.front"] = ["FRONT"],
        ["view.back"] = ["BACK"],
        ["view.left"] = ["LEFT"],
        ["view.right"] = ["RIGHT"],
        ["display.wireframe"] = ["WIREFRAME", "WF"],
        ["display.shaded"] = ["SHADED", "SHADE"],
        ["display.transparent"] = ["TRANSPARENT", "TRANS"],
        ["display.hiddenline"] = ["HIDDENLINE", "HLR"],
        ["view.hide"] = ["HIDE"],
        ["view.isolate"] = ["ISOLATE"],
        ["view.showall"] = ["SHOWALL"],
    };
    internal static readonly IReadOnlyDictionary<string, (string Key, string Name)> Captions =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["file.clear"] = ("Cad.Text.ClearModel", "Clear Model"),
        ["edit.undo"] = ("Cad.Text.Undo", "Undo"),
        ["edit.redo"] = ("Cad.Text.Redo", "Redo"),
        ["edit.delete"] = ("Cad.Text.Delete", "Delete"),
        ["select"] = ("Cad.Text.Select", "Select"),
        ["select.all"] = ("Cad.Text.SelectAll", "Select All"),
        ["select.invert"] = ("Cad.Text.SelectInvert", "Invert"),
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
        ["modify.move"] = ("Cad.Text.Move", "Move"),
        ["modify.copy"] = ("Cad.Text.Copy", "Copy"),
        ["modify.rotate"] = ("Cad.Text.Rotate", "Rotate"),
        ["modify.scale"] = ("Cad.Text.Scale", "Scale"),
        ["modify.mirror"] = ("Cad.Text.Mirror", "Mirror"),
        ["modify.array"] = ("Cad.Text.Array", "Array"),
        ["modify.offset"] = ("Cad.Text.Offset", "Offset"),
        ["modify.trim"] = ("Cad.Text.Trim", "Trim"),
        ["modify.extend"] = ("Cad.Text.Extend", "Extend"),
        ["modify.fillet"] = ("Cad.Text.Fillet", "Fillet"),
        ["modify.chamfer"] = ("Cad.Text.Chamfer", "Chamfer"),
        ["annotate.text"] = ("Cad.Text.Text", "Text"),
        ["annotate.length"] = ("Cad.Text.LengthDimension", "Length Dimension"),
        ["annotate.angle"] = ("Cad.Text.AngleDimension", "Angle Dimension"),
        ["annotate.radius"] = ("Cad.Text.RadiusDimension", "Radius Dimension"),
        ["annotate.diameter"] = ("Cad.Text.DiameterDimension", "Diameter Dimension"),
        ["measure.distance"] = ("Cad.Text.Distance", "Distance"),
        ["view.fit"] = ("Cad.Text.Fit", "Fit"),
        ["view.isometric"] = ("Cad.Text.Isometric", "Isometric"),
        ["view.top"] = ("Cad.Text.Top", "Top"),
        ["view.bottom"] = ("Cad.Text.Bottom", "Bottom"),
        ["view.front"] = ("Cad.Text.Front", "Front"),
        ["view.back"] = ("Cad.Text.Back", "Back"),
        ["view.left"] = ("Cad.Text.Left", "Left"),
        ["view.right"] = ("Cad.Text.Right", "Right"),
        ["display.wireframe"] = ("Cad.Text.Wireframe", "Wireframe"),
        ["display.shaded"] = ("Cad.Text.Shaded", "Shaded"),
        ["display.transparent"] = ("Cad.Text.Transparent", "Transparent"),
        ["display.hiddenline"] = ("Cad.Text.HiddenLine", "Hidden Line"),
        ["view.hide"] = ("Cad.Text.Hide", "Hide"),
        ["view.isolate"] = ("Cad.Text.Isolate", "Isolate"),
        ["view.showall"] = ("Cad.Text.ShowAll", "Show All"),
    };
}
