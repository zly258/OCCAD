using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class InteractionTests
{
    private static CadLineEntity Line() => new(OcctPoint3d.Origin, new(10, 0, 0));

    [TestMethod]
    public void EntitySelectionOperationsAndCategoryFilter()
    {
        using var w = new CadWorkspace();
        var a = Line(); var b = Line(); var box = new CadBoxEntity(default, 10, 10, 10);
        w.Document.AddRange([a, b, box]);
        w.Selection.Select(a);
        w.Selection.Select(b, CadSelectionOperation.Add);
        Assert.HasCount(2, w.Selection.Selected);
        w.Selection.Select(a, CadSelectionOperation.Toggle);
        Assert.AreSame(b, w.Selection.Primary);
        w.Selection.Select(b, CadSelectionOperation.Remove);
        Assert.IsEmpty(w.Selection.Selected);
        w.Selection.FilterKind = CadEntityFilterKind.Curve;
        w.Selection.Apply([a, box], CadSelectionOperation.Replace);
        CollectionAssert.AreEqual(new CadEntity[] { a }, w.Selection.Selected.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Selection.FilterKind = (CadEntityFilterKind)123);
    }

    [TestMethod]
    public void TopologyMaskIsIndependentOfEntityCategoryAndScopeIsExclusive()
    {
        using var w = new CadWorkspace();
        var box = new CadBoxEntity(default, 10, 10, 10);
        w.Document.Add(box);
        w.Selection.Select(box);
        w.Selection.FilterKind = CadEntityFilterKind.Curve;
        w.Selection.SetScope(CadSelectionScope.Subobject, CadSubshapeMask.Edge | CadSubshapeMask.Face);
        Assert.IsTrue(w.Selection.CanSelectSubshape(box, OcctShapeType.Edge));
        w.Subobjects.Apply(new(box, OcctShapeType.Edge, 0, default), CadSelectionOperation.Add);
        w.Subobjects.Apply(new(box, OcctShapeType.Face, 0, default), CadSelectionOperation.Add);
        w.Subobjects.Apply(new(box, OcctShapeType.Vertex, 0, default), CadSelectionOperation.Add);
        Assert.HasCount(2, w.Subobjects.Selected);
        w.Selection.Select(box);
        Assert.IsEmpty(w.Selection.Selected);
        w.Selection.SubshapeMask = CadSubshapeMask.Face;
        Assert.HasCount(1, w.Subobjects.Selected);
        Assert.AreEqual(OcctShapeType.Face, w.Subobjects.Primary!.Value.SubshapeType);
        w.Selection.Scope = CadSelectionScope.Entity;
        Assert.IsEmpty(w.Subobjects.Selected);
    }

    [TestMethod]
    public void HoverNormalizesEntityHitsAndRejectsDisallowedTopology()
    {
        using var w = new CadWorkspace();
        var line = Line(); w.Document.Add(line);
        // Owner resolution happens in the viewport; the manager only needs hit geometry.
        var hit = new OcctSelectionHitDetail(default!, OcctShapeType.Edge, 0, new(5, 0, 0), 0, 0);
        w.Preselection.Update(line, hit);
        Assert.IsFalse(w.Preselection.Current!.Value.IsSubshape);
        Assert.AreEqual(new OcctPoint3d(5, 0, 0), w.Preselection.Current.Value.Point);
        w.Selection.SetScope(CadSelectionScope.Subobject, CadSubshapeMask.Face);
        Assert.IsNull(w.Preselection.Current);
        w.Preselection.Update(line, hit);
        Assert.IsNull(w.Preselection.Current);
        w.Selection.SubshapeMask = CadSubshapeMask.Edge;
        w.Preselection.Update(line, hit);
        Assert.IsTrue(w.Preselection.Current!.Value.IsSubshape);
    }

    [TestMethod]
    public void PlacementAxesIncludeRotationButNotTranslation()
    {
        var placement = CadPlacement.Identity.RotateWorld(default, OcctVector3d.UnitX, 90)
            .TranslateWorld(new(7, 8, 9));
        var axes = placement.GetWorldAxes();
        Assert.AreEqual(new OcctPoint3d(7, 8, 9), axes.Origin);
        Assert.AreEqual(1, axes.XAxis.X, 1e-10);
        Assert.AreEqual(1, axes.YAxis.Z, 1e-10);
        Assert.AreEqual(-1, axes.ZAxis.Y, 1e-10);
        Assert.AreEqual(1, axes.XAxis.Dot(axes.YAxis.Cross(axes.ZAxis)), 1e-10);
    }

    [TestMethod]
    [DataRow("circle")]
    [DataRow("arc")]
    [DataRow("polyline")]
    [DataRow("move")]
    [DataRow("copy")]
    [DataRow("rotate")]
    [DataRow("scale")]
    [DataRow("mirror")]
    [DataRow("array")]
    public void ActivateCancelRestoresNeutralStateAndSelectionScope(string id)
    {
        using var w = new CadWorkspace();
        w.Selection.SetScope(CadSelectionScope.Subobject, CadSubshapeMask.Face);
        Assert.IsTrue(w.Tools.Activate(id));
        Assert.AreEqual(CadSelectionScope.Entity, w.Selection.Scope);
        w.Snap.TemporaryModes = CadSnapType.Endpoint;
        Assert.IsTrue(w.Tools.CancelCurrent());
        AssertNeutral(w);
        Assert.AreEqual(CadSelectionScope.Subobject, w.Selection.Scope);
        Assert.AreEqual(CadSubshapeMask.Face, w.Selection.SubshapeMask);
    }

    [TestMethod]
    public void CleanupContinuesWhenToolAndObserverThrow()
    {
        using var w = new CadWorkspace();
        w.Tools.Register<FailingCleanupTool>("test.cleanup");
        w.Tools.Activate("test.cleanup");
        w.WorkPlane.Changed += Throw;
        Assert.Throws<InvalidOperationException>(() => w.Tools.CancelCurrent());
        AssertNeutral(w);
        w.WorkPlane.Changed -= Throw;
        static void Throw(object? sender, EventArgs args) => throw new InvalidOperationException("observer failure");
    }

    [TestMethod]
    public void ActivationFailureRestoresNeutralState()
    {
        using var w = new CadWorkspace();
        w.Tools.Register<FailingActivationTool>("test.activation");
        Assert.Throws<InvalidOperationException>(() => w.Tools.Activate("test.activation"));
        AssertNeutral(w);
    }

    [TestMethod]
    public void PreviewRejectsLiveDocumentEntityBeforeTouchingPresentation()
    {
        using var w = new CadWorkspace(); var line = Line(); w.Document.Add(line);
        Assert.Throws<ArgumentException>(() => w.Preview.Show(line));
        Assert.IsFalse(w.Preview.HasTransient);
        Assert.HasCount(1, w.Document.Entities);
    }

    internal static void AssertNeutral(CadWorkspace w) =>
        Assert.IsEmpty(w.Tools.NeutralStateViolations, string.Join(", ", w.Tools.NeutralStateViolations));

    public sealed class FailingCleanupTool : CadTool
    {
        public override string Id => "test.cleanup";
        public override string DisplayName => Id;
        protected override void OnDeactivated() => throw new InvalidOperationException("tool failure");
    }
    public sealed class FailingActivationTool : CadTool
    {
        public override string Id => "test.activation";
        public override string DisplayName => Id;
        protected override void OnActivated() => throw new InvalidOperationException("activation failure");
    }
}
