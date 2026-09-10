using System.ComponentModel;
using System.Text.Json.Nodes;
using OcctNet;

namespace OCCAD;

public sealed class CadAngleDimensionEntity : CadEntity
{
    private OcctPoint3d _vertex;
    private OcctVector3d _firstDirection;
    private OcctVector3d _secondDirection;
    private double _radius;
    private double _textHeight;
    private double _arrowSize;
    private string _fontName;

    public CadAngleDimensionEntity(
        OcctPoint3d vertex,
        OcctVector3d firstDirection,
        OcctVector3d secondDirection,
        double radius,
        double textHeight=4,
        double arrowSize=2.5,
        string fontName="Arial") : base("Angle Dimension")
    {
        if(!vertex.IsFinite) throw new ArgumentOutOfRangeException(nameof(vertex));
        _firstDirection=CadTransformMath.Normalize(firstDirection,nameof(firstDirection));
        _secondDirection=CadTransformMath.Normalize(secondDirection,nameof(secondDirection));
        ValidateDirections(_firstDirection,_secondDirection);
        ValidatePositive(radius,nameof(radius)); ValidatePositive(textHeight,nameof(textHeight)); ValidatePositive(arrowSize,nameof(arrowSize));
        ArgumentException.ThrowIfNullOrWhiteSpace(fontName);
        _vertex=vertex; _radius=radius; _textHeight=textHeight; _arrowSize=arrowSize; _fontName=fontName.Trim();
        DisplayMode=OcctDisplayMode.Wireframe;
    }

    [Browsable(false)] public OcctPoint3d Vertex=>_vertex;
    [Browsable(false)] public OcctVector3d FirstDirection=>_firstDirection;
    [Browsable(false)] public OcctVector3d SecondDirection=>_secondDirection;
    [Browsable(false)] public OcctVector3d Normal=>_firstDirection.Cross(_secondDirection).Normalized();
    [Category("Measurement"),ReadOnly(true)] public double AngleDegrees=>Math.Acos(Math.Clamp(_firstDirection.Dot(_secondDirection),-1,1))*180/Math.PI;
    [Category("Dimension")] public double Radius {get=>_radius;set{ValidatePositive(value,nameof(value));SetGeometry(ref _radius,value);}}
    [Category("Dimension")] public double TextHeight {get=>_textHeight;set{ValidatePositive(value,nameof(value));SetGeometry(ref _textHeight,value);}}
    [Category("Dimension")] public double ArrowSize {get=>_arrowSize;set{ValidatePositive(value,nameof(value));SetGeometry(ref _arrowSize,value);}}
    [Category("Dimension")] public string FontName {get=>_fontName;set{ArgumentException.ThrowIfNullOrWhiteSpace(value);SetGeometry(ref _fontName,value.Trim());}}

    internal override OcctShape BuildShape(OcctEngine engine)
    {
        var length=Math.Max(_radius,1);
        using var model=new OcctModelingSession();
        var first=model.MakeLine(_vertex,_vertex+_firstDirection*length);
        var second=model.MakeLine(_vertex,_vertex+_secondDirection*length);
        var annotation=model.MakeAngleAnnotation(first,second,new OcctBRepAnnotationOptions(_radius,_textHeight,_arrowSize,_fontName));
        return engine.CreateShapeFromModel(model,annotation);
    }

    public override IReadOnlyList<CadSnapPoint> GetSnapPoints()
    {
        var first=_vertex+_firstDirection*_radius; var second=_vertex+_secondDirection*_radius;
        return [new(this,_vertex,CadSnapType.Vertex,0),new(this,first,CadSnapType.Endpoint,1),new(this,second,CadSnapType.Endpoint,2)];
    }
    public override IReadOnlyList<CadGripPoint> GetGripPoints()
    {
        var middle=Bisector(); return [new(this,0,_vertex+middle*_radius)];
    }
    public override void MoveGrip(int index,OcctPoint3d targetPoint)
    {
        if(index!=0)throw new ArgumentOutOfRangeException(nameof(index)); if(!targetPoint.IsFinite)throw new ArgumentOutOfRangeException(nameof(targetPoint));
        var delta=targetPoint-_vertex; var normal=Normal; delta-=normal*delta.Dot(normal); if(delta.Length<=1e-9)return;
        _radius=delta.Length; RaiseGeometryChanged(nameof(MoveGrip));
    }

    public override CadEntity Duplicate()=>CopyPropertiesTo(new CadAngleDimensionEntity(_vertex,_firstDirection,_secondDirection,_radius,_textHeight,_arrowSize,_fontName));
    public override void RestoreGeometry(CadEntity snapshot)
    {
        if(snapshot is not CadAngleDimensionEntity e)throw new ArgumentException("Snapshot type does not match.",nameof(snapshot));
        _vertex=e._vertex;_firstDirection=e._firstDirection;_secondDirection=e._secondDirection;_radius=e._radius;_textHeight=e._textHeight;_arrowSize=e._arrowSize;_fontName=e._fontName;RaiseGeometryChanged(nameof(RestoreGeometry));
    }
    public override void Translate(OcctVector3d displacement){ValidateDisplacement(displacement);_vertex=Translated(_vertex,displacement);RaiseGeometryChanged(nameof(Translate));}
    public override void Rotate(OcctPoint3d center,OcctVector3d axis,double angleDegrees){_vertex=CadTransformMath.RotatePoint(_vertex,center,axis,angleDegrees);_firstDirection=CadTransformMath.RotateVector(_firstDirection,axis,angleDegrees).Normalized();_secondDirection=CadTransformMath.RotateVector(_secondDirection,axis,angleDegrees).Normalized();RaiseGeometryChanged(nameof(Rotate));}
    public override void Scale(OcctPoint3d center,double factor){CadTransformMath.ValidateScale(factor);_vertex=CadTransformMath.ScalePoint(_vertex,center,factor);_radius*=factor;_textHeight*=factor;_arrowSize*=factor;RaiseGeometryChanged(nameof(Scale));}

    private OcctVector3d Bisector(){var sum=_firstDirection+_secondDirection;return sum.TryNormalize(out var result)?result:_firstDirection;}
    private static void ValidateDirections(OcctVector3d first,OcctVector3d second){var dot=Math.Abs(first.Dot(second));if(dot>=1-1e-10)throw new ArgumentException("Angle dimension directions must not be parallel.");}
    internal static JsonObject WriteGeometry(CadAngleDimensionEntity e)=>new(){["vertex"]=CadEntityJson.Point(e.Vertex),["firstDirection"]=CadEntityJson.Vector(e.FirstDirection),["secondDirection"]=CadEntityJson.Vector(e.SecondDirection),["radius"]=e.Radius,["textHeight"]=e.TextHeight,["arrowSize"]=e.ArrowSize,["fontName"]=e.FontName};
    internal static CadAngleDimensionEntity ReadGeometry(JsonObject data){var font=data["fontName"]?.GetValue<string>();if(string.IsNullOrWhiteSpace(font))throw new InvalidDataException("CAD dimension font cannot be empty.");return new(CadEntityJson.ReadPoint(data,"vertex"),CadEntityJson.ReadVector(data,"firstDirection"),CadEntityJson.ReadVector(data,"secondDirection"),CadEntityJson.ReadDouble(data,"radius"),CadEntityJson.ReadDouble(data,"textHeight"),CadEntityJson.ReadDouble(data,"arrowSize"),font);}
}
