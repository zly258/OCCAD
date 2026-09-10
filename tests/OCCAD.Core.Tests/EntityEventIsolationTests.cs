using System.Drawing;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class EntityEventIsolationTests
{
    [TestMethod]
    public void PublicChangedObserverFailureCannotRollbackGeometryAfterDocumentAcceptsIt()
    {
        var layers = new CadLayerManager();
        var document = new CadDocument(layers);
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        document.Add(line);

        var documentGeometryChanges = 0;
        var laterEntityObserverCalls = 0;
        document.Changed += (_, args) =>
        {
            if (args.Kind == CadDocumentChangeKind.Changed &&
                args.EntityChangeKind == CadEntityChangeKind.Geometry &&
                ReferenceEquals(args.Entity, line))
            {
                documentGeometryChanges++;
            }
        };
        line.Changed += (_, _) =>
            throw new InvalidOperationException("property grid observer failure");
        line.Changed += (_, args) =>
        {
            laterEntityObserverCalls++;
            Assert.AreEqual(CadEntityChangeKind.Geometry, args.Kind);
        };

        line.StartX = 2.0;

        Assert.AreEqual(2.0, line.Start.X, 1e-10);
        Assert.AreEqual(1, documentGeometryChanges);
        Assert.AreEqual(1, laterEntityObserverCalls);
    }

    [TestMethod]
    public void PublicChangedObserverFailureCannotRollbackMetadataOrAppearance()
    {
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        var laterObserverCalls = 0;
        line.Changed += (_, _) =>
            throw new InvalidOperationException("UI observer failure");
        line.Changed += (_, _) => laterObserverCalls++;

        line.Name = "Axis A";
        line.Color = Color.Red;

        Assert.AreEqual("Axis A", line.Name);
        Assert.AreEqual(Color.Red, line.Color);
        Assert.AreEqual(2, laterObserverCalls);
    }

    [TestMethod]
    public void ChangingObserverStillVetoesBeforeEntityMutation()
    {
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        line.Changing += (_, _) =>
            throw new InvalidOperationException("validation veto");

        Assert.Throws<InvalidOperationException>(() => line.Name = "Rejected");
        Assert.AreEqual("Line", line.Name);

        Assert.Throws<InvalidOperationException>(() => line.StartX = 2.0);
        Assert.AreEqual(0.0, line.Start.X, 1e-10);
    }
}
