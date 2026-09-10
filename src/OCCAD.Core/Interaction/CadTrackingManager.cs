using System.Drawing;
using OcctNet;

namespace OCCAD;

public sealed class CadTrackingManager
{
    private OcctEngine? _engine;
    private OcctOverlay? _guide;

    public CadTrackingResult? Current { get; private set; }

    public event EventHandler? Changed;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();
        _engine = engine;
    }

    public void Update(CadWorkPlane workPlane, OcctPoint3d point, CadTrackingResult? tracking)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!point.IsFinite) throw new ArgumentOutOfRangeException(nameof(point));

        if (!workPlane.IsActive)
        {
            Clear();
            return;
        }

        var currentChanged = !Nullable.Equals(Current, tracking);
        Current = tracking;

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            ShowGuide(tracking);
        }

        if (currentChanged) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        var hadState = Current is not null || _guide is not null;
        Current = null;

        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            Delete(engine, ref _guide);
        }
        else
        {
            _guide = null;
        }

        if (hadState) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowGuide(CadTrackingResult? tracking)
    {
        if (_engine is not { IsInitialized: true } engine) return;

        // The active work plane is a geometric constraint, not a permanent
        // viewport decoration. Its local X/Y axes are intentionally not drawn.
        // A guide is shown only when ORTHO/POLAR actually resolved a tracking
        // direction, so ordinary drawing previews (especially polyline segments)
        // are not duplicated by an overlapping origin-to-pointer line.
        if (tracking is not { } value)
        {
            Delete(engine, ref _guide);
            return;
        }

        var vector = CadTransformMath.Between(value.Reference, value.Point);
        if (!vector.TryNormalize(out var direction))
        {
            Delete(engine, ref _guide);
            return;
        }

        var length = Math.Max(value.Reference.DistanceTo(value.Point) * 6.0, 100.0);
        var start = CadTransformMath.Add(value.Reference, direction, -length);
        var end = CadTransformMath.Add(value.Reference, direction, length);

        UpdateLine(
            engine,
            ref _guide,
            start,
            end,
            new OcctOverlayLineStyle
            {
                Color = Color.FromArgb(160, 90, 90),
                Width = 1.0,
                Pattern = OcctOverlayLinePattern.Dashed
            });
    }

    private static void UpdateLine(
        OcctEngine engine,
        ref OcctOverlay? overlay,
        OcctPoint3d start,
        OcctPoint3d end,
        OcctOverlayLineStyle style)
    {
        if (overlay is { } existing && engine.ContainsObject(existing.Id))
        {
            engine.UpdateOverlayLine(existing, start, end);
            engine.SetOverlayLineStyle(existing, style);
            return;
        }

        overlay = engine.AddOverlayLine(start, end, style);
        engine.SetObjectSelectable(overlay.Value, false);
    }

    private static void Delete(OcctEngine engine, ref OcctOverlay? overlay)
    {
        if (overlay is { } value && engine.ContainsObject(value.Id))
            engine.Delete(value);
        overlay = null;
    }
}
