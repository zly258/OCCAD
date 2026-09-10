namespace OCCAD;

internal static class CadCoreRegistration
{
    public static CadEntityRegistry CreateEntityRegistry()
    {
        CadPlanarNormalPropertyMetadata.Initialize();
        var entities = new CadEntityRegistry();

        // 2D drafting entities exposed by the initial release.
        entities.Register("point", "Point",
            static () => new CadPointEntity(OcctNet.OcctPoint3d.Origin),
            CadPointEntity.WriteGeometry, CadPointEntity.ReadGeometry);
        entities.Register("line", "Line",
            static () => new CadLineEntity(
                OcctNet.OcctPoint3d.Origin,
                new OcctNet.OcctPoint3d(1, 0, 0)),
            CadLineEntity.WriteGeometry, CadLineEntity.ReadGeometry);
        entities.Register("polyline", "Polyline",
            static () => new CadPolylineEntity([
                OcctNet.OcctPoint3d.Origin,
                new OcctNet.OcctPoint3d(1, 0, 0)]),
            CadPolylineEntity.WriteGeometry, CadPolylineEntity.ReadGeometry);
        entities.Register("polygon", "Polygon",
            static () => new CadPolygonEntity([
                OcctNet.OcctPoint3d.Origin,
                new OcctNet.OcctPoint3d(1, 0, 0),
                new OcctNet.OcctPoint3d(0, 1, 0)]),
            CadPolygonEntity.WriteGeometry, CadPolygonEntity.ReadGeometry);
        entities.Register("regularpolygon", "Regular Polygon",
            static () => new CadRegularPolygonEntity(
                OcctNet.OcctPoint3d.Origin,
                OcctNet.OcctVector3d.UnitZ,
                OcctNet.OcctVector3d.UnitX,
                1,
                3),
            CadRegularPolygonEntity.WriteGeometry, CadRegularPolygonEntity.ReadGeometry);
        entities.Register("rectangle", "Rectangle",
            static () => new CadRectangleEntity(
                OcctNet.OcctPoint3d.Origin,
                OcctNet.OcctVector3d.UnitX,
                OcctNet.OcctVector3d.UnitY,
                1,
                1),
            CadRectangleEntity.WriteGeometry, CadRectangleEntity.ReadGeometry);
        entities.Register("circle", "Circle",
            static () => new CadCircleEntity(
                OcctNet.OcctPoint3d.Origin,
                OcctNet.OcctVector3d.UnitZ,
                1),
            CadCircleEntity.WriteGeometry, CadCircleEntity.ReadGeometry);
        entities.Register("arc", "Arc",
            static () => new CadArcEntity(
                new OcctNet.OcctPoint3d(1, 0, 0),
                new OcctNet.OcctPoint3d(0, 1, 0),
                new OcctNet.OcctPoint3d(-1, 0, 0)),
            CadArcEntity.WriteGeometry, CadArcEntity.ReadGeometry);
        entities.Register("ellipse", "Ellipse",
            static () => new CadEllipseEntity(
                OcctNet.OcctPoint3d.Origin,
                OcctNet.OcctVector3d.UnitZ,
                2,
                1),
            CadEllipseEntity.WriteGeometry, CadEllipseEntity.ReadGeometry);
        entities.Register("spline", "Spline",
            static () => new CadSplineEntity([
                OcctNet.OcctPoint3d.Origin,
                new OcctNet.OcctPoint3d(1, 0, 0)]),
            CadSplineEntity.WriteGeometry, CadSplineEntity.ReadGeometry);
        entities.RegisterPersistent("path", "Path",
            CadPathEntity.WriteGeometry, CadPathEntity.ReadGeometry);

        // Common 3D primitives and curves.
        entities.Register("box", "Box",
            static () => new CadBoxEntity(OcctNet.OcctPoint3d.Origin, 1, 1, 1),
            CadBoxEntity.WriteGeometry, CadBoxEntity.ReadGeometry);
        entities.Register("cylinder", "Cylinder",
            static () => new CadCylinderEntity(OcctNet.OcctPoint3d.Origin, 1, 1),
            CadCylinderEntity.WriteGeometry, CadCylinderEntity.ReadGeometry);
        entities.Register("cone", "Cone",
            static () => new CadConeEntity(OcctNet.OcctPoint3d.Origin, 1, 1),
            CadConeEntity.WriteGeometry, CadConeEntity.ReadGeometry);
        entities.Register("frustum", "Frustum",
            static () => new CadFrustumEntity(OcctNet.OcctPoint3d.Origin, 1, 0.5, 1),
            CadFrustumEntity.WriteGeometry, CadFrustumEntity.ReadGeometry);
        entities.Register("sphere", "Sphere",
            static () => new CadSphereEntity(OcctNet.OcctPoint3d.Origin, 1),
            CadSphereEntity.WriteGeometry, CadSphereEntity.ReadGeometry);
        entities.Register("ellipsoid", "Ellipsoid",
            static () => new CadEllipsoidEntity(OcctNet.OcctPoint3d.Origin, 2, 1.5, 1),
            CadEllipsoidEntity.WriteGeometry, CadEllipsoidEntity.ReadGeometry);
        entities.Register("torus", "Torus",
            static () => new CadTorusEntity(OcctNet.OcctPoint3d.Origin, 2, 0.5),
            CadTorusEntity.WriteGeometry, CadTorusEntity.ReadGeometry);
        entities.Register("helix", "Helix",
            static () => new CadHelixEntity(
                OcctNet.OcctPoint3d.Origin,
                OcctNet.OcctVector3d.UnitZ,
                OcctNet.OcctVector3d.UnitX,
                1,
                1,
                3),
            CadHelixEntity.WriteGeometry, CadHelixEntity.ReadGeometry);

        // Feature entities require document context, so only persistence is
        // registered here. Their interactive creation is owned by Tools.
        entities.RegisterPersistent("extrude", "Extrude",
            CadExtrudeEntity.WriteGeometry, CadExtrudeEntity.ReadGeometry);
        entities.RegisterPersistent("revolve", "Revolve",
            CadRevolveEntity.WriteGeometry, CadRevolveEntity.ReadGeometry);
        entities.RegisterPersistent("sweep", "Sweep",
            CadSweepEntity.WriteGeometry, CadSweepEntity.ReadGeometry);
        entities.RegisterPersistent("loft", "Loft",
            CadLoftEntity.WriteGeometry, CadLoftEntity.ReadGeometry);

        return entities;
    }

    public static CadToolRegistry CreateToolRegistry()
    {
        var tools = new CadToolRegistry();

        tools.Register<PointTool>("point");
        tools.Register<LineTool>("line");
        tools.Register<PolylineTool>("polyline");
        tools.Register<PolygonTool>("polygon");
        tools.Register<RegularPolygonTool>("regularpolygon");
        tools.Register<RectangleTool>("rectangle");
        tools.Register<CircleTool>("circle");
        tools.Register<ArcTool>("arc");
        tools.Register<EllipseTool>("ellipse");
        tools.Register<SplineTool>("spline");

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

        return tools;
    }

    public static void RegisterActions(CadActionManager actions, CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(workspace);

        RegisterViewActions(actions, workspace);
        Register2DActions(actions, workspace);
        Register3DActions(actions, workspace);
    }

    private static void RegisterViewActions(CadActionManager actions, CadWorkspace workspace)
    {
        actions.Register(new CadViewAction(workspace, "view.fit", "Fit"));
        actions.Register(new CadViewAction(workspace, "view.top", "Top", OcctNet.OcctViewOrientation.Top));
        actions.Register(new CadViewAction(workspace, "view.bottom", "Bottom", OcctNet.OcctViewOrientation.Bottom));
        actions.Register(new CadViewAction(workspace, "view.front", "Front", OcctNet.OcctViewOrientation.Front));
        actions.Register(new CadViewAction(workspace, "view.back", "Back", OcctNet.OcctViewOrientation.Back));
        actions.Register(new CadViewAction(workspace, "view.left", "Left", OcctNet.OcctViewOrientation.Left));
        actions.Register(new CadViewAction(workspace, "view.right", "Right", OcctNet.OcctViewOrientation.Right));

        actions.Register(new CadCornerViewAction(workspace, "view.iso.ne", "Iso NE", 1.0, 1.0));
        actions.Register(new CadCornerViewAction(workspace, "view.iso.nw", "Iso NW", -1.0, 1.0));
        actions.Register(new CadCornerViewAction(workspace, "view.iso.se", "Iso SE", 1.0, -1.0));
        actions.Register(new CadCornerViewAction(workspace, "view.iso.sw", "Iso SW", -1.0, -1.0));

        actions.Register(new CadDisplayModeAction(
            workspace, "display.wireframe", "Wireframe", OcctNet.OcctDisplayMode.Wireframe));
        actions.Register(new CadDisplayModeAction(
            workspace, "display.shaded", "Shaded", OcctNet.OcctDisplayMode.Shaded));
    }

    private static void Register2DActions(CadActionManager actions, CadWorkspace workspace)
    {
        actions.Register(new CadToolAction(workspace, "draw.point", "Point", "point"));
        actions.Register(new CadToolAction(workspace, "draw.line", "Line", "line"));
        actions.Register(new CadToolAction(workspace, "draw.polyline", "Polyline", "polyline"));
        actions.Register(new CadToolAction(workspace, "draw.polygon", "Polygon", "polygon"));
        actions.Register(new CadToolAction(workspace, "draw.rectangle", "Rectangle", "rectangle"));
        actions.Register(new CadToolAction(workspace, "draw.spline", "Spline", "spline"));

        actions.Register(new CadToolAction(
            workspace, "draw.regularpolygon.inscribed", "Regular Polygon - Inscribed", "regularpolygon",
            initialParameters: new Dictionary<string, string> { ["Mode"] = "Inscribed" }));
        actions.Register(new CadToolAction(
            workspace, "draw.regularpolygon.circumscribed", "Regular Polygon - Circumscribed", "regularpolygon",
            initialParameters: new Dictionary<string, string> { ["Mode"] = "Circumscribed" }));

        RegisterToolVariant(actions, workspace, "draw.circle.centerradius", "Circle - Center Radius", "circle", "Method", "CenterRadius");
        RegisterToolVariant(actions, workspace, "draw.circle.centerdiameter", "Circle - Center Diameter", "circle", "Method", "CenterDiameter");
        RegisterToolVariant(actions, workspace, "draw.circle.twopoints", "Circle - Two Points", "circle", "Method", "TwoPoints");
        RegisterToolVariant(actions, workspace, "draw.circle.threepoints", "Circle - Three Points", "circle", "Method", "ThreePoints");
        RegisterToolVariant(actions, workspace, "draw.circle.pointcenter", "Circle - Point Center", "circle", "Method", "PointCenter");

        RegisterToolVariant(actions, workspace, "draw.arc.threepoints", "Arc - Three Points", "arc", "Method", "ThreePoints");
        RegisterToolVariant(actions, workspace, "draw.arc.centerstartend", "Arc - Center Start End", "arc", "Method", "CenterStartEnd");
        RegisterToolVariant(actions, workspace, "draw.arc.startcenterend", "Arc - Start Center End", "arc", "Method", "StartCenterEnd");
        RegisterToolVariant(actions, workspace, "draw.arc.startendcenter", "Arc - Start End Center", "arc", "Method", "StartEndCenter");
        RegisterToolVariant(actions, workspace, "draw.arc.startendpoint", "Arc - Start End Point", "arc", "Method", "StartEndPoint");
        RegisterToolVariant(actions, workspace, "draw.arc.startendtangent", "Arc - Start End Tangent", "arc", "Method", "StartEndTangent");

        RegisterToolVariant(actions, workspace, "draw.ellipse.centermajor", "Ellipse - Center Axes", "ellipse", "Method", "CenterMajorMinor");
        RegisterToolVariant(actions, workspace, "draw.ellipse.axisendpoints", "Ellipse - Axis Endpoints", "ellipse", "Method", "AxisEndpointsMinor");
    }

    private static void Register3DActions(CadActionManager actions, CadWorkspace workspace)
    {
        actions.Register(new CadToolAction(workspace, "solid.box", "Box", "box"));
        actions.Register(new CadToolAction(workspace, "solid.cylinder", "Cylinder", "cylinder"));
        actions.Register(new CadToolAction(workspace, "solid.cone", "Cone", "cone"));
        actions.Register(new CadToolAction(workspace, "solid.frustum", "Frustum", "frustum"));
        actions.Register(new CadToolAction(workspace, "solid.sphere", "Sphere", "sphere"));
        actions.Register(new CadToolAction(workspace, "solid.ellipsoid", "Ellipsoid", "ellipsoid"));
        actions.Register(new CadToolAction(workspace, "solid.torus", "Torus", "torus"));
        actions.Register(new CadToolAction(workspace, "curve.helix", "Helix", "helix"));

        actions.Register(new CadToolAction(workspace, "feature.extrude", "Extrude", "extrude"));
        actions.Register(new CadToolAction(workspace, "feature.revolve", "Revolve", "revolve"));
        actions.Register(new CadToolAction(workspace, "feature.sweep", "Sweep", "sweep"));
        actions.Register(new CadToolAction(workspace, "feature.loft", "Loft", "loft"));
    }

    private static void RegisterToolVariant(
        CadActionManager actions,
        CadWorkspace workspace,
        string actionId,
        string displayName,
        string toolId,
        string parameter,
        string value) =>
        actions.Register(new CadToolAction(
            workspace,
            actionId,
            displayName,
            toolId,
            initialParameters: new Dictionary<string, string> { [parameter] = value }));
}
