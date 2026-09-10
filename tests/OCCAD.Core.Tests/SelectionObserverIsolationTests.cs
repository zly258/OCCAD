using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SelectionObserverIsolationTests
{
    [TestMethod]
    public void EntitySelectionObserversCannotInvalidateFormalSelection()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);

        var secondObserverCalls = 0;
        workspace.Selection.Changed += (_, _) =>
            throw new InvalidOperationException("presentation observer failure");
        workspace.Selection.Changed += (_, args) =>
        {
            secondObserverCalls++;
            Assert.HasCount(1, args.Entities);
            Assert.AreSame(line, args.Primary);
        };

        workspace.Selection.Select(line);

        Assert.HasCount(1, workspace.Selection.Selected);
        Assert.AreSame(line, workspace.Selection.Primary);
        Assert.AreEqual(1, secondObserverCalls);
    }

    [TestMethod]
    public void SelectionClearObserversDoNotPreventLaterObservers()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.Selection.Select(line);

        var secondObserverCalls = 0;
        workspace.Selection.Cleared += (_, _) =>
            throw new InvalidOperationException("clear observer failure");
        workspace.Selection.Cleared += (_, _) =>
            secondObserverCalls++;

        workspace.Selection.Clear();

        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.IsNull(workspace.Selection.Primary);
        Assert.AreEqual(1, secondObserverCalls);
    }

    [TestMethod]
    public void SubobjectObserversCannotInvalidateFormalSubobjectSelection()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);
        workspace.Selection.SetScope(
            CadSelectionScope.Subobject,
            CadSubshapeMask.Edge);

        var secondObserverCalls = 0;
        workspace.Subobjects.Changed += (_, _) =>
            throw new InvalidOperationException("marker rebuild failure");
        workspace.Subobjects.Changed += (_, args) =>
        {
            secondObserverCalls++;
            Assert.HasCount(1, args.Items);
            Assert.IsNotNull(args.Primary);
        };

        workspace.Subobjects.Apply(
            new CadSubobjectSelection(
                line,
                OcctShapeType.Edge,
                0,
                new OcctPoint3d(5.0, 0.0, 0.0)),
            CadSelectionOperation.Replace);

        Assert.HasCount(1, workspace.Subobjects.Selected);
        Assert.IsNotNull(workspace.Subobjects.Primary);
        Assert.AreEqual(1, secondObserverCalls);
    }

    [TestMethod]
    public void PreselectionObserversCannotInvalidateHoverState()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        workspace.Document.Add(line);

        var secondObserverCalls = 0;
        workspace.Preselection.Changed += (_, _) =>
            throw new InvalidOperationException("hover observer failure");
        workspace.Preselection.Changed += (_, args) =>
        {
            secondObserverCalls++;
            Assert.IsNotNull(args.Value);
            Assert.AreSame(line, args.Value.Value.Entity);
        };

        workspace.Preselection.Update(line, null);

        Assert.IsNotNull(workspace.Preselection.Current);
        Assert.AreSame(line, workspace.Preselection.Current.Value.Entity);
        Assert.AreEqual(1, secondObserverCalls);
    }
}
