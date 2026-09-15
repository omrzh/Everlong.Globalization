namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Rewrites every catalog the tool owns with the configured line ending — the default locale's
///   included, because that one is nobody's write target: <c>gen</c> writes the files it generates and
///   <c>sync</c> the catalogs it fills, so without this a change of convention would leave the
///   repository half converted, or convert it in the middle of an unrelated <c>sync</c>.
/// </summary>
/// <remarks>
///   This is a byte-level migration, not a content edit.  Every catalog is read and validated before
///   any of them is written, so a broken one leaves the tree as it was instead of half converted; a
///   file that already uses the ending is left alone.  The config file itself is never rewritten — it
///   is the hand-written JSONC that declares the value, and regenerating it would drop its comments.
/// </remarks>
public class LangNormalizeService(CsprojLocator locator, JsonLangReader reader)
{
  /// <summary>Returns the number of files whose bytes changed.</summary>
  public async Task<int> RunAsync(string? projectOverride, CancellationToken ct = default)
  {
    var csprojPaths = ResolveCsprojPaths(projectOverride);
    if (csprojPaths.Count == 0)
      return 0;

    var normalized = 0;
    foreach (var csprojPath in csprojPaths)
    {
      ct.ThrowIfCancellationRequested();
      if (csprojPaths.Count > 1)
        Console.WriteLine(csprojPath);

      normalized += await NormalizeProjectAsync(csprojPath, ct);
    }

    return normalized;
  }

  private async Task<int> NormalizeProjectAsync(string csprojPath, CancellationToken ct)
  {
    var config = locator.ReadConfig(csprojPath);
    if (!Directory.Exists(config.SourceDir))
      throw new DirectoryNotFoundException($"Source directory not found: {config.SourceDir}");

    // Read and validate the whole tree first: a broken catalog must leave the repository unconverted
    // rather than half converted, which is the state this command exists to remove.
    var catalogs = new List<(string Path, string Locale, string Content)>();
    foreach (var localeDir in Directory.GetDirectories(config.SourceDir).OrderBy(d => d, StringComparer.Ordinal))
    {
      foreach (var file in CatalogFiles(localeDir))
      {
        ct.ThrowIfCancellationRequested();
        var content = await File.ReadAllTextAsync(file, ct);
        LocaleFile.Parse(file, content, reader);
        catalogs.Add((file, Path.GetFileName(localeDir), content));
      }
    }

    var normalized = 0;
    foreach (var catalog in catalogs)
    {
      if (LineEndings.Uses(catalog.Content, config.LineEnding))
        continue;

      await File.WriteAllTextAsync(
        catalog.Path, LineEndings.Apply(catalog.Content, config.LineEnding), ct);
      Console.WriteLine(
        $"  {catalog.Locale}/{Path.GetFileName(catalog.Path)}: " +
        $"{LineEndings.Describe(catalog.Content)} → {config.LineEnding}");
      normalized++;
    }

    return normalized;
  }

  private static IEnumerable<string> CatalogFiles(string localeDir)
    => Directory.GetFiles(localeDir, "*.json")
      .Concat(Directory.GetFiles(localeDir, "*.jsonc"))
      .OrderBy(f => f, StringComparer.Ordinal);

  private IReadOnlyList<string> ResolveCsprojPaths(string? projectOverride)
  {
    if (!string.IsNullOrWhiteSpace(projectOverride))
    {
      var fullPath = Path.GetFullPath(projectOverride);
      if (!File.Exists(fullPath))
      {
        Console.WriteLine($"Project not found: {fullPath}");
        return [];
      }

      if (!locator.HasElgDeclaration(fullPath))
      {
        Console.WriteLine($"Project '{fullPath}' does not declare <Elg>. Nothing to normalize.");
        return [];
      }

      return [fullPath];
    }

    var cwd = Directory.GetCurrentDirectory();
    var located = locator.FindAllCsprojWithElg(cwd);
    if (located.Count > 0)
      return located;

    if (locator.FindCsproj(cwd) is null)
      Console.WriteLine($"No .csproj file found walking down from: {cwd}");
    else
      Console.WriteLine($"No project with <Elg> found walking down from: {cwd}");
    return [];
  }
}
