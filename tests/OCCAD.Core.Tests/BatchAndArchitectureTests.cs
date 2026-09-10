using System.Drawing;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class BatchAndArchitectureTests
{
    [TestMethod]
    public void MixedTransactionRestoresEntitiesLayersAndOneHistoryEntry()
    {
        using var w = new CadWorkspace();
        var kept = new CadLineEntity(default, new(10, 0, 0));
        var removed = new CadLineEntity(default, new(0, 10, 0));
        w.Document.AddRange([kept, removed]); w.MarkSaved();
        CadLayer layer;
        CadPointEntity added = new(new(5, 5, 0));
        using (var transaction = w.BeginTransaction("Mixed"))
        {
            layer = w.AddLayer("Detail");
            w.AddEntity(added);
            w.DeleteEntities([removed]);
            w.TranslateEntities([kept], new(2, 3, 0));
            w.AssignEntitiesToLayer([kept], layer.Name);
            transaction.Commit();
        }
        Assert.AreEqual("Mixed", w.History.UndoName);
        Assert.IsTrue(w.Undo());
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.IsModified);
        CollectionAssert.AreEqual(new CadEntity[] { kept, removed }, w.Document.Entities.ToArray());
        Assert.AreEqual(CadPlacement.Identity, kept.Placement);
        Assert.AreEqual("0", kept.Layer);
        Assert.HasCount(1, w.Layers.Layers);
        Assert.IsTrue(w.Redo());
        Assert.AreSame(layer, w.Layers.Current);
        Assert.IsTrue(w.Document.Entities.Contains(added));
        Assert.IsFalse(w.Document.Entities.Contains(removed));
        Assert.AreEqual(new OcctPoint3d(2, 3, 0), kept.Placement.Position);
    }

    [TestMethod]
    public void AbortedTransactionPreservesRedoAndSavedState()
    {
        using var w = new CadWorkspace();
        w.AddEntity(new CadPointEntity(default)); w.Undo(); w.MarkSaved();
        var state = w.History.CurrentStateId;
        using (w.BeginTransaction("Abort"))
        {
            w.AddLayer("Temporary");
            w.AddEntity(new CadPointEntity(new(2, 3, 0)));
        }
        Assert.IsEmpty(w.Document.Entities);
        Assert.HasCount(1, w.Layers.Layers);
        Assert.AreEqual(state, w.History.CurrentStateId);
        Assert.IsFalse(w.IsModified);
        Assert.IsTrue(w.History.CanRedo);
        Assert.IsTrue(w.Redo());
        Assert.HasCount(1, w.Document.Entities);
    }

    [TestMethod]
    public void EmptyAndNoopTransactionsDoNotDirtyOrAddHistory()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0)); w.Document.Add(line); w.MarkSaved();
        using (var transaction = w.BeginTransaction())
        {
            line.EndX = 20; line.EndX = 10;
            transaction.Commit();
        }
        Assert.IsFalse(w.History.CanUndo);
        Assert.IsFalse(w.IsModified);
        using (var transaction = w.BeginTransaction()) transaction.Commit();
        Assert.IsFalse(w.History.CanUndo);
    }

    [TestMethod]
    public void PropertyFailureRollsBackEveryTarget()
    {
        using var w = new CadWorkspace();
        var a = new CadLineEntity(default, new(10, 0, 0));
        var b = new CadLineEntity(new(5, 0, 0), new(15, 0, 0));
        w.Document.AddRange([a, b]); w.MarkSaved();
        Assert.IsFalse(CadPropertyTransaction.TryApply(w, [a, b], "EndX", 5d, out var error));
        Assert.IsNotNull(error);
        Assert.AreEqual(10, a.EndX); Assert.AreEqual(15, b.EndX);
        Assert.IsFalse(w.History.CanUndo); Assert.IsFalse(w.IsModified);
    }

    [TestMethod]
    public void ArrayUsesOneUndo()
    {
        using var w = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0)); w.Document.Add(line);
        var copies = w.CreateRectangularArray([line], 10, 10, new(20, 0, 0), new(0, 20, 0));
        Assert.IsGreaterThanOrEqualTo(99, copies.Count);
        Assert.IsTrue(w.Undo());
        Assert.HasCount(1, w.Document.Entities);
        Assert.IsFalse(w.History.CanUndo);
    }

    [TestMethod]
    public void DomainEventsDescribeModelSelectionAndToolChanges()
    {
        using var w = new CadWorkspace();
        List<CadDomainEventKind> kinds = [];
        w.Events.Changed += (_, args) => kinds.Add(args.Kind);
        var line = new CadLineEntity(default, new(10, 0, 0)); w.Document.Add(line);
        line.EndX = 15; line.Color = Color.Red; w.Selection.Select(line);
        w.Tools.Activate("line"); w.Tools.CancelCurrent(); w.Document.Remove(line);
        foreach (var expected in new[] { CadDomainEventKind.EntityAdded, CadDomainEventKind.EntityGeometryChanged,
            CadDomainEventKind.EntityAppearanceChanged, CadDomainEventKind.SelectionChanged,
            CadDomainEventKind.ActiveToolChanged, CadDomainEventKind.EntityRemoved })
            CollectionAssert.Contains(kinds, expected);
    }

    [TestMethod]
    public void TransientCleanupContinuesAndOwnerDoesNotClearWorkspaceChannels()
    {
        using var w = new CadWorkspace();
        var cleared = false; var workspaceState = true;
        using var window = w.Transients.Register(CadTransientChannel.SelectionWindow, () => cleared = true, () => !cleared);
        using var selection = w.Transients.Register(CadTransientChannel.SelectionMarkers, () => workspaceState = false,
            () => workspaceState, CadTransientLifetime.Workspace);
        w.Tools.Activate("line");
        var owner = w.Transients.CurrentToolOwner;
        w.Tools.CancelCurrent();
        Assert.IsTrue(cleared); Assert.IsTrue(workspaceState);
        w.Tools.Activate("line"); cleared = false;
        w.Transients.ClearOwner(owner);
        Assert.IsFalse(cleared);
        w.Tools.CancelCurrent();
        InteractionTests.AssertNeutral(w);

        var scene = new CadTransientScene(); var laterCleared = false;
        scene.Register(CadTransientChannel.ToolPreview, () => throw new InvalidOperationException(), () => false);
        scene.Register(CadTransientChannel.Tracking, () => laterCleared = true, () => false);
        Assert.Throws<AggregateException>(scene.ClearToolState);
        Assert.IsTrue(laterCleared);
    }

    [TestMethod]
    public void CommandAndPropertyDescriptorsAreAuthoritative()
    {
        using var w = new CadWorkspace();
        Assert.AreEqual("draw.line", w.Actions.ResolveCommand("L")!.Id);
        Assert.AreEqual("line", w.Actions.ResolveCommand("LINE")!.ToolId);
        Assert.IsTrue(w.Actions.Describe("draw.line")!.Repeatable);
        Assert.AreEqual("Ctrl+Z", w.Actions.Describe("edit.undo")!.Shortcut);
        CollectionAssert.Contains(CadCommandManager.ForWorkspace(w).Complete("LI").ToArray(), "LINE");
        var line = new CadLineEntity(default, new(10, 0, 0));
        var properties = CadPropertyCatalog.Describe(line);
        var layer = properties.Single(p => p.Name == "Layer");
        Assert.AreEqual(CadPropertyEditorKind.Layer, layer.Editor);
        w.AddLayer("Review");
        CollectionAssert.Contains(layer.GetChoices(w).ToArray(), "Review");
        Assert.AreEqual(CadPropertyEditorKind.ByLayer, properties.Single(p => p.Name == "Color").Editor);
        Assert.AreEqual(CadPropertyEditorKind.ReadOnly, properties.Single(p => p.Name == "Length").Editor);
    }

    [TestMethod]
    public void PixelAperturesRejectInvalidValuesAndShareDetectionTolerance()
    {
        using var w = new CadWorkspace();
        w.Selection.PixelTolerance = 12;
        Assert.AreEqual(12, w.Preselection.PixelTolerance);
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Snap.PixelTolerance = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Grips.PixelTolerance = -1);
    }
}
