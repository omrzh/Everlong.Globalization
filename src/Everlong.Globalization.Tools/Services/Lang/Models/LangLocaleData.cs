namespace Everlong.Globalization.Tools.Services.Lang.Models;

/// <summary>Holds the parsed data for a single locale.</summary>
public record LangLocaleData(
  string Locale,
  IReadOnlyList<LangNode> Nodes);
