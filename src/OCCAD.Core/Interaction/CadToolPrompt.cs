namespace OCCAD;

[Flags]
public enum CadPrecisionInputKind
{
    None = 0,
    Length = 1 << 0,
    Angle = 1 << 1,
    Factor = 1 << 2,
    LengthAndAngle = Length | Angle
}

public sealed record CadToolPrompt(
    string Message,
    CadPrecisionInputKind PrecisionInputs = CadPrecisionInputKind.None)
{
    public string? ResourceKey { get; init; }
    public IReadOnlyList<object?> FormatArguments { get; init; } = [];

    public override string ToString() => Message;
}
