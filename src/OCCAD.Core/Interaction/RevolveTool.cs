using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class RevolveTool :
    CadSelectionTransformToolBase,
    ICadPointInputTool
{
    private const double MinimumAngle = 1e-6;
    private double _angleDegrees = 360.0;
    private OcctPoint3d? _axisStart;
    private OcctPoint3d? _axisEnd;
    private OcctVector3d _axisDirection = OcctVector3d.UnitZ;
    private OcctVector3d _angleXAxis = OcctVector3d.UnitX;
    private OcctVector3d _angleYAxis = OcctVector3d.UnitY;
    private CadPlaneFrame _initialPlane;

    public override string Id => "revolve";
    public override string DisplayName => "Revolve";
    public override CadToolInputKind InputKind =>
        State == CadToolState.WaitForSelect
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;
    public override CadToolInteractionPolicy InteractionPolicy =>
        State == CadToolState.WaitForSelect
            ? CadToolInteractionPolicy.Selection
            : CadToolInteractionPolicy.Drawing;
    public override string PrecisionAngleLabel => "Angle";
    public override OcctPoint3d? PrecisionReferencePoint =>
        _axisStart ?? base.PrecisionReferencePoint;

    protected override bool AutoCommitValidSelection => true;

    public override CadToolPanelDescriptor ParameterPanel =>
        new(
            "Revolve",
            [
                new CadDoubleToolParameterDescriptor(
                    "Angle",
                    "Angle",
                    _angleDegrees,
                    -360.0,
                    360.0)
            ]);

    protected override bool IsSelectionValid(CadEntity[] entities) =>
        entities.Length == 1 &&
        CadPlanarProfileGeometry.IsSupported(entities[0]);

    protected override void OnActivated()
    {
        _initialPlane = Context.WorkPlane.EffectivePlane;
        SetSelectionFilter(
            new CadSelectionFilter(
                "revolve.profiles",
                CadPlanarProfileGeometry.IsSupported));

        if (!IsSelectionValid(
                Context.Selection.Selected.ToArray()))
            Context.Selection.Clear();

        base.OnActivated();
    }

    protected override void OnTransformStarted()
    {
        _axisStart = null;
        _axisEnd = null;
        SetSelectionFilter(null);
        SetStageLocalized(
            1,
            "Cad.Prompt.revolve.AxisStart",
            "Revolve: specify axis start point [Backspace profile, Esc cancel]");
    }

    public override bool HandlePointer(
        OcctPointerInputEventArgs input)
    {
        if (CancelOnRightClick(input))
            return true;

        if (State == CadToolState.WaitForSelect)
            return false;

        if (input.Kind == OcctPointerInputKind.Moved)
        {
            var reference =
                Stage >= 2
                    ? _axisStart
                    : null;
            var point =
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    reference).Point;

            if (Stage == 2 &&
                _axisStart is { } start)
            {
                if (start.DistanceTo(point) > 1e-9)
                    Context.Preview.Show(
                        new CadLineEntity(start, point));
                else
                    Context.Preview.Clear();
            }
            else if (Stage == 3)
            {
                if (TrySetAngleFromPoint(point))
                    RefreshPreview();
                else
                    Context.Preview.Clear();
            }

            return true;
        }

        if (input.Kind == OcctPointerInputKind.Pressed &&
            input.Button == OcctPointerButton.Left)
        {
            var reference =
                Stage >= 2
                    ? _axisStart
                    : null;
            var point =
                Context.ResolvePoint(
                    input.X,
                    input.Y,
                    reference).Point;
            return TryAcceptPoint(point);
        }

        return false;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if (!IsActive ||
            State != CadToolState.Drawing ||
            !point.IsFinite)
            return false;

        if (Stage == 1)
        {
            _axisStart = point;
            Context.WorkPlane.SetOrigin(point);
            SetStageLocalized(
                2,
                "Cad.Prompt.revolve.AxisEnd",
                "Revolve: specify axis end point [Backspace axis start, Esc cancel]");
            return true;
        }

        if (Stage == 2 &&
            _axisStart is { } start)
        {
            var axis =
                CadTransformMath.Between(start, point);
            if (!axis.TryNormalize(out _axisDirection))
                return false;

            _axisEnd = point;
            ConfigureAnglePlane();
            SetStageLocalized(
                3,
                "Cad.Prompt.revolve.Angle",
                "Revolve: move the pointer to set angle, click or press Enter to accept [Backspace axis, Esc cancel]",
                CadPrecisionInputKind.Angle);
            RefreshPreview();
            return true;
        }

        if (Stage == 3)
        {
            if (!TrySetAngleFromPoint(point))
                return false;

            RefreshPreview();
            return CommitRevolve();
        }

        return false;
    }

    protected override bool CanStepBackCore =>
        State == CadToolState.Drawing &&
        Stage >= 1;

    protected override bool OnStepBack()
    {
        Context.Preview.Clear();

        if (Stage == 3)
        {
            _axisEnd = null;
            RestoreInitialPlane(_axisStart);
            SetStageLocalized(
                2,
                "Cad.Prompt.revolve.AxisEnd",
                "Revolve: specify axis end point [Backspace axis start, Esc cancel]");
            return true;
        }

        if (Stage == 2)
        {
            _axisStart = null;
            RestoreInitialPlane(null);
            SetStageLocalized(
                1,
                "Cad.Prompt.revolve.AxisStart",
                "Revolve: specify axis start point [Backspace profile, Esc cancel]");
            return true;
        }

        SetSelectionFilter(
            new CadSelectionFilter(
                "revolve.profiles",
                CadPlanarProfileGeometry.IsSupported));
        RestartSelection();
        return true;
    }

    protected override bool CanFinishCore =>
        State == CadToolState.Drawing &&
        Stage == 3 &&
        _axisStart is not null &&
        _axisEnd is not null &&
        IsValidAngle(_angleDegrees);

    protected override bool OnFinish() =>
        CommitRevolve();

    protected override bool OnPrecisionInputApplied(
        CadPrecisionInput input)
    {
        if (input.AngleDegrees is { } angle)
        {
            if (!IsValidAngle(angle))
                return false;

            _angleDegrees = angle;
        }

        if (Stage == 3)
            RefreshPreview();
        return true;
    }

    protected override bool OnSetParameter(
        string id,
        string value)
    {
        if (!id.Equals(
                "Angle",
                StringComparison.OrdinalIgnoreCase) ||
            !TryAngle(value, out var angle))
            return false;

        _angleDegrees = angle;
        if (Stage == 3)
            RefreshPreview();
        NotifyUpdated();
        return true;
    }

    protected override void ResetTransformState()
    {
        _angleDegrees = 360.0;
        _axisStart = null;
        _axisEnd = null;
        _axisDirection = OcctVector3d.UnitZ;
        _angleXAxis = OcctVector3d.UnitX;
        _angleYAxis = OcctVector3d.UnitY;
    }

    private void ConfigureAnglePlane()
    {
        if (_axisStart is not { } start ||
            Entities.Count != 1)
            return;

        var profile =
            CadPlanarProfileGeometry.Snapshot(Entities[0]);
        var center =
            CadPlanarProfileGeometry.Center(profile);
        var radial =
            CadTransformMath.Between(start, center);
        var axial =
            CadTransformMath.Dot(
                radial,
                _axisDirection);
        var projected =
            new OcctVector3d(
                radial.X - _axisDirection.X * axial,
                radial.Y - _axisDirection.Y * axial,
                radial.Z - _axisDirection.Z * axial);

        if (!projected.TryNormalize(out _angleXAxis))
        {
            var axes =
                CadTransformMath.PerpendicularAxes(
                    _axisDirection);
            _angleXAxis = axes.XAxis;
        }

        _angleYAxis =
            _axisDirection
                .Cross(_angleXAxis)
                .Normalized();

        SetWorkPlane(
            start,
            _angleXAxis,
            _angleYAxis,
            lockPlane: true);
    }

    private void RestoreInitialPlane(
        OcctPoint3d? origin)
    {
        SetWorkPlane(
            origin ?? _initialPlane.Origin,
            _initialPlane.XAxis,
            _initialPlane.YAxis,
            lockPlane: false);
    }

    private bool TrySetAngleFromPoint(
        OcctPoint3d point)
    {
        if (_axisStart is not { } start)
            return false;

        var vector =
            CadTransformMath.Between(start, point);
        var axial =
            CadTransformMath.Dot(
                vector,
                _axisDirection);
        var radial =
            new OcctVector3d(
                vector.X - _axisDirection.X * axial,
                vector.Y - _axisDirection.Y * axial,
                vector.Z - _axisDirection.Z * axial);
        if (!radial.TryNormalize(out var direction))
            return false;

        var angle =
            Math.Atan2(
                CadTransformMath.Dot(
                    direction,
                    _angleYAxis),
                CadTransformMath.Dot(
                    direction,
                    _angleXAxis)) *
            180.0 / Math.PI;

        if (angle < 0.0)
            angle += 360.0;
        if (angle < MinimumAngle)
            angle = 360.0;

        if (!IsValidAngle(angle))
            return false;

        _angleDegrees = angle;
        return true;
    }

    private bool CommitRevolve()
    {
        if (!TryCreate(out var entity))
            return false;

        Context.Preview.Clear();
        Context.Workspace.AddGeneratedEntities(
            [entity],
            "Revolve");
        Context.Workspace.Tools.CompleteCurrent();
        return true;
    }

    private void RefreshPreview()
    {
        if (TryCreate(out var entity))
            Context.Preview.Show(entity);
        else
            Context.Preview.Clear();
    }

    private bool TryCreate(
        out CadRevolveEntity entity)
    {
        entity = null!;
        if (Entities.Count != 1 ||
            _axisStart is not { } start ||
            _axisEnd is null ||
            !IsValidAngle(_angleDegrees))
            return false;

        entity = new CadRevolveEntity(
            Entities[0],
            start,
            _axisDirection,
            _angleDegrees);
        return true;
    }

    private static bool TryAngle(
        string text,
        out double value)
    {
        var parsed =
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out value) ||
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);

        return parsed &&
               IsValidAngle(value);
    }

    private static bool IsValidAngle(double value) =>
        double.IsFinite(value) &&
        Math.Abs(value) >= MinimumAngle &&
        Math.Abs(value) <= 360.0;
}
