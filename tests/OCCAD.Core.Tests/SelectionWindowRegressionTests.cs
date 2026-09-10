using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
[TestCategory("Native")]
public sealed class SelectionWindowRegressionTests
{
    [TestMethod]
    public void WindowRequiresFullContainmentAndCrossingAllowsOverlap()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        var contained = new CadLineEntity(
            new OcctPoint3d(-5.0, 0.0, 0.0),
            new OcctPoint3d(5.0, 0.0, 0.0));
        var crossing = new CadLineEntity(
            new OcctPoint3d(-100.0, 5.0, 0.0),
            new OcctPoint3d(100.0, 5.0, 0.0));
        var outside = new CadLineEntity(
            new OcctPoint3d(30.0, 30.0, 0.0),
            new OcctPoint3d(40.0, 30.0, 0.0));
        w.Document.AddRange([contained, crossing, outside]);

        scene.Engine.SetView(OcctViewOrientation.Top);
        scene.Engine.FitAll();

        var a = scene.Engine.WorldToScreen(new OcctPoint3d(-10.0, -10.0, 0.0));
        var b = scene.Engine.WorldToScreen(new OcctPoint3d(10.0, 10.0, 0.0));

        var window = scene.Engine.QueryRectangle(
            a.X,
            a.Y,
            b.X,
            b.Y,
            allowOverlap: false)
            .Select(w.Document.FindByViewerObject)
            .OfType<CadEntity>()
            .ToHashSet();

        Assert.IsTrue(window.Contains(contained));
        Assert.IsFalse(window.Contains(crossing));
        Assert.IsFalse(window.Contains(outside));

        var crossingSelection = scene.Engine.QueryRectangle(
            a.X,
            a.Y,
            b.X,
            b.Y,
            allowOverlap: true)
            .Select(w.Document.FindByViewerObject)
            .OfType<CadEntity>()
            .ToHashSet();

        Assert.IsTrue(crossingSelection.Contains(contained));
        Assert.IsTrue(crossingSelection.Contains(crossing));
        Assert.IsFalse(crossingSelection.Contains(outside));
    }

    [TestMethod]
    public void SelectionOperationsPreserveWindowCrossingResultsAndGripRefresh()
    {
        using var scene = new NativeScene();
        var w = scene.Workspace;

        var a = new CadLineEntity(
            new OcctPoint3d(-5.0, 0.0, 0.0),
            new OcctPoint3d(5.0, 0.0, 0.0));
        var b = new CadLineEntity(
            new OcctPoint3d(-5.0, 5.0, 0.0),
            new OcctPoint3d(5.0, 5.0, 0.0));
        var c = new CadLineEntity(
            new OcctPoint3d(-5.0, 10.0, 0.0),
            new OcctPoint3d(5.0, 10.0, 0.0));
        w.Document.AddRange([a, b, c]);

        w.Selection.Apply([a, b], CadSelectionOperation.Replace, b);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, b },
            w.Selection.Selected.ToArray());
        Assert.AreSame(b, w.Selection.Primary);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, b },
            w.Grips.Entities.ToArray());

        w.Selection.Apply([c], CadSelectionOperation.Add, c);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, b, c },
            w.Selection.Selected.ToArray());
        Assert.AreSame(c, w.Selection.Primary);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, b, c },
            w.Grips.Entities.ToArray());

        w.Selection.Apply([b], CadSelectionOperation.Remove);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, c },
            w.Selection.Selected.ToArray());
        CollectionAssert.AreEquivalent(
            new CadEntity[] { a, c },
            w.Grips.Entities.ToArray());

        w.Selection.Apply([a, b], CadSelectionOperation.Toggle);
        CollectionAssert.AreEquivalent(
            new CadEntity[] { b, c },
            w.Selection.Selected.ToArray());
        CollectionAssert.AreEquivalent(
            new CadEntity[] { b, c },
            w.Grips.Entities.ToArray());
    }
}
