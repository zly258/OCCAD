namespace OCCAD.Core.Tests;

[TestClass]
public sealed class CapabilitySurfaceTests
{
    [TestMethod]
    public void EntityRegistryContainsOnlyCurrentCommon2DAnd3DSurface()
    {
        using var workspace = new CadWorkspace();

        var expected = new[]
        {
            "point",
            "line",
            "centerline",
            "centermark",
            "polyline",
            "rectangle",
            "circle",
            "arc",
            "ellipse",
            "spline",
            "polygon",
            "regularpolygon",
            "box",
            "cylinder",
            "cone",
            "sphere"
        };

        CollectionAssert.AreEquivalent(
            expected,
            workspace.Entities.Descriptors.Select(static item => item.Id).ToArray());
    }

    [TestMethod]
    public void EditSurfaceExposesOnlySourceAlignedCommonTransforms()
    {
        using var workspace = new CadWorkspace();

        foreach (var toolId in new[]
                 {
                     "move",
                     "copy",
                     "rotate",
                     "scale",
                     "mirror",
                     "array"
                 })
        {
            Assert.IsTrue(
                workspace.Tools.IsRegistered(toolId),
                $"Expected edit tool '{toolId}' to be registered.");
            Assert.IsNotNull(
                workspace.Actions.Find($"modify.{toolId}"),
                $"Expected edit action 'modify.{toolId}' to be registered.");
        }

        foreach (var toolId in new[]
                 {
                     "offset",
                     "trim",
                     "extend",
                     "fillet",
                     "chamfer"
                 })
        {
            Assert.IsFalse(
                workspace.Tools.IsRegistered(toolId),
                $"Dormant edit tool '{toolId}' leaked into the active surface.");
            Assert.IsNull(
                workspace.Actions.Find($"modify.{toolId}"),
                $"Dormant edit action 'modify.{toolId}' leaked into the active surface.");
        }
    }

    [TestMethod]
    public void ExperimentalModelingAndAnnotationSurfaceIsNotRegistered()
    {
        using var workspace = new CadWorkspace();

        foreach (var entityId in new[]
                 {
                     "text",
                     "lengthdimension",
                     "angledimension",
                     "circulardimension",
                     "frustum",
                     "helix",
                     "ellipsoid",
                     "torus",
                     "region",
                     "extrude",
                     "revolve",
                     "boolean",
                     "sweep",
                     "loft",
                     "edgefillet",
                     "edgechamfer",
                     "shell",
                     "shapeoffset",
                     "importedshape",
                     "path"
                 })
        {
            Assert.IsFalse(
                workspace.Entities.Contains(entityId),
                $"Dormant entity '{entityId}' leaked into the active registry.");
        }

        foreach (var toolId in new[]
                 {
                     "text",
                     "lengthdimension",
                     "angledimension",
                     "circulardimension",
                     "frustum",
                     "helix",
                     "ellipsoid",
                     "torus",
                     "extrude",
                     "revolve",
                     "sweep",
                     "loft"
                 })
        {
            Assert.IsFalse(
                workspace.Tools.IsRegistered(toolId),
                $"Dormant tool '{toolId}' leaked into the active surface.");
        }

        foreach (var actionId in new[]
                 {
                     "annotate.text",
                     "annotate.length",
                     "annotate.angle",
                     "annotate.radius",
                     "annotate.diameter",
                     "solid.frustum",
                     "solid.ellipsoid",
                     "solid.torus",
                     "curve.helix",
                     "feature.extrude",
                     "feature.revolve",
                     "feature.sweep",
                     "feature.loft"
                 })
        {
            Assert.IsNull(
                workspace.Actions.Find(actionId),
                $"Dormant action '{actionId}' leaked into the active surface.");
        }
    }

    [TestMethod]
    public void CommandCatalogResolvesOnlyActiveAliases()
    {
        using var workspace = new CadWorkspace();

        Assert.AreEqual(
            "draw.centerline",
            workspace.Actions.ResolveCommand("CL")?.Id);
        Assert.AreEqual(
            "draw.centermark",
            workspace.Actions.ResolveCommand("CM")?.Id);
        Assert.AreEqual(
            "draw.line",
            workspace.Actions.ResolveCommand("L")?.Id);
        Assert.AreEqual(
            "modify.move",
            workspace.Actions.ResolveCommand("M")?.Id);

        foreach (var dormantAlias in new[]
                 {
                     "TRIM",
                     "EXTEND",
                     "FILLET",
                     "CHAMFER",
                     "EXTRUDE",
                     "REVOLVE",
                     "SWEEP",
                     "LOFT",
                     "TEXT",
                     "DIMLINEAR"
                 })
        {
            Assert.IsNull(
                workspace.Actions.ResolveCommand(dormantAlias),
                $"Dormant command alias '{dormantAlias}' leaked into the active catalog.");
        }
    }

    [TestMethod]
    public void ActiveCoreSurfaceHasNoDeletedPathCompatibilityTypes()
    {
        foreach (var entityType in new[]
                 {
                     typeof(CadLineEntity),
                     typeof(CadArcEntity),
                     typeof(CadPolylineEntity)
                 })
        {
            Assert.IsFalse(
                entityType.GetMethods()
                    .Any(static method =>
                        method.ReturnType.Name == "CadPathEntity" ||
                        method.GetParameters().Any(static parameter =>
                            parameter.ParameterType.Name == "CadPathEntity")),
                $"{entityType.Name} still exposes deleted CadPathEntity compatibility.");
        }

        Assert.IsFalse(
            typeof(CadSubobjectSelectionManager)
                .GetMethods()
                .Any(static method =>
                    method.ReturnType.Name == "CadPathSegmentInfo" ||
                    method.GetParameters().Any(static parameter =>
                        parameter.ParameterType.Name == "CadPathSegmentInfo")),
            "Subobject selection still exposes deleted CadPathSegmentInfo compatibility.");
    }
}
