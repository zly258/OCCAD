using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class GripLoopRegressionTests
{
    [TestMethod]
    public void CircleCenterGripMovesCenterWithoutChangingRadiusAndHasOneUndo()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            5.0);
        w.Document.Add(circle);
        w.MarkSaved();

        var grip = circle.GetGripPoints().Single(point => point.Kind == CadGripKind.Center);
        w.Tools.BeginGripEdit(grip);
        Assert.IsTrue(w.Preview.HasTransient);
        Assert.IsTrue(w.Grips.HasDragTransient);
        Assert.IsFalse(w.History.CanUndo);

        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(10.0, 20.0, 0.0)));

        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(new OcctPoint3d(10.0, 20.0, 0.0), circle.Center);
        Assert.AreEqual(5.0, circle.Radius, 1e-10);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Grip Edit", w.History.UndoName);
        Assert.AreEqual(1, scene.Engine.ObjectCount);

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(OcctPoint3d.Origin, circle.Center);
        Assert.AreEqual(5.0, circle.Radius, 1e-10);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.IsModified);
    }

    [TestMethod]
    public void CircleRadiusGripRejectsDegeneratePointThenContinuesToValidCommit()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            5.0);
        w.Document.Add(circle);
        w.MarkSaved();

        var grip = circle.GetGripPoints().First(point => point.Kind == CadGripKind.Radius);
        w.Tools.BeginGripEdit(grip);

        Assert.IsFalse(w.Tools.CommitPoint(circle.Center));
        Assert.IsNotNull(w.Tools.ActiveTool);
        Assert.IsTrue(w.Preview.HasTransient);
        Assert.IsFalse(w.History.CanUndo);
        Assert.AreEqual(5.0, circle.Radius, 1e-10);

        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(8.0, 0.0, 0.0)));

        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(OcctPoint3d.Origin, circle.Center);
        Assert.AreEqual(8.0, circle.Radius, 1e-10);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Grip Edit", w.History.UndoName);
        Assert.AreEqual(1, scene.Engine.ObjectCount);

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(5.0, circle.Radius, 1e-10);
        Assert.IsFalse(w.History.CanUndo);
    }

    [TestMethod]
    public void LineMidpointGripTranslatesWholeEntityAndUndoRestoresBothEnds()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();

        var grip = line.GetGripPoints().Single(point => point.Kind == CadGripKind.Center);
        w.Tools.BeginGripEdit(grip);
        Assert.IsFalse(w.History.CanUndo);

        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(10.0, 5.0, 0.0)));

        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(new OcctPoint3d(5.0, 5.0, 0.0), line.Start);
        Assert.AreEqual(new OcctPoint3d(15.0, 5.0, 0.0), line.End);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Grip Edit", w.History.UndoName);

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.AreEqual(OcctPoint3d.Origin, line.Start);
        Assert.AreEqual(new OcctPoint3d(10.0, 0.0, 0.0), line.End);
        Assert.IsFalse(w.History.CanUndo);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }
}
