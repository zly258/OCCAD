using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using OCCAD;

namespace OCCAD.Avalonia;

internal static class CadLanguageManager
{
    private static readonly string[] Supported = ["en-US", "zh-CN"];
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string CurrentLanguage { get; private set; } = "en-US";
    public static event EventHandler? Changed;

    public static string Text(string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(fallback);

        var values = Get(CurrentLanguage);
        return values.TryGetValue(key, out var value)
            ? value
            : fallback;
    }

    public static string ToolPrompt(CadToolPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (string.IsNullOrWhiteSpace(prompt.ResourceKey))
            return prompt.Message;

        var template = Text(prompt.ResourceKey, prompt.Message);
        if (prompt.FormatArguments.Count == 0)
            return template;

        try
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                prompt.FormatArguments.ToArray());
        }
        catch (FormatException)
        {
            return prompt.Message;
        }
    }

    public static string CommandMessage(CadCommandResult result)
    {
        var fallback = result.Message ?? string.Empty;
        if (string.IsNullOrEmpty(result.MessageKey))
            return fallback;

        var template = Text(result.MessageKey, fallback);
        if (result.MessageArguments.Count == 0)
            return template;

        try
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                result.MessageArguments.ToArray());
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    public static string HistoryName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        if (name.StartsWith("Property ", StringComparison.Ordinal) &&
            name != "Property Edit")
        {
            var property = name[9..];
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.PropertyEditNamed", "Property {0}"),
                Text($"Cad.Property.{property}", property));
        }

        if (name.StartsWith("Create ", StringComparison.Ordinal))
        {
            var entityName = name["Create ".Length..];
            var localizedEntityName = Text(
                $"Cad.Text.{entityName.Replace(" ", string.Empty, StringComparison.Ordinal)}",
                entityName);
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.CreateNamed", "Create {0}"),
                localizedEntityName);
        }

        if (name.StartsWith("Assign Layer ", StringComparison.Ordinal))
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.AssignLayerNamed", "Assign Layer {0}"),
                name["Assign Layer ".Length..]);
        }

        foreach (var operation in new[] { "Copy", "Delete", "Hide" })
        {
            if (name.StartsWith(operation + " ", StringComparison.Ordinal) &&
                int.TryParse(
                    name[(operation.Length + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var count))
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Text($"Cad.History.{operation}Count", operation + " {0}"),
                    count);
            }
        }

        if (name is "Display Wireframe" or "Display Shaded")
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Text("Cad.History.DisplayNamed", "Display {0}"),
                Text(
                    $"Cad.Value.OcctDisplayMode.{name[8..]}",
                    name[8..]));
        }

        return Text(
            $"Cad.History.{name.Replace(" ", string.Empty, StringComparison.Ordinal)}",
            name);
    }

    public static void Apply(string language)
    {
        var normalized = Supported.FirstOrDefault(
            value => string.Equals(
                value,
                language,
                StringComparison.OrdinalIgnoreCase)) ?? "en-US";

        CurrentLanguage = normalized;
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        _ = Get(normalized);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static IReadOnlyDictionary<string, string> Get(string language)
    {
        if (Cache.TryGetValue(language, out var cached))
            return cached;

        var assembly = typeof(CadLanguageManager).Assembly;
        var suffix = $"Localization.Strings.{language}.xml";
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
        if (resourceName is null)
            return Cache[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Localization resource '{resourceName}' cannot be opened.");
        var document = XDocument.Load(stream);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var values = document
            .Descendants()
            .Select(element => new
            {
                Key = (string?)element.Attribute(xaml + "Key"),
                Value = element.Value
            })
            .Where(value => !string.IsNullOrWhiteSpace(value.Key))
            .ToDictionary(
                value => value.Key!,
                value => value.Value,
                StringComparer.OrdinalIgnoreCase);
        Cache[language] = values;
        return values;
    }
}
