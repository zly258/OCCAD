using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class CenterMarkTests
{
    [TestMethod]
    public void AssociativeCenterMarkCopiesHostSemantics()
    {
        var circle = new CadCircleEntity(
            new(10, 20, 0),
            OcctVector3d.UnitZ,
            25)
        {
            LayerId = "A-CENTER",
            ColorByLayer = false,
            LineWidthByLayer = false,
            LineWidth = 2.0
        };

        var mark = CadCenterMarkEntity.CreateAssociative(circle);

        Assert.AreEqual(circle.Center, mark.Center);
        Assert.AreEqual(circle.Normal, mark.Normal);
        Assert.AreEqual(circle.Radius, mark.Radius);
        Assert.AreEqual(circle.LayerId, mark.LayerId);
        Assert.AreEqual(circle.Id, mark.HostCircleId);
        CollectionAssert.AreEqual(
            new[] { circle.Id },
            mark.SourceEntityIds.ToArray());
        Assert.AreEqual(OcctLineStyle.DotDash, mark.LineStyle);
        Assert.IsFalse(mark.LineStyleByLayer);
    }

    [TestMethod]
    public void DefaultParametersAndGripsMatchSourceContract()
    {
        var mark = new CadCenterMarkEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            20);

        Assert.AreEqual(1.0, mark.CrossSizeFactor);
        Assert.AreEqual(0.05, mark.CrossSpacingFactor);
        Assert.AreEqual(0.0, mark.LeftExtend);
        Assert.AreEqual(0.0, mark.RightExtend);
        Assert.AreEqual(0.0, mark.TopExtend);
        Assert.AreEqual(0.0, mark.BottomExtend);
        Assert.IsFalse(mark.ShowExtend);
        Assert.HasCount(5, mark.GetGripPoints());

        mark.MoveGrip(2, new OcctPoint3d(30, 0, 0));
        Assert.AreEqual(10.0, mark.RightExtend, 1e-9);
    }

    [TestMethod]
    public void DocumentRefreshesCenterMarkWhenHostCircleChanges()
    {
        using var workspace = new CadWorkspace();
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            10);
        workspace.Document.Add(circle);

        var mark = CadCenterMarkEntity.CreateAssociative(circle);
        workspace.Document.Add(mark);

        circle.Radius = 30;
        circle.CenterX = 50;

        Assert.AreEqual(30.0, mark.Radius);
        Assert.AreEqual(new OcctPoint3d(50, 0, 0), mark.Center);
        CollectionAssert.Contains(
            workspace.Document.GetDependentEntities(circle.Id).ToArray(),
            mark);
    }

    [TestMethod]
    public void DeletingHostCircleRemovesMarkAndUndoRestoresBoth()
    {
        using var workspace = new CadWorkspace();
        var circle = new CadCircleEntity(
            OcctPoint3d.Origin,
            OcctVector3d.UnitZ,
            10);
        workspace.Document.Add(circle);
        var mark = CadCenterMarkEntity.CreateAssociative(circle);
        workspace.Document.Add(mark);
        workspace.History.Clear();

        workspace.DeleteEntities([circle]);

        Assert.IsFalse(workspace.Document.Entities.Contains(circle));
        Assert.IsFalse(workspace.Document.Entities.Contains(mark));
        Assert.AreEqual("Delete 2", workspace.History.UndoName);

        Assert.IsTrue(workspace.Undo());
        Assert.IsTrue(workspace.Document.Entities.Contains(circle));
        Assert.IsTrue(workspace.Document.Entities.Contains(mark));
        Assert.AreEqual(circle.Id, mark.HostCircleId);

        Assert.IsTrue(workspace.Redo());
        Assert.IsFalse(workspace.Document.Entities.Contains(circle));
        Assert.IsFalse(workspace.Document.Entities.Contains(mark));
    }

    [TestMethod]
    public void RegistryContainsCenterMarkEntityToolAndAction()
    {
        using var workspace = new CadWorkspace();

        Assert.AreEqual(
            typeof(CadCenterMarkEntity),
            workspace.Entities.GetRequired("centermark").EntityType);
        Assert.IsTrue(workspace.Tools.IsRegistered("centermark"));
        Assert.AreEqual(
            "centermark",
            workspace.Actions.Describe("draw.centermark")!.ToolId);
    }
}
