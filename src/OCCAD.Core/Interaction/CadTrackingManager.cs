using OcctNet;

namespace OCCAD;

public sealed class CadTrackingManager
{
    private readonly CadTrackingPresenter _presenter = new();

    public bool HasTransient =>
        Current is not null ||
        _presenter.HasTransient;

    public CadTrackingResult? Current { get; private set; }

    public event EventHandler? Changed;

    public void AttachEngine(OcctEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        Clear();
        _presenter.AttachEngine(engine);
    }

    public void Update(
        CadWorkPlane workPlane,
        OcctPoint3d point,
        CadTrackingResult? tracking)
    {
        ArgumentNullException.ThrowIfNull(workPlane);
        if (!point.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(point));

        if (!workPlane.IsActive)
        {
            Clear();
            return;
        }

        var currentChanged =
            !Nullable.Equals(Current, tracking);
        Current = tracking;
        _presenter.Show(tracking);

        if (currentChanged)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        var hadState = HasTransient;
        Current = null;
        _presenter.Clear();

        if (hadState)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
