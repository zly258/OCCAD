using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace OCCAD.Avalonia;

internal static class CadDrawingCursor
{
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAsUlEQVR4nO3asQrCQBRFwbfi///yWimChVgsI+RMlSKwJ5d0yUwi7b33npmtAm7q4H/RADpAawAdoDWADtAaQAdoDaADtAbQAVoD6ACtAXSA1gA6QGsAHaA1gA5IkkDr7fr0B8r1/ZYPJ5vWzMz9ddI++/xr/f78J5uePZd/A5IkUD9JaQ2gA7QG0AFaA+gArQF0gNYAOkBrAB2gNYAO0BpAB2gNoAO0BtAB2uUHyNU9AFhOFlXFxQL6AAAAAElFTkSuQmCC";

    private static readonly Bitmap Bitmap = CreateBitmap();

    public static Cursor Instance { get; } =
        new(Bitmap, new PixelPoint(32, 32));

    private static Bitmap CreateBitmap()
    {
        var bytes = Convert.FromBase64String(Png);
        return new Bitmap(new MemoryStream(bytes, writable: false));
    }
}
