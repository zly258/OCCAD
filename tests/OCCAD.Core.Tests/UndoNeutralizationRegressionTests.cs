using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class UndoNeutralizationRegressionTests
{
    [TestMethod]
    public void UndoCancelsActiveToolAndClearsTransientInteractionBeforeHistoryMoves()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.AddEntity(line);

        Assert.IsTrue(workspace.History.CanUndo);
        Assert.IsTrue(workspace.Tools.Activate("line"));
        workspace.Snap.TemporaryModes = CadSnapType.Endpoint;
        workspace.Drafting.LengthLockEnabled = true;
        workspace.Drafting.LockedLength = 12.0;

        Assert.IsNotNull(workspace.Tools.ActiveTool);
        Assert.AreNotEqual(0L, workspace.Transients.CurrentToolOwner);

        Assert.IsTrue(workspace.Undo());

        InteractionTests.AssertNeutral(workspace);
        Assert.IsNull(workspace.Tools.ActiveTool);
        Assert.IsEmpty(workspace.Document.Entities);
        Assert.IsNull(workspace.Snap.TemporaryModes);
        Assert.IsNull(workspace.Snap.Current);
        Assert.IsEmpty(workspace.Snap.Candidates);
        Assert.IsFalse(workspace.Drafting.LengthLockEnabled);
        Assert.AreEqual(0.0, workspace.Drafting.LockedLength, 1e-12);
        Assert.IsNull(workspace.Preselection.Current);
        Assert.IsNull(workspace.LastPointerPosition);
        Assert.IsNull(workspace.LastResolvedPoint);
        Assert.IsTrue(workspace.History.CanRedo);
    }
}