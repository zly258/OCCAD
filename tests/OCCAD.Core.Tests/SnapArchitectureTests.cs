using System.Drawing;
using System.Reflection;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class SnapArchitectureTests
{
    [TestMethod]
    public void SnapManagerDoesNotOwnNativeMarkerObjects()
    {
        var fields = typeof(CadSnapManager)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsFalse(
            fields.Any(field =>
                field.FieldType == typeof(OcctPoint) ||
                field.FieldType == typeof(OcctPoint?) ||
                field.Name.Contains("markerPixels", StringComparison.OrdinalIgnoreCase) ||
                field.Name.Contains("markerType", StringComparison.OrdinalIgnoreCase)),
            "CadSnapManager must own snap semantics, not native marker presentation.");
    }

    [TestMethod]
    public void SnapPresentationSettingsRemainValidatedByCoreContract()
    {
        using var workspace = new CadWorkspace();

        workspace.Snap.MarkerSize = 9;
        Assert.AreEqual(9, workspace.Snap.MarkerSize);
        workspace.Snap.MarkerSize = 31;
        Assert.AreEqual(31, workspace.Snap.MarkerSize);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => workspace.Snap.MarkerSize = 8);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => workspace.Snap.MarkerSize = 32);

        workspace.Snap.MarkerColor = Color.Magenta;
        Assert.AreEqual(Color.Magenta, workspace.Snap.MarkerColor);
    }

    [TestMethod]
    public void TemporarySnapModesAreToolStateAndClearWhenSnapDeactivates()
    {
        using var workspace = new CadWorkspace();

        workspace.Snap.Active = true;
        workspace.Snap.TemporaryModes =
            CadSnapType.Endpoint |
            CadSnapType.Midpoint;

        Assert.IsNotNull(workspace.Snap.TemporaryModes);

        workspace.Snap.Active = false;

        Assert.IsNull(workspace.Snap.TemporaryModes);
        Assert.IsNull(workspace.Snap.Current);
        Assert.IsEmpty(workspace.Snap.Candidates);
    }

    [TestMethod]
    public void UnsupportedSnapBitsAreRejected()
    {
        using var workspace = new CadWorkspace();
        var unsupported = (CadSnapType)(1 << 29);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => workspace.Snap.Modes = unsupported);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => workspace.Snap.TemporaryModes = unsupported);
    }
}
