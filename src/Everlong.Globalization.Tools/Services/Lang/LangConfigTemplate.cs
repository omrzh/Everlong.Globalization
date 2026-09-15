using System.Text;
using System.Text.Json;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Renders the <c>i18n.jsonc</c> scaffold <c>init</c> writes from <see cref="LangOptions" />, so the
///   template cannot fall behind the parser: every group and key the reader accepts appears here, at
///   the value a config that leaves it out gets, with the same sentence the option carries.
/// </summary>
internal static class LangConfigTemplate
{
  private const int CommentWidth = 96;

  /// <summary>
  ///   Renders the scaffold.  Options whose value the repository supplies rather than the table — the
  ///   neutral locale and the derived namespace — arrive through <paramref name="literals" /> as ready
  ///   JSON text, keyed by option path (e.g. <c>output.namespace</c>).
  /// </summary>
  public static string Render(IReadOnlyDictionary<string, string> literals)
  {
    var builder = new StringBuilder();
    var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
    builder.Append("{\n");

    var groups = LangOptions.Groups;
    for (var g = 0; g < groups.Count; g++)
    {
      var group = groups[g];
      builder.Append($"  // --- {group.Title} ---\n");
      builder.Append($"  \"{group.Name}\": {{\n");

      var options = LangOptions.InGroup(group.Name).ToList();
      for (var i = 0; i < options.Count; i++)
      {
        var option = options[i];
        foreach (var line in Wrap(option.Doc, CommentWidth))
          builder.Append($"    // {line}\n");
        if (option.AllowedValues is { Count: > 0 } allowed)
          builder.Append($"    // One of: {string.Join(" | ", allowed.Select(v => $"\"{v}\""))}.\n");

        var literal = Literal(option, literals, resolved);
        resolved[option.Path] = literal;
        builder.Append($"    \"{option.Name}\": {literal}");
        builder.Append(i == options.Count - 1 ? "\n" : ",\n");
      }

      builder.Append(g == groups.Count - 1 ? "  }\n" : "  },\n\n");
    }

    builder.Append('}');
    return builder.ToString();
  }

  /// <summary>The JSON text of the value the scaffold shows: the caller's, an earlier option's, or the default.</summary>
  private static string Literal(
    LangOption option,
    IReadOnlyDictionary<string, string> supplied,
    IReadOnlyDictionary<string, string> resolved)
  {
    if (supplied.TryGetValue(option.Path, out var given))
      return given;

    if (option.DefaultFrom is { } source)
      return resolved.TryGetValue(source, out var chained)
        ? chained
        : throw new InvalidOperationException(
          $"'{option.Path}' falls back to '{source}', which the table lists after it.");

    return option.Default switch
    {
      bool flag => flag ? "true" : "false",
      string[] items => "[" + string.Join(", ", items.Select(item => JsonSerializer.Serialize(item))) + "]",
      string text => JsonSerializer.Serialize(text),
      _ => throw new InvalidOperationException(
        $"'{option.Path}' has no default of its own; init must supply one.")
    };
  }

  /// <summary>Greedy word wrap, so a long doc comment stays readable inside the config file.</summary>
  private static IEnumerable<string> Wrap(string text, int width)
  {
    var line = new StringBuilder();
    foreach (var word in text.Split(' '))
    {
      if (line.Length > 0 && line.Length + 1 + word.Length > width)
      {
        yield return line.ToString();
        line.Clear();
      }

      if (line.Length > 0)
        line.Append(' ');
      line.Append(word);
    }

    if (line.Length > 0)
      yield return line.ToString();
  }
}
