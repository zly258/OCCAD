using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class TransactionAtomicityTests
{
    [TestMethod]
    public void EntityMutationRollsBackWhenToolCompletionFails()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.MarkSaved();

        var before = line.Duplicate();
        var historyState = workspace.History.CurrentStateId;

        Assert.Throws<InvalidOperationException>(() =>
            CadTransaction.ApplyEntities(
                workspace,
                [line],
                "Move",
                entity => entity.TranslatePlacement(
                    new OcctVector3d(5.0, 2.0, 0.0)),
                geometryOnly: true,
                complete: () => throw new InvalidOperationException(
                    "simulated tool cleanup failure")));

        Assert.IsTrue(
            workspace.Entities.GeometryEquals(before, line),
            "Failed tool completion must restore the entity geometry.");
        Assert.AreEqual(historyState, workspace.History.CurrentStateId);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsFalse(workspace.History.CanRedo);
        Assert.IsFalse(workspace.IsModified);
    }

    [TestMethod]
    public void MultiCreateRollsBackWhenToolCompletionFails()
    {
        using var workspace = new CadWorkspace();
        workspace.MarkSaved();

        var entities = new CadEntity[]
        {
            new CadLineEntity(
                OcctPoint3d.Origin,
                new OcctPoint3d(10.0, 0.0, 0.0)),
            new CadCircleEntity(
                new OcctPoint3d(20.0, 0.0, 0.0),
                new OcctVector3d(0.0, 0.0, 1.0),
                5.0)
        };

        Assert.Throws<InvalidOperationException>(() =>
            CadTransaction.ApplyCreatedEntities(
                workspace,
                entities,
                "Copy 2",
                () => throw new InvalidOperationException(
                    "simulated tool cleanup failure")));

        Assert.IsEmpty(workspace.Document.Entities);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsFalse(workspace.History.CanRedo);
        Assert.IsFalse(workspace.IsModified);
    }

    [TestMethod]
    public void EntityMutationInstallsHistoryOnlyAfterCompletionSucceeds()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.MarkSaved();

        var before = line.Duplicate();
        var completionRan = false;

        var recorded = CadTransaction.ApplyEntities(
            workspace,
            [line],
            "Move",
            entity => entity.TranslatePlacement(
                new OcctVector3d(5.0, 2.0, 0.0)),
            geometryOnly: true,
            complete: () => completionRan = true);

        Assert.IsTrue(recorded);
        Assert.IsTrue(completionRan);
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.AreEqual("Move", workspace.History.UndoName);
        Assert.IsTrue(workspace.IsModified);

        Assert.IsTrue(workspace.Undo());
        Assert.IsTrue(workspace.Entities.GeometryEquals(before, line));
        Assert.IsFalse(workspace.IsModified);

        Assert.IsTrue(workspace.Redo());
        Assert.IsTrue(workspace.IsModified);
    }

    [TestMethod]
    public void EntityMutationRemainsCommittedWhenHistoryObserverFails()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.MarkSaved();

        var before = line.Duplicate();
        EventHandler<CadHistoryChangedEventArgs> throwingObserver =
            (_, _) => throw new InvalidOperationException(
                "simulated UI history observer failure");
        workspace.History.Changed += throwingObserver;

        var recorded = CadTransaction.ApplyEntities(
            workspace,
            [line],
            "Move",
            entity => entity.TranslatePlacement(
                new OcctVector3d(5.0, 0.0, 0.0)),
            geometryOnly: true);

        workspace.History.Changed -= throwingObserver;

        Assert.IsTrue(recorded);
        Assert.IsFalse(workspace.Entities.GeometryEquals(before, line));
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.AreEqual("Move", workspace.History.UndoName);
        Assert.IsTrue(workspace.IsModified);

        Assert.IsTrue(workspace.Undo());
        Assert.IsTrue(workspace.Entities.GeometryEquals(before, line));
        Assert.IsFalse(workspace.IsModified);
    }

    [TestMethod]
    public void CreatedEntitiesRemainSingleCommitWhenHistoryObserverFails()
    {
        using var workspace = new CadWorkspace();
        workspace.MarkSaved();

        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        EventHandler<CadHistoryChangedEventArgs> throwingObserver =
            (_, _) => throw new InvalidOperationException(
                "simulated UI history observer failure");
        workspace.History.Changed += throwingObserver;

        CadTransaction.ApplyCreatedEntities(
            workspace,
            [line],
            "Create Line",
            static () => { });

        workspace.History.Changed -= throwingObserver;

        Assert.HasCount(1, workspace.Document.Entities);
        Assert.IsTrue(workspace.History.CanUndo);
        Assert.AreEqual("Create Line", workspace.History.UndoName);
        Assert.IsTrue(workspace.IsModified);

        Assert.IsTrue(workspace.Undo());
        Assert.IsEmpty(workspace.Document.Entities);
        Assert.IsFalse(workspace.IsModified);
    }

    [TestMethod]
    public void HistoryObserverFailureCannotBreakUndoRedoOrStarveLaterObservers()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.MarkSaved();
        var before = line.Duplicate();

        var delivered = 0;
        EventHandler<CadHistoryChangedEventArgs> throwingObserver =
            (_, _) => throw new InvalidOperationException(
                "simulated UI history observer failure");
        EventHandler<CadHistoryChangedEventArgs> laterObserver =
            (_, _) => delivered++;
        workspace.History.Changed += throwingObserver;
        workspace.History.Changed += laterObserver;

        Assert.IsTrue(CadTransaction.ApplyEntities(
            workspace,
            [line],
            "Move",
            entity => entity.TranslatePlacement(
                new OcctVector3d(3.0, 0.0, 0.0)),
            geometryOnly: true));
        Assert.AreEqual(1, delivered);
        Assert.IsTrue(workspace.History.CanUndo);

        Assert.IsTrue(workspace.Undo());
        Assert.AreEqual(2, delivered);
        Assert.IsTrue(workspace.Entities.GeometryEquals(before, line));
        Assert.IsTrue(workspace.History.CanRedo);

        Assert.IsTrue(workspace.Redo());
        Assert.AreEqual(3, delivered);
        Assert.IsFalse(workspace.Entities.GeometryEquals(before, line));
        Assert.IsTrue(workspace.History.CanUndo);

        workspace.History.Changed -= laterObserver;
        workspace.History.Changed -= throwingObserver;
    }
}
