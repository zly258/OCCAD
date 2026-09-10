using System.Drawing;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns the native ORTHO/POLAR tracking guide. Tracking resolution remains in
/// CadTrackingManager; this class owns only viewer presentation lifecycle.
/// </summary>
internal sealed class CadTrackingPresenter
{
    private OcctEngine? _engine;
    private OcctOverlay? _guide;

    public bool HasTransient => _guide is not null;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();
        _engine = engine;
    }

    public void Show(CadTrackingResult? tracking)
    {
        if (_engine is not { IsInitialized: true } engine)
            return;

        using var batch = engine.BeginDisplayBatch();

        // The active work plane is a geometric constraint, not a permanent
        // viewport decoration. Draw only the direction actually resolved by
        // ORTHO/POLAR so ordinary drawing preview is never duplicated.
        if (tracking is not { } value)
        {
            Delete(engine, ref _guide);
            return;
        }

        var vector = CadTransformMath.Between(
            value.Reference,
            value.Point);
        if (!vector.TryNormalize(out var direction))
        {
            Delete(engine, ref _guide);
            return;
        }

        var length = Math.Max(
            value.Reference.DistanceTo(value.Point) * 6.0,
            100.0);
        var start = CadTransformMath.Add(
            value.Reference,
            direction,
            -length);
        var end = CadTransformMath.Add(
            value.Reference,
            direction,
            length);

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

    public void Clear()
    {
        if (_engine is { IsInitialized: true } engine)
        {
            using var batch = engine.BeginDisplayBatch();
            Delete(engine, ref _guide);
        }
        else
        {
            _guide = null;
        }
    }

    private static void UpdateLine(
        OcctEngine engine,
        ref OcctOverlay? overlay,
        OcctPoint3d start,
        OcctPoint3d end,
        OcctOverlayLineStyle style)
    {
        if (overlay is { } existing &&
            engine.ContainsObject(existing.Id))
        {
            engine.UpdateOverlayLine(
                existing,
                start,
                end);
            engine.SetOverlayLineStyle(
                existing,
                style);
            return;
        }

        overlay = engine.AddOverlayLine(
            start,
            end,
            style);
        engine.SetObjectSelectable(
            overlay.Value,
            false);
    }

    private static void Delete(
        OcctEngine engine,
        ref OcctOverlay? overlay)
    {
        if (overlay is { } value &&
            engine.ContainsObject(value.Id))
            engine.Delete(value);
        overlay = null;
    }
}
