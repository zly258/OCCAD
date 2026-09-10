using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class WorkspaceLifecycleRegressionTests
{
    [TestMethod]
    public void ModifiedObserverFailureCannotInvalidateModifiedState()
    {
        using var workspace = new CadWorkspace();
        var laterObserverCalls = 0;
        workspace.ModifiedChanged += (_, _) =>
            throw new InvalidOperationException("title observer failure");
        workspace.ModifiedChanged += (_, _) =>
            laterObserverCalls++;

        workspace.AddEntity(new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0)));

        Assert.IsTrue(workspace.IsModified);
        Assert.AreEqual(1, laterObserverCalls);

        workspace.MarkSaved();

        Assert.IsFalse(workspace.IsModified);
        Assert.AreEqual(2, laterObserverCalls);
    }

    [TestMethod]
    public void DisposeContinuesAfterRecoverableWorkspaceTransientFailure()
    {
        var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.Selection.Select(line);
        workspace.Transients.Register(
            CadTransientChannel.SelectionMarkers,
            () => throw new InvalidOperationException("marker cleanup failure"),
            () => true,
            CadTransientLifetime.Workspace);

        workspace.Dispose();

        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsEmpty(workspace.Subobjects.Selected);
        Assert.IsNull(workspace.Engine);
        Assert.IsNull(workspace.LastPointerPosition);
        Assert.IsNull(workspace.LastResolvedPoint);
    }
}
