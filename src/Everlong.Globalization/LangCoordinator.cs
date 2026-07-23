using System.Globalization;

namespace Everlong.Globalization;

/// <summary>
///   Coordinates locale switching across multiple assemblies, each represented by an
///   <see cref="ILangManifest" />.
/// </summary>
/// <remarks>
///   Register every assembly's manifest at startup, then call <see cref="Use" /> once.
///   The coordinator resolves the best matching locale for each manifest using a four-step
///   algorithm: exact match → <see cref="CultureInfo" /> parent-chain walk → language-subtag
///   prefix match → no-op.
/// </remarks>
/// <example>
/// <code>
/// var coord = new LangCoordinator();
/// coord.Register(MyApp.Properties.Lang.Manifest);
/// coord.Register(Everlong.Nester.Properties.Lang.Manifest);
/// coord.Use("zh-Hans-CN");
/// </code>
/// </example>
public sealed class LangCoordinator
{
  private readonly List<ILangManifest> _manifests = [];

  /// <summary>Adds <paramref name="manifest" /> to the set of coordinated assemblies.</summary>
  public void Register(ILangManifest manifest) => _manifests.Add(manifest);

  /// <summary>
  ///   Switches every registered assembly to the best available match for <paramref name="locale" />.
  ///   Assemblies that have no suitable locale are left unchanged.
  /// </summary>
  public void Use(string locale)
  {
    foreach (var manifest in _manifests)
    {
      var provider = Resolve(manifest.SupportedLocales, locale);
      if (provider is not null)
        manifest.Provider.Use(provider);
    }
  }

  /// <summary>
  ///   Resolves the best available locale match from <paramref name="supported" /> for
  ///   <paramref name="locale" /> using the same four-step algorithm as
  ///   <see cref="Use" />: exact match → <see cref="CultureInfo" /> parent-chain walk →
  ///   language-subtag prefix match → <see langword="null" />.
  /// </summary>
  /// <remarks>
  ///   Exposed as public so generated coordinator code can call it directly
  ///   rather than duplicating the resolution algorithm.
  /// </remarks>
  public static IStringProvider? Resolve(
    IReadOnlyDictionary<string, IStringProvider> supported, string locale)
  {
    // 1. Exact match
    if (supported.TryGetValue(locale, out var exact))
      return exact;

    // 2. Parent-chain walk via CultureInfo (e.g. "en-US" → "en")
    try
    {
      var culture = CultureInfo.GetCultureInfo(locale);
      while (!string.IsNullOrEmpty(culture.Parent.Name))
      {
        culture = culture.Parent;
        if (supported.TryGetValue(culture.Name, out var parent))
          return parent;
      }
    }
    catch (CultureNotFoundException) { }

    // 3. Language-subtag prefix match (e.g. "zh-CN" → first "zh-*" alphabetically)
    var dash = locale.IndexOf('-');
    if (dash > 0)
    {
      var tag = locale[..dash];
      var match = supported.Keys
        .Where(k => k.StartsWith(tag + "-", StringComparison.OrdinalIgnoreCase)
                    || k.Equals(tag, StringComparison.OrdinalIgnoreCase))
        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
      if (match is not null)
        return supported[match];
    }

    return null; // no match → no-op for this manifest
  }
}
