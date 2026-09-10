namespace OCCAD;

internal static class CadCoreRegistration
{
    public static CadEntityRegistry CreateEntityRegistry()
    {
        var entities = new CadEntityRegistry();

        entities.Register("angledimension", "Angle Dimension", static () => new CadAngleDimensionEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitX, OcctNet.OcctVector3d.UnitY, 1), CadAngleDimensionEntity.WriteGeometry, CadAngleDimensionEntity.ReadGeometry);
        entities.Register("circulardimension", "Circular Dimension", static () => new CadCircularDimensionEntity(CadCircularDimensionKind.Radius, OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, OcctNet.OcctVector3d.UnitX, 1), CadCircularDimensionEntity.WriteGeometry, CadCircularDimensionEntity.ReadGeometry);
        entities.Register("lengthdimension", "Length Dimension", static () => new CadLengthDimensionEntity(OcctNet.OcctPoint3d.Origin, new OcctNet.OcctPoint3d(1, 0, 0), OcctNet.OcctVector3d.UnitZ, 1), CadLengthDimensionEntity.WriteGeometry, CadLengthDimensionEntity.ReadGeometry);
        entities.Register("text", "Text", static () => new CadTextEntity("Text", OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, OcctNet.OcctVector3d.UnitX, 5), CadTextEntity.WriteGeometry, CadTextEntity.ReadGeometry);

        entities.Register("point", "Point", static () => new CadPointEntity(OcctNet.OcctPoint3d.Origin), CadPointEntity.WriteGeometry, CadPointEntity.ReadGeometry);
        entities.Register("line", "Line", static () => new CadLineEntity(OcctNet.OcctPoint3d.Origin, new OcctNet.OcctPoint3d(1, 0, 0)), CadLineEntity.WriteGeometry, CadLineEntity.ReadGeometry);
        entities.Register("polyline", "Polyline", static () => new CadPolylineEntity([OcctNet.OcctPoint3d.Origin, new OcctNet.OcctPoint3d(1, 0, 0)]), CadPolylineEntity.WriteGeometry, CadPolylineEntity.ReadGeometry);
        entities.Register("polygon", "Polygon", static () => new CadPolygonEntity([OcctNet.OcctPoint3d.Origin, new OcctNet.OcctPoint3d(1, 0, 0), new OcctNet.OcctPoint3d(0, 1, 0)]), CadPolygonEntity.WriteGeometry, CadPolygonEntity.ReadGeometry);
        entities.Register("regularpolygon", "Regular Polygon", static () => new CadRegularPolygonEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, OcctNet.OcctVector3d.UnitX, 1, 3), CadRegularPolygonEntity.WriteGeometry, CadRegularPolygonEntity.ReadGeometry);
        entities.Register("circle", "Circle", static () => new CadCircleEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, 1), CadCircleEntity.WriteGeometry, CadCircleEntity.ReadGeometry);
        entities.Register("rectangle", "Rectangle", static () => new CadRectangleEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitX, OcctNet.OcctVector3d.UnitY, 1, 1), CadRectangleEntity.WriteGeometry, CadRectangleEntity.ReadGeometry);
        entities.Register("arc", "Arc", static () => new CadArcEntity(new OcctNet.OcctPoint3d(1, 0, 0), new OcctNet.OcctPoint3d(0, 1, 0), new OcctNet.OcctPoint3d(-1, 0, 0)), CadArcEntity.WriteGeometry, CadArcEntity.ReadGeometry);
        entities.Register("ellipse", "Ellipse", static () => new CadEllipseEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, 2, 1), CadEllipseEntity.WriteGeometry, CadEllipseEntity.ReadGeometry);
        entities.Register("spline", "Spline", static () => new CadSplineEntity([OcctNet.OcctPoint3d.Origin, new OcctNet.OcctPoint3d(1, 0, 0)]), CadSplineEntity.WriteGeometry, CadSplineEntity.ReadGeometry);
        entities.RegisterPersistent("path", "Path", CadPathEntity.WriteGeometry, CadPathEntity.ReadGeometry);

        entities.Register("box", "Box", static () => new CadBoxEntity(OcctNet.OcctPoint3d.Origin, 1, 1, 1), CadBoxEntity.WriteGeometry, CadBoxEntity.ReadGeometry);
        entities.Register("cylinder", "Cylinder", static () => new CadCylinderEntity(OcctNet.OcctPoint3d.Origin, 1, 1), CadCylinderEntity.WriteGeometry, CadCylinderEntity.ReadGeometry);
        entities.Register("cone", "Cone", static () => new CadConeEntity(OcctNet.OcctPoint3d.Origin, 1, 1), CadConeEntity.WriteGeometry, CadConeEntity.ReadGeometry);
        entities.Register("frustum", "Frustum", static () => new CadFrustumEntity(OcctNet.OcctPoint3d.Origin, 1, 0.5, 1), CadFrustumEntity.WriteGeometry, CadFrustumEntity.ReadGeometry);
        entities.Register("sphere", "Sphere", static () => new CadSphereEntity(OcctNet.OcctPoint3d.Origin, 1), CadSphereEntity.WriteGeometry, CadSphereEntity.ReadGeometry);
        entities.Register("helix", "Helix", static () => new CadHelixEntity(OcctNet.OcctPoint3d.Origin, OcctNet.OcctVector3d.UnitZ, OcctNet.OcctVector3d.UnitX, 1, 1, 3), CadHelixEntity.WriteGeometry, CadHelixEntity.ReadGeometry);
        entities.Register("ellipsoid", "Ellipsoid", static () => new CadEllipsoidEntity(OcctNet.OcctPoint3d.Origin, 2, 1.5, 1), CadEllipsoidEntity.WriteGeometry, CadEllipsoidEntity.ReadGeometry);

        entities.Register("torus", "Torus", static () => new CadTorusEntity(OcctNet.OcctPoint3d.Origin, 2, 0.5), CadTorusEntity.WriteGeometry, CadTorusEntity.ReadGeometry);

        entities.RegisterPersistent("region", "Region", CadRegionEntity.WriteGeometry, CadRegionEntity.ReadGeometry);
        entities.RegisterPersistent("extrude", "Extrude", CadExtrudeEntity.WriteGeometry, CadExtrudeEntity.ReadGeometry);
        entities.RegisterPersistent("revolve", "Revolve", CadRevolveEntity.WriteGeometry, CadRevolveEntity.ReadGeometry);
        entities.RegisterPersistent("boolean", "Boolean", CadBooleanEntity.WriteGeometry, CadBooleanEntity.ReadGeometry);
        entities.RegisterPersistent("sweep", "Sweep", CadSweepEntity.WriteGeometry, CadSweepEntity.ReadGeometry);
        entities.RegisterPersistent("loft", "Loft", CadLoftEntity.WriteGeometry, CadLoftEntity.ReadGeometry);
        entities.RegisterPersistent("edgefillet", "Edge Fillet", CadEdgeFilletEntity.WriteGeometry, CadEdgeFilletEntity.ReadGeometry);
        entities.RegisterPersistent("edgechamfer", "Edge Chamfer", CadEdgeChamferEntity.WriteGeometry, CadEdgeChamferEntity.ReadGeometry);
        entities.RegisterPersistent("shell", "Shell", CadShellEntity.WriteGeometry, CadShellEntity.ReadGeometry);
        entities.RegisterPersistent("shapeoffset", "Shape Offset", CadShapeOffsetEntity.WriteGeometry, CadShapeOffsetEntity.ReadGeometry);
        entities.RegisterPersistent("importedshape", "Imported Shape", CadImportedShapeEntity.WriteGeometry, CadImportedShapeEntity.ReadGeometry);

        return entities;
    }

    public static CadToolRegistry CreateToolRegistry()
    {
        var tools = new CadToolRegistry();
        tools.Register<MeasureDistanceTool>("distance");
        tools.Register<AngleDimensionTool>("angledimension");
        tools.Register<RadiusDimensionTool>("radiusdimension");
        tools.Register<DiameterDimensionTool>("diameterdimension");
        tools.Register<LengthDimensionTool>("lengthdimension");
        tools.Register<TextTool>("text");
        tools.Register<PointTool>("point");
        tools.Register<LineTool>("line");
        tools.Register<PolylineTool>("polyline");
        tools.Register<RectangleTool>("rectangle");
        tools.Register<PolygonTool>("polygon");
        tools.Register<RegularPolygonTool>("regularpolygon");
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
        tools.Register<HelixTool>("helix");
        tools.Register<TorusTool>("torus");
        tools.Register<CircularArrayTool>("circlearray");
        tools.Register<PathArrayTool>("patharray");
        tools.Register<RectangularArrayTool>("rectarray");
        tools.Register<MirrorTool>("mirror");
        tools.Register<OffsetTool>("offset");
        tools.Register<TrimTool>("trim");
        tools.Register<ExtendTool>("extend");
        tools.Register<BreakTool>("break");
        tools.Register<FilletTool>("fillet");
        tools.Register<ChamferTool>("chamfer");
        tools.Register<ExtrudeTool>("extrude");
        tools.Register<RevolveTool>("revolve");
        tools.Register<BooleanTool>("boolean");
        tools.Register<SweepTool>("sweep");
        tools.Register<LoftTool>("loft");
        tools.Register<EdgeFilletTool>("edgefillet");
        tools.Register<EdgeChamferTool>("edgechamfer");
        tools.Register<ShellTool>("shell");
        tools.Register<ShapeOffsetTool>("shapeoffset");
        tools.Register<RotateTool>("rotate");
        tools.Register<ScaleTool>("scale");
        tools.Register<MoveTool>("move");
        tools.Register<CopyTool>("copy");
        return tools;
    }

    public static void RegisterActions(CadActionManager actions, CadWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(workspace);

        actions.Register(new CadNewDocumentAction(workspace));
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

        actions.Register(new CadViewAction(workspace, "view.fit", "Fit"));
        actions.Register(new CadViewAction(workspace, "view.isometric", "Isometric", OcctNet.OcctViewOrientation.Isometric));
        actions.Register(new CadViewAction(workspace, "view.top", "Top", OcctNet.OcctViewOrientation.Top));
        actions.Register(new CadViewAction(workspace, "view.bottom", "Bottom", OcctNet.OcctViewOrientation.Bottom));
        actions.Register(new CadViewAction(workspace, "view.front", "Front", OcctNet.OcctViewOrientation.Front));
        actions.Register(new CadViewAction(workspace, "view.back", "Back", OcctNet.OcctViewOrientation.Back));
        actions.Register(new CadViewAction(workspace, "view.left", "Left", OcctNet.OcctViewOrientation.Left));
        actions.Register(new CadViewAction(workspace, "view.right", "Right", OcctNet.OcctViewOrientation.Right));
        actions.Register(new CadDisplayModeAction(workspace, "display.wireframe", "Wireframe", OcctNet.OcctDisplayMode.Wireframe));
        actions.Register(new CadDisplayModeAction(workspace, "display.shaded", "Shaded", OcctNet.OcctDisplayMode.Shaded));

        actions.Register(new CadToolAction(workspace, "annotation.angledimension", "Angle Dimension", "angledimension"));
        actions.Register(new CadToolAction(workspace, "annotation.radiusdimension", "Radius Dimension", "radiusdimension"));
        actions.Register(new CadToolAction(workspace, "annotation.diameterdimension", "Diameter Dimension", "diameterdimension"));
        actions.Register(new CadToolAction(workspace, "annotation.lengthdimension", "Length Dimension", "lengthdimension"));
        actions.Register(new CadToolAction(workspace, "measure.distance", "Distance", "distance"));
        actions.Register(new CadToolAction(workspace, "draw.text", "Text", "text"));
        actions.Register(new CadToolAction(workspace, "draw.point", "Point", "point"));
        actions.Register(new CadToolAction(workspace, "draw.line", "Line", "line"));
        actions.Register(new CadToolAction(workspace, "draw.polyline", "Polyline", "polyline"));
        actions.Register(new CadToolAction(workspace, "draw.rectangle", "Rectangle", "rectangle"));
        actions.Register(new CadToolAction(workspace, "draw.polygon", "Polygon", "polygon"));
        actions.Register(new CadToolAction(workspace, "draw.regularpolygon", "Regular Polygon", "regularpolygon"));

        actions.Register(new CadToolAction(workspace, "draw.circle", "Circle", "circle"));
        actions.Register(new CadToolAction(workspace, "draw.circle.centerradius", "Circle - Center Radius", "circle", initialParameters: new Dictionary<string, string> { ["Method"] = "CenterRadius" }));
        actions.Register(new CadToolAction(workspace, "draw.circle.centerdiameter", "Circle - Center Diameter", "circle", initialParameters: new Dictionary<string, string> { ["Method"] = "CenterDiameter" }));
        actions.Register(new CadToolAction(workspace, "draw.circle.twopoints", "Circle - Two Points", "circle", initialParameters: new Dictionary<string, string> { ["Method"] = "TwoPoints" }));
        actions.Register(new CadToolAction(workspace, "draw.circle.threepoints", "Circle - Three Points", "circle", initialParameters: new Dictionary<string, string> { ["Method"] = "ThreePoints" }));
        actions.Register(new CadToolAction(workspace, "draw.circle.pointcenter", "Circle - Point Center", "circle", initialParameters: new Dictionary<string, string> { ["Method"] = "PointCenter" }));

        actions.Register(new CadToolAction(workspace, "draw.arc", "Arc", "arc"));
        actions.Register(new CadToolAction(workspace, "draw.arc.threepoints", "Arc - Three Points", "arc", initialParameters: new Dictionary<string, string> { ["Method"] = "ThreePoints" }));
        actions.Register(new CadToolAction(workspace, "draw.arc.centerstartend", "Arc - Center Start End", "arc", initialParameters: new Dictionary<string, string> { ["Method"] = "CenterStartEnd" }));
        actions.Register(new CadToolAction(workspace, "draw.arc.startcenterend", "Arc - Start Center End", "arc", initialParameters: new Dictionary<string, string> { ["Method"] = "StartCenterEnd" }));
        actions.Register(new CadToolAction(workspace, "draw.arc.startendcenter", "Arc - Start End Center", "arc", initialParameters: new Dictionary<string, string> { ["Method"] = "StartEndCenter" }));
        actions.Register(new CadToolAction(workspace, "draw.arc.startendpoint", "Arc - Start End Point", "arc", initialParameters: new Dictionary<string, string> { ["Method"] = "StartEndPoint" }));

        actions.Register(new CadToolAction(workspace, "draw.ellipse", "Ellipse", "ellipse"));
        actions.Register(new CadToolAction(workspace, "draw.ellipse.centermajorminor", "Ellipse - Center Major Minor", "ellipse", initialParameters: new Dictionary<string, string> { ["Method"] = "CenterMajorMinor" }));
        actions.Register(new CadToolAction(workspace, "draw.ellipse.axisendpointsminor", "Ellipse - Axis Endpoints Minor", "ellipse", initialParameters: new Dictionary<string, string> { ["Method"] = "AxisEndpointsMinor" }));
        actions.Register(new CadToolAction(workspace, "draw.spline", "Spline", "spline"));
        actions.Register(new CadToolAction(workspace, "solid.box", "Box", "box"));
        actions.Register(new CadToolAction(workspace, "solid.cylinder", "Cylinder", "cylinder"));
        actions.Register(new CadToolAction(workspace, "solid.cone", "Cone", "cone"));
        actions.Register(new CadToolAction(workspace, "solid.frustum", "Frustum", "frustum"));
        actions.Register(new CadToolAction(workspace, "solid.sphere", "Sphere", "sphere"));
        actions.Register(new CadToolAction(workspace, "solid.ellipsoid", "Ellipsoid", "ellipsoid"));
        actions.Register(new CadToolAction(workspace, "solid.helix", "Helix", "helix"));
        actions.Register(new CadToolAction(workspace, "solid.torus", "Torus", "torus"));
        actions.Register(new CadToolAction(workspace, "modify.circlearray", "Circular Array", "circlearray"));
        actions.Register(new CadToolAction(workspace, "modify.patharray", "Path Array", "patharray"));
        actions.Register(new CadToolAction(workspace, "modify.rectarray", "Rectangular Array", "rectarray"));
        actions.Register(new CadToolAction(workspace, "modify.mirror", "Mirror", "mirror"));
        actions.Register(new CadToolAction(workspace, "modify.offset", "Offset", "offset"));
        actions.Register(new CadToolAction(workspace, "modify.trim", "Trim", "trim"));
        actions.Register(new CadToolAction(workspace, "modify.extend", "Extend", "extend"));
        actions.Register(new CadJoinAction(workspace));
        actions.Register(new CadToolAction(workspace, "modify.break", "Break", "break"));
        actions.Register(new CadToolAction(workspace, "modify.fillet", "Fillet", "fillet"));
        actions.Register(new CadToolAction(workspace, "modify.chamfer", "Chamfer", "chamfer"));
        actions.Register(new CadRegionAction(workspace));
        actions.Register(new CadToolAction(workspace, "solid.extrude", "Extrude", "extrude"));
        actions.Register(new CadToolAction(workspace, "solid.revolve", "Revolve", "revolve"));
        actions.Register(new CadToolAction(workspace, "solid.boolean.union", "Boolean Union", "boolean", initialParameters: new Dictionary<string, string> { ["Operation"] = "Union" }));
        actions.Register(new CadToolAction(workspace, "solid.boolean.cut", "Boolean Cut", "boolean", initialParameters: new Dictionary<string, string> { ["Operation"] = "Cut" }));
        actions.Register(new CadToolAction(workspace, "solid.boolean.common", "Boolean Common", "boolean", initialParameters: new Dictionary<string, string> { ["Operation"] = "Common" }));
        actions.Register(new CadToolAction(workspace, "solid.sweep", "Sweep", "sweep"));
        actions.Register(new CadToolAction(workspace, "solid.loft", "Loft", "loft"));
        actions.Register(new CadEdgeFeatureAction(workspace, "solid.edgefillet", "Edge Fillet", "edgefillet"));
        actions.Register(new CadEdgeFeatureAction(workspace, "solid.edgechamfer", "Edge Chamfer", "edgechamfer"));
        actions.Register(new CadShellAction(workspace));
        actions.Register(new CadToolAction(workspace, "solid.shapeoffset", "Shape Offset", "shapeoffset"));
        actions.Register(new CadToolAction(workspace, "modify.rotate", "Rotate", "rotate"));
        actions.Register(new CadToolAction(workspace, "modify.scale", "Scale", "scale"));
        actions.Register(new CadToolAction(workspace, "modify.move", "Move", "move"));
        actions.Register(new CadToolAction(workspace, "modify.copy", "Copy", "copy"));
    }
}





