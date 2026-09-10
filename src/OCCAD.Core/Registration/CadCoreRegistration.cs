using OcctNet;

namespace OCCAD;

/// <summary>
/// Single executable capability composition root for Core. Entity persistence,
/// tools and user-facing actions are registered here once; UI only consumes the
/// resulting registries and never creates a second command catalog.
/// </summary>
internal static class CadCoreRegistration
{
    public static CadEntityRegistry CreateEntityRegistry()
    {
        var entities = new CadEntityRegistry();
        RegisterAnnotationEntities(entities);
        RegisterDraftingEntities(entities);
        RegisterSolidEntities(entities);
        RegisterFeatureEntities(entities);
        return entities;
    }

    public static CadToolRegistry CreateToolRegistry()
    {
        var tools = new CadToolRegistry();
        RegisterMeasurementTools(tools);
        RegisterDraftingTools(tools);
        RegisterAnnotationTools(tools);
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
        RegisterAnnotationActions(actions, workspace);
        RegisterModelingActions(actions, workspace);
        RegisterModifyActions(actions, workspace);
    }

    private static void RegisterAnnotationEntities(CadEntityRegistry entities)
    {
        entities.Register(
            "angledimension",
            "Angle Dimension",
            static () => new CadAngleDimensionEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitX,
                OcctVector3d.UnitY,
                1),
            CadAngleDimensionEntity.WriteGeometry,
            CadAngleDimensionEntity.ReadGeometry);
        entities.Register(
            "circulardimension",
            "Circular Dimension",
            static () => new CadCircularDimensionEntity(
                CadCircularDimensionKind.Radius,
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                OcctVector3d.UnitX,
                1),
            CadCircularDimensionEntity.WriteGeometry,
            CadCircularDimensionEntity.ReadGeometry);
        entities.Register(
            "lengthdimension",
            "Length Dimension",
            static () => new CadLengthDimensionEntity(
                OcctPoint3d.Origin,
                new OcctPoint3d(1, 0, 0),
                OcctVector3d.UnitZ,
                1),
            CadLengthDimensionEntity.WriteGeometry,
            CadLengthDimensionEntity.ReadGeometry);
        entities.Register(
            "text",
            "Text",
            static () => new CadTextEntity(
                "Text",
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                OcctVector3d.UnitX,
                5),
            CadTextEntity.WriteGeometry,
            CadTextEntity.ReadGeometry);
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
        entities.RegisterPersistent(
            "path",
            "Path",
            CadPathEntity.WriteGeometry,
            CadPathEntity.ReadGeometry);
    }

    private static void RegisterSolidEntities(CadEntityRegistry entities)
    {
        entities.Register("box", "Box",
            static () => new CadBoxEntity(OcctPoint3d.Origin, 1, 1, 1),
            CadBoxEntity.WriteGeometry, CadBoxEntity.ReadGeometry);
        entities.Register("cylinder", "Cylinder",
            static () => new CadCylinderEntity(OcctPoint3d.Origin, 1, 1),
            CadCylinderEntity.WriteGeometry, CadCylinderEntity.ReadGeometry);
        entities.Register("cone", "Cone",
            static () => new CadConeEntity(OcctPoint3d.Origin, 1, 1),
            CadConeEntity.WriteGeometry, CadConeEntity.ReadGeometry);
        entities.Register("frustum", "Frustum",
            static () => new CadFrustumEntity(OcctPoint3d.Origin, 1, 0.5, 1),
            CadFrustumEntity.WriteGeometry, CadFrustumEntity.ReadGeometry);
        entities.Register("sphere", "Sphere",
            static () => new CadSphereEntity(OcctPoint3d.Origin, 1),
            CadSphereEntity.WriteGeometry, CadSphereEntity.ReadGeometry);
        entities.Register("helix", "Helix",
            static () => new CadHelixEntity(
                OcctPoint3d.Origin,
                OcctVector3d.UnitZ,
                OcctVector3d.UnitX,
                1,
                1,
                3),
            CadHelixEntity.WriteGeometry, CadHelixEntity.ReadGeometry);
        entities.Register("ellipsoid", "Ellipsoid",
            static () => new CadEllipsoidEntity(OcctPoint3d.Origin, 2, 1.5, 1),
            CadEllipsoidEntity.WriteGeometry, CadEllipsoidEntity.ReadGeometry);
        entities.Register("torus", "Torus",
            static () => new CadTorusEntity(OcctPoint3d.Origin, 2, 0.5),
            CadTorusEntity.WriteGeometry, CadTorusEntity.ReadGeometry);
    }

    private static void RegisterFeatureEntities(CadEntityRegistry entities)
    {
        entities.RegisterPersistent("region", "Region",
            CadRegionEntity.WriteGeometry, CadRegionEntity.ReadGeometry);
        entities.RegisterPersistent("extrude", "Extrude",
            CadExtrudeEntity.WriteGeometry, CadExtrudeEntity.ReadGeometry);
        entities.RegisterPersistent("revolve", "Revolve",
            CadRevolveEntity.WriteGeometry, CadRevolveEntity.ReadGeometry);
        entities.RegisterPersistent("boolean", "Boolean",
            CadBooleanEntity.WriteGeometry, CadBooleanEntity.ReadGeometry);
        entities.RegisterPersistent("sweep", "Sweep",
            CadSweepEntity.WriteGeometry, CadSweepEntity.ReadGeometry);
        entities.RegisterPersistent("loft", "Loft",
            CadLoftEntity.WriteGeometry, CadLoftEntity.ReadGeometry);
        entities.RegisterPersistent("edgefillet", "Edge Fillet",
            CadEdgeFilletEntity.WriteGeometry, CadEdgeFilletEntity.ReadGeometry);
        entities.RegisterPersistent("edgechamfer", "Edge Chamfer",
            CadEdgeChamferEntity.WriteGeometry, CadEdgeChamferEntity.ReadGeometry);
        entities.RegisterPersistent("shell", "Shell",
            CadShellEntity.WriteGeometry, CadShellEntity.ReadGeometry);
        entities.RegisterPersistent("shapeoffset", "Shape Offset",
            CadShapeOffsetEntity.WriteGeometry, CadShapeOffsetEntity.ReadGeometry);
        entities.RegisterPersistent("importedshape", "Imported Shape",
            CadImportedShapeEntity.WriteGeometry, CadImportedShapeEntity.ReadGeometry);
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
        tools.Register<PolygonTool>("polygon");
        tools.Register<RegularPolygonTool>("regularpolygon");
        tools.Register<CircleTool>("circle");
        tools.Register<ArcTool>("arc");
        tools.Register<EllipseTool>("ellipse");
        tools.Register<SplineTool>("spline");
    }

    private static void RegisterAnnotationTools(CadToolRegistry tools)
    {
        tools.Register<TextTool>("text");
        tools.Register<LengthDimensionTool>("lengthdimension");
        tools.Register<AngleDimensionTool>("angledimension");
        tools.Register<CircularDimensionTool>("circulardimension");
    }

    private static void RegisterModelingTools(CadToolRegistry tools)
    {
        tools.Register<BoxTool>("box");
        tools.Register<CylinderTool>("cylinder");
        tools.Register<ConeTool>("cone");
        tools.Register<FrustumTool>("frustum");
        tools.Register<SphereTool>("sphere");
        tools.Register<EllipsoidTool>("ellipsoid");
        tools.Register<TorusTool>("torus");
        tools.Register<HelixTool>("helix");
        tools.Register<ExtrudeTool>("extrude");
        tools.Register<RevolveTool>("revolve");
        tools.Register<SweepTool>("sweep");
        tools.Register<LoftTool>("loft");
    }

    private static void RegisterModifyTools(CadToolRegistry tools)
    {
        tools.Register<MoveTool>("move");
        tools.Register<CopyTool>("copy");
        tools.Register<RotateTool>("rotate");
        tools.Register<ScaleTool>("scale");
        tools.Register<MirrorTool>("mirror");
        tools.Register<ArrayTool>("array");
        tools.Register<OffsetTool>("offset");
        tools.Register<TrimTool>("trim");
        tools.Register<ExtendTool>("extend");
        tools.Register<FilletTool>("fillet");
        tools.Register<ChamferTool>("chamfer");
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
        actions.Register(new CadViewAction(workspace, "view.isometric", "Isometric", OcctViewOrientation.Isometric));
        actions.Register(new CadViewAction(workspace, "view.top", "Top", OcctViewOrientation.Top));
        actions.Register(new CadViewAction(workspace, "view.bottom", "Bottom", OcctViewOrientation.Bottom));
        actions.Register(new CadViewAction(workspace, "view.front", "Front", OcctViewOrientation.Front));
        actions.Register(new CadViewAction(workspace, "view.back", "Back", OcctViewOrientation.Back));
        actions.Register(new CadViewAction(workspace, "view.left", "Left", OcctViewOrientation.Left));
        actions.Register(new CadViewAction(workspace, "view.right", "Right", OcctViewOrientation.Right));
        actions.Register(new CadDisplayModeAction(workspace, "display.wireframe", "Wireframe", OcctDisplayMode.Wireframe));
        actions.Register(new CadDisplayModeAction(workspace, "display.shaded", "Shaded", OcctDisplayMode.Shaded));
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
        actions.Register(Tool(workspace, "draw.regularpolygon.inscribed", "Regular Polygon - Inscribed", "regularpolygon",
            Params(("mode", "Inscribed"))));
        actions.Register(Tool(workspace, "draw.regularpolygon.circumscribed", "Regular Polygon - Circumscribed", "regularpolygon",
            Params(("mode", "Circumscribed"))));
        actions.Register(Tool(workspace, "draw.ellipse", "Ellipse", "ellipse"));
        actions.Register(Tool(workspace, "draw.ellipse.centermajor", "Ellipse - Center Axes", "ellipse",
            Params(("Method", "CenterMajorMinor"))));
        actions.Register(Tool(workspace, "draw.ellipse.axisendpoints", "Ellipse - Axis Endpoints", "ellipse",
            Params(("Method", "AxisEndpointsMinor"))));
        actions.Register(Tool(workspace, "draw.spline", "Spline", "spline"));

        actions.Register(Tool(workspace, "draw.circle", "Circle", "circle"));
        actions.Register(Tool(workspace, "draw.circle.centerradius", "Circle - Center Radius", "circle",
            Params(("Method", "CenterRadius"))));
        actions.Register(Tool(workspace, "draw.circle.centerdiameter", "Circle - Center Diameter", "circle",
            Params(("Method", "CenterDiameter"))));
        actions.Register(Tool(workspace, "draw.circle.twopoints", "Circle - Two Points", "circle",
            Params(("Method", "TwoPoints"))));
        actions.Register(Tool(workspace, "draw.circle.threepoints", "Circle - Three Points", "circle",
            Params(("Method", "ThreePoints"))));
        actions.Register(Tool(workspace, "draw.circle.pointcenter", "Circle - Point Center", "circle",
            Params(("Method", "PointCenter"))));

        actions.Register(Tool(workspace, "draw.arc", "Arc", "arc"));
        actions.Register(Tool(workspace, "draw.arc.threepoints", "Arc - Three Points", "arc",
            Params(("Method", "ThreePoints"))));
        actions.Register(Tool(workspace, "draw.arc.centerstartend", "Arc - Center Start End", "arc",
            Params(("Method", "CenterStartEnd"))));
        actions.Register(Tool(workspace, "draw.arc.startcenterend", "Arc - Start Center End", "arc",
            Params(("Method", "StartCenterEnd"))));
        actions.Register(Tool(workspace, "draw.arc.startendcenter", "Arc - Start End Center", "arc",
            Params(("Method", "StartEndCenter"))));
        actions.Register(Tool(workspace, "draw.arc.startendpoint", "Arc - Start End Point", "arc",
            Params(("Method", "StartEndPoint"))));
        actions.Register(Tool(workspace, "draw.arc.startendtangent", "Arc - Start End Tangent", "arc",
            Params(("Method", "StartEndTangent"))));
    }

    private static void RegisterAnnotationActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(Tool(workspace, "annotate.text", "Text", "text"));
        actions.Register(Tool(workspace, "annotate.length", "Length Dimension", "lengthdimension"));
        actions.Register(Tool(workspace, "annotate.angle", "Angle Dimension", "angledimension"));
        actions.Register(Tool(workspace, "annotate.radius", "Radius Dimension", "circulardimension",
            Params(("Kind", "Radius"))));
        actions.Register(Tool(workspace, "annotate.diameter", "Diameter Dimension", "circulardimension",
            Params(("Kind", "Diameter"))));
    }

    private static void RegisterModelingActions(
        CadActionManager actions,
        CadWorkspace workspace)
    {
        actions.Register(Tool(workspace, "solid.box", "Box", "box"));
        actions.Register(Tool(workspace, "solid.cylinder", "Cylinder", "cylinder"));
        actions.Register(Tool(workspace, "solid.cone", "Cone", "cone"));
        actions.Register(Tool(workspace, "solid.frustum", "Frustum", "frustum"));
        actions.Register(Tool(workspace, "solid.sphere", "Sphere", "sphere"));
        actions.Register(Tool(workspace, "solid.ellipsoid", "Ellipsoid", "ellipsoid"));
        actions.Register(Tool(workspace, "solid.torus", "Torus", "torus"));
        actions.Register(Tool(workspace, "curve.helix", "Helix", "helix"));
        actions.Register(Tool(workspace, "feature.extrude", "Extrude", "extrude"));
        actions.Register(Tool(workspace, "feature.revolve", "Revolve", "revolve"));
        actions.Register(Tool(workspace, "feature.sweep", "Sweep", "sweep"));
        actions.Register(Tool(workspace, "feature.loft", "Loft", "loft"));
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
        actions.Register(Tool(workspace, "modify.offset", "Offset", "offset"));
        actions.Register(Tool(workspace, "modify.trim", "Trim", "trim"));
        actions.Register(Tool(workspace, "modify.extend", "Extend", "extend"));
        actions.Register(Tool(workspace, "modify.fillet", "Fillet", "fillet"));
        actions.Register(Tool(workspace, "modify.chamfer", "Chamfer", "chamfer"));
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
