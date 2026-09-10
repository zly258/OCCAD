using OcctNet;

namespace OCCAD;

public enum CadExchangeFormat
{
    Step,
    Iges,
    Brep,
    Stl,
    Obj,
    Gltf
}

public static class CadExchangeService
{
    public static CadExchangeFormat FormatFromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetExtension(path)
            .ToLowerInvariant() switch
        {
            ".step" or ".stp" => CadExchangeFormat.Step,
            ".iges" or ".igs" => CadExchangeFormat.Iges,
            ".brep" or ".brp" => CadExchangeFormat.Brep,
            ".stl" => CadExchangeFormat.Stl,
            ".obj" => CadExchangeFormat.Obj,
            ".gltf" or ".glb" => CadExchangeFormat.Gltf,
            _ => throw new NotSupportedException(
                $"File extension '{Path.GetExtension(path)}' is not supported.")
        };
    }

    public static Task<CadImportedShapeEntity> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var format = FormatFromPath(fullPath);

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var model = new OcctModelingSession();
                var shape = format switch
                {
                    CadExchangeFormat.Step =>
                        model.ImportStep(fullPath),
                    CadExchangeFormat.Iges =>
                        model.ImportIges(fullPath),
                    CadExchangeFormat.Brep =>
                        model.ImportBrep(fullPath),
                    CadExchangeFormat.Stl =>
                        model.ImportStl(fullPath),
                    CadExchangeFormat.Obj =>
                        model.ImportObj(fullPath),
                    CadExchangeFormat.Gltf =>
                        model.ImportGltf(fullPath),
                    _ => throw new InvalidOperationException()
                };

                cancellationToken.ThrowIfCancellationRequested();
                var brep = model.SerializeBrep(shape);
                return new CadImportedShapeEntity(
                    brep,
                    format.ToString(),
                    Path.GetFileName(fullPath));
            },
            cancellationToken);
    }

    public static async Task ExportAsync(
        CadWorkspace workspace,
        CadEntity entity,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var engine =
            workspace.Engine ??
            throw new InvalidOperationException(
                "OCCT engine is not attached.");
        var fullPath = Path.GetFullPath(path);
        var format = FormatFromPath(fullPath);

        var shape = entity.BuildShape(engine);
        try
        {
            switch (format)
            {
                case CadExchangeFormat.Step:
                    await engine.ExportStepAsync(
                        shape,
                        fullPath,
                        cancellationToken);
                    return;

                case CadExchangeFormat.Iges:
                    await engine.ExportIgesAsync(
                        shape,
                        fullPath,
                        cancellationToken);
                    return;

                case CadExchangeFormat.Brep:
                    await engine.ExportBrepAsync(
                        shape,
                        fullPath,
                        cancellationToken);
                    return;

                case CadExchangeFormat.Stl:
                    await engine.ExportStlAsync(
                        shape,
                        fullPath,
                        cancellationToken: cancellationToken);
                    return;

                case CadExchangeFormat.Obj:
                case CadExchangeFormat.Gltf:
                    await ExportMeshFormatAsync(
                        engine,
                        shape,
                        fullPath,
                        format,
                        cancellationToken);
                    return;

                default:
                    throw new InvalidOperationException();
            }
        }
        finally
        {
            if (engine.ContainsObject(shape.Id))
                engine.Delete(shape);
        }
    }

    private static async Task ExportMeshFormatAsync(
        OcctEngine engine,
        OcctShape shape,
        string path,
        CadExchangeFormat format,
        CancellationToken cancellationToken)
    {
        var temporaryBrep =
            Path.Combine(
                Path.GetTempPath(),
                $"occad-{Guid.NewGuid():N}.brep");

        try
        {
            await engine.ExportBrepAsync(
                shape,
                temporaryBrep,
                cancellationToken);

            await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var model =
                        new OcctModelingSession();
                    var imported =
                        model.ImportBrep(temporaryBrep);

                    if (format == CadExchangeFormat.Obj)
                    {
                        model.ExportObj(
                            imported,
                            path);
                    }
                    else
                    {
                        model.ExportGltf(
                            imported,
                            path,
                            new OcctGltfExportOptions
                            {
                                WriteBinary =
                                    Path.GetExtension(path)
                                        .Equals(
                                            ".glb",
                                            StringComparison.OrdinalIgnoreCase)
                            });
                    }
                },
                cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryBrep))
                    File.Delete(temporaryBrep);
            }
            catch
            {
                // Temporary-file cleanup must not mask exchange errors.
            }
        }
    }
}
