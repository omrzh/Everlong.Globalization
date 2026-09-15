using System.Text.Json;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Reads <c>i18n.jsonc</c> against <see cref="LangOptions" />: an unknown group, an unknown key, a
///   value of the wrong JSON kind and a value outside the option's allowed set all fail the run and
///   name the config file, the JSON path and — where one is close — the key that was probably meant.
/// </summary>
/// <remarks>
///   Strict reading gives up forward compatibility on purpose: a config carrying an option the
///   installed tool predates fails here instead of being half-read, so a repository is told to update
///   <c>dotnet-elg</c> rather than silently getting bytes another machine would not produce.  The
///   locale-file parser (<see cref="JsonLangReader" />) keeps the opposite contract — any key there is
///   a legitimate string entry — because a locale file is content, not configuration.
/// </remarks>
internal static class LangConfigFileReader
{
  private static readonly JsonDocumentOptions JsoncOptions = new()
  {
    CommentHandling = JsonCommentHandling.Skip
  };

  public static I18nFileConfig Read(string path)
  {
    if (!File.Exists(path))
      return new I18nFileConfig();

    using var doc = JsonDocument.Parse(File.ReadAllText(path), JsoncOptions);
    var root = doc.RootElement;

    if (root.ValueKind != JsonValueKind.Object)
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{path}': the root must be a JSON object, but was {Describe(root)}.");

    var values = new Dictionary<string, object?>(StringComparer.Ordinal);

    foreach (var group in root.EnumerateObject())
    {
      if (!LangOptions.Groups.Any(g => g.Name == group.Name))
        throw new InvalidLangConfigException(
          $"Invalid i18n config in '{path}': unknown group '{group.Name}'." +
          Suggestion(LangOptions.NearestGroup(group.Name)));

      if (group.Value.ValueKind != JsonValueKind.Object)
        throw new InvalidLangConfigException(
          $"Invalid i18n config in '{path}': '{group.Name}' must be a JSON object, but was {Describe(group.Value)}.");

      foreach (var property in group.Value.EnumerateObject())
      {
        var option = LangOptions.Find(group.Name, property.Name)
                     ?? throw new InvalidLangConfigException(
                       $"Invalid i18n config in '{path}': unknown key '{group.Name}.{property.Name}'." +
                       Suggestion(LangOptions.NearestOption(group.Name, property.Name)));

        values[option.Path] = ReadValue(option, property.Value, path);
      }
    }

    var config = new I18nFileConfig(
      Namespace: Text("output.namespace"),
      DefaultLocale: Text("locale.default"),
      OutputDir: Text("output.dir"),
      SourceDir: Text("locale.sourceDir"),
      ClassName: Text("output.className"),
      ClassVisibility: Text("types.classVisibility"),
      MemberVisibility: Text("types.memberVisibility"),
      GenerateXmlDoc: Flag("codegen.xmlDoc"),
      GenerateFormatMethod: Flag("codegen.formattingMethod"),
      LocalesVisibility: Text("types.localesVisibility"),
      LocalesInPartialFile: Flag("codegen.localesPartial"),
      SectionClassesInPartialFiles: Flag("codegen.sectionsPartial"),
      SectionTypeSuffix: Text("types.suffix"),
      GenerateCoordinator: Flag("coordinator.generate"),
      CoordinatorManifests: Items("coordinator.manifests"),
      GlobalizationNamespace: Text("codegen.globalizationNamespace"),
      LineEnding: Text("output.lineEnding"));

    ValidateCoherence(config, path);
    return config;

    string? Text(string optionPath) => values.TryGetValue(optionPath, out var value) ? (string?)value : null;
    bool? Flag(string optionPath) => values.TryGetValue(optionPath, out var value) ? (bool?)value : null;
    IReadOnlyList<string>? Items(string optionPath) => values.TryGetValue(optionPath, out var value)
      ? (IReadOnlyList<string>?)value
      : null;
  }

  /// <summary>
  ///   Rejects the combinations that are individually valid and together produce code a consumer
  ///   cannot reach.
  /// </summary>
  private static void ValidateCoherence(I18nFileConfig config, string path)
  {
    if (config.MemberVisibility == "public" && config.ClassVisibility == "internal")
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{path}': " +
        "memberVisibility cannot be \"public\" when classVisibility is \"internal\". " +
        "Public members on an internal class are inaccessible to consumers.");

    if (config.LocalesVisibility == "public" && config.ClassVisibility == "internal")
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{path}': " +
        "localesVisibility cannot be \"public\" when classVisibility is \"internal\". " +
        "Public members on an internal class are inaccessible to consumers.");
  }

  private static object? ReadValue(LangOption option, JsonElement element, string path)
  {
    // JSON null is how a config says "leave it out": the option falls back to its default.
    if (element.ValueKind == JsonValueKind.Null)
      return null;

    return option.Kind switch
    {
      LangOptionKind.String => ReadString(option, element, path),
      LangOptionKind.Boolean => ReadBoolean(option, element, path),
      LangOptionKind.StringArray => ReadStringArray(option, element, path),
      _ => throw new InvalidOperationException($"Unhandled option kind {option.Kind}.")
    };
  }

  private static string ReadString(LangOption option, JsonElement element, string path)
  {
    if (element.ValueKind != JsonValueKind.String)
      throw WrongKind(option, element, path, "a string");

    var value = element.GetString()!;
    if (option.ValueCaseInsensitive)
      value = value.Trim().ToLowerInvariant();

    if (option.AllowedValues is { Count: > 0 } allowed && !allowed.Contains(value, StringComparer.Ordinal))
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{path}': '{option.Path}' must be {Join(allowed)}, but was \"{value}\".");

    return value;
  }

  private static bool ReadBoolean(LangOption option, JsonElement element, string path)
    => element.ValueKind switch
    {
      JsonValueKind.True => true,
      JsonValueKind.False => false,
      _ => throw WrongKind(option, element, path, "a boolean")
    };

  private static IReadOnlyList<string> ReadStringArray(LangOption option, JsonElement element, string path)
  {
    if (element.ValueKind != JsonValueKind.Array)
      throw WrongKind(option, element, path, "an array of strings");

    var items = new List<string>();
    var index = 0;

    foreach (var item in element.EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.String)
        throw new InvalidLangConfigException(
          $"Invalid i18n config in '{path}': '{option.Path}[{index}]' must be a string, but was {Describe(item)}.");

      var text = item.GetString()!;
      if (string.IsNullOrWhiteSpace(text))
        throw new InvalidLangConfigException(
          $"Invalid i18n config in '{path}': '{option.Path}[{index}]' must name an assembly, but was blank.");

      items.Add(text);
      index++;
    }

    return items;
  }

  private static InvalidLangConfigException WrongKind(LangOption option, JsonElement element, string path, string expected)
    => new($"Invalid i18n config in '{path}': '{option.Path}' must be {expected}, but was {Describe(element)}.");

  private static string Suggestion(string? nearest)
    => nearest is null ? "" : $" Did you mean '{nearest}'?";

  private static string Join(IReadOnlyList<string> values)
    => values.Count == 1
      ? $"\"{values[0]}\""
      : string.Join(", ", values.Take(values.Count - 1).Select(v => $"\"{v}\"")) + $" or \"{values[^1]}\"";

  private static string Describe(JsonElement element) => element.ValueKind switch
  {
    JsonValueKind.String => $"\"{element.GetString()}\"",
    JsonValueKind.Number => element.GetRawText(),
    JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
    JsonValueKind.Array => "an array",
    JsonValueKind.Object => "an object",
    _ => "null"
  };
}

/// <summary>
///   The options a config sets, with <see langword="null" /> for every key the file leaves out — the
///   reader's output before <see cref="CsprojLocator" /> layers the project facts and the defaults on.
/// </summary>
internal record I18nFileConfig(
  string? Namespace = null,
  string? DefaultLocale = null,
  string? OutputDir = null,
  string? SourceDir = null,
  string? ClassName = null,
  string? ClassVisibility = null,
  string? MemberVisibility = null,
  bool? GenerateXmlDoc = null,
  bool? GenerateFormatMethod = null,
  string? LocalesVisibility = null,
  bool? LocalesInPartialFile = null,
  bool? SectionClassesInPartialFiles = null,
  string? SectionTypeSuffix = null,
  bool? GenerateCoordinator = null,
  IReadOnlyList<string>? CoordinatorManifests = null,
  string? GlobalizationNamespace = null,
  string? LineEnding = null);
