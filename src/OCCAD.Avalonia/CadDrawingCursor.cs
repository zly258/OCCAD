using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace OCCAD.Avalonia;

internal static class CadDrawingCursor
{
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAkklEQVR42u3asQmAMBBA0Zw4hfvPFseIhZ0E0SIXwfcbIQgeLzZiSpGkSbXW6nlpddYMy983AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAk6TfFdWHkb6qI2J7emzVH9J6dCX5nkDHH2pHfB8qXF2/AJ+aQpGE5IeJrEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAkJXYAZNsjt3LVNhQAAAAASUVORK5CYII=";

    private static readonly Bitmap Bitmap = CreateBitmap();

    public static Cursor Instance { get; } =
        new(Bitmap, new PixelPoint(32, 32));

    private static Bitmap CreateBitmap()
    {
        var bytes = Convert.FromBase64String(Png);
        return new Bitmap(new MemoryStream(bytes, writable: false));
    }
}
