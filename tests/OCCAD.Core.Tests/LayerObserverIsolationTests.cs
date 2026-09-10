using System.Drawing;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class LayerObserverIsolationTests
{
    [TestMethod]
    public void LayerChangedObserverFailureCannotInvalidateAppliedValue()
    {
        var manager = new CadLayerManager();
        var layer = manager.Add("A");
        var laterObserverCalls = 0;

        layer.Changed += (_, _) =>
            throw new InvalidOperationException("property grid observer failure");
        layer.Changed += (_, args) =>
        {
            laterObserverCalls++;
            Assert.AreEqual(CadLayerChangeKind.Appearance, args.Kind);
        };

        layer.Color = Color.Red;

        Assert.AreEqual(Color.Red, layer.Color);
        Assert.AreEqual(1, laterObserverCalls);
    }

    [TestMethod]
    public void LayerManagerObserverFailureCannotInvalidateAddedOrCurrentLayer()
    {
        var manager = new CadLayerManager();
        var laterObserverCalls = 0;
        manager.Changed += (_, _) =>
            throw new InvalidOperationException("layer panel observer failure");
        manager.Changed += (_, _) =>
            laterObserverCalls++;

        var layer = manager.Add("A");
        manager.SetCurrent(layer);

        Assert.AreSame(layer, manager.GetRequired("A"));
        Assert.AreSame(layer, manager.Current);
        Assert.AreEqual(2, laterObserverCalls);
    }

    [TestMethod]
    public void ChangingObserverRemainsStrictBeforeMutation()
    {
        var manager = new CadLayerManager();
        var layer = manager.Add("A");
        layer.Changing += (_, _) =>
            throw new InvalidOperationException("validation veto");

        Assert.Throws<InvalidOperationException>(() => layer.Visible = false);
        Assert.IsTrue(layer.Visible);
    }
}
