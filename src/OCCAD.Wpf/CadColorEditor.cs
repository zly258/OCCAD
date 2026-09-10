using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using OCCAD;
using WinForms = System.Windows.Forms;

namespace OCCAD.Wpf;

internal sealed class CadColorEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) =>
        UITypeEditorEditStyle.Modal;

    public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

    public override void PaintValue(PaintValueEventArgs e)
    {
        if (e.Value is not Color color)
            return;

        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, e.Bounds);
        e.Graphics.DrawRectangle(
            SystemPens.ControlDark,
            e.Bounds.X,
            e.Bounds.Y,
            Math.Max(0, e.Bounds.Width - 1),
            Math.Max(0, e.Bounds.Height - 1));
    }

    public override object? EditValue(
        ITypeDescriptorContext? context,
        IServiceProvider? provider,
        object? value)
    {
        var current = value is Color color ? color : Color.White;
        using var dialog = new WinForms.ColorDialog
        {
            Color = current,
            FullOpen = true,
            AnyColor = true
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
            return current;

        foreach (var entity in ResolveEntities(context?.Instance))
            entity.ColorByLayer = false;

        return dialog.Color;
    }

    private static CadEntity[] ResolveEntities(object? instance)
    {
        if (instance is CadLocalizedPropertyObject wrapper)
            return wrapper.Target is CadEntity wrappedEntity ? [wrappedEntity] : [];
        if (instance is CadEntity directEntity)
            return [directEntity];
        if (instance is object[] values)
        {
            return values
                .Select(value => value is CadLocalizedPropertyObject localized
                    ? localized.Target as CadEntity
                    : value as CadEntity)
                .Where(static value => value is not null)
                .Cast<CadEntity>()
                .Distinct()
                .ToArray();
        }

        return [];
    }
}
