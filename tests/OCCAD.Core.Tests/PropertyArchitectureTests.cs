using System.Drawing;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class PropertyArchitectureTests
{
    [TestMethod]
    public void EntityLayerReferenceRemainsStableWhenLayerNameChanges()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("Review");
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);

        Assert.AreEqual(layer.Id, line.LayerId);
        workspace.RenameLayer(layer, "Issued");

        Assert.AreEqual(layer.Id, line.LayerId);
        Assert.AreEqual("Issued", workspace.Layers.GetRequiredById(line.LayerId).Name);
    }

    [TestMethod]
    public void BulkPropertyUpdateUsesOneTransactionAndStableLayerId()
    {
        using var workspace = new CadWorkspace();
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);
        var layer = workspace.AddLayer("Review", makeCurrent: false);
        workspace.History.Clear();
        workspace.MarkSaved();

        var applied = CadPropertyService.TryApply(
            workspace,
            [line],
            new Dictionary<string, object?>
            {
                [nameof(CadEntity.LayerId)] = layer.Name,
                [nameof(CadEntity.Color)] = Color.Red
            },
            out var error);

        Assert.IsTrue(applied, error);
        Assert.AreEqual(layer.Id, line.LayerId);
        Assert.AreEqual(Color.Red, line.Color);
        Assert.IsFalse(line.ColorByLayer);
        Assert.AreEqual("Properties", workspace.History.UndoName);

        Assert.IsTrue(workspace.Undo());
        Assert.AreEqual(CadLayer.DefaultId, line.LayerId);
        Assert.IsTrue(line.ColorByLayer);
        Assert.IsFalse(workspace.History.CanUndo);
        Assert.IsFalse(workspace.IsModified);
    }
}
