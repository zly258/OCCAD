using OcctNet;

namespace OCCAD;

/// <summary>
/// Sets one of the four upper corner isometric views using the public OCCT
/// camera API. The current camera center and distance are preserved and the
/// resulting view is fitted to the model.
/// </summary>
public sealed class CadCornerViewAction : CadAction
{
    private readonly string _id;
    private readonly string _displayName;
    private readonly double _xSign;
    private readonly double _ySign;

    public CadCornerViewAction(
        CadWorkspace workspace,
        string id,
        string displayName,
        double xSign,
        double ySign) : base(workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (xSign is not (-1.0 or 1.0))
            throw new ArgumentOutOfRangeException(nameof(xSign));
        if (ySign is not (-1.0 or 1.0))
            throw new ArgumentOutOfRangeException(nameof(ySign));

        _id = id.Trim();
        _displayName = displayName.Trim();
        _xSign = xSign;
        _ySign = ySign;
    }

    public override string Id => _id;
    public override string DisplayName => _displayName;
    public override string Description => $"Set viewport to {DisplayName}";

    public override bool CanExecute() =>
        Workspace.Engine is { IsInitialized: true };

    public override void Execute()
    {
        var engine = Workspace.Engine ??
            throw new InvalidOperationException("No OCCT engine is attached.");

        var camera = engine.GetCamera();
        var offset = camera.Eye - camera.Center;
        var distance = offset.Length;
        if (!double.IsFinite(distance) || distance <= 1e-9)
            distance = 1000.0;

        var eyeDirection = new OcctVector3d(_xSign, _ySign, 1.0).Normalized();
        var eye = camera.Center + eyeDirection * distance;
        var viewDirection = (-eyeDirection).Normalized();

        engine.SetCamera(new OcctCameraState
        {
            Eye = eye,
            Center = camera.Center,
            Up = OcctVector3d.UnitZ,
            Direction = viewDirection,
            Scale = camera.Scale
        });
        engine.FitAll();
    }
}
