using System.Reflection;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SubobjectSelectionArchitectureTests
{
    [TestMethod]
    public void SubobjectSelectionManagerDoesNotOwnViewerEngine()
    {
        var fields = typeof(CadSubobjectSelectionManager)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsFalse(
            fields.Any(field => field.FieldType == typeof(OcctEngine)),
            "Subobject selection state must not own the native viewer engine.");
    }

    [TestMethod]
    public void ReplaceAddRemoveToggleUseOneFormalSubobjectSelectionSet()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);
        workspace.Selection.Scope = CadSelectionScope.Subobject;
        workspace.Selection.SubshapeMask = CadSubshapeMask.Edge;

        var a = new CadSubobjectSelection(
            line,
            OcctShapeType.Edge,
            0,
            new(2, 0, 0));
        var b = new CadSubobjectSelection(
            line,
            OcctShapeType.Edge,
            1,
            new(8, 0, 0));

        workspace.Subobjects.Apply(a, CadSelectionOperation.Replace);
        Assert.HasCount(1, workspace.Subobjects.Selected);
        Assert.AreEqual(a.Reference, workspace.Subobjects.PrimaryReference);

        workspace.Subobjects.Apply(b, CadSelectionOperation.Add);
        Assert.HasCount(2, workspace.Subobjects.Selected);
        Assert.AreEqual(b.Reference, workspace.Subobjects.PrimaryReference);

        workspace.Subobjects.Apply(a, CadSelectionOperation.Remove);
        Assert.HasCount(1, workspace.Subobjects.Selected);
        Assert.AreEqual(b.Reference, workspace.Subobjects.PrimaryReference);

        workspace.Subobjects.Apply(b, CadSelectionOperation.Toggle);
        Assert.IsEmpty(workspace.Subobjects.Selected);
        Assert.IsNull(workspace.Subobjects.Primary);
    }

    [TestMethod]
    public void WholeEntityAndSubobjectFormalSelectionsDoNotCoexist()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);

        workspace.Selection.Select(line);
        Assert.HasCount(1, workspace.Selection.Selected);

        workspace.Selection.Scope = CadSelectionScope.Subobject;
        workspace.Selection.SubshapeMask = CadSubshapeMask.Edge;
        workspace.Subobjects.Apply(
            new CadSubobjectSelection(
                line,
                OcctShapeType.Edge,
                0,
                new(5, 0, 0)),
            CadSelectionOperation.Replace);

        Assert.IsEmpty(workspace.Selection.Selected);
        Assert.HasCount(1, workspace.Subobjects.Selected);
    }

    [TestMethod]
    public void RemovingEntityClearsItsSubobjectSelection()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.Document.Add(line);
        workspace.Selection.Scope = CadSelectionScope.Subobject;
        workspace.Selection.SubshapeMask = CadSubshapeMask.Edge;
        workspace.Subobjects.Apply(
            new CadSubobjectSelection(
                line,
                OcctShapeType.Edge,
                0,
                new(5, 0, 0)),
            CadSelectionOperation.Replace);

        workspace.Document.Remove(line);

        Assert.IsEmpty(workspace.Subobjects.Selected);
        Assert.IsNull(workspace.Subobjects.Primary);
    }
}
