using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class PointerNotificationIsolationTests
{
    [TestMethod]
    public void SnapObserverFailureCannotInvalidateResolvedCandidateOrStarveLaterObserver()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(20.0, 0.0, 0.0));
        w.Document.Add(line);

        scene.Engine.SetView(OcctViewOrientation.Top);
        scene.Engine.FitAll();
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Active = true;
        w.Snap.Modes = CadSnapType.Endpoint;

        var laterCalls = 0;
        w.Snap.CurrentChanged += (_, _) =>
            throw new InvalidOperationException("simulated snap HUD failure");
        w.Snap.CurrentChanged += (_, _) => laterCalls++;

        var pixel = scene.Engine.WorldToScreen(OcctPoint3d.Origin);
        var resolved = w.Snap.Resolve(
            pixel.X,
            pixel.Y,
            OcctPoint3d.Origin,
            w.WorkPlane);

        Assert.IsNotNull(resolved);
        Assert.IsNotNull(w.Snap.Current);
        Assert.AreEqual(CadSnapType.Endpoint, w.Snap.Current.Value.Type);
        Assert.AreEqual(1, laterCalls);

        w.Snap.Clear();
        Assert.IsNull(w.Snap.Current);
        Assert.AreEqual(2, laterCalls);
        w.WorkPlane.EndToolPlane();
    }

    [TestMethod]
    public void GripObserverFailureCannotInvalidateHotGripOrStarveLaterObserver()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(20.0, 0.0, 0.0));
        w.Document.Add(line);
        scene.Engine.SetView(OcctViewOrientation.Top);
        scene.Engine.FitAll();
        w.Grips.Show(line);
        Assert.IsNotEmpty(w.Grips.Grips);

        var laterCalls = 0;
        w.Grips.HotChanged += (_, _) =>
            throw new InvalidOperationException("simulated grip HUD failure");
        w.Grips.HotChanged += (_, _) => laterCalls++;

        var grip = w.Grips.Grips[0];
        var pixel = scene.Engine.WorldToScreen(grip.Position);
        Assert.IsTrue(w.Grips.UpdateHot(pixel.X, pixel.Y));
        Assert.IsNotNull(w.Grips.HotGrip);
        Assert.AreEqual(grip.Index, w.Grips.HotGrip.Value.Index);
        Assert.AreEqual(1, laterCalls);

        w.Grips.ClearHot();
        Assert.IsNull(w.Grips.HotGrip);
        Assert.AreEqual(2, laterCalls);
    }
}