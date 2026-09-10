namespace OCCAD.Core.Tests;

[TestClass]
public sealed class LayerIdentityTests
{
    [TestMethod]
    public void NewEntityUsesCurrentLayerStableId()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("Steel");
        var line = new CadLineEntity(default, new(10, 0, 0));

        workspace.AddEntity(line);

        Assert.AreEqual(layer.Id, line.LayerId);
        Assert.AreNotEqual(layer.Name, line.LayerId);
    }

    [TestMethod]
    public void RenamingLayerDoesNotRewriteEntityReference()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("Steel");
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);
        var id = line.LayerId;

        workspace.RenameLayer(layer, "Structure");

        Assert.AreEqual(id, line.LayerId);
        Assert.AreEqual(layer.Id, line.LayerId);
        Assert.AreEqual("Structure", workspace.Layers.GetRequiredById(id).Name);
    }

    [TestMethod]
    public void AssignByDisplayNameStoresStableId()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("Review", makeCurrent: false);
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);

        workspace.AssignEntitiesToLayer([line], "Review");

        Assert.AreEqual(layer.Id, line.LayerId);
        CollectionAssert.Contains(
            workspace.Document.GetEntitiesByLayer(layer.Id).ToArray(),
            line);
    }

    [TestMethod]
    public void LayerWithEntitiesCannotBeRemovedAfterRename()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("A");
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);
        workspace.RenameLayer(layer, "B");

        Assert.Throws<InvalidOperationException>(
            () => workspace.RemoveLayer(layer));
    }
}
