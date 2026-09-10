using System.Globalization;
using OcctNet;

namespace OCCAD.Core.Tests;

[TestClass]
public sealed class PreciseDrawingTests
{
    [TestMethod]
    public void AbsoluteCoordinatesUseActiveCustomPlane()
    {
        var plane = new CadWorkPlane();
        plane.SetCustom(
            new OcctPoint3d(10, 20, 30),
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "2,3,4",
            OcctPoint3d.Origin,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.AbsoluteCartesian, input.Mode);
        Assert.AreEqual(new OcctPoint3d(14, 22, 33), input.Point);
    }

    [TestMethod]
    public void RelativeCoordinatesUseActiveCustomPlane()
    {
        var plane = new CadWorkPlane();
        plane.SetCustom(
            OcctPoint3d.Origin,
            OcctVector3d.UnitY,
            OcctVector3d.UnitZ);
        var reference = new OcctPoint3d(100, 200, 300);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "@2,3,4",
            reference,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.RelativeCartesian, input.Mode);
        Assert.AreEqual(new OcctPoint3d(104, 202, 303), input.Point);
    }

    [TestMethod]
    public void XzPresetUsesWorldXHorizontalAndWorldZVertical()
    {
        var plane = new CadWorkPlane();
        plane.SetPreset(CadWorkPlanePreset.XZ);

        Assert.AreEqual(OcctVector3d.UnitX, plane.XAxis);
        Assert.AreEqual(OcctVector3d.UnitZ, plane.YAxis);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "25,40",
            OcctPoint3d.Origin,
            plane,
            out var input));
        Assert.AreEqual(new OcctPoint3d(25, 0, 40), input.Point);
    }

    [TestMethod]
    public void RelativePolarUsesActivePlaneAxes()
    {
        var plane = new CadWorkPlane();
        plane.SetPreset(CadWorkPlanePreset.XZ);
        var reference = new OcctPoint3d(5, 6, 7);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "@10<90",
            reference,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.RelativePolar, input.Mode);
        Assert.AreEqual(5.0, input.Point.X, 1e-9);
        Assert.AreEqual(6.0, input.Point.Y, 1e-9);
        Assert.AreEqual(17.0, input.Point.Z, 1e-9);
    }

    [TestMethod]
    public void AbsolutePolarUsesWorkPlaneOriginNotReferencePoint()
    {
        var plane = new CadWorkPlane();
        plane.SetCustom(
            new OcctPoint3d(10, 20, 30),
            OcctVector3d.UnitX,
            OcctVector3d.UnitZ);
        var reference = new OcctPoint3d(100, 200, 300);

        Assert.IsTrue(CadCoordinateInputParser.TryParse(
            "#10<90",
            reference,
            plane,
            out var input));

        Assert.AreEqual(CadCoordinateInputMode.AbsolutePolar, input.Mode);
        Assert.AreEqual(10.0, input.Point.X, 1e-9);
        Assert.AreEqual(20.0, input.Point.Y, 1e-9);
        Assert.AreEqual(40.0, input.Point.Z, 1e-9);
    }

    [TestMethod]
    public void SemicolonSeparatorSupportsCommaDecimalCultures()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var plane = new CadWorkPlane();
            plane.SetPreset(CadWorkPlanePreset.XY);

            Assert.IsTrue(CadCoordinateInputParser.TryParse(
                "1,5;2,5;3,5",
                OcctPoint3d.Origin,
                plane,
                out var input));

            Assert.AreEqual(new OcctPoint3d(1.5, 2.5, 3.5), input.Point);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void MalformedPolarAndEmptyPrefixedInputAreRejected()
    {
        var plane = new CadWorkPlane();

        Assert.IsFalse(CadCoordinateInputParser.TryParse(
            "@",
            OcctPoint3d.Origin,
            plane,
            out _));
        Assert.IsFalse(CadCoordinateInputParser.TryParse(
            "#",
            OcctPoint3d.Origin,
            plane,
            out _));
        Assert.IsFalse(CadCoordinateInputParser.TryParse(
            "10<20<30",
            OcctPoint3d.Origin,
            plane,
            out _));
        Assert.IsFalse(CadCoordinateInputParser.TryParse(
            "0<45",
            OcctPoint3d.Origin,
            plane,
            out _));
    }
}
