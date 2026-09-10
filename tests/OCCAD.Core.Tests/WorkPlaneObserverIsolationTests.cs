using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class WorkPlaneObserverIsolationTests
{
    [TestMethod]
    public void ChangedObserverFailureCannotInvalidateAppliedPresetOrStarveLaterObservers()
    {
        var plane = new CadWorkPlane();
        var laterCalls = 0;

        plane.Changed += (_, _) =>
            throw new InvalidOperationException("simulated UI observer failure");
        plane.Changed += (_, _) => laterCalls++;

        plane.SetPreset(CadWorkPlanePreset.YZ, new OcctPoint3d(1, 2, 3));

        Assert.AreEqual(CadWorkPlanePreset.YZ, plane.Preset);
        Assert.AreEqual(new OcctPoint3d(1, 2, 3), plane.Origin);
        Assert.AreEqual(1, laterCalls);
    }

    [TestMethod]
    public void ChangedObserverFailureCannotBreakToolPlaneLifecycle()
    {
        var plane = new CadWorkPlane();
        var laterCalls = 0;

        plane.Changed += (_, _) =>
            throw new InvalidOperationException("simulated status observer failure");
        plane.Changed += (_, _) => laterCalls++;

        plane.BeginToolPlane(new OcctPoint3d(5, 6, 7));
        Assert.IsTrue(plane.IsActive);
        Assert.AreEqual(new OcctPoint3d(5, 6, 7), plane.Origin);

        plane.SetGripPlane(
            new OcctPoint3d(8, 9, 10),
            OcctVector3d.UnitX,
            OcctVector3d.UnitY,
            fixedPlane: true);
        Assert.IsTrue(plane.GripPlaneFixed);
        Assert.AreEqual(new OcctPoint3d(8, 9, 10), plane.Origin);

        plane.EndToolPlane();
        Assert.IsFalse(plane.IsActive);
        Assert.IsFalse(plane.GripPlaneFixed);
        Assert.AreEqual(3, laterCalls);
    }
}