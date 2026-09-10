using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SelectionArchitectureTests
{
    [TestMethod]
    public void SelectionOperationsKeepDeterministicPrimaryEntity()
    {
        using var workspace = new CadWorkspace();
        var first = new CadLineEntity(default, new(10, 0, 0));
        var second = new CadLineEntity(default, new(0, 10, 0));
        var third = new CadPointEntity(new(5, 5, 0));
        workspace.Document.AddRange([first, second, third]);

        workspace.Selection.Select(first);
        Assert.AreSame(first, workspace.Selection.Primary);

        workspace.Selection.Select(second, CadSelectionOperation.Add);
        CollectionAssert.AreEqual(
            new CadEntity[] { first, second },
            workspace.Selection.Selected.ToArray());
        Assert.AreSame(second, workspace.Selection.Primary);

        workspace.Selection.Select(first, CadSelectionOperation.Toggle);
        CollectionAssert.AreEqual(
            new CadEntity[] { second },
            workspace.Selection.Selected.ToArray());
        Assert.AreSame(second, workspace.Selection.Primary);

        workspace.Selection.Select(third, CadSelectionOperation.Add);
        Assert.AreSame(third, workspace.Selection.Primary);

        workspace.Selection.Select(third, CadSelectionOperation.Remove);
        CollectionAssert.AreEqual(
            new CadEntity[] { second },
            workspace.Selection.Selected.ToArray());
        Assert.AreSame(second, workspace.Selection.Primary);
    }

    [TestMethod]
    public void EntityAndLayerStateImmediatelyInvalidateSelection()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);

        workspace.Selection.Select(line);
        Assert.HasCount(1, workspace.Selection.Selected);
        line.Visible = false;
        Assert.IsEmpty(workspace.Selection.Selected);

        line.Visible = true;
        workspace.Selection.Select(line);
        var review = workspace.AddLayer("Review");
        line.LayerId = review.Id;
        Assert.HasCount(1, workspace.Selection.Selected);

        review.Locked = true;
        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsFalse(workspace.Selection.CanSelect(line));
    }

    [TestMethod]
    public void ScopeChangeClearsIncompatibleSelectionAndPreselection()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);

        workspace.Selection.Select(line);
        workspace.Preselection.Update(line, hit: null);
        Assert.IsNotNull(workspace.Preselection.Current);

        workspace.Selection.SetScope(
            CadSelectionScope.Subobject,
            CadSubshapeMask.Edge);

        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsNull(workspace.Selection.Primary);
        Assert.IsNull(workspace.Preselection.Current);
        Assert.IsTrue(
            workspace.Selection.CanSelectSubshape(
                line,
                OcctShapeType.Edge));
        Assert.IsFalse(
            workspace.Selection.CanSelectSubshape(
                line,
                OcctShapeType.Face));
    }
}
