using OcctNet;
namespace OCCAD;

public sealed class EllipsoidTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _center; private OcctVector3d _xAxis, _yAxis, _zAxis; private double _rx, _ry; private CadEllipsoidEntity? _preview; private WorkPlaneFrame _initial;
    public override string Id => "ellipsoid"; public override string DisplayName => "Ellipsoid"; protected override bool CanStepBackCore => Stage > 0;
    protected override void OnActivated() { _preview = null; _initial = CaptureWorkPlaneFrame(); SetStageLocalized(0, "Cad.Prompt.Ellipsoid.Center", "Ellipsoid: specify center [Esc cancel]"); }
    public override bool HandlePointer(OcctPointerInputEventArgs e) { if (CancelOnRightClick(e)) return true; if (e.Kind == OcctPointerInputKind.Moved && Stage > 0) { Update(Context.ResolvePoint(e.X, e.Y, Reference()).Point); return true; } if (e.Kind != OcctPointerInputKind.Pressed || e.Button != OcctPointerButton.Left) return false; return Accept(Context.ResolvePoint(e.X, e.Y, Reference()).Point); }
    protected override bool OnCommitCurrentStage(CadPointerPosition p) => CommitResolvedPoint(p, Reference(), Accept);
    public bool TryAcceptPoint(OcctPoint3d p) => IsActive && State == CadToolState.Drawing && Accept(p);
    protected override bool OnStepBack() { Context.Preview.Clear(); _preview = null; if (Stage == 3) { RestoreWorkPlaneFrame(_initial, _center); SetStageLocalized(2, "Cad.Prompt.Ellipsoid.YRadius", "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } if (Stage == 2) { SetStageLocalized(1, "Cad.Prompt.Ellipsoid.XRadius", "Ellipsoid: specify X semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } if (Stage == 1) { RestoreWorkPlaneFrame(_initial); SetStageLocalized(0, "Cad.Prompt.Ellipsoid.Center", "Ellipsoid: specify center [Esc cancel]"); return true; } return false; }
    protected override void OnCanceled() { _preview = null; }
    private OcctPoint3d? Reference() => Stage > 0 ? _center : null;
    private bool Accept(OcctPoint3d p) { if (!p.IsFinite) return false; if (Stage == 0) { _center = p; Context.WorkPlane.SetOrigin(p); SetStageLocalized(1, "Cad.Prompt.Ellipsoid.XRadius", "Ellipsoid: specify X semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } if (Stage == 1) { var d = Planar(p - _center); if (!d.TryNormalize(out _xAxis)) return false; _rx = d.Length; _zAxis = _initial.XAxis.Cross(_initial.YAxis).Normalized(); _yAxis = _zAxis.Cross(_xAxis).Normalized(); Show(_rx, _rx, _rx); SetStageLocalized(2, "Cad.Prompt.Ellipsoid.YRadius", "Ellipsoid: specify Y semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } if (Stage == 2) { _ry = Math.Abs((p - _center).Dot(_yAxis)); if (_ry <= 1e-9) return false; Show(_rx, _ry, _ry); Context.WorkPlane.SetToolPlaneFixed(false); Context.WorkPlane.SetToolPlane(_center, _zAxis, _xAxis); SetStageLocalized(3, "Cad.Prompt.Ellipsoid.ZRadius", "Ellipsoid: specify Z semi-axis [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } var rz = Math.Abs((p - _center).Dot(_zAxis)); if (rz <= 1e-9) return false; Show(_rx, _ry, rz); CommitPreview(_preview); return true; }
    private void Update(OcctPoint3d p)
    {
        var valid = false;
        if (Stage == 1)
        {
            var d = Planar(p - _center);
            var r = d.Length;
            if (r > 1e-9)
            {
                ShowPreview(new CadEllipsoidEntity(_center, r, r, r));
                valid = true;
            }
        }
        else if (Stage == 2)
        {
            var r = Math.Abs((p - _center).Dot(_yAxis));
            if (r > 1e-9)
            {
                Show(_rx, r, r);
                valid = true;
            }
        }
        else if (Stage == 3)
        {
            var r = Math.Abs((p - _center).Dot(_zAxis));
            if (r > 1e-9)
            {
                Show(_rx, _ry, r);
                valid = true;
            }
        }

        if (!valid)
        {
            _preview = null;
            Context.Preview.Clear();
        }
    }
    private OcctVector3d Planar(OcctVector3d d) { var n = _initial.XAxis.Cross(_initial.YAxis).Normalized(); return d - n * d.Dot(n); }
    private void Show(double x, double y, double z) { _preview = new CadEllipsoidEntity(_center, _xAxis, _yAxis, _zAxis, x, y, z); ShowPreview(_preview); }
}
