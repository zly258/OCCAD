using System.ComponentModel;
using System.IO.Compression;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadImportedShapeEntity : CadEntity
{
    private byte[] _brep;
    private readonly string _sourceFormat;
    private readonly string _sourceName;

    public CadImportedShapeEntity(
        ReadOnlySpan<byte> brep,
        string sourceFormat,
        string sourceName) : base("Imported Shape")
    {
        if (brep.IsEmpty)
            throw new ArgumentException(
                "Imported BREP data must not be empty.",
                nameof(brep));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        _brep = brep.ToArray();
        _sourceFormat = sourceFormat.Trim().ToUpperInvariant();
        _sourceName = sourceName.Trim();
        Name = Path.GetFileNameWithoutExtension(_sourceName);
        DisplayMode = OcctDisplayMode.Shaded;
    }

    [Category("Source"), ReadOnly(true)]
    public string SourceFormat => _sourceFormat;

    [Category("Source"), ReadOnly(true)]
    public string SourceName => _sourceName;

    [Category("Source"), ReadOnly(true)]
    public long BrepBytes => _brep.LongLength;

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        using var model = new OcctModelingSession();
        var shape = model.DeserializeBrep(_brep);
        return engine.CreateShapeFromModel(model, shape);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints() =>
        Array.Empty<CadSnapPoint>();

    public override IReadOnlyList<CadGripPoint> GetGripPoints() =>
        Array.Empty<CadGripPoint>();

    public override void MoveGrip(int index, OcctPoint3d targetPoint) =>
        throw new ArgumentOutOfRangeException(nameof(index));

    public override CadEntity Duplicate() =>
        CopyPropertiesTo(
            new CadImportedShapeEntity(
                _brep,
                _sourceFormat,
                _sourceName));

    public override void RestoreGeometry(CadEntity snapshot)
    {
        if (snapshot is not CadImportedShapeEntity value)
            throw new ArgumentException(
                "Snapshot type does not match.",
                nameof(snapshot));

        _brep = value._brep.ToArray();
        RaiseGeometryChanged(nameof(RestoreGeometry));
    }

    public override void Translate(OcctVector3d displacement)
    {
        ValidateDisplacement(displacement);
        Transform(
            (model, shape) =>
                model.Translate(shape, displacement),
            nameof(Translate));
    }

    public override void Rotate(
        OcctPoint3d center,
        OcctVector3d axis,
        double angleDegrees)
    {
        Transform(
            (model, shape) =>
                model.Rotate(
                    shape,
                    center,
                    axis,
                    angleDegrees),
            nameof(Rotate));
    }

    public override void Scale(
        OcctPoint3d center,
        double factor)
    {
        CadTransformMath.ValidateScale(factor);
        Transform(
            (model, shape) =>
                model.Scale(shape, center, factor),
            nameof(Scale));
    }

    internal static JsonObject WriteGeometry(
        CadImportedShapeEntity entity) =>
        new()
        {
            ["sourceFormat"] = entity._sourceFormat,
            ["sourceName"] = entity._sourceName,
            ["brepGzip"] =
                Convert.ToBase64String(
                    Compress(entity._brep))
        };

    internal static CadImportedShapeEntity ReadGeometry(JsonObject data)
    {
        var encoded =
            data["brepGzip"]?.GetValue<string>() ??
            throw new FormatException(
                "Imported shape BREP data is missing.");

        return new CadImportedShapeEntity(
            Decompress(
                Convert.FromBase64String(encoded)),
            data["sourceFormat"]?.GetValue<string>() ??
                throw new FormatException(
                    "Imported shape source format is missing."),
            data["sourceName"]?.GetValue<string>() ??
                throw new FormatException(
                    "Imported shape source name is missing."));
    }

    internal byte[] SnapshotBrep() =>
        _brep.ToArray();

    private void Transform(
        Func<OcctModelingSession, OcctModelShape, OcctModelShape> transform,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(transform);

        using var model = new OcctModelingSession();
        var source = model.DeserializeBrep(_brep);
        var result = transform(model, source);
        _brep = model.SerializeBrep(result);
        RaiseGeometryChanged(propertyName);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(
                   output,
                   CompressionLevel.SmallestSize,
                   leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(
            input,
            CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
