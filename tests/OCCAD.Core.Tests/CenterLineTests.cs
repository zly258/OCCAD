using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class CenterLineTests
{
    [TestMethod]
    public void AssociativeCenterLineUsesMidpointsAndSourceIds()
    {
        var first = new CadLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));
        var second = new CadLineEntity(
            new(0, 20, 0),
            new(100, 20, 0));

        var center =
            CadCenterLineEntity.CreateAssociative(first, second);

        Assert.AreEqual(new OcctPoint3d(0, 10, 0), center.Start);
        Assert.AreEqual(new OcctPoint3d(100, 10, 0), center.End);
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            center.SourceEntityIds.ToArray());
        Assert.AreEqual(OcctLineStyle.DotDash, center.LineStyle);
        Assert.IsFalse(center.LineStyleByLayer);
    }

    [TestMethod]
    public void OppositeHostDirectionsPairCorrespondingEnds()
    {
        var first = new CadLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));
        var second = new CadLineEntity(
            new(100, 20, 0),
            new(0, 20, 0));

        var center =
            CadCenterLineEntity.CreateAssociative(first, second);

        Assert.IsTrue(center.ReverseHost2);
        Assert.AreEqual(new OcctPoint3d(0, 10, 0), center.Start);
        Assert.AreEqual(new OcctPoint3d(100, 10, 0), center.End);
    }

    [TestMethod]
    public void DocumentAutomaticallyRefreshesAssociativeGeometry()
    {
        using var workspace = new CadWorkspace();
        var first = new CadLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));
        var second = new CadLineEntity(
            new(0, 20, 0),
            new(100, 20, 0));
        workspace.Document.AddRange([first, second]);
        var center =
            CadCenterLineEntity.CreateAssociative(first, second);
        workspace.Document.Add(center);

        second.StartY = 40;
        second.EndY = 40;

        Assert.AreEqual(new OcctPoint3d(0, 20, 0), center.Start);
        Assert.AreEqual(new OcctPoint3d(100, 20, 0), center.End);
        CollectionAssert.Contains(
            workspace.Document.GetDependentEntities(second.Id).ToArray(),
            center);
    }

    [TestMethod]
    public void DeletingHostRemovesDependentAndUndoRestoresDependencyGraph()
    {
        using var workspace = new CadWorkspace();
        var first = new CadLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));
        var second = new CadLineEntity(
            new(0, 20, 0),
            new(100, 20, 0));
        workspace.Document.AddRange([first, second]);
        var center = CadCenterLineEntity.CreateAssociative(first, second);
        workspace.Document.Add(center);
        workspace.History.Clear();

        workspace.DeleteEntities([first]);

        Assert.IsFalse(workspace.Document.Entities.Contains(first));
        Assert.IsFalse(workspace.Document.Entities.Contains(center));
        Assert.IsTrue(workspace.Document.Entities.Contains(second));
        Assert.AreEqual("Delete 2", workspace.History.UndoName);

        Assert.IsTrue(workspace.Undo());
        Assert.IsTrue(workspace.Document.Entities.Contains(first));
        Assert.IsTrue(workspace.Document.Entities.Contains(center));
        Assert.IsTrue(workspace.Document.Entities.Contains(second));
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            center.SourceEntityIds.ToArray());

        Assert.IsTrue(workspace.Redo());
        Assert.IsFalse(workspace.Document.Entities.Contains(first));
        Assert.IsFalse(workspace.Document.Entities.Contains(center));
        Assert.IsTrue(workspace.Document.Entities.Contains(second));
    }

    [TestMethod]
    public void DeletingBothHostsIncludesSharedDependentOnlyOnce()
    {
        using var workspace = new CadWorkspace();
        var first = new CadLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));
        var second = new CadLineEntity(
            new(0, 20, 0),
            new(100, 20, 0));
        workspace.Document.AddRange([first, second]);
        var center = CadCenterLineEntity.CreateAssociative(first, second);
        workspace.Document.Add(center);
        workspace.History.Clear();

        workspace.DeleteEntities([first, second]);

        Assert.IsEmpty(workspace.Document.Entities);
        Assert.AreEqual("Delete 3", workspace.History.UndoName);

        Assert.IsTrue(workspace.Undo());
        Assert.HasCount(3, workspace.Document.Entities);
        Assert.AreEqual(1, workspace.Document.Entities.Count(entity => entity.Id == center.Id));
        CollectionAssert.AreEquivalent(
            new[] { first.Id, second.Id },
            center.SourceEntityIds.ToArray());

        Assert.IsTrue(workspace.Redo());
        Assert.IsEmpty(workspace.Document.Entities);
    }

    [TestMethod]
    public void ExtensionAndGripContractMatchesSourceEntity()
    {
        var center = new CadCenterLineEntity(
            new(0, 0, 0),
            new(100, 0, 0));

        Assert.AreEqual(20.0, center.StartExtend);
        Assert.AreEqual(20.0, center.EndExtend);
        Assert.IsTrue(center.ShowExtend);
        Assert.HasCount(2, center.GetGripPoints());

        center.MoveGrip(0, new(-10, 0, 0));
        Assert.AreEqual(new OcctPoint3d(-10, 0, 0), center.Start);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => center.StartExtend = -1);
    }

    [TestMethod]
    public void RegistryContainsCenterLineEntityAndTool()
    {
        using var workspace = new CadWorkspace();

        Assert.AreEqual(
            typeof(CadCenterLineEntity),
            workspace.Entities.GetRequired("centerline").EntityType);
        Assert.IsTrue(workspace.Tools.IsRegistered("centerline"));
        Assert.AreEqual(
            "centerline",
            workspace.Actions.Describe("draw.centerline")!.ToolId);
    }
}
