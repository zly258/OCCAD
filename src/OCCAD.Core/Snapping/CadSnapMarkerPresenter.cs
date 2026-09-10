using System.Drawing;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns the native object-snap glyph. CadSnapManager owns snap semantics and
/// candidate state; this presenter owns viewer objects and marker styling only.
/// </summary>
internal sealed class CadSnapMarkerPresenter
{
    private const int MinimumMarkerSize = 9;
    private const int MaximumMarkerSize = 31;
    private const int DisplayPriority = 10;

    private const CadSnapType MarkerModes =
        CadSnapType.Endpoint |
        CadSnapType.Midpoint |
        CadSnapType.Center |
        CadSnapType.Vertex |
        CadSnapType.Quadrant |
        CadSnapType.Nearest |
        CadSnapType.Intersection |
        CadSnapType.Perpendicular |
        CadSnapType.Tangent |
        CadSnapType.ApparentIntersection |
        CadSnapType.Extension |
        CadSnapType.Insertion |
        CadSnapType.Node;

    private OcctEngine? _engine;
    private OcctPoint? _marker;
    private CadSnapType? _markerType;
    private int _markerSize = 15;
    private Color _markerColor = Color.FromArgb(245, 220, 45, 45);
    private IReadOnlyDictionary<CadSnapType, byte[]> _markerPixels =
        CreateMarkerSet(15, Color.FromArgb(245, 220, 45, 45));

    public bool HasMarker => _marker is not null;

    public int MarkerSize
    {
        get => _markerSize;
        set
        {
            if (value < MinimumMarkerSize || value > MaximumMarkerSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Snap marker size must be between {MinimumMarkerSize} and {MaximumMarkerSize} pixels.");
            }

            if (_markerSize == value)
                return;

            _markerSize = value;
            _markerPixels = CreateMarkerSet(value, _markerColor);
            RefreshMarkerStyle();
        }
    }

    public Color MarkerColor
    {
        get => _markerColor;
        set
        {
            if (_markerColor == value)
                return;

            _markerColor = value;
            _markerPixels = CreateMarkerSet(_markerSize, value);
            RefreshMarkerStyle();
        }
    }

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine))
            return;

        if (_engine is { IsInitialized: true })
        {
            try
            {
                Clear();
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                // Object ids are engine-local. A viewer switch is a hard
                // presentation boundary, so stale handles cannot be carried to
                // the new engine even when the previous viewer is already dying.
                _marker = null;
                _markerType = null;
            }
        }
        else
        {
            _marker = null;
            _markerType = null;
        }

        _engine = engine;
    }

    public void Show(OcctPoint3d position, CadSnapType type)
    {
        if (!position.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (_engine is not { IsInitialized: true } engine)
            return;

        var pixels = PixelsFor(type);
        using var batch = engine.BeginDisplayBatch();

        if (_marker is { } existing &&
            engine.ContainsObject(existing.Id))
        {
            engine.UpdatePoints([
                new OcctPointStateUpdate(existing, position, true)
            ]);

            if (_markerType != type)
            {
                engine.SetPointPixmapStyle(
                    existing,
                    MarkerSize,
                    MarkerSize,
                    pixels);
                _markerType = type;
            }

            engine.SetObjectVisible(existing, true);
            return;
        }

        var marker = engine.AddPointPixmap(
            position,
            MarkerSize,
            MarkerSize,
            pixels);
        engine.SetObjectSelectable(marker, false);
        engine.SetDisplayPriority(marker, DisplayPriority);

        _marker = marker;
        _markerType = type;
    }

    public void Hide()
    {
        if (_marker is not { } marker ||
            _engine is not { IsInitialized: true } engine ||
            !engine.ContainsObject(marker.Id))
            return;

        engine.SetObjectVisible(marker, false);
    }

    public void Clear()
    {
        if (_marker is not { } marker)
            return;

        if (_engine is not { IsInitialized: true } engine)
        {
            _marker = null;
            _markerType = null;
            return;
        }

        if (engine.ContainsObject(marker.Id))
        {
            using var batch = engine.BeginDisplayBatch();
            engine.Delete(marker);
        }

        _marker = null;
        _markerType = null;
    }

    private void RefreshMarkerStyle()
    {
        if (_marker is not { } marker ||
            _markerType is not { } type ||
            _engine is not { IsInitialized: true } engine ||
            !engine.ContainsObject(marker.Id))
            return;

        engine.SetPointPixmapStyle(
            marker,
            MarkerSize,
            MarkerSize,
            PixelsFor(type));
    }

    private byte[] PixelsFor(CadSnapType type) =>
        _markerPixels.TryGetValue(type, out var value)
            ? value
            : CreateMarkerPixels(
                CadSnapType.Endpoint,
                MarkerSize,
                _markerColor);

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static IReadOnlyDictionary<CadSnapType, byte[]> CreateMarkerSet(
        int size,
        Color color) =>
        Enum.GetValues<CadSnapType>()
            .Where(static type =>
                type != CadSnapType.None &&
                type != CadSnapType.Default &&
                (type & MarkerModes) == type)
            .ToDictionary(
                static type => type,
                type => CreateMarkerPixels(type, size, color));

    private static byte[] CreateMarkerPixels(
        CadSnapType type,
        int size,
        Color color)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2;
        var radius = Math.Max(3, center - 2);
        var stroke = Math.Max(1, size / 7);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var adx = Math.Abs(dx);
                var ady = Math.Abs(dy);

                var draw = type switch
                {
                    CadSnapType.Endpoint =>
                        Math.Max(adx, ady) <= radius,
                    CadSnapType.Midpoint =>
                        IsTriangleFilled(dx, dy, radius),
                    CadSnapType.Center =>
                        dx * dx + dy * dy <= radius * radius,
                    CadSnapType.Vertex or CadSnapType.Quadrant =>
                        adx + ady <= radius,
                    CadSnapType.Intersection =>
                        Math.Abs(adx - ady) <= stroke &&
                        adx <= radius &&
                        ady <= radius,
                    CadSnapType.ApparentIntersection =>
                        (Math.Abs(adx - ady) <= stroke &&
                         adx <= radius && ady <= radius) ||
                        (Math.Max(adx, ady) <= radius &&
                         Math.Max(adx, ady) >= radius - stroke),
                    CadSnapType.Extension =>
                        ady <= stroke &&
                        (adx <= stroke ||
                         Math.Abs(adx - (radius - stroke)) <= stroke),
                    CadSnapType.Insertion =>
                        (Math.Max(Math.Abs(dx + 2), Math.Abs(dy - 2)) <= Math.Max(2, radius - 2) &&
                         Math.Max(Math.Abs(dx + 2), Math.Abs(dy - 2)) >= Math.Max(1, radius - 2 - stroke)) ||
                        (Math.Max(Math.Abs(dx - 2), Math.Abs(dy + 2)) <= Math.Max(2, radius - 2) &&
                         Math.Max(Math.Abs(dx - 2), Math.Abs(dy + 2)) >= Math.Max(1, radius - 2 - stroke)),
                    CadSnapType.Perpendicular =>
                        (dx >= -radius && dx <= -radius + stroke * 2 && dy <= radius) ||
                        (dy >= radius - stroke * 2 && dy <= radius && dx >= -radius) ||
                        (dx >= -radius && dx <= -radius + radius / 2 &&
                         Math.Abs(dy - (radius - radius / 2)) <= stroke) ||
                        (dy <= radius && dy >= radius - radius / 2 &&
                         Math.Abs(dx - (-radius + radius / 2)) <= stroke),
                    CadSnapType.Tangent =>
                        dx * dx + (dy + 1) * (dy + 1) <=
                            Math.Max(2, radius - 1) *
                            Math.Max(2, radius - 1) ||
                        (Math.Abs(dy + radius) <= stroke && adx <= radius),
                    CadSnapType.Node =>
                        dx * dx + dy * dy <= radius * radius &&
                        (dx * dx + dy * dy >=
                             (radius - stroke) * (radius - stroke) ||
                         Math.Abs(adx - ady) <= stroke),
                    CadSnapType.Nearest =>
                        adx <= ady && ady <= radius,
                    _ =>
                        Math.Max(adx, ady) <= radius
                };

                if (!draw)
                    continue;

                var offset = (y * size + x) * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }

        return pixels;
    }

    private static bool IsTriangleFilled(
        int dx,
        int dy,
        int radius)
    {
        var top = -radius;
        var bottom = radius;
        if (dy < top || dy > bottom)
            return false;

        var progress =
            (double)(dy - top) /
            Math.Max(1, bottom - top);
        var halfWidth =
            (int)Math.Round(progress * radius);
        return Math.Abs(dx) <= halfWidth;
    }
}
