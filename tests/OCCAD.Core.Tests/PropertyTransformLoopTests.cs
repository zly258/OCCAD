using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class PropertyTransformLoopTests
{
    [TestMethod]
    public void PropertyNoOpDoesNotCreateSecondUndoEntry()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();

        Assert.IsTrue(CadPropertyTransaction.TryApply(
            w,
            [line],
            nameof(CadEntity.Name),
            "Axis A",
            out var firstError));
        Assert.IsNull(firstError);
        Assert.AreEqual("Axis A", line.Name);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Property Name", w.History.UndoName);
        var stateAfterChange = w.History.CurrentStateId;

        Assert.IsTrue(CadPropertyTransaction.TryApply(
            w,
            [line],
            nameof(CadEntity.Name),
            "Axis A",
            out var secondError));
        Assert.IsNull(secondError);
        Assert.AreEqual(stateAfterChange, w.History.CurrentStateId);
        Assert.AreEqual("Property Name", w.History.UndoName);

        Assert.IsTrue(w.Undo());
        Assert.AreEqual("Line", line.Name);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.IsFalse(w.IsModified);

        Assert.IsTrue(w.Redo());
        Assert.AreEqual("Axis A", line.Name);
        Assert.IsTrue(w.History.CanUndo);
        Assert.IsFalse(w.History.CanRedo);
        Assert.IsTrue(w.IsModified);
    }

    [TestMethod]
    public void MultiEntityPropertyEditIsOneAtomicUndo()
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

        Assert.IsTrue(CadPropertyTransaction.TryApply(
            w,
            [a, b],
            nameof(CadEntity.Transparency),
            0.35,
            out var error));
        Assert.IsNull(error);
        Assert.AreEqual(0.35, a.Transparency, 1e-12);
        Assert.AreEqual(0.35, b.Transparency, 1e-12);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual("Property Transparency", w.History.UndoName);

        Assert.IsTrue(w.Undo());
        Assert.AreEqual(0.0, a.Transparency, 1e-12);
        Assert.AreEqual(0.0, b.Transparency, 1e-12);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.IsModified);

        Assert.IsTrue(w.Redo());
        Assert.AreEqual(0.35, a.Transparency, 1e-12);
        Assert.AreEqual(0.35, b.Transparency, 1e-12);
        Assert.IsTrue(w.History.CanUndo);
    }

    [TestMethod]
    [TestCategory("Native")]
    [DataRow("move")]
    [DataRow("copy")]
    public void TranslateCommitRestoresSelectionGripStateAndUndoIsAtomic(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();
        w.Selection.Select(line);

        Assert.HasCount(1, w.Selection.Selected);
        Assert.HasCount(1, w.Grips.Entities);
        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsEmpty(w.Grips.Entities);
        Assert.IsTrue(w.Tools.CommitPoint(OcctPoint3d.Origin));
        Assert.IsTrue(w.Tools.CommitPoint(new OcctPoint3d(5.0, 0.0, 0.0)));

        InteractionTests.AssertNeutral(w);
        AssertSelectionAndGripsRestored(w, line);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual(id == "move" ? "Move" : "Copy", w.History.UndoName);

        if (id == "move")
        {
            Assert.HasCount(1, w.Document.Entities);
            Assert.AreEqual(5.0, line.ToWorldPoint(line.Start).X, 1e-8);
        }
        else
        {
            Assert.HasCount(2, w.Document.Entities);
            Assert.AreEqual(0.0, line.ToWorldPoint(line.Start).X, 1e-8);
            var copy = w.Document.Entities.Single(entity => !ReferenceEquals(entity, line));
            Assert.AreEqual(5.0, copy.ToWorldPoint(((CadLineEntity)copy).Start).X, 1e-8);
        }

        AssertUndoClearsInteraction(w, line);

        Assert.IsTrue(w.Redo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Selection.Selected);
        Assert.IsEmpty(w.Grips.Entities);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual(id == "move" ? 1 : 2, w.Document.Entities.Count);
    }

    [TestMethod]
    [TestCategory("Native")]
    [DataRow("rotate")]
    [DataRow("scale")]
    [DataRow("mirror")]
    public void TransformCommitRestoresSelectionGripStateAndUndoRedoIsAtomic(string id)
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;
        var line = new CadLineEntity(
            new OcctPoint3d(10.0, 0.0, 0.0),
            new OcctPoint3d(20.0, 0.0, 0.0));
        w.Document.Add(line);
        w.MarkSaved();
        w.Selection.Select(line);

        var beforeStart = line.ToWorldPoint(line.Start);
        var beforeEnd = line.ToWorldPoint(line.End);

        Assert.IsTrue(w.Tools.Activate(id));
        Assert.IsEmpty(w.Grips.Entities);
        Assert.IsTrue(w.Tools.CommitPoint(OcctPoint3d.Origin));

        var target = id switch
        {
            "rotate" => new OcctPoint3d(0.0, 10.0, 0.0),
            "scale" => new OcctPoint3d(2.0, 0.0, 0.0),
            "mirror" => new OcctPoint3d(0.0, 10.0, 0.0),
            _ => throw new InvalidOperationException($"Unexpected transform '{id}'.")
        };
        Assert.IsTrue(w.Tools.CommitPoint(target));

        InteractionTests.AssertNeutral(w);
        AssertSelectionAndGripsRestored(w, line);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual(
            id switch
            {
                "rotate" => "Rotate",
                "scale" => "Scale",
                _ => "Mirror"
            },
            w.History.UndoName);

        switch (id)
        {
            case "rotate":
            {
                Assert.HasCount(1, w.Document.Entities);
                var worldStart = line.ToWorldPoint(line.Start);
                var worldEnd = line.ToWorldPoint(line.End);
                Assert.AreEqual(0.0, worldStart.X, 1e-8);
                Assert.AreEqual(10.0, worldStart.Y, 1e-8);
                Assert.AreEqual(0.0, worldEnd.X, 1e-8);
                Assert.AreEqual(20.0, worldEnd.Y, 1e-8);
                break;
            }
            case "scale":
                Assert.HasCount(1, w.Document.Entities);
                Assert.AreEqual(20.0, line.ToWorldPoint(line.Start).X, 1e-8);
                Assert.AreEqual(40.0, line.ToWorldPoint(line.End).X, 1e-8);
                break;
            case "mirror":
            {
                Assert.HasCount(2, w.Document.Entities);
                var mirrored = (CadLineEntity)w.Document.Entities.Single(
                    entity => !ReferenceEquals(entity, line));
                Assert.AreEqual(-10.0, mirrored.ToWorldPoint(mirrored.Start).X, 1e-8);
                Assert.AreEqual(-20.0, mirrored.ToWorldPoint(mirrored.End).X, 1e-8);
                Assert.AreEqual(beforeStart, line.ToWorldPoint(line.Start));
                Assert.AreEqual(beforeEnd, line.ToWorldPoint(line.End));
                break;
            }
        }

        Assert.IsTrue(w.Undo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Selection.Selected);
        Assert.IsEmpty(w.Grips.Entities);
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsTrue(w.History.CanRedo);
        Assert.HasCount(1, w.Document.Entities);
        Assert.AreEqual(beforeStart, line.ToWorldPoint(line.Start));
        Assert.AreEqual(beforeEnd, line.ToWorldPoint(line.End));

        Assert.IsTrue(w.Redo());
        InteractionTests.AssertNeutral(w);
        Assert.IsEmpty(w.Selection.Selected);
        Assert.IsEmpty(w.Grips.Entities);
        Assert.IsTrue(w.History.CanUndo);
        Assert.AreEqual(id == "mirror" ? 2 : 1, w.Document.Entities.Count);
    }

    private static void AssertSelectionAndGripsRestored(
        CadWorkspace workspace,
        CadEntity entity)
    {
        Assert.HasCount(1, workspace.Selection.Selected);
        Assert.AreSame(entity, workspace.Selection.Primary);
        Assert.HasCount(1, workspace.Grips.Entities);
        Assert.AreSame(entity, workspace.Grips.Entity);
    }

    private static void AssertUndoClearsInteraction(
        CadWorkspace workspace,
        CadLineEntity line)
    {
        Assert.IsTrue(workspace.Undo());
        InteractionTests.AssertNeutral(workspace);
        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsEmpty(workspace.Grips.Entities);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsTrue(workspace.History.CanRedo);
        Assert.HasCount(1, workspace.Document.Entities);
        Assert.AreEqual(0.0, line.ToWorldPoint(line.Start).X, 1e-8);
    }
}
