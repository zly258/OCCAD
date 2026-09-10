using OcctNet;

namespace OCCAD;

public sealed class EllipsoidTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _center;
    private OcctVector3d _xAxis;
    private OcctVector3d _yAxis;
    private OcctVector3d _zAxis;
    private double _rx;
    private double _ry;
    private CadEllipsoidEntity? _preview;
    private WorkPlaneFrame _initial;

    public override string Id => "ellipsoid";
    public override string DisplayName => "Ellipsoid";
    protected override bool CanStepBackCore => Stage > 0;

    protected override void OnActivated()
    {
        _preview = null;
        _initial = CaptureWorkPlaneFrame();
        SetStageLocalized(0, "Cad.Prompt.Ellipsoid.Center", "Ellipsoid: specify center [Esc cancel]");
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input)) return true;
        if (input.Kind == OcctPointerInputKind.Moved && Stage > 0)
        {
            Update(Context.ResolvePoint(input.X, input.Y, Reference()).Point);
            return true;
        }
        if (input.Kind != OcctPointerInputKind.Pressed || input.Button != OcctPointerButton.Left)
            return false;
        return Accept(Context.ResolvePoint(input.X, input.Y, Reference()).Point);
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition pointer) =>
        CommitResolvedPoint(pointer, Reference(), Accept);

    public bool TryAcceptPoint(OcctPoint3d point) =>
        IsActive && State == CadToolState.Drawing && Accept(point);

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();
        _preview = null;
        if (Stage == 3)
        {
            RestoreWorkPlaneFrame(_initial, _center);
            SetStageLocalized(2, "Cad.Prompt.Ellipsoid.YRadius", "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }
        if (Stage == 2)
        {
            SetStageLocalized(1, "Cad.Prompt.Ellipsoid.XRadius", "Ellipsoid: specify X semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }
        if (Stage == 1)
        {
            RestoreWorkPlaneFrame(_initial);
            SetStageLocalized(0, "Cad.Prompt.Ellipsoid.Center", "Ellipsoid: specify center [Esc cancel]");
            return true;
        }
        return false;
    }

    protected override void OnCanceled() => _preview = null;

    private OcctPoint3d? Reference() => Stage > 0 ? _center : null;

    private bool Accept(OcctPoint3d point)
    {
        if (!point.IsFinite) return false;
        if (Stage == 0)
        {
            _center = point;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(1, "Cad.Prompt.Ellipsoid.XRadius", "Ellipsoid: specify X semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 1)
        {
            var direction = Planar(point - _center);
            if (!direction.TryNormalize(out _xAxis)) return false;
            _rx = direction.Length;
            _zAxis = _initial.XAxis.Cross(_initial.YAxis).Normalized();
            _yAxis = _zAxis.Cross(_xAxis).Normalized();
            Show(_rx, _rx, _rx);
            SetStageLocalized(2, "Cad.Prompt.Ellipsoid.YRadius", "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }

        if (Stage == 2)
        {
            _ry = Math.Abs((point - _center).Dot(_yAxis));
            if (_ry <= 1e-9) return false;
            Show(_rx, _ry, _ry);
            Context.WorkPlane.SetToolPlaneFixed(false);
            Context.WorkPlane.SetToolPlane(_center, _zAxis, _xAxis);
            SetStageLocalized(3, "Cad.Prompt.Ellipsoid.ZRadius", "Ellipsoid: specify Z semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length);
            return true;
        }

        var rz = Math.Abs((point - _center).Dot(_zAxis));
        if (rz <= 1e-9) return false;
        Show(_rx, _ry, rz);
        if (_preview is null) return false;
        CommitPreview(_preview);
        return true;
    }

    private void Update(OcctPoint3d point)
    {
        if (Stage == 1)
        {
            var direction = Planar(point - _center);
            var radius = direction.Length;
            if (radius > 1e-9)
                ShowPreview(new CadEllipsoidEntity(_center, radius, radius, radius));
            else
                Context.Preview.Clear();
            return;
        }
        if (Stage == 2)
        {
            var radius = Math.Abs((point - _center).Dot(_yAxis));
            if (radius > 1e-9) Show(_rx, radius, radius); else Context.Preview.Clear();
            return;
        }
        if (Stage == 3)
        {
            var radius = Math.Abs((point - _center).Dot(_zAxis));
            if (radius > 1e-9) Show(_rx, _ry, radius); else Context.Preview.Clear();
        }
    }

    private OcctVector3d Planar(OcctVector3d direction)
    {
        var normal = _initial.XAxis.Cross(_initial.YAxis).Normalized();
        return direction - normal * direction.Dot(normal);
    }

    private void Show(double x, double y, double z)
    {
        _preview = new CadEllipsoidEntity(_center, _xAxis, _yAxis, _zAxis, x, y, z);
        ShowPreview(_preview);
    }
}
