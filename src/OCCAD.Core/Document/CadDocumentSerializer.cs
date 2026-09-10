using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public static class CadDocumentSerializer
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(CadWorkspace workspace, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
            throw new ArgumentException("Stream must be writable.", nameof(stream));

        JsonSerializer.Serialize(stream, Capture(workspace), Options);
    }

    public static void Save(CadWorkspace workspace, string path)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Document path has no parent directory.");

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                Save(workspace, stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static void Load(CadWorkspace workspace, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        CadDocumentFile model;
        try
        {
            model = JsonSerializer.Deserialize<CadDocumentFile>(stream, Options) ??
                throw new InvalidDataException("CAD document contains no root object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("CAD document JSON is invalid.", exception);
        }

        Apply(workspace, Prepare(workspace.Entities, model));
    }

    public static void Load(CadWorkspace workspace, string path)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        Load(workspace, stream);
    }

    private static CadDocumentFile Capture(CadWorkspace workspace)
    {
        var layers = workspace.Layers.Layers
            .Select(static layer => new CadLayerFile
            {
                Name = layer.Name,
                ColorArgb = layer.Color.ToArgb(),
                LineWidth = layer.LineWidth,
                LineStyle = layer.LineStyle.ToString(),
                Visible = layer.Visible,
                Locked = layer.Locked
            })
            .ToList();

        var entities = workspace.Document.Entities
            .Select(entity => CaptureEntity(workspace.Entities, entity))
            .ToList();

        return new CadDocumentFile
        {
            Version = CurrentVersion,
            CurrentLayer = workspace.Layers.Current.Name,
            Layers = layers,
            Entities = entities
        };
    }

    private static CadEntityFile CaptureEntity(
        CadEntityRegistry registry,
        CadEntity entity)
    {
        var descriptor = registry.GetRequired(entity);
        if (!descriptor.SupportsPersistence)
            throw new InvalidOperationException(
                $"Entity '{descriptor.Id}' does not support persistence.");

        return new CadEntityFile
        {
            Type = descriptor.Id,
            Id = entity.Id,
            Name = entity.Name,
            Layer = entity.Layer,
            Visible = entity.Visible,
            Selectable = entity.Selectable,
            ColorByLayer = entity.ColorByLayer,
            LineWidthByLayer = entity.LineWidthByLayer,
            LineStyleByLayer = entity.LineStyleByLayer,
            ColorArgb = entity.Color.ToArgb(),
            Transparency = entity.Transparency,
            LineWidth = entity.LineWidth,
            LineStyle = entity.LineStyle.ToString(),
            DisplayMode = entity.DisplayMode.ToString(),
            Material = entity.Material.ToString(),
            Placement = entity.Placement.Transform,
            Geometry = registry.WriteGeometry(entity)
        };
    }

    private static PreparedDocument Prepare(
        CadEntityRegistry registry,
        CadDocumentFile model)
    {
        if (model.Version != CurrentVersion)
            throw new InvalidDataException(
                $"Unsupported CAD document version '{model.Version}'. Expected '{CurrentVersion}'.");

        var layers = PrepareLayers(model, out var layerNames);
        var entityIds = new HashSet<Guid>();
        var entities = new List<CadEntity>(model.Entities?.Count ?? 0);
        foreach (var item in model.Entities ?? [])
            entities.Add(PrepareEntity(registry, item, layerNames, entityIds));

        return new PreparedDocument(
            layers,
            model.CurrentLayer!.Trim(),
            entities);
    }

    private static IReadOnlyList<CadLayerState> PrepareLayers(
        CadDocumentFile model,
        out HashSet<string> layerNames)
    {
        if (model.Layers is null || model.Layers.Count == 0)
            throw new InvalidDataException("CAD document must contain at least layer '0'.");

        layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var layers = new List<CadLayerState>(model.Layers.Count);
        foreach (var item in model.Layers)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name))
                throw new InvalidDataException("CAD document contains an unnamed layer.");

            var name = item.Name.Trim();
            if (!layerNames.Add(name))
                throw new InvalidDataException(
                    $"CAD document contains duplicate layer '{name}'.");
            if (!double.IsFinite(item.LineWidth) || item.LineWidth <= 0.0)
                throw new InvalidDataException(
                    $"Layer '{name}' has an invalid line width.");
            if (!Enum.TryParse<OcctLineStyle>(item.LineStyle, true, out var lineStyle) ||
                !Enum.IsDefined(lineStyle))
                throw new InvalidDataException(
                    $"Layer '{name}' has invalid line style '{item.LineStyle}'.");

            layers.Add(new CadLayerState(
                name,
                Color.FromArgb(item.ColorArgb),
                item.LineWidth,
                lineStyle,
                item.Visible,
                item.Locked));
        }

        if (!layerNames.Contains("0"))
            throw new InvalidDataException("CAD document must contain default layer '0'.");
        if (string.IsNullOrWhiteSpace(model.CurrentLayer) ||
            !layerNames.Contains(model.CurrentLayer))
            throw new InvalidDataException("CAD document current layer is invalid.");

        return layers;
    }

    private static CadEntity PrepareEntity(
        CadEntityRegistry registry,
        CadEntityFile? item,
        HashSet<string> layerNames,
        HashSet<Guid> ids)
    {
        if (item is null)
            throw new InvalidDataException("CAD document contains an empty entity entry.");
        if (item.Id == Guid.Empty || !ids.Add(item.Id))
            throw new InvalidDataException(
                $"CAD document contains invalid or duplicate entity id '{item.Id}'.");
        if (string.IsNullOrWhiteSpace(item.Type))
            throw new InvalidDataException($"Entity '{item.Id}' has no type.");
        if (string.IsNullOrWhiteSpace(item.Name))
            throw new InvalidDataException($"Entity '{item.Id}' has no name.");
        if (string.IsNullOrWhiteSpace(item.Layer) || !layerNames.Contains(item.Layer))
            throw new InvalidDataException(
                $"Entity '{item.Id}' references unknown layer '{item.Layer}'.");
        if (item.Geometry is null)
            throw new InvalidDataException($"Entity '{item.Id}' has no geometry.");

        CadEntityDescriptor descriptor;
        try
        {
            descriptor = registry.GetRequired(item.Type);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException(
                $"Entity '{item.Id}' uses unsupported type '{item.Type}'.",
                exception);
        }

        if (!descriptor.SupportsPersistence)
            throw new InvalidDataException(
                $"Entity type '{item.Type}' does not support persistence.");

        var entity = registry.ReadGeometry(descriptor.Id, item.Geometry);
        entity.RestoreIdentity(item.Id);
        entity.Name = item.Name.Trim();
        entity.Layer = item.Layer.Trim();
        entity.Visible = item.Visible;
        entity.Selectable = item.Selectable;
        entity.ColorByLayer = item.ColorByLayer;
        entity.LineWidthByLayer = item.LineWidthByLayer;
        entity.LineStyleByLayer = item.LineStyleByLayer;
        entity.Color = Color.FromArgb(item.ColorArgb);
        entity.Transparency = item.Transparency;
        entity.LineWidth = item.LineWidth;
        entity.LineStyle = ParseEnum<OcctLineStyle>(
            item.LineStyle,
            "line style",
            item.Id);
        entity.DisplayMode = ParseEnum<OcctDisplayMode>(
            item.DisplayMode,
            "display mode",
            item.Id);
        entity.Material = ParseEnum<OcctMaterial>(
            item.Material,
            "material",
            item.Id);

        try
        {
            entity.RestorePlacement(
                new CadPlacement(
                    item.Placement ??
                    OcctTransform3d.Identity));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Entity '{item.Id}' has invalid placement.",
                exception);
        }

        return entity;
    }

    private static void Apply(
        CadWorkspace workspace,
        PreparedDocument prepared)
    {
        workspace.ResetDocument();

        var defaultState = prepared.Layers.Single(static layer =>
            string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase));
        workspace.Layers.GetRequired("0").RestoreState(defaultState);
        foreach (var state in prepared.Layers)
        {
            if (string.Equals(state.Name, "0", StringComparison.OrdinalIgnoreCase))
                continue;

            var layer = workspace.Layers.Add(state.Name);
            layer.RestoreState(state);
        }

        workspace.Layers.SetCurrent(prepared.CurrentLayer);
        workspace.Document.AddRange(prepared.Entities);
        workspace.History.Clear();
        workspace.Selection.Clear();
        workspace.Subobjects.Clear();
        workspace.Preselection.Clear();
        workspace.Grips.Clear();
        workspace.MarkSaved();
    }

    private static T ParseEnum<T>(
        string? value,
        string field,
        Guid entityId)
        where T : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<T>(value, true, out var result) &&
            Enum.IsDefined(result))
            return result;

        throw new InvalidDataException(
            $"Entity '{entityId}' has invalid {field} '{value}'.");
    }

    private sealed class CadDocumentFile
    {
        public int Version { get; set; }
        public string? CurrentLayer { get; set; } = "0";
        public List<CadLayerFile>? Layers { get; set; } = [];
        public List<CadEntityFile>? Entities { get; set; } = [];
    }

    private sealed class CadLayerFile
    {
        public string Name { get; set; } = string.Empty;
        public int ColorArgb { get; set; }
        public double LineWidth { get; set; }
        public string LineStyle { get; set; } = nameof(OcctLineStyle.Solid);
        public bool Visible { get; set; }
        public bool Locked { get; set; }
    }

    private sealed class CadEntityFile
    {
        public string? Type { get; set; }
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Layer { get; set; }
        public bool Visible { get; set; } = true;
        public bool Selectable { get; set; } = true;
        public bool ColorByLayer { get; set; } = true;
        public bool LineWidthByLayer { get; set; } = true;
        public bool LineStyleByLayer { get; set; } = true;
        public int ColorArgb { get; set; } = Color.White.ToArgb();
        public double Transparency { get; set; }
        public double LineWidth { get; set; } = 1.0;
        public string? LineStyle { get; set; } = nameof(OcctLineStyle.Solid);
        public string? DisplayMode { get; set; } = nameof(OcctDisplayMode.Shaded);
        public string? Material { get; set; } = nameof(OcctMaterial.Plastified);
        public OcctTransform3d? Placement { get; set; }
        public JsonObject? Geometry { get; set; }
    }

    private sealed record PreparedDocument(
        IReadOnlyList<CadLayerState> Layers,
        string CurrentLayer,
        IReadOnlyList<CadEntity> Entities);
}
