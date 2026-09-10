using System.Globalization;
using OcctNet;

namespace OCCAD;

public sealed class AngleDimensionTool : CadDrawingTool, ICadPointInputTool
{
    private readonly List<CadLineEntity> _lines=[];
    private OcctPoint3d _vertex;
    private OcctVector3d _firstDirection,_secondDirection,_normal;
    private double _textHeight=4,_arrowSize=2.5;

    public override string Id=>"angledimension";
    public override string DisplayName=>"Angle Dimension";
    public override CadToolInputKind InputKind =>
        _lines.Count < 2
            ? CadToolInputKind.Selection
            : CadToolInputKind.Point;
    public override CadToolInteractionPolicy InteractionPolicy =>
        base.InteractionPolicy with
        {
            PreselectionEnabled = _lines.Count < 2
        };
    protected override bool CanStepBackCore=>_lines.Count>0;
    public override CadToolPanelDescriptor ParameterPanel=>new("Angle Dimension",
        [new CadDoubleToolParameterDescriptor("TextHeight","Text Height",_textHeight,1e-9,1_000_000),new CadDoubleToolParameterDescriptor("ArrowSize","Arrow Size",_arrowSize,1e-9,1_000_000)]);

    protected override void OnActivated(){_lines.Clear();TryAcceptSelected();UpdatePrompt();}
    public override bool HandlePointer(OcctPointerInputEventArgs input)
    {
        if(CancelOnRightClick(input))return true;
        if(_lines.Count<2 && input.Kind==OcctPointerInputKind.Pressed && input.Button==OcctPointerButton.Left)
        {
            foreach(var hit in Context.Engine.DetectAt(input.X,input.Y)){var e=Context.Document.FindByViewerObject(hit.Owner);if(e is CadLineEntity line && TryAcceptLine(line))return true;}return true;
        }
        if(_lines.Count==2 && input.Kind==OcctPointerInputKind.Moved){Show(Resolve(input,_vertex));return true;}
        if(_lines.Count==2 && input.Kind==OcctPointerInputKind.Pressed && input.Button==OcctPointerButton.Left)return TryAcceptPoint(Resolve(input,_vertex));
        return false;
    }
    protected override bool OnCommitCurrentStage(CadPointerPosition p)
    {
        if(_lines.Count<2){foreach(var hit in Context.Engine.DetectAt(p.X,p.Y)){if(Context.Document.FindByViewerObject(hit.Owner) is CadLineEntity line&&TryAcceptLine(line))return true;}return false;}
        return CommitResolvedPoint(p,_vertex,TryAcceptPoint);
    }
    public bool TryAcceptLine(CadLineEntity line)
    {
        if(!IsActive||_lines.Contains(line)||!Context.Document.IsEntitySelectable(line)||_lines.Count>=2)return false;
        if(_lines.Count==0){_lines.Add(line);UpdatePrompt();return true;}
        if(!TryFrame(_lines[0],line,out _vertex,out _firstDirection,out _secondDirection,out _normal)){SetPromptLocalized("Cad.Prompt.angledimension.Invalid","Angle dimension: lines must share one endpoint and not be parallel [Esc cancel]");return false;}
        _lines.Add(line);Context.WorkPlane.SetToolPlaneFixed(true);UpdatePrompt();return true;
    }
    public bool TryAcceptPoint(OcctPoint3d point)
    {
        if(!IsActive||_lines.Count!=2||!point.IsFinite)return false;
        var delta=point-_vertex;delta-=_normal*delta.Dot(_normal);if(delta.Length<=1e-9)return false;
        CommitPreview(new CadAngleDimensionEntity(_vertex,_firstDirection,_secondDirection,delta.Length,_textHeight,_arrowSize));return true;
    }
    protected override bool OnSetParameter(string id,string value)
    {
        if((!double.TryParse(value,NumberStyles.Float,CultureInfo.CurrentCulture,out var n)&&!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n))||!double.IsFinite(n)||n<=1e-9)return false;
        if(id.Equals("TextHeight",StringComparison.OrdinalIgnoreCase))_textHeight=n;else if(id.Equals("ArrowSize",StringComparison.OrdinalIgnoreCase))_arrowSize=n;else return false;NotifyUpdated();return true;
    }
    protected override bool OnStepBack(){if(_lines.Count==0)return false;_lines.RemoveAt(_lines.Count-1);Context.Preview.Clear();Context.WorkPlane.SetToolPlaneFixed(false);UpdatePrompt();return true;}
    private void TryAcceptSelected(){foreach(var line in Context.Selection.Selected.OfType<CadLineEntity>().Take(2))if(!TryAcceptLine(line))break;}
    private void Show(OcctPoint3d point)
    {
        var delta=point-_vertex;
        delta-=_normal*delta.Dot(_normal);
        if(delta.Length>1e-9)
            ShowPreview(new CadAngleDimensionEntity(_vertex,_firstDirection,_secondDirection,delta.Length,_textHeight,_arrowSize));
        else
            Context.Preview.Clear();
    }
    private void UpdatePrompt(){var key=_lines.Count switch{0=>"First",1=>"Second",_=>"Position"};var text=_lines.Count switch{0=>"Angle dimension: select first line [Esc cancel]",1=>"Angle dimension: select second line [Backspace undo, Esc cancel]",_=>"Angle dimension: specify arc position [Backspace undo, Esc cancel]"};SetStageLocalized(_lines.Count,$"Cad.Prompt.angledimension.{key}",text);}
    private static bool TryFrame(CadLineEntity first,CadLineEntity second,out OcctPoint3d vertex,out OcctVector3d d1,out OcctVector3d d2,out OcctVector3d normal)
    {
        var firstStart=first.ToWorldPoint(first.Start);
        var firstEnd=first.ToWorldPoint(first.End);
        var secondStart=second.ToWorldPoint(second.Start);
        var secondEnd=second.ToWorldPoint(second.End);
        var pairs=new[]{(firstStart,firstEnd,secondStart,secondEnd),(firstStart,firstEnd,secondEnd,secondStart),(firstEnd,firstStart,secondStart,secondEnd),(firstEnd,firstStart,secondEnd,secondStart)};
        foreach(var p in pairs)if((p.Item1-p.Item3).Length<=1e-7){vertex=p.Item1;d1=(p.Item2-vertex).Normalized();d2=(p.Item4-vertex).Normalized();var cross=d1.Cross(d2);if(cross.TryNormalize(out normal)&&Math.Abs(d1.Dot(d2))<1-1e-10)return true;break;}
        vertex=default;d1=default;d2=default;normal=default;return false;
    }
}
