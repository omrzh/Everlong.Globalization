namespace Everlong.Globalization.Tools.Services.Lang.Models;

/// <summary>Holds data for one i18n module (one JSON file in the neutral locale).</summary>
/// <param name="ModuleName">
///   PascalCase module name derived from the JSON filename (e.g. <c>"App"</c> from <c>app.json</c>).
/// </param>
/// <param name="DefaultNodes">Node tree from the neutral locale for this module.</param>
/// <param name="AllFlatValues">
///   Flat key→value map per locale for this module's keys.
///   ICU MessageFormat placeholders are preserved as-is.
/// </param>
public record ModuleData(
  string ModuleName,
  IReadOnlyList<LangNode> DefaultNodes,
  IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> AllFlatValues,
  string? KeyPrefix = null,
  bool DataOnly = false);
