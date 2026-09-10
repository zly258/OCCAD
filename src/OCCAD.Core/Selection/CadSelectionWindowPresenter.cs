using System.Drawing;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Owns the native Window/Crossing selection rectangle. Gesture coordinates
/// come from a frontend, but the transient viewer object and its styling are
/// Core presentation state and participate in the workspace transient lifecycle.
/// </summary>
internal sealed class CadSelectionWindowPresenter
{
    private OcctEngine? _engine;
    private bool _visible;

    public bool IsVisible => _visible;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (ReferenceEquals(_engine, engine))
            return;

        ClearForEngineSwitch();
        _engine = engine;
    }

    public void Show(
        int startX,
        int startY,
        int endX,
        int endY,
        bool crossing)
    {
        if (_engine is not { IsInitialized: true } engine)
            return;

        engine.ShowSelectionRectangle(
            Math.Min(startX, endX),
            Math.Min(startY, endY),
            Math.Max(startX, endX),
            Math.Max(startY, endY),
            crossing ? Color.ForestGreen : Color.CornflowerBlue,
            crossing
                ? Color.FromArgb(48, 70, 150, 70)
                : Color.FromArgb(42, 80, 120, 210),
            0.74,
            1.0);
        _visible = true;
    }

    public void Clear()
    {
        if (!_visible)
            return;

        if (_engine is not { IsInitialized: true } engine)
        {
            _visible = false;
            return;
        }

        try
        {
            engine.HideSelectionRectangle();
            _visible = false;
        }
        catch (InvalidOperationException)
        {
            // Keep the visible flag so the transient scene can retry cleanup.
        }
    }

    private void ClearForEngineSwitch()
    {
        if (!_visible)
            return;

        if (_engine is { IsInitialized: true } engine)
        {
            try
            {
                engine.HideSelectionRectangle();
            }
            catch (InvalidOperationException)
            {
                // Viewer replacement is a hard presentation boundary. The old
                // rectangle cannot be referenced by the new engine.
            }
        }

        _visible = false;
    }
}
