namespace Everlong.Globalization.Tools.Services.Lang.Models;

/// <summary>
/// Represents a node in the parsed i18n JSON tree.
/// Interior node: Value == null, Children populated.
/// Leaf node: Value != null; FormatParams empty = plain string, non-empty = ICU MessageFormat.
/// </summary>
public record LangNode(
  string Key,
  string? Value,
  IReadOnlyList<LangNode> Children,
  IReadOnlyList<string> FormatParams,
  string? XmlDoc = null)
{
  public bool IsLeaf => Value is not null;
}
