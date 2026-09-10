using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class CreateCommitAtomicityTests
{
    [TestMethod]
    public void ToolDeactivationFailureRollsBackCreatedEntityAndHistory()
    {
        using var scene = new NativeScene();
        var workspace = scene.Workspace;

        workspace.Tools.Register<FailingCreateCleanupTool>("test.create.cleanup");
        Assert.IsTrue(workspace.Tools.Activate("test.create.cleanup"));

        Assert.Throws<InvalidOperationException>(() =>
            workspace.Tools.CommitPoint(OcctPoint3d.Origin));

        InteractionTests.AssertNeutral(workspace);
        Assert.IsEmpty(workspace.Document.Entities);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsFalse(workspace.History.CanRedo);
        Assert.IsFalse(workspace.IsModified);
        Assert.AreEqual(0, scene.Engine.ObjectCount);
    }

    [TestMethod]
    public void SuccessfulCreateStillProducesOneAtomicUndoRedoEntry()
    {
        using var scene = new NativeScene();
        var workspace = scene.Workspace;

        workspace.Tools.Register<SuccessfulCreateTool>("test.create.success");
        Assert.IsTrue(workspace.Tools.Activate("test.create.success"));
        Assert.IsTrue(workspace.Tools.CommitPoint(OcctPoint3d.Origin));

        InteractionTests.AssertNeutral(workspace);
        Assert.HasCount(1, workspace.Document.Entities);
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.AreEqual("Create Line", workspace.History.UndoName);
        Assert.AreEqual(1, scene.Engine.ObjectCount);

        Assert.IsTrue(workspace.Undo());
        Assert.IsEmpty(workspace.Document.Entities);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsTrue(workspace.History.CanRedo);
        Assert.AreEqual(0, scene.Engine.ObjectCount);

        Assert.IsTrue(workspace.Redo());
        Assert.HasCount(1, workspace.Document.Entities);
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.IsFalse(workspace.History.CanRedo);
        Assert.AreEqual(1, scene.Engine.ObjectCount);
    }

    public sealed class FailingCreateCleanupTool : CadDrawingTool, ICadPointInputTool
    {
        public override string Id => "test.create.cleanup";
        public override string DisplayName => "Test Create Cleanup";

        protected override void OnActivated() =>
            SetStage(0, "Specify point");

        public bool TryAcceptPoint(OcctPoint3d point)
        {
            if (!point.IsFinite)
                return false;

            CommitPreview(new CadLineEntity(
                point,
                new OcctPoint3d(point.X + 10.0, point.Y, point.Z)));
            return true;
        }

        protected override void OnDeactivated() =>
            throw new InvalidOperationException("simulated cleanup failure");
    }

    public sealed class SuccessfulCreateTool : CadDrawingTool, ICadPointInputTool
    {
        public override string Id => "test.create.success";
        public override string DisplayName => "Test Create Success";

        protected override void OnActivated() =>
            SetStage(0, "Specify point");

        public bool TryAcceptPoint(OcctPoint3d point)
        {
            if (!point.IsFinite)
                return false;

            CommitPreview(new CadLineEntity(
                point,
                new OcctPoint3d(point.X + 10.0, point.Y, point.Z)));
            return true;
        }
    }
}
