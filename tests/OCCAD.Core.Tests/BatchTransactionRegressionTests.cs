using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class BatchTransactionRegressionTests
{
    [TestMethod]
    public void OuterTransactionCoalescesMultipleEntityChangesIntoOneUndo()
    {
        using var w = new CadWorkspace();
        var a = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        var b = new CadLineEntity(
            new OcctPoint3d(0.0, 10.0, 0.0),
            new OcctPoint3d(10.0, 10.0, 0.0));
        w.Document.AddRange([a, b]);
        w.MarkSaved();

        using (var transaction = w.BeginTransaction("Batch Edit"))
        {
            Assert.IsTrue(CadTransaction.ApplyEntities(
                w,
                [a],
                "Inner Move",
                entity => entity.TranslatePlacement(
                    new OcctVector3d(5.0, 0.0, 0.0)),
                geometryOnly: true));
            Assert.IsTrue(CadTransaction.ApplyEntities(
                w,
                [b],
                "Inner Property",
                entity => entity.Transparency = 0.4));

            Assert.IsFalse(w.History.CanUndo,
                "Inner lightweight changes must not install nested history while recording is suspended.");
            transaction.Commit();
        }

        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.AreEqual("Batch Edit", w.History.UndoName);
        Assert.AreEqual(5.0, a.ToWorldPoint(a.Start).X, 1e-8);
        Assert.AreEqual(0.4, b.Transparency, 1e-12);
        Assert.IsTrue(w.IsModified);

        Assert.IsTrue(w.Undo());
        Assert.AreEqual(0.0, a.ToWorldPoint(a.Start).X, 1e-8);
        Assert.AreEqual(0.0, b.Transparency, 1e-12);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.IsFalse(w.IsModified);

        Assert.IsTrue(w.Redo());
        Assert.AreEqual(5.0, a.ToWorldPoint(a.Start).X, 1e-8);
        Assert.AreEqual(0.4, b.Transparency, 1e-12);
        Assert.IsTrue(w.IsModified);
    }

    [TestMethod]
    public void ExecuteInsideOuterTransactionUsesOnlyOuterUndoEntry()
    {
        using var w = new CadWorkspace();
        w.MarkSaved();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));

        using (var transaction = w.BeginTransaction("Batch Create"))
        {
            // AddEntity routes through CadTransaction.Execute. While the outer
            // transaction has history recording suspended, Execute must apply
            // the mutation without installing its own nested history entry.
            w.AddEntity(line);

            Assert.HasCount(1, w.Document.Entities);
            Assert.IsFalse(w.History.CanUndo);
            transaction.Commit();
        }

        Assert.HasCount(1, w.Document.Entities);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Batch Create", w.History.UndoName);

        Assert.IsTrue(w.Undo());
        Assert.IsEmpty(w.Document.Entities);
        Assert.IsFalse(w.IsModified);

        Assert.IsTrue(w.Redo());
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreSame(line, w.Document.Entities[0]);
        Assert.IsTrue(w.IsModified);
    }

    [TestMethod]
    public void DisposingUncommittedOuterTransactionRollsBackAllInnerChanges()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();

        using (w.BeginTransaction("Canceled Batch"))
        {
            Assert.IsTrue(CadTransaction.ApplyEntities(
                w,
                [line],
                "Inner Move",
                entity => entity.TranslatePlacement(
                    new OcctVector3d(7.0, 3.0, 0.0)),
                geometryOnly: true));
            Assert.IsTrue(CadTransaction.ApplyEntities(
                w,
                [line],
                "Inner Property",
                entity => entity.Transparency = 0.65));

            Assert.AreEqual(7.0, line.ToWorldPoint(line.Start).X, 1e-8);
            Assert.AreEqual(3.0, line.ToWorldPoint(line.Start).Y, 1e-8);
            Assert.AreEqual(0.65, line.Transparency, 1e-12);
            Assert.IsFalse(w.History.CanUndo);
        }

        Assert.AreEqual(0.0, line.ToWorldPoint(line.Start).X, 1e-8);
        Assert.AreEqual(0.0, line.ToWorldPoint(line.Start).Y, 1e-8);
        Assert.AreEqual(0.0, line.Transparency, 1e-12);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsFalse(w.IsModified);
    }

    [TestMethod]
    public void NoOpOuterTransactionDoesNotPolluteHistoryOrModifiedState()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();

        using (var transaction = w.BeginTransaction("No-op Batch"))
        {
            transaction.Commit();
        }

        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsFalse(w.IsModified);
    }
}
