using System.ComponentModel;
using System.Threading;
using OcctNet;

namespace OCCAD;

/// <summary>
/// Adds one consistent editable Normal vector to planar entities whose legacy
/// public API exposed only hidden/read-only normal components. The normal edit
/// is implemented as a rigid orientation change around the entity center, so
/// normal, local axes, geometry, presentation and history stay synchronized.
/// </summary>
internal static class CadPlanarNormalPropertyMetadata
{
    private static int _initialized;

    internal static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        Register<CadCircleEntity>();
        Register<CadArcEntity>();
        Register<CadEllipseEntity>();
        Register<CadRectangleEntity>();
        Register<CadRegularPolygonEntity>();
    }

    private static void Register<T>() where T : CadEntity =>
        TypeDescriptor.AddProvider(
            new PlanarNormalProvider(TypeDescriptor.GetProvider(typeof(T)), typeof(T)),
            typeof(T));

    private sealed class PlanarNormalProvider(
        TypeDescriptionProvider parent,
        Type componentType) : TypeDescriptionProvider(parent)
    {
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance)
        {
            var parentDescriptor = base.GetTypeDescriptor(objectType, instance)
                ?? throw new InvalidOperationException(
                    $"Unable to obtain a type descriptor for '{componentType.Name}'.");
            return new PlanarNormalDescriptor(parentDescriptor, componentType);
        }
    }

    private sealed class PlanarNormalDescriptor(
        ICustomTypeDescriptor parent,
        Type componentType) : CustomTypeDescriptor(parent)
    {
        public override PropertyDescriptorCollection GetProperties() =>
            GetProperties([]);

        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            var source = base.GetProperties(attributes)
                .Cast<PropertyDescriptor>()
                .Where(static property => property.Name is not
                    ("Normal" or "NormalX" or "NormalY" or "NormalZ"))
                .ToList();
            source.Add(new NormalPropertyDescriptor(componentType));
            return new PropertyDescriptorCollection(source.ToArray(), readOnly: true);
        }
    }

    private sealed class NormalPropertyDescriptor(Type componentType) : PropertyDescriptor(
        "Normal",
        [
            new BrowsableAttribute(true),
            new CategoryAttribute("Orientation"),
            new DisplayNameAttribute("Normal"),
            new DescriptionAttribute("Unit normal vector of the entity plane."),
            new CadPropertyAttribute(CadValueSemantic.Vector)
        ])
    {
        public override Type ComponentType { get; } = componentType;
        public override Type PropertyType => typeof(OcctVector3d);
        public override bool IsReadOnly => false;

        public override object GetValue(object? component) =>
            component is CadEntity entity
                ? NormalOf(entity)
                : throw new ArgumentException("Normal is available only for a CAD entity.", nameof(component));

        public override void SetValue(object? component, object? value)
        {
            if (component is not CadEntity entity)
                throw new ArgumentException("Normal is available only for a CAD entity.", nameof(component));
            if (value is not OcctVector3d requested || !requested.TryNormalize(out var target))
                throw new ArgumentOutOfRangeException(nameof(value), "Normal must be a finite non-zero vector.");

            var current = NormalOf(entity).Normalized();
            var dot = Math.Clamp(current.Dot(target), -1.0, 1.0);
            if (dot >= 1.0 - 1e-12)
                return;

            var axisCandidate = current.Cross(target);
            OcctVector3d axis;
            if (!axisCandidate.TryNormalize(out axis))
                axis = CadTransformMath.PerpendicularAxes(current).XAxis;

            var angleDegrees = Math.Acos(dot) * 180.0 / Math.PI;
            entity.Rotate(CenterOf(entity), axis, angleDegrees);
            OnValueChanged(component, EventArgs.Empty);
        }

        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => false;

        private static OcctPoint3d CenterOf(CadEntity entity) => entity switch
        {
            CadCircleEntity circle => circle.Center,
            CadArcEntity arc => arc.Center,
            CadEllipseEntity ellipse => ellipse.Center,
            CadRectangleEntity rectangle => rectangle.Center,
            CadRegularPolygonEntity polygon => polygon.Center,
            _ => throw new NotSupportedException($"Editable normal is not supported for {entity.GetType().Name}.")
        };

        private static OcctVector3d NormalOf(CadEntity entity) => entity switch
        {
            CadCircleEntity circle => circle.Normal,
            CadArcEntity arc => arc.Normal,
            CadEllipseEntity ellipse => ellipse.Normal,
            CadRectangleEntity rectangle => rectangle.XAxis.Cross(rectangle.YAxis).Normalized(),
            CadRegularPolygonEntity polygon => polygon.Normal,
            _ => throw new NotSupportedException($"Editable normal is not supported for {entity.GetType().Name}.")
        };
    }
}
