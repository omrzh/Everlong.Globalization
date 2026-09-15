using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public record LocaleCheckResult(string Locale, IReadOnlyList<string> MissingKeys, IReadOnlyList<string> OrphanKeys)
{
  public bool IsConsistent => MissingKeys.Count == 0 && OrphanKeys.Count == 0;
}

public class LangCheckService(CsprojLocator locator, JsonLangReader reader)
{
  public Task<IReadOnlyList<LocaleCheckResult>> RunAsync(string? projectOverride, CancellationToken ct = default)
  {
    var csprojPath = ResolveCsproj(projectOverride);
    if (csprojPath is null)
      return Task.FromResult<IReadOnlyList<LocaleCheckResult>>([]);

    var config = locator.ReadConfig(csprojPath);

    var defaultKeys = GetFlatKeys(ReadLocale(config, config.DefaultLocale));
    var otherLocales = GetOtherLocales(config);

    var results = new List<LocaleCheckResult>();
    foreach (var locale in otherLocales)
    {
      ct.ThrowIfCancellationRequested();
      var localeKeys = GetFlatKeys(ReadLocale(config, locale));
      var missing = defaultKeys.Except(localeKeys).OrderBy(k => k).ToList();
      var orphan = localeKeys.Except(defaultKeys).OrderBy(k => k).ToList();
      results.Add(new LocaleCheckResult(locale, missing, orphan));
    }
    return Task.FromResult<IReadOnlyList<LocaleCheckResult>>(results);
  }

  private string? ResolveCsproj(string? projectOverride)
  {
    if (!string.IsNullOrWhiteSpace(projectOverride))
    {
      var fullPath = Path.GetFullPath(projectOverride);
      if (!File.Exists(fullPath))
      {
        Console.WriteLine($"Project not found: {fullPath}");
        return null;
      }

      if (!locator.HasElgDeclaration(fullPath))
      {
        Console.WriteLine($"Project '{fullPath}' does not declare <Elg>. Nothing to check.");
        return null;
      }

      return fullPath;
    }

    var cwd = Directory.GetCurrentDirectory();
    var located = locator.FindCsprojWithElg(cwd);
    if (located is not null)
      return located;

    if (locator.FindCsproj(cwd) is null)
      Console.WriteLine($"No .csproj file found walking down from: {cwd}");
    else
      Console.WriteLine($"No project with <Elg> found walking down from: {cwd}");
    return null;
  }

  private IReadOnlyList<LangNode> ReadLocale(LangConfig config, string locale)
  {
    var folder = Path.Combine(config.SourceDir, locale);
    if (!Directory.Exists(folder))
      throw new DirectoryNotFoundException($"Locale folder not found: {folder}");
    var merged = new List<LangNode>();
    foreach (var f in Directory.GetFiles(folder, "*.json")
               .Concat(Directory.GetFiles(folder, "*.jsonc"))
               .OrderBy(x => x))
    {
      var meta = LocaleFile.Parse(f, File.ReadAllText(f), reader);
      if (meta.DataOnly)
        continue;
      merged.AddRange(meta.Nodes);
    }
    return merged;
  }

  private static IEnumerable<string> GetOtherLocales(LangConfig config)
    => Directory.GetDirectories(config.SourceDir)
      .Select(d => Path.GetFileName(d)!)
      .Where(n => n != config.DefaultLocale);

  private static HashSet<string> GetFlatKeys(IReadOnlyList<LangNode> nodes, string prefix = "")
  {
    var keys = new HashSet<string>();
    foreach (var node in nodes)
    {
      var fullKey = prefix.Length > 0 ? $"{prefix}.{node.Key}" : node.Key;
      if (node.IsLeaf)
        keys.Add(fullKey);
      else
        foreach (var k in GetFlatKeys(node.Children, fullKey))
          keys.Add(k);
    }
    return keys;
  }

  /// <summary>
  ///   Cross-validates <c>&lt;SatelliteResourceLanguages&gt;</c> against the locale
  ///   subdirectories found in the source directory.
  /// </summary>
  public IReadOnlyList<string> CheckSatelliteConsistency(string? projectOverride)
  {
    var csprojPath = ResolveAnyProjectCsproj(projectOverride);
    if (csprojPath is null)
      return [];

    var satellites = locator.ReadSatelliteLanguages(csprojPath);
    if (satellites.Count == 0)
      return [];

    var config = locator.ReadConfig(csprojPath);
    var localeFolders = Directory.GetDirectories(config.SourceDir)
      .Select(d => Path.GetFileName(d)!)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var warnings = new List<string>();

    foreach (var s in satellites)
    {
      if (!localeFolders.Contains(s))
        warnings.Add($"SatelliteResourceLanguages declares '{s}' but no locale folder was found at '{Path.Combine(config.SourceDir, s)}'.");
    }

    var satelliteSet = new HashSet<string>(satellites, StringComparer.OrdinalIgnoreCase);
    foreach (var folder in localeFolders)
    {
      if (!satelliteSet.Contains(folder))
        warnings.Add($"Locale folder '{folder}' exists but is not listed in <SatelliteResourceLanguages>.");
    }

    return warnings;
  }

  private string? ResolveAnyProjectCsproj(string? projectOverride)
  {
    if (!string.IsNullOrWhiteSpace(projectOverride))
    {
      var fullPath = Path.GetFullPath(projectOverride);
      return File.Exists(fullPath) ? fullPath : null;
    }

    var cwd = Directory.GetCurrentDirectory();
    return locator.FindCsprojWithElg(cwd) ?? locator.FindCsproj(cwd);
  }
}
