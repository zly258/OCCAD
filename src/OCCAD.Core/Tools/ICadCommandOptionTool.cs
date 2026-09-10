namespace OCCAD;

public interface ICadCommandOptionTool
{
    bool TryExecuteOption(string input, out bool success, out string? message);
}
