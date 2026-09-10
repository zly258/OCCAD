using System.Drawing;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns native presentation objects for grip markers. The grip manager owns
/// semantic grip state and hit testing; this presenter owns only marker pixels,
/// native objects, and their lifecycle in the attached OCCT engine.
/// </summary>
internal sealed class CadGripMarkerPresenter
{
    private const int MinimumMarkerSize = 7;
    private const int MaximumMarkerSize = 31;
    private const int MarkerDisplayPriority = 10;

    private readonly List<OcctPoint> _markers = [];
    private OcctEngine? _engine;
    private int _markerSize = 15;
    private IReadOnlyDictionary<CadGripKind, byte[]> _normalMarkerPixels =
        CreateMarkerSet(
            15,
            Color.FromArgb(235, 45, 105, 190),
            circular: false);
    private IReadOnlyDictionary<CadGripKind, byte[]> _hotMarkerPixels =
        CreateMarkerSet(
            19,
            Color.FromArgb(255, 235, 175, 35),
            circular: false);
    private OcctPoint? _dragMarker;
    private byte[]? _dragMarkerPixels;

    public int MarkerSize
    {
        get => _markerSize;
        set
        {
            if (value < MinimumMarkerSize ||
                value > MaximumMarkerSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Grip marker size must be between {MinimumMarkerSize} and {MaximumMarkerSize} pixels.");
            }

            if (_markerSize == value)
                return;

            _markerSize = value;
            _dragMarkerPixels = null;
            _normalMarkerPixels =
                CreateMarkerSet(
                    _markerSize,
                    Color.FromArgb(235, 45, 105, 190),
                    circular: false);
            _hotMarkerPixels =
                CreateMarkerSet(
                    HotMarkerSize,
                    Color.FromArgb(255, 235, 175, 35),
                    circular: false);
        }
    }

    public int HotMarkerSize =>
        Math.Min(
            MaximumMarkerSize + 4,
            _markerSize + 4);

    public int MarkerCount => _markers.Count;
    public bool HasDragTransient => _dragMarker is not null;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine))
            return;

        ClearMarkers();
        ClearDragMarker();
        _dragMarker = null;
        _engine = engine;
    }

    public void Rebuild(IReadOnlyList<CadGripPoint> grips)
    {
        ArgumentNullException.ThrowIfNull(grips);
        ClearMarkers();

        if (_engine is not { IsInitialized: true } engine)
            return;

        using (engine.BeginDisplayBatch())
        {
            foreach (var grip in grips)
            {
                var marker = engine.AddPointPixmap(
                    grip.Position,
                    MarkerSize,
                    MarkerSize,
                    _normalMarkerPixels[grip.Kind]);
                engine.SetObjectSelectable(marker, false);
                engine.SetDisplayPriority(marker, MarkerDisplayPriority);
                _markers.Add(marker);
            }
        }
    }

    public bool TryUpdatePositions(
        IReadOnlyList<CadGripPoint> grips,
        bool restyle,
        int hotIndex)
    {
        ArgumentNullException.ThrowIfNull(grips);
        if (_engine is not { IsInitialized: true } engine ||
            grips.Count != _markers.Count ||
            _markers.Any(marker => !engine.ContainsObject(marker.Id)))
            return false;

        var updates = new OcctPointStateUpdate[_markers.Count];
        for (var index = 0; index < _markers.Count; index++)
        {
            updates[index] = new OcctPointStateUpdate(
                _markers[index],
                grips[index].Position,
                true);
        }

        using var batch = engine.BeginDisplayBatch();
        engine.UpdatePoints(updates);

        if (!restyle)
            return true;

        for (var index = 0; index < _markers.Count; index++)
            SetMarkerStyle(
                engine,
                index,
                MarkerSize,
                _normalMarkerPixels,
                grips);

        if (hotIndex >= 0 && hotIndex < _markers.Count)
        {
            SetMarkerStyle(
                engine,
                hotIndex,
                HotMarkerSize,
                _hotMarkerPixels,
                grips);
        }

        return true;
    }

    public void SetHot(
        int previousIndex,
        int currentIndex,
        IReadOnlyList<CadGripPoint> grips)
    {
        ArgumentNullException.ThrowIfNull(grips);
        if (_engine is not { IsInitialized: true } engine)
            return;

        using var batch = engine.BeginDisplayBatch();
        if (IsExistingMarker(engine, previousIndex))
        {
            SetMarkerStyle(
                engine,
                previousIndex,
                MarkerSize,
                _normalMarkerPixels,
                grips);
        }

        if (IsExistingMarker(engine, currentIndex))
        {
            SetMarkerStyle(
                engine,
                currentIndex,
                HotMarkerSize,
                _hotMarkerPixels,
                grips);
        }
    }

    public void ShowDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite ||
            _engine is not { IsInitialized: true } engine)
            return;

        ClearDragMarker();
        if (_dragMarker is not null)
        {
            throw new InvalidOperationException(
                "Previous grip drag marker could not be removed.");
        }

        var size = Math.Min(35, HotMarkerSize + 2);
        _dragMarkerPixels ??= CreateCircularMarkerPixels(
            size,
            Color.FromArgb(255, 245, 178, 35));

        var marker = engine.AddPointPixmap(
            point,
            size,
            size,
            _dragMarkerPixels);
        _dragMarker = marker;
        engine.SetObjectSelectable(marker, false);
        engine.SetDisplayPriority(marker, MarkerDisplayPriority);
    }

    public void UpdateDragMarker(OcctPoint3d point)
    {
        if (!point.IsFinite ||
            _engine is not { IsInitialized: true } engine)
            return;

        if (_dragMarker is not { } marker ||
            !engine.ContainsObject(marker.Id))
        {
            ShowDragMarker(point);
            return;
        }

        engine.UpdatePoints([
            new OcctPointStateUpdate(
                marker,
                point,
                true)
        ]);
    }

    public void ClearDragMarker()
    {
        var marker = _dragMarker;
        if (marker is not { } existingMarker ||
            _engine is not { IsInitialized: true } engine ||
            !engine.ContainsObject(existingMarker.Id))
        {
            _dragMarker = null;
            return;
        }

        try
        {
            engine.Delete(existingMarker);
            _dragMarker = null;
        }
        catch (Exception exception)
            when (IsRecoverablePresentationFailure(exception))
        {
            // Keep the handle so lifecycle cleanup can retry.
        }
    }

    public void ClearMarkers()
    {
        if (_engine is { IsInitialized: true } engine &&
            _markers.Count > 0)
        {
            using var batch = engine.BeginDisplayBatch();
            var existing = _markers
                .Where(marker => engine.ContainsObject(marker.Id))
                .Cast<IOcctObject>()
                .ToArray();
            if (existing.Length > 0)
                engine.Delete(existing);
        }

        _markers.Clear();
    }

    private bool IsExistingMarker(
        OcctEngine engine,
        int index) =>
        index >= 0 &&
        index < _markers.Count &&
        engine.ContainsObject(_markers[index].Id);

    private void SetMarkerStyle(
        OcctEngine engine,
        int index,
        int size,
        IReadOnlyDictionary<CadGripKind, byte[]> styles,
        IReadOnlyList<CadGripPoint> grips)
    {
        if (index < 0 ||
            index >= _markers.Count ||
            index >= grips.Count)
            return;

        engine.SetPointPixmapStyle(
            _markers[index],
            size,
            size,
            styles[grips[index].Kind]);
    }

    private static bool IsRecoverablePresentationFailure(
        Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;

    private static IReadOnlyDictionary<CadGripKind, byte[]> CreateMarkerSet(
        int size,
        Color color,
        bool circular) =>
        Enum.GetValues<CadGripKind>()
            .ToDictionary(
                static kind => kind,
                kind => CreateMarkerPixels(
                    size,
                    color,
                    circular));

    private static byte[] CreateMarkerPixels(
        int size,
        Color color,
        bool circular)
    {
        var pixels = new byte[size * size * 4];
        var center = size / 2;
        var radius = Math.Max(2, center - 1);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var draw = circular
                    ? dx * dx + dy * dy <= radius * radius
                    : Math.Abs(dx) <= radius && Math.Abs(dy) <= radius;
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

    private static byte[] CreateCircularMarkerPixels(
        int size,
        Color color) =>
        CreateMarkerPixels(
            size,
            color,
            circular: true);
}
