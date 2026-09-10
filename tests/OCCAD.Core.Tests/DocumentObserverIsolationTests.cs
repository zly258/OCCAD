using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class DocumentObserverIsolationTests
{
    [TestMethod]
    public void ChangedObserverFailureCannotInvalidateAddedEntity()
    {
        var layers = new CadLayerManager();
        var document = new CadDocument(layers);
        var line = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));

        var secondObserverCalls = 0;
        document.Changed += (_, _) =>
            throw new InvalidOperationException("presentation cache failure");
        document.Changed += (_, args) =>
        {
            secondObserverCalls++;
            Assert.AreEqual(CadDocumentChangeKind.Added, args.Kind);
            Assert.AreSame(line, args.Entity);
        };

        document.Add(line);

        Assert.HasCount(1, document.Entities);
        Assert.AreSame(line, document.Entities[0]);
        Assert.AreEqual(1, secondObserverCalls);
    }

    [TestMethod]
    public void ChangeSetObserverFailureCannotStarveLaterObservers()
    {
        var layers = new CadLayerManager();
        var document = new CadDocument(layers);
        var first = new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0));
        var second = new CadLineEntity(
            new OcctPoint3d(0.0, 5.0, 0.0),
            new OcctPoint3d(10.0, 5.0, 0.0));

        var laterObserverCalls = 0;
        document.ChangeSetCommitted += (_, _) =>
            throw new InvalidOperationException("workspace observer failure");
        document.ChangeSetCommitted += (_, args) =>
        {
            laterObserverCalls++;
            Assert.HasCount(2, args.Changes);
            Assert.HasCount(2, args.Entities);
        };

        document.AddRange([first, second]);

        Assert.HasCount(2, document.Entities);
        Assert.AreEqual(1, laterObserverCalls);
    }
}
