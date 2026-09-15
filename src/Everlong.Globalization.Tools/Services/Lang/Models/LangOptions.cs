namespace Everlong.Globalization.Tools.Services.Lang.Models;

/// <summary>The JSON kind an <see cref="LangOption" /> accepts.</summary>
internal enum LangOptionKind
{
  String,
  Boolean,
  StringArray
}

/// <summary>
///   One key of <c>i18n.jsonc</c>: what the reader accepts, what a config that leaves it out gets, and
///   the sentence the docs and <c>init</c>'s scaffold show.  Keeping the four facts together is what
///   stops strictness and documentation from drifting apart.
/// </summary>
/// <param name="Group">The JSON object holding the option, e.g. <c>output</c>.</param>
/// <param name="Name">The key inside that object, e.g. <c>lineEnding</c>.</param>
/// <param name="Kind">The JSON kind the value must have.</param>
/// <param name="AllowedValues">The accepted values, or <see langword="null" /> when any string goes.</param>
/// <param name="Default">
///   The constant a config that leaves the option out falls back to — a <see cref="string" />, a
///   <see cref="bool" />, an empty <see cref="string" /> array for <c>[]</c>, or <see langword="null" />
///   when the repository supplies the value (a derived path, the project's neutral language, another
///   option).
/// </param>
/// <param name="DefaultDisplay">How the default reads in a table, where the constant alone is ambiguous.</param>
/// <param name="Doc">The option's one-sentence contract, as it appears in the docs and the scaffold.</param>
/// <param name="ValueCaseInsensitive">
///   Whether an allowed value is matched after trimming and lower-casing — for options whose value is a
///   token of this tool's own vocabulary (<c>output.lineEnding</c>) rather than one emitted verbatim
///   into the generated code (<c>types.classVisibility</c>).
/// </param>
/// <param name="DefaultFrom">
///   The path of another option this one falls back to when <paramref name="Default" /> is
///   <see langword="null" />, for a default that is a chain rather than a constant.
/// </param>
internal sealed record LangOption(
  string Group,
  string Name,
  LangOptionKind Kind,
  IReadOnlyList<string>? AllowedValues,
  object? Default,
  string DefaultDisplay,
  string Doc,
  bool ValueCaseInsensitive = false,
  string? DefaultFrom = null)
{
  /// <summary>The dotted path a config error names, e.g. <c>output.lineEnding</c>.</summary>
  public string Path => $"{Group}.{Name}";
}

/// <summary>A JSON object of <see cref="LangOption" />s, in the order the scaffold writes them.</summary>
/// <param name="Name">The object's key, e.g. <c>codegen</c>.</param>
/// <param name="Title">The heading the scaffold's comment uses, e.g. <c>Code generation options</c>.</param>
internal sealed record LangOptionGroup(string Name, string Title);

/// <summary>
///   The whole <c>i18n.jsonc</c> surface: the groups in scaffold order and every option they hold.
///   The config reader validates against it, <c>init</c> renders its scaffold from it, and the
///   documentation tests compare the docs to it.
/// </summary>
internal static class LangOptions
{
  public static readonly IReadOnlyList<LangOptionGroup> Groups =
  [
    new("locale", "Locale"),
    new("output", "Output"),
    new("types", "Type visibility"),
    new("codegen", "Code generation options"),
    new("coordinator", "Coordinator generation")
  ];

  public static readonly IReadOnlyList<LangOption> All =
  [
    // ── locale ───────────────────────────────────────────────────────────────
    new("locale", "default", LangOptionKind.String, null, null,
      "en, or `<NeutralLanguage>` when the project sets it",
      "BCP 47 tag of the neutral (default) locale, e.g. \"en\", \"zh-CN\". A <NeutralLanguage> " +
      "in the .csproj wins when this key is absent."),
    new("locale", "sourceDir", LangOptionKind.String, null, null,
      "the folder holding this config file",
      "Root folder containing per-locale subdirectories. Relative to the .csproj file."),

    // ── output ──────────────────────────────────────────────────────────────
    new("output", "dir", LangOptionKind.String, null, "Properties",
      "Properties",
      "Directory for generated *.g.cs files. Relative to the .csproj file."),
    new("output", "namespace", LangOptionKind.String, null, null,
      "`<RootNamespace>` plus the output directory",
      "C# namespace of the generated class."),
    new("output", "className", LangOptionKind.String, null, "Lang",
      "Lang",
      "Root static class name."),
    new("output", "lineEnding", LangOptionKind.String, ["lf", "crlf", "platform"], "lf",
      "lf",
      "Line endings of every file this tool writes. The tool never reads .editorconfig or " +
      ".gitattributes; declare \"crlf\" or \"platform\" when the repository needs them.",
      ValueCaseInsensitive: true),

    // ── types ───────────────────────────────────────────────────────────────
    new("types", "classVisibility", LangOptionKind.String, ["public", "internal"], "public",
      "public",
      "Visibility of the generated root class."),
    new("types", "memberVisibility", LangOptionKind.String, ["public", "internal"], "public",
      "public",
      "Visibility of the members the generated classes expose."),
    new("types", "localesVisibility", LangOptionKind.String, ["public", "internal"], null,
      "memberVisibility",
      "Visibility of the generated Locales class. Defaults to memberVisibility.",
      DefaultFrom: "types.memberVisibility"),
    new("types", "suffix", LangOptionKind.String, null, "Strings",
      "Strings",
      "Suffix appended to each per-module string-section class name, e.g. \"Strings\" → AppStrings."),

    // ── codegen ─────────────────────────────────────────────────────────────
    new("codegen", "xmlDoc", LangOptionKind.Boolean, null, false,
      "false",
      "Generate XML documentation comments on all public members. When false, CS1591 is suppressed " +
      "with a #pragma instead."),
    new("codegen", "formattingMethod", LangOptionKind.Boolean, null, true,
      "true",
      "Emit a Format(...) helper on string entries that contain format placeholders."),
    new("codegen", "globalizationNamespace", LangOptionKind.String, null, "Everlong.Globalization",
      "Everlong.Globalization",
      "Namespace of the globalization runtime types."),
    new("codegen", "localesPartial", LangOptionKind.Boolean, null, false,
      "false",
      "Place the Locales nested class in a separate *.Locales.g.cs partial file."),
    new("codegen", "sectionsPartial", LangOptionKind.Boolean, null, false,
      "false",
      "Place each string-section class in its own *.{Module}.g.cs partial file."),

    // ── coordinator ─────────────────────────────────────────────────────────
    new("coordinator", "generate", LangOptionKind.Boolean, null, false,
      "false",
      "Generate Lang.Coordinator.g.cs: extends Lang with Initialize() and UseLocale(). Only set this " +
      "in app projects, not in library or package projects."),
    new("coordinator", "manifests", LangOptionKind.StringArray, null, Array.Empty<string>(),
      "[]",
      "External assembly manifests to register and apply the collaborative overlay for.")
  ];

  private static readonly Dictionary<string, LangOption> ByPath =
    All.ToDictionary(o => o.Path, StringComparer.Ordinal);

  /// <summary>Returns the option at <paramref name="group" />.<paramref name="name" />, or <see langword="null" />.</summary>
  public static LangOption? Find(string group, string name)
    => ByPath.GetValueOrDefault($"{group}.{name}");

  /// <summary>Returns the option at a dotted <paramref name="path" />, or throws when the table has no such key.</summary>
  public static LangOption At(string path)
    => ByPath.TryGetValue(path, out var option)
      ? option
      : throw new ArgumentOutOfRangeException(nameof(path), path, "No such i18n option.");

  /// <summary>The <see cref="string" /> default of an option whose fallback is a constant.</summary>
  public static string Text(string path) => At(path).Default as string
    ?? throw new InvalidOperationException($"'{path}' has no constant string default.");

  /// <summary>
  ///   The value of an option that falls back to another option — the other side of
  ///   <see cref="LangOption.DefaultFrom" /> — or <see langword="null" /> when the fallback is not a chain.
  /// </summary>
  public static LangOption? ChainedTo(string path)
    => At(path).DefaultFrom is { } source ? At(source) : null;

  /// <summary>The <see cref="bool" /> default of an option whose fallback is a constant.</summary>
  public static bool Flag(string path) => At(path).Default as bool?
    ?? throw new InvalidOperationException($"'{path}' has no constant boolean default.");

  /// <summary>The options of one group, in table order.</summary>
  public static IEnumerable<LangOption> InGroup(string group)
    => All.Where(o => o.Group == group);

  /// <summary>
  ///   The closest known option path to a key the config got wrong, or <see langword="null" /> when
  ///   nothing is close enough to be worth suggesting.  The name decides and the group breaks a tie, so
  ///   a key filed under the wrong group still points at the option it meant.
  /// </summary>
  public static string? NearestOption(string group, string name)
  {
    string? best = null;
    var bestNameDistance = int.MaxValue;
    var bestGroupDistance = int.MaxValue;

    foreach (var option in All)
    {
      var nameDistance = Distance(option.Name, name);
      var groupDistance = Distance(option.Group, group);
      if (nameDistance < bestNameDistance ||
          (nameDistance == bestNameDistance && groupDistance < bestGroupDistance))
      {
        bestNameDistance = nameDistance;
        bestGroupDistance = groupDistance;
        best = option.Path;
      }
    }

    return bestNameDistance <= Math.Max(2, name.Length / 2) ? best : null;
  }

  /// <summary>The closest known group name to a group the config got wrong, or <see langword="null" />.</summary>
  public static string? NearestGroup(string name)
  {
    string? best = null;
    var bestDistance = int.MaxValue;

    foreach (var group in Groups)
    {
      var distance = Distance(group.Name, name);
      if (distance < bestDistance)
      {
        bestDistance = distance;
        best = group.Name;
      }
    }

    var limit = Math.Max(2, name.Length / 2);
    return bestDistance <= limit ? best : null;
  }

  /// <summary>Levenshtein distance, the smallest number of edits that turns <paramref name="a" /> into <paramref name="b" />.</summary>
  internal static int Distance(string a, string b)
  {
    var previous = new int[b.Length + 1];
    var current = new int[b.Length + 1];

    for (var j = 0; j <= b.Length; j++)
      previous[j] = j;

    for (var i = 1; i <= a.Length; i++)
    {
      current[0] = i;
      for (var j = 1; j <= b.Length; j++)
      {
        var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
        current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
      }

      (previous, current) = (current, previous);
    }

    return previous[b.Length];
  }
}
