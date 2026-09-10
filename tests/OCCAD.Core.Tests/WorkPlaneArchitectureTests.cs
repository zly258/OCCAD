using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class WorkPlaneArchitectureTests
{
    [TestMethod]
    public void FixedToolPlaneRejectsOriginAndPresetChangesUntilCleanup()
    {
        var plane = new CadWorkPlane();
        plane.BeginToolPlane(new(10, 20, 30));
        plane.SetToolPlaneFixed(true);
        var fixedFrame = plane.EffectivePlane;

        plane.SetOrigin(new(100, 200, 300));
        plane.SetPreset(CadWorkPlanePreset.YZ, new(5, 6, 7));

        Assert.AreEqual(fixedFrame, plane.EffectivePlane);
        Assert.IsTrue(plane.EffectivePlaneFixed);
        Assert.IsFalse(plane.CanChangeEffectivePlane);

        plane.EndToolPlane();
        Assert.IsFalse(plane.IsActive);
        Assert.IsFalse(plane.EffectivePlaneFixed);
        Assert.IsTrue(plane.CanChangeEffectivePlane);
    }

    [TestMethod]
    public void FixedGripPlaneCannotBeReplacedButCanBeClearedByLifecycle()
    {
        var plane = new CadWorkPlane();
        plane.BeginToolPlane(OcctPoint3d.Origin);
        plane.SetGripPlane(
            new(1, 2, 3),
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ,
            fixedPlane: true);
        var fixedFrame = plane.EffectivePlane;

        plane.SetGripPlane(
            new(9, 9, 9),
            OcctVector3d.UnitX,
            OcctVector3d.UnitY,
            fixedPlane: false);
        plane.SetOrigin(new(4, 5, 6));

        Assert.AreEqual(fixedFrame, plane.EffectivePlane);
        Assert.IsTrue(plane.GripPlaneFixed);

        plane.ClearGripPlane();
        Assert.IsNull(plane.GripPlane);
        Assert.IsFalse(plane.GripPlaneFixed);
        Assert.IsNotNull(plane.ToolPlane);
    }

    [TestMethod]
    public void CustomUserPlaneUpdatesUnfixedActiveToolPlane()
    {
        var plane = new CadWorkPlane();
        plane.BeginToolPlane(new(3, 4, 5));

        plane.SetUserCustomPlane(
            new(7, 8, 9),
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ);

        Assert.AreEqual(CadWorkPlanePreset.Custom, plane.Preset);
        Assert.AreEqual(CadWorkPlanePreset.Custom, plane.EffectivePreset);
        Assert.AreEqual(new OcctPoint3d(7, 8, 9), plane.Origin);
        Assert.AreEqual(OcctVector3d.UnitY, plane.XAxis);
        Assert.AreEqual(OcctVector3d.UnitZ, plane.YAxis);
    }
}
