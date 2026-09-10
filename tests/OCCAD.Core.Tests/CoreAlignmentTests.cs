namespace OCCAD.Core.Tests;

[TestClass]
public sealed class CoreAlignmentTests
{
    [TestMethod]
    public void LayerIdentityIsStableAcrossRename()
    {
        var layers = new CadLayerManager();
        var layer = layers.Add("Detail");
        var id = layer.Id;

        Assert.AreEqual(CadLayer.DefaultId, layers.Current.Id);
        Assert.AreNotEqual(layer.Name, id);

        layers.Rename(layer, "Review");

        Assert.AreEqual(id, layer.Id);
        Assert.AreSame(layer, layers.GetRequiredById(id));
        Assert.AreSame(layer, layers.GetRequiredByName("Review"));
        Assert.AreSame(layer, layers.GetRequired(id));
        Assert.AreSame(layer, layers.GetRequired("Review"));
    }

    [TestMethod]
    public void DocumentCanonicalizesLayerNamesToStableIds()
    {
        using var workspace = new CadWorkspace();
        var layer = workspace.AddLayer("Detail");
        var line = new CadLineEntity(default, new(10, 0, 0));

        workspace.AddEntity(line);
        workspace.AssignEntitiesToLayer([line], layer.Name);

        Assert.AreEqual(layer.Id, line.LayerId);
        workspace.Layers.Rename(layer, "Review");
        Assert.AreEqual(layer.Id, line.LayerId);
        Assert.AreSame(layer, workspace.Layers.GetRequiredByName("Review"));
        Assert.AreSame(layer, workspace.Layers.GetRequiredById(line.LayerId));
        Assert.HasCount(1, workspace.Document.GetEntitiesByLayer("Review"));
    }

    [TestMethod]
    public void StableLayerIdsRoundTripAndSurviveRename()
    {
        using var source = new CadWorkspace();
        var layer = source.AddLayer("Detail");
        var line = new CadLineEntity(default, new(10, 0, 0));
        source.AddEntity(line);
        source.AssignEntitiesToLayer([line], layer.Name);
        source.Layers.Rename(layer, "Review");
        var expectedId = layer.Id;

        using var stream = new MemoryStream();
        CadDocumentSerializer.Save(source, stream);
        stream.Position = 0;

        using var target = new CadWorkspace();
        CadDocumentSerializer.Load(target, stream);

        var restoredLayer = target.Layers.GetRequiredByName("Review");
        var restoredEntity = target.Document.Entities.Single();
        Assert.AreEqual(expectedId, restoredLayer.Id);
        Assert.AreEqual(expectedId, restoredEntity.LayerId);
        Assert.AreSame(restoredLayer, target.Layers.GetRequiredById(restoredEntity.LayerId));
    }

    [TestMethod]
    public void PropertyServiceProjectsSourceLikeEntityPropertyMetadata()
    {
        using var workspace = new CadWorkspace();
        var review = workspace.AddLayer("Review");
        var line = new CadLineEntity(default, new(10, 0, 0));
        workspace.AddEntity(line);

        var properties = CadPropertyService.Describe(workspace, line);
        var layer = properties.Single(value => value.Name == nameof(CadEntity.LayerId));

        Assert.AreEqual("General", layer.Group);
        Assert.AreEqual(CadPropertyEditorKind.Layer, layer.EditorType);
        Assert.IsFalse(layer.ReadOnly);
        Assert.IsTrue(layer.EditorParams.TryGetValue("choices", out var choices));
        CollectionAssert.Contains(((string[])choices!), "Review");

        Assert.IsTrue(CadPropertyService.TryApply(
            workspace,
            [line],
            nameof(CadEntity.LayerId),
            "Review",
            out var error), error);
        Assert.AreEqual(review.Id, line.LayerId);
        Assert.AreEqual("Review", CadPropertyService.Describe(workspace, line)
            .Single(value => value.Name == nameof(CadEntity.LayerId)).Value);
    }

    [TestMethod]
    public void SettingsStoreRoundTripsWithoutUiDependency()
    {
        var settings = new CadSettingsStore();
        settings.Set(CadSettingKeys.GripSize, 9.0);
        settings.Set(CadSettingKeys.SnapSize, 11.0);
        settings.Set(CadSettingKeys.GeometryPrecision, 1e-7);

        using var stream = new MemoryStream();
        settings.Save(stream);
        stream.Position = 0;

        var restored = new CadSettingsStore();
        restored.Load(stream);

        Assert.AreEqual(9.0, restored.Get<double>(CadSettingKeys.GripSize));
        Assert.AreEqual(11.0, restored.Get<double>(CadSettingKeys.SnapSize));
        Assert.AreEqual(1e-7, restored.Get<double>(CadSettingKeys.GeometryPrecision));
    }
}
