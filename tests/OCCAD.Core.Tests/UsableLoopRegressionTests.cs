using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class UsableLoopRegressionTests
{
    [TestMethod]
    [DataRow("polyline")]
    [DataRow("spline")]
    public void ContinuousCurveStepBackFinishCreatesExactlyOneUndo(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(0.0, 0.0, 0.0)));
        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(10.0, 0.0, 0.0)));
        Assert.IsTrue(w.Preview.HasTransient);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsEmpty(w.Document.Entities);

        Assert.IsTrue(w.Tools.StepBackCurrent());
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsEmpty(w.Document.Entities);

        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(10.0, 0.0, 0.0)));
        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(20.0, 10.0, 0.0)));
        Assert.IsTrue(w.Tools.FinishCurrent());

        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.AreEqual(
            id == "polyline" ? "Create Polyline" : "Create Spline",
            w.History.UndoName);

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.AreEqual(0, scene.Engine.ObjectCount);

        Assert.IsTrue(w.Redo());
        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }

    [TestMethod]
    [DataRow("line")]
    [DataRow("circle")]
    public void ExactLengthInputCommitsWithoutPointerMotion(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsTrue(w.Tools.CommitPoint(OcctPoint3d.Origin));
        Assert.IsNull(w.LastPointerPosition);

        LockLength(w, 25.0, 0.0);
        Assert.IsTrue(w.Tools.CommitCurrentStage());

        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.Preview.HasTransient);

        if (id == "line")
        {
            var line = (CadLineEntity)w.Document.Entities.Single();
            Assert.AreEqual(25.0, line.Start.DistanceTo(line.End), 1e-8);
        }
        else
        {
            var circle = (CadCircleEntity)w.Document.Entities.Single();
            Assert.AreEqual(25.0, circle.Radius, 1e-8);
        }

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.AreEqual(0, scene.Engine.ObjectCount);
    }

    [TestMethod]
    [DataRow("rectangle")]
    [DataRow("arc")]
    [DataRow("ellipse")]
    public void RemainingPlanarToolsCommitFromExactInputWithoutPointerMotion(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsTrue(w.Tools.CommitPoint(OcctPoint3d.Origin));
        Assert.IsNull(w.LastPointerPosition);

        switch (id)
        {
            case "rectangle":
                Assert.IsTrue(w.Tools.ActiveTool!.TrySetParameter("Width", "30"));
                Assert.IsTrue(w.Tools.ActiveTool.TrySetParameter("Height", "20"));
                Assert.IsTrue(w.Tools.ActiveTool.TrySetParameter("Angle", "15"));
                Assert.IsTrue(w.Preview.HasTransient);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            case "arc":
                LockLength(w, 10.0, 0.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                Assert.IsNotNull(w.Tools.ActiveTool);
                LockLength(w, 10.0, 90.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            case "ellipse":
                Assert.IsTrue(w.Tools.ActiveTool!.TrySetParameter("MajorRadius", "20"));
                Assert.IsTrue(w.Tools.ActiveTool.TrySetParameter("Angle", "30"));
                Assert.IsTrue(w.Preview.HasTransient);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                Assert.IsNotNull(w.Tools.ActiveTool);
                Assert.IsTrue(w.Tools.ActiveTool!.TrySetParameter("MinorRadius", "10"));
                Assert.IsTrue(w.Preview.HasTransient);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            default:
                Assert.Fail($"Unexpected planar tool '{id}'.");
                break;
        }

        Assert.IsNull(w.LastPointerPosition);
        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.IsTrue(id switch
        {
            "rectangle" => w.Document.Entities[0] is CadRectangleEntity,
            "arc" => w.Document.Entities[0] is CadArcEntity,
            "ellipse" => w.Document.Entities[0] is CadEllipseEntity,
            _ => false
        });

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.AreEqual(0, scene.Engine.ObjectCount);

        Assert.IsTrue(w.Redo());
        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }

    [TestMethod]
    [DataRow("box")]
    [DataRow("cylinder")]
    [DataRow("cone")]
    [DataRow("sphere")]
    public void PrimitiveExactStagesCommitWithoutPointerMotion(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsTrue(w.Tools.CommitPoint(OcctPoint3d.Origin));
        Assert.IsNull(w.LastPointerPosition);

        switch (id)
        {
            case "sphere":
                LockLength(w, 12.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            case "cylinder":
            case "cone":
                LockLength(w, 8.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                Assert.IsNotNull(w.Tools.ActiveTool);
                LockLength(w, 20.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            case "box":
                LockLength(w, 20.0, 45.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                Assert.IsNotNull(w.Tools.ActiveTool);
                LockLength(w, 15.0);
                Assert.IsTrue(w.Tools.CommitCurrentStage());
                break;

            default:
                Assert.Fail($"Unexpected primitive '{id}'.");
                break;
        }

        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.IsFalse(w.Drafting.LengthLockEnabled);
        Assert.IsFalse(w.Drafting.AngleLockEnabled);

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.AreEqual(0, scene.Engine.ObjectCount);

        Assert.IsTrue(w.Redo());
        InteractionTests.AssertNeutral(w);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }

    private static void LockLength(
        CadWorkspace workspace,
        double length,
        double? angleDegrees = null)
    {
        workspace.Drafting.LockedLength = length;
        workspace.Drafting.LengthLockEnabled = true;

        if (angleDegrees is not { } angle)
            return;

        workspace.Drafting.LockedAngleDegrees = angle;
        workspace.Drafting.AngleLockEnabled = true;
    }
}
