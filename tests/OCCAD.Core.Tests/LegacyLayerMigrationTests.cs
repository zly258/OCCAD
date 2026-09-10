using System.Text.Json;
using System.Text.Json.Nodes;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class LegacyLayerMigrationTests
{
    [TestMethod]
    public void VersionOneLayerNamesMigrateToStableIds()
    {
        using var source = new CadWorkspace();
        var sourceLayer = source.AddLayer("Steel");
        source.AddEntity(new CadLineEntity(default, new(10, 0, 0)));

        using var current = new MemoryStream();
        CadDocumentSerializer.Save(source, current);
        current.Position = 0;

        var root = JsonNode.Parse(current)?.AsObject() ??
            throw new InvalidDataException("Serialized CAD document has no root object.");
        root["version"] = 1;
        root["currentLayer"] = sourceLayer.Name;
        root.Remove("currentLayerId");

        foreach (var layerNode in root["layers"]?.AsArray() ?? [])
            layerNode?.AsObject().Remove("id");

        var entityNode = root["entities"]?.AsArray().SingleOrDefault()?.AsObject() ??
            throw new InvalidDataException("Serialized CAD document has no test entity.");
        entityNode["layer"] = sourceLayer.Name;
        entityNode.Remove("layerId");

        using var legacy = new MemoryStream();
        using (var writer = new Utf8JsonWriter(legacy))
        {
            root.WriteTo(writer);
            writer.Flush();
        }
        legacy.Position = 0;

        using var target = new CadWorkspace();
        CadDocumentSerializer.Load(target, legacy);

        var migratedLayer = target.Layers.GetRequired(sourceLayer.Name);
        Assert.AreNotEqual(sourceLayer.Name, migratedLayer.Id);
        Assert.AreEqual(migratedLayer.Id, target.Layers.Current.Id);
        Assert.HasCount(1, target.Document.Entities);
        Assert.AreEqual(migratedLayer.Id, target.Document.Entities[0].LayerId);
        Assert.IsFalse(target.IsModified);
    }
}
