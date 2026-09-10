using System.Globalization;
using OcctNet;

namespace OCCAD;

public class CircularDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private readonly CadCircularDimensionKind _kind;
    private CadEntity? _source;
    private OcctPoint3d _center;
    private OcctVector3d _normal;
    private double _radius;
    private double _textHeight=4, _arrowSize=2.5;

    public CircularDimensionTool(CadCircularDimensionKind kind) => _kind=kind;
    public override string Id => _kind==CadCircularDimensionKind.Radius ? "radiusdimension" : "diameterdimension";
    public override string DisplayName => _kind==CadCircularDimensionKind.Radius ? "Radius Dimension" : "Diameter Dimension";
    public override CadToolInputKind InputKind =>
        _source is null
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;
    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with
        {
            PreselectionEnabled = _source is null
        };
    protected override bool CanStepBackCore => _source is not null;
    public override CadToolPanelDescriptor ParameterPanel => new(DisplayName,
        [new CadDoubleToolParameterDescriptor("TextHeight","Text Height",_textHeight,1e-9,1_000_000),
         new CadDoubleToolParameterDescriptor("ArrowSize","Arrow Size",_arrowSize,1e-9,1_000_000)]);

    protected override void OnActivated()
    {
        _source=null;
        var selected=Context.Selection.Primary;
        if(selected is not null) TryAcceptSource(selected);
        UpdatePrompt();
    }

    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if(CancelOnRightClick(input)) return true;
        if(_source is null && input.Kind==OcctPointerInputKind.Pressed && input.Button==OcctPointerButton.Left)
        {
            foreach(var hit in Context.Engine.DetectAt(input.X,input.Y))
            {
                var entity=Context.Document.FindByViewerObject(hit.Owner);
                if(entity is not null && TryAcceptSource(entity)) return true;
            }
            return true;
        }
        if(_source is not null && input.Kind==OcctPointerInputKind.Moved) { Show(Resolve(input,_center)); return true; }
        if(_source is not null && input.Kind==OcctPointerInputKind.Pressed && input.Button==OcctPointerButton.Left) return TryAcceptPoint(Resolve(input,_center));
        return false;
    }

    protected override bool OnCommitCurrentStage(CadPointerPosition p)
    {
        if(_source is null)
        {
            foreach(var hit in Context.Engine.DetectAt(p.X,p.Y))
            {
                var entity=Context.Document.FindByViewerObject(hit.Owner);
                if(entity is not null && TryAcceptSource(entity)) return true;
            }
            return false;
        }
        return CommitResolvedPoint(p,_center,TryAcceptPoint);
    }

    public bool TryAcceptSource(CadEntity entity)
    {
        if(!IsActive || _source is not null || !Context.Document.IsEntitySelectable(entity)) return false;
        var world=entity.CreateWorldGeometrySnapshot();
        if(world is CadCircleEntity circle) { _center=circle.Center; _normal=circle.Normal; _radius=circle.Radius; }
        else if(_kind==CadCircularDimensionKind.Radius && world is CadArcEntity arc) { _center=arc.Center; _normal=arc.Normal; _radius=arc.Radius; }
        else return false;
        _source=entity; Context.WorkPlane.SetToolPlaneFixed(true); UpdatePrompt(); return true;
    }

    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if(!IsActive || _source is null || !point.IsFinite) return false;
        var entity=Create(point); if(entity is null) return false;
        CommitPreview(entity); return true;
    }

    protected override bool OnSetParameter(string id,string value)
    {
        if((!double.TryParse(value,NumberStyles.Float,CultureInfo.CurrentCulture,out var n) && !double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)) || !double.IsFinite(n) || n<=1e-9) return false;
        if(id.Equals("TextHeight",StringComparison.OrdinalIgnoreCase)) _textHeight=n;
        else if(id.Equals("ArrowSize",StringComparison.OrdinalIgnoreCase)) _arrowSize=n;
        else return false;
        NotifyUpdated(); return true;
    }

    protected override bool OnStepBack() { if(_source is null)return false; _source=null; Context.Preview.Clear(); Context.WorkPlane.SetToolPlaneFixed(false); UpdatePrompt(); return true; }
    private CadCircularDimensionEntity? Create(OcctPoint3d point)
    {
        var planar=point-_center; planar-=_normal*planar.Dot(_normal);
        if(!planar.TryNormalize(out var direction)) return null;
        var offset=_kind==CadCircularDimensionKind.Radius ? planar.Length-_radius : 0;
        return new(_kind,_center,_normal,direction,_radius,offset,_textHeight,_arrowSize);
    }
    private void Show(OcctPoint3d point)
    {
        var e=Create(point);
        if(e is not null)
            ShowPreview(e);
        else
            Context.Preview.Clear();
    }
    private void UpdatePrompt() => SetStageLocalized(_source is null?0:1,
        _source is null ? $"Cad.Prompt.{Id}.Select" : $"Cad.Prompt.{Id}.Position",
        _source is null ? $"{DisplayName}: select circle{(_kind==CadCircularDimensionKind.Radius ? " or arc" : "")} [Esc cancel]" : $"{DisplayName}: specify label position [Backspace undo, Esc cancel]");
}

public sealed class RadiusDimensionTool : CircularDimensionTool { public RadiusDimensionTool() : base(CadCircularDimensionKind.Radius) { } }
public sealed class DiameterDimensionTool : CircularDimensionTool { public DiameterDimensionTool() : base(CadCircularDimensionKind.Diameter) { } }

