using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class PropertyGripHistoryLoopTests
{
    [TestMethod]
    public void PropertyThenGripUndoRedoKeepsGeometryPresentationAndHistoryCoherent()
    {
        using var scene = new NativeScene();
        var workspace = scene.Workspace;
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            5.0);
        workspace.Document.Add(circle);
        workspace.MarkSaved();
        workspace.Selection.Select(circle);

        Assert.HasCount(5, workspace.Grips.Grips);
        Assert.AreEqual(5.0, workspace.Grips.Grips[1].Position.X, 1e-10);

        Assert.IsTrue(CadPropertyTransaction.TryApply(
            workspace,
            [circle],
            nameof(CadCircleEntity.Radius),
            8.0,
            out var propertyError),
            propertyError?.ToString());

        Assert.AreEqual(8.0, circle.Radius, 1e-10);
        Assert.HasCount(5, workspace.Grips.Grips);
        Assert.AreEqual(8.0, workspace.Grips.Grips[1].Position.X, 1e-10,
            "Selected grips must refresh immediately after a geometry property edit.");
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.AreEqual("Property Radius", workspace.History.UndoName);

        var radiusGrip = workspace.Grips.Grips[1];
        workspace.Tools.BeginGripEdit(radiusGrip);
        Assert.IsTrue(workspace.Preview.HasTransient);
        Assert.IsTrue(workspace.Grips.HasDragTransient);
        Assert.IsTrue(workspace.Tools.CommitPoint(new OcctPoint3d(10.0, 0.0, 0.0)));

        InteractionTests.AssertNeutral(workspace);
        Assert.AreEqual(10.0, circle.Radius, 1e-10);
        Assert.AreEqual("Grip Edit", workspace.History.UndoName);
        Assert.HasCount(1, workspace.Selection.Selected);
        Assert.AreSame(circle, workspace.Selection.Primary);
        Assert.HasCount(5, workspace.Grips.Grips);
        Assert.AreEqual(10.0, workspace.Grips.Grips[1].Position.X, 1e-10);

        Assert.IsTrue(workspace.Undo());
        InteractionTests.AssertNeutral(workspace);
        Assert.AreEqual(8.0, circle.Radius, 1e-10);
        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsEmpty(workspace.Grips.Grips);
        Assert.AreEqual(1, scene.Engine.ObjectCount,
            "Undo must leave only the persistent circle presentation.");

        Assert.IsTrue(workspace.Undo());
        InteractionTests.AssertNeutral(workspace);
        Assert.AreEqual(5.0, circle.Radius, 1e-10);
        Assert.IsFalse(workspace.IsModified,
            "Undoing both edits must return to the saved history state.");
        Assert.AreEqual(1, scene.Engine.ObjectCount);

        Assert.IsTrue(workspace.Redo());
        InteractionTests.AssertNeutral(workspace);
        Assert.AreEqual(8.0, circle.Radius, 1e-10);
        Assert.IsTrue(workspace.IsModified);
        Assert.AreEqual(1, scene.Engine.ObjectCount);

        Assert.IsTrue(workspace.Redo());
        InteractionTests.AssertNeutral(workspace);
        Assert.AreEqual(10.0, circle.Radius, 1e-10);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
        Assert.IsFalse(workspace.Preview.HasTransient);
        Assert.IsFalse(workspace.Snap.HasTransient);
        Assert.IsFalse(workspace.Tracking.HasTransient);
    }
}
