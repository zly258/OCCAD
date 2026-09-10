using OcctNet;

namespace OCCAD;

internal static class CadRectangularArray
{
    public const int MaximumCopies = 10000;
    public const int PreviewCopies = 200;

    public static bool IsValid(int sourceCount, int columns, int rows, double columnSpacing, double rowSpacing) =>
        sourceCount > 0 && columns is >= 1 and <= 100 && rows is >= 1 and <= 100 &&
        (long)columns * rows > 1 && (long)sourceCount * (columns * rows - 1) <= MaximumCopies &&
        double.IsFinite(columnSpacing) && double.IsFinite(rowSpacing) &&
        (columns == 1 || Math.Abs(columnSpacing) > 1e-9) &&
        (rows == 1 || Math.Abs(rowSpacing) > 1e-9);

    public static CadEntity[] CreateCopies(IReadOnlyList<CadEntity> sources, int columns, int rows,
        OcctVector3d columnStep, OcctVector3d rowStep, int limit = MaximumCopies)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (!columnStep.IsFinite || !rowStep.IsFinite ||
            !IsValid(sources.Count, columns, rows, columnStep.Length, rowStep.Length))
            throw new ArgumentException("Invalid rectangular array dimensions or copy count.");
        if (limit <= 0 || limit > MaximumCopies) throw new ArgumentOutOfRangeException(nameof(limit));
        var copies = new List<CadEntity>();
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (column == 0 && row == 0) continue; // Source objects are the first cell.
                var displacement = columnStep * column + rowStep * row;
                if (!displacement.IsFinite) throw new ArgumentOutOfRangeException(nameof(columnStep));
                foreach (var source in sources)
                {
                    var copy = source.Duplicate();
                    copy.Translate(displacement);
                    copies.Add(copy);
                    if (copies.Count == limit) return copies.ToArray();
                }
            }
        return copies.ToArray();
    }
}
