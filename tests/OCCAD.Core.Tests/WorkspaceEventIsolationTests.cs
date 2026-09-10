using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class WorkspaceEventIsolationTests
{
    [TestMethod]
    public void DomainEventObserverFailureCannotStarveLaterObservers()
    {
        using var workspace = new CadWorkspace();
        var laterObserverCalls = 0;
        CadDomainEventKind? lastKind = null;

        workspace.Events.Changed += (_, _) =>
            throw new InvalidOperationException("integration observer failure");
        workspace.Events.Changed += (_, args) =>
        {
            laterObserverCalls++;
            lastKind = args.Kind;
        };

        workspace.Document.Add(new CadLineEntity(
            OcctPoint3d.Origin,
            new OcctPoint3d(10.0, 0.0, 0.0)));

        Assert.IsTrue(laterObserverCalls >= 1);
        Assert.IsNotNull(lastKind);
    }
}
