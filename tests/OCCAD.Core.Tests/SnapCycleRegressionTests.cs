using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class SnapCycleRegressionTests
{
    [TestMethod]
    public void CandidateCyclePersistsAtSamePointerAndWrapsBothDirections()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        var endpointLine = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(20.0, 0.0, 0.0));
        var midpointLine = new CadLineEntity(
            new OcctPoint3d(-10.0, 0.0, 0.0),
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.AddRange([endpointLine, midpointLine]);

        scene.Engine.SetView(OcctViewOrientation.Top);
        scene.Engine.FitAll();
        w.WorkPlane.BeginToolPlane(OcctPoint3d.Origin);
        w.Snap.Active = true;
        w.Snap.Modes = CadSnapType.Endpoint | CadSnapType.Midpoint;

        var pixel = scene.Engine.WorldToScreen(OcctPoint3d.Origin);
        var first = w.Snap.Resolve(
            pixel.X,
            pixel.Y,
            OcctPoint3d.Origin,
            w.WorkPlane);

        Assert.IsNotNull(first);
        Assert.IsTrue(w.Snap.Candidates.Count >= 2);
        var firstIndex = w.Snap.CurrentCandidateIndex;
        var firstCandidate = w.Snap.Current!.Value;

        Assert.IsTrue(w.Snap.CycleNext());
        var nextIndex = w.Snap.CurrentCandidateIndex;
        var nextCandidate = w.Snap.Current!.Value;
        Assert.AreNotEqual(firstIndex, nextIndex);
        Assert.IsFalse(Same(firstCandidate, nextCandidate));

        var resolvedAgain = w.Snap.Resolve(
            pixel.X,
            pixel.Y,
            OcctPoint3d.Origin,
            w.WorkPlane);
        Assert.IsNotNull(resolvedAgain);
        Assert.AreEqual(nextIndex, w.Snap.CurrentCandidateIndex,
            "Resolving the same pointer position must preserve the cycled candidate.");
        Assert.IsTrue(Same(nextCandidate, resolvedAgain.Value));

        Assert.IsTrue(w.Snap.CyclePrevious());
        Assert.AreEqual(firstIndex, w.Snap.CurrentCandidateIndex);
        Assert.IsTrue(Same(firstCandidate, w.Snap.Current!.Value));

        w.Snap.Clear();
        w.WorkPlane.EndToolPlane();
    }

    [TestMethod]
    public void TemporaryModesResetCycleAndDoNotLeakAcrossToolSession()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        var endpointLine = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(20.0, 0.0, 0.0));
        var midpointLine = new CadLineEntity(
            new OcctPoint3d(-10.0, 0.0, 0.0),
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.AddRange([endpointLine, midpointLine]);

        scene.Engine.SetView(OcctViewOrientation.Top);
        scene.Engine.FitAll();
        Assert.IsTrue(w.Tools.Activate("line"));
        w.Snap.Modes = CadSnapType.Endpoint | CadSnapType.Midpoint;

        var pixel = scene.Engine.WorldToScreen(OcctPoint3d.Origin);
        Assert.IsNotNull(w.Snap.Resolve(
            pixel.X,
            pixel.Y,
            OcctPoint3d.Origin,
            w.WorkPlane));
        Assert.IsTrue(w.Snap.Candidates.Count >= 2);
        Assert.IsTrue(w.Snap.CycleNext());

        w.Snap.TemporaryModes = CadSnapType.Midpoint;
        Assert.IsEmpty(w.Snap.Candidates);
        Assert.AreEqual(-1, w.Snap.CurrentCandidateIndex);
        Assert.IsNull(w.Snap.Current);

        var temporary = w.Snap.Resolve(
            pixel.X,
            pixel.Y,
            OcctPoint3d.Origin,
            w.WorkPlane);
        Assert.IsNotNull(temporary);
        Assert.AreEqual(CadSnapType.Midpoint, temporary.Value.Type);
        Assert.IsTrue(w.Snap.Candidates.All(candidate =>
            candidate.Type == CadSnapType.Midpoint));

        w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);
        Assert.IsNull(w.Snap.TemporaryModes);
        Assert.IsNull(w.Snap.Current);
        Assert.IsEmpty(w.Snap.Candidates);
        Assert.AreEqual(-1, w.Snap.CurrentCandidateIndex);

        Assert.IsTrue(w.Tools.Activate("line"));
        Assert.IsNull(w.Snap.TemporaryModes);
        Assert.AreEqual(
            CadSnapType.Endpoint | CadSnapType.Midpoint,
            w.Snap.EffectiveModes);
        w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);
    }

    private static bool Same(CadSnapPoint left, CadSnapPoint right) =>
        ReferenceEquals(left.Entity, right.Entity) &&
        left.Type == right.Type &&
        left.Index == right.Index &&
        left.Position.DistanceTo(right.Position) <= 1e-9;
}
