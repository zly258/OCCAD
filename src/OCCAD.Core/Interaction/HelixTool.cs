using System.Globalization;
using OcctNet;
namespace OCCAD;

public sealed class HelixTool : CadDrawingTool, ICadPointInputTool
{
    private OcctPoint3d _origin; private OcctVector3d _axis, _xAxis; private double _radius, _pitch = 1, _turns = 3; private WorkPlaneFrame _initial;
    public override string Id => "helix"; public override string DisplayName => "Helix"; protected override bool CanStepBackCore => Stage > 0; protected override bool CanFinishCore => Stage == 2;
    public override CadToolPanelDescriptor? ParameterPanel => Stage != 2 ? null : new("Helix", [new CadDoubleToolParameterDescriptor("Pitch", "Pitch", _pitch, -1000000, 1000000), new CadDoubleToolParameterDescriptor("Turns", "Turns", _turns, 0.01, 10000)]);
    protected override void OnActivated() { _initial = CaptureWorkPlaneFrame(); SetStageLocalized(0, "Cad.Prompt.Helix.Center", "Helix: specify axis origin [Esc cancel]"); }
    public override bool HandlePointer(OcctPointerInputEventArgs e) { if (CancelOnRightClick(e)) return true; if (e.Kind == OcctPointerInputKind.Moved && Stage == 1) { Update(Context.ResolvePoint(e.X, e.Y, _origin).Point); return true; } if (e.Kind != OcctPointerInputKind.Pressed || e.Button != OcctPointerButton.Left) return false; if (Stage < 2) return Accept(Context.ResolvePoint(e.X, e.Y, Stage == 1 ? _origin : null).Point); return true; }
    protected override bool OnCommitCurrentStage(CadPointerPosition p) => CommitResolvedPoint(p, Stage == 1 ? _origin : null, Accept); public bool TryAcceptPoint(OcctPoint3d p) => IsActive && State == CadToolState.Drawing && Accept(p);
    protected override bool OnSetParameter(string id, string value) { if (Stage != 2 || (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) || !double.IsFinite(n)) return false; if (id.Equals("Pitch", StringComparison.OrdinalIgnoreCase)) { if (Math.Abs(n) <= 1e-9) return false; _pitch = n; } else if (id.Equals("Turns", StringComparison.OrdinalIgnoreCase)) { if (n <= 0 || n > 10000) return false; _turns = n; } else return false; Show(); return true; }
    protected override bool OnFinish() { if (!CanFinish) return false; Context.AddEntity(new CadHelixEntity(_origin, _axis, _xAxis, _radius, _pitch, _turns)); Context.Workspace.Tools.CompleteCurrent(); return true; }
    protected override bool OnStepBack() { Context.Preview.Clear(); if (Stage == 2) { SetStageLocalized(1, "Cad.Prompt.Helix.Radius", "Helix: specify radius and start direction [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } RestoreWorkPlaneFrame(_initial); SetStageLocalized(0, "Cad.Prompt.Helix.Center", "Helix: specify axis origin [Esc cancel]"); return true; }
    private bool Accept(OcctPoint3d p) { if (!p.IsFinite) return false; if (Stage == 0) { _origin = p; _axis = Context.WorkPlane.Normal; Context.WorkPlane.SetOrigin(p); SetStageLocalized(1, "Cad.Prompt.Helix.Radius", "Helix: specify radius and start direction [Backspace undo, Esc cancel]", CadPrecisionInputKind.Length); return true; } var d = p - _origin; d = d - _axis * d.Dot(_axis); if (!d.TryNormalize(out _xAxis)) return true; _radius = d.Length; _pitch = _radius; SetStageLocalized(2, "Cad.Prompt.Helix.Parameters", "Helix: set pitch and turns [Enter accept, Esc cancel]"); Show(); return true; }
    private void Update(OcctPoint3d p) { var d = p - _origin; d = d - _axis * d.Dot(_axis); if (!d.TryNormalize(out var x)) return; ShowPreview(new CadHelixEntity(_origin, _axis, x, d.Length, d.Length, _turns)); }
    private void Show() => ShowPreview(new CadHelixEntity(_origin, _axis, _xAxis, _radius, _pitch, _turns));
}
