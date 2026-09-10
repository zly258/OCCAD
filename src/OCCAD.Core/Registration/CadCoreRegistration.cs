using OcctNet;

namespace OCCAD;

/// <summary>
/// Single executable capability composition root for Core.
///
/// The current product surface intentionally contains only the common 2D/3D
/// primitives and edit operations that map cleanly to OCCTBIM-Source. Dormant
/// experimental entities/features must not leak into command, tool or UI
/// discovery simply because implementation files still exist in the assembly.
/// </summary>
internal static class CadCoreRegistration
{
    public static CadEntityRegistry CreateEntityRegistry()
    {
        var entities = new CadEntityRegistry();
        RegisterDraftingEntities(entities);
        RegisterSolidEntities(entities);
        return entities;
    }

    public static CadToolRegistry CreateToolRegistry()
    {
        var tools = new CadToolRegistry();
        RegisterMeasurementTools(tools);
        RegisterDraftingTools(tools);
        RegisterModelingTools(tools);
        RegisterModifyTools(tools);
        return tools;
    }

    public static void RegisterActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(workspace);

        RegisterDocumentActions(actions, workspace);
        RegisterViewActions(actions, workspace);
        RegisterDraftingActions(actions, workspace);
        RegisterModelingActions(actions, workspace);
        RegisterModifyActions(actions, workspace);
    }

    private static void RegisterDraftingEntities(CadEntityRegistry entities)
    {
        entities.Register(
            "point",
            "Point",
            static () => new CadPointEntity(OcctPoint3d.Origin),
            CadPointEntity.WriteGeometry,
            CadPointEntity.ReadGeometry);
        entities.Register(
            "line",
            "Line",
            static () => new CadLineEntity(
                OcctPoint3d.Origin,
                new OcctPoint3d(1, 0, 0)),
            CadLineEntity.WriteGeometry,
            CadLineEntity.ReadGeometry);
        entities.Register(
            "centerline",
            "Center Line",
            static () => new CadCenterLineEntity(
                OcctPoint3d.Origin,
                new OcctPoint3d(1, 0, 0)),
            CadCenterLineEntity.WriteGeometry,
            CadCenterLineEntity.ReadGeometry);
        entities.Register(
            "centermark",
            "Center Mark",
            static () => new CadCenterMarkEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                1),
            CadCenterMarkEntity.WriteGeometry,
            CadCenterMarkEntity.ReadGeometry);
        entities.Register(
            "polyline",
            "Polyline",
            static () => new CadPolylineEntity(
                [OcctPoint3d.Origin, new OcctPoint3d(1, 0, 0)]),
            CadPolylineEntity.WriteGeometry,
            CadPolylineEntity.ReadGeometry);
        entities.Register(
            "rectangle",
            "Rectangle",
            static () => new CadRectangleEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitX,
                OcctVector3d.UnitY,
                1,
                1),
            CadRectangleEntity.WriteGeometry,
            CadRectangleEntity.ReadGeometry);
        entities.Register(
            "circle",
            "Circle",
            static () => new CadCircleEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                1),
            CadCircleEntity.WriteGeometry,
            CadCircleEntity.ReadGeometry);
        entities.Register(
            "arc",
            "Arc",
            static () => new CadArcEntity(
                new OcctPoint3d(1, 0, 0),
                new OcctPoint3d(0, 1, 0),
                new OcctPoint3d(-1, 0, 0)),
            CadArcEntity.WriteGeometry,
            CadArcEntity.ReadGeometry);
        entities.Register(
            "ellipse",
            "Ellipse",
            static () => new CadEllipseEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                2,
                1),
            CadEllipseEntity.WriteGeometry,
            CadEllipseEntity.ReadGeometry);
        entities.Register(
            "spline",
            "Spline",
            static () => new CadSplineEntity(
                [OcctPoint3d.Origin, new OcctPoint3d(1, 0, 0)]),
            CadSplineEntity.WriteGeometry,
            CadSplineEntity.ReadGeometry);
        entities.Register(
            "polygon",
            "Polygon",
            static () => new CadPolygonEntity(
                [
                    OcctPoint3d.Origin,
                    new OcctPoint3d(1, 0, 0),
                    new OcctPoint3d(0, 1, 0)
                ]),
            CadPolygonEntity.WriteGeometry,
            CadPolygonEntity.ReadGeometry);
        entities.Register(
            "regularpolygon",
            "Regular Polygon",
            static () => new CadRegularPolygonEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                OcctVector3d.UnitX,
                1,
                3),
            CadRegularPolygonEntity.WriteGeometry,
            CadRegularPolygonEntity.ReadGeometry);
    }

    private static void RegisterSolidEntities(CadEntityRegistry entities)
    {
        entities.Register(
            "box",
            "Box",
            static () => new CadBoxEntity(OcctPoint3d.Origin, 1, 1, 1),
            CadBoxEntity.WriteGeometry,
            CadBoxEntity.ReadGeometry);
        entities.Register(
            "cylinder",
            "Cylinder",
            static () => new CadCylinderEntity(OcctPoint3d.Origin, 1, 1),
            CadCylinderEntity.WriteGeometry,
            CadCylinderEntity.ReadGeometry);
        entities.Register(
            "cone",
            "Cone",
            static () => new CadConeEntity(OcctPoint3d.Origin, 1, 1),
            CadConeEntity.WriteGeometry,
            CadConeEntity.ReadGeometry);
        entities.Register(
            "sphere",
            "Sphere",
            static () => new CadSphereEntity(OcctPoint3d.Origin, 1),
            CadSphereEntity.WriteGeometry,
            CadSphereEntity.ReadGeometry);
    }

    private static void RegisterMeasurementTools(CadToolRegistry tools) =>
        tools.Register<MeasureDistanceTool>("distance");

    private static void RegisterDraftingTools(CadToolRegistry tools)
    {
        tools.Register<PointTool>("point");
        tools.Register<LineTool>("line");
        tools.Register<CenterLineTool>("centerline");
        tools.Register<CenterMarkTool>("centermark");
        tools.Register<PolylineTool>("polyline");
        tools.Register<RectangleTool>("rectangle");
        tools.Register<CircleTool>("circle");
        tools.Register<ArcTool>("arc");
        tools.Register<EllipseTool>("ellipse");
        tools.Register<SplineTool>("spline");
        tools.Register<PolygonTool>("polygon");
        tools.Register<RegularPolygonTool>("regularpolygon");
    }

    private static void RegisterModelingTools(CadToolRegistry tools)
    {
        tools.Register<BoxTool>("box");
        tools.Register<CylinderTool>("cylinder");
        tools.Register<ConeTool>("cone");
        tools.Register<SphereTool>("sphere");
    }

    private static void RegisterModifyTools(CadToolRegistry tools)
    {
        // Keep this set aligned with OCCTBIM-Source/src/tools/edit. OCCAD's
        // generic ArrayTool is the current compact equivalent of the source's
        // rectangular/circular/path array family.
        tools.Register<MoveTool>("move");
        tools.Register<CopyTool>("copy");
        tools.Register<RotateTool>("rotate");
        tools.Register<ScaleTool>("scale");
        tools.Register<MirrorTool>("mirror");
        tools.Register<ArrayTool>("array");
    }

    private static void RegisterDocumentActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(new CadClearModelAction(workspace));
        actions.Register(new CadUndoAction(workspace));
        actions.Register(new CadRedoAction(workspace));
        actions.Register(new CadDeleteAction(workspace));
        actions.Register(new CadSelectAction(workspace));
        actions.Register(new CadSelectAllAction(workspace));
        actions.Register(new CadSelectInvertAction(workspace));
        actions.Register(new CadHideAction(workspace));
        actions.Register(new CadIsolateAction(workspace));
        actions.Register(new CadShowAllAction(workspace));
    }

    private static void RegisterViewActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(new CadViewAction(workspace, "view.fit", "Fit"));
        actions.Register(new CadViewAction(
            workspace,
            "view.isometric",
            "Isometric",
            OcctViewOrientation.Isometric));
        actions.Register(new CadViewAction(
            workspace,
            "view.top",
            "Top",
            OcctViewOrientation.Top));
        actions.Register(new CadViewAction(
            workspace,
            "view.bottom",
            "Bottom",
            OcctViewOrientation.Bottom));
        actions.Register(new CadViewAction(
            workspace,
            "view.front",
            "Front",
            OcctViewOrientation.Front));
        actions.Register(new CadViewAction(
            workspace,
            "view.back",
            "Back",
            OcctViewOrientation.Back));
        actions.Register(new CadViewAction(
            workspace,
            "view.left",
            "Left",
            OcctViewOrientation.Left));
        actions.Register(new CadViewAction(
            workspace,
            "view.right",
            "Right",
            OcctViewOrientation.Right));
        actions.Register(new CadDisplayModeAction(
            workspace,
            "display.wireframe",
            "Wireframe",
            OcctDisplayMode.Wireframe));
        actions.Register(new CadDisplayModeAction(
            workspace,
            "display.shaded",
            "Shaded",
            OcctDisplayMode.Shaded));
        actions.Register(new CadTransparencyAction(workspace));
        actions.Register(new CadHiddenLineAction(workspace));
    }

    private static void RegisterDraftingActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(Tool(workspace, "draw.point", "Point", "point"));
        actions.Register(Tool(workspace, "draw.line", "Line", "line"));
        actions.Register(Tool(workspace, "draw.centerline", "Center Line", "centerline"));
        actions.Register(Tool(workspace, "draw.centermark", "Center Mark", "centermark"));
        actions.Register(Tool(workspace, "draw.polyline", "Polyline", "polyline"));
        actions.Register(Tool(workspace, "draw.rectangle", "Rectangle", "rectangle"));
        actions.Register(Tool(workspace, "draw.polygon", "Polygon", "polygon"));
        actions.Register(Tool(workspace, "draw.regularpolygon", "Regular Polygon", "regularpolygon"));
        actions.Register(Tool(
            workspace,
            "draw.regularpolygon.inscribed",
            "Regular Polygon - Inscribed",
            "regularpolygon",
            Params(("mode", "Inscribed"))));
        actions.Register(Tool(
            workspace,
            "draw.regularpolygon.circumscribed",
            "Regular Polygon - Circumscribed",
            "regularpolygon",
            Params(("mode", "Circumscribed"))));
        actions.Register(Tool(workspace, "draw.ellipse", "Ellipse", "ellipse"));
        actions.Register(Tool(
            workspace,
            "draw.ellipse.centermajor",
            "Ellipse - Center Axes",
            "ellipse",
            Params(("Method", "CenterMajorMinor"))));
        actions.Register(Tool(
            workspace,
            "draw.ellipse.axisendpoints",
            "Ellipse - Axis Endpoints",
            "ellipse",
            Params(("Method", "AxisEndpointsMinor"))));
        actions.Register(Tool(workspace, "draw.spline", "Spline", "spline"));

        actions.Register(Tool(workspace, "draw.circle", "Circle", "circle"));
        actions.Register(Tool(
            workspace,
            "draw.circle.centerradius",
            "Circle - Center Radius",
            "circle",
            Params(("Method", "CenterRadius"))));
        actions.Register(Tool(
            workspace,
            "draw.circle.centerdiameter",
            "Circle - Center Diameter",
            "circle",
            Params(("Method", "CenterDiameter"))));
        actions.Register(Tool(
            workspace,
            "draw.circle.twopoints",
            "Circle - Two Points",
            "circle",
            Params(("Method", "TwoPoints"))));
        actions.Register(Tool(
            workspace,
            "draw.circle.threepoints",
            "Circle - Three Points",
            "circle",
            Params(("Method", "ThreePoints"))));
        actions.Register(Tool(
            workspace,
            "draw.circle.pointcenter",
            "Circle - Point Center",
            "circle",
            Params(("Method", "PointCenter"))));

        actions.Register(Tool(workspace, "draw.arc", "Arc", "arc"));
        actions.Register(Tool(
            workspace,
            "draw.arc.threepoints",
            "Arc - Three Points",
            "arc",
            Params(("Method", "ThreePoints"))));
        actions.Register(Tool(
            workspace,
            "draw.arc.centerstartend",
            "Arc - Center Start End",
            "arc",
            Params(("Method", "CenterStartEnd"))));
        actions.Register(Tool(
            workspace,
            "draw.arc.startcenterend",
            "Arc - Start Center End",
            "arc",
            Params(("Method", "StartCenterEnd"))));
        actions.Register(Tool(
            workspace,
            "draw.arc.startendcenter",
            "Arc - Start End Center",
            "arc",
            Params(("Method", "StartEndCenter"))));
        actions.Register(Tool(
            workspace,
            "draw.arc.startendpoint",
            "Arc - Start End Point",
            "arc",
            Params(("Method", "StartEndPoint"))));
        actions.Register(Tool(
            workspace,
            "draw.arc.startendtangent",
            "Arc - Start End Tangent",
            "arc",
            Params(("Method", "StartEndTangent"))));
    }

    private static void RegisterModelingActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(Tool(workspace, "solid.box", "Box", "box"));
        actions.Register(Tool(workspace, "solid.cylinder", "Cylinder", "cylinder"));
        actions.Register(Tool(workspace, "solid.cone", "Cone", "cone"));
        actions.Register(Tool(workspace, "solid.sphere", "Sphere", "sphere"));
    }

    private static void RegisterModifyActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(Tool(workspace, "modify.move", "Move", "move"));
        actions.Register(Tool(workspace, "modify.copy", "Copy", "copy"));
        actions.Register(Tool(workspace, "modify.rotate", "Rotate", "rotate"));
        actions.Register(Tool(workspace, "modify.scale", "Scale", "scale"));
        actions.Register(Tool(workspace, "modify.mirror", "Mirror", "mirror"));
        actions.Register(Tool(workspace, "modify.array", "Array", "array"));
        actions.Register(Tool(workspace, "measure.distance", "Distance", "distance"));
    }

    private static CadToolAction Tool(
        CadWorkspace workspace,
        string actionId,
        string displayName,
        string toolId,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        new(
            workspace,
            actionId,
            displayName,
            toolId,
            initialParameters: parameters);

    private static IReadOnlyDictionary<string, string> Params(
        params (string Name, string Value)[] values) =>
        values.ToDictionary(
            static item => item.Name,
            static item => item.Value,
            StringComparer.OrdinalIgnoreCase);
}
