using System.Text.Json;
using System.Text.Json.Nodes;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class LangSyncService(CsprojLocator locator, JsonLangReader reader)
{
  private static readonly JsonSerializerOptions WriteOptions = new()
  {
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  public async Task RunAsync(string? locale, string? projectOverride, CancellationToken ct = default)
  {
    var csprojPath = ResolveCsproj(projectOverride);
    if (csprojPath is null)
      return;

    var config = locator.ReadConfig(csprojPath);
    var localesToSync = locale is not null
      ? (IEnumerable<string>)[locale]
      : GetOtherLocales(config);

    foreach (var loc in localesToSync)
    {
      ct.ThrowIfCancellationRequested();
      Console.WriteLine(loc);
      await SyncLocaleAsync(config, loc, ct);
    }
  }

  private async Task SyncLocaleAsync(LangConfig config, string locale, CancellationToken ct)
  {
    var defaultDir = Path.Combine(config.SourceDir, config.DefaultLocale);
    var targetDir = Path.Combine(config.SourceDir, locale);
    Directory.CreateDirectory(targetDir);

    foreach (var defaultFile in GetLocaleFiles(defaultDir))
    {
      ct.ThrowIfCancellationRequested();
      await SyncFileAsync(locale, defaultFile, targetDir, config.LineEnding, ct);
    }
  }

  private async Task SyncFileAsync(string locale, string defaultFile, string targetDir, string lineEnding, CancellationToken ct)
  {
    var defaultContent = await File.ReadAllTextAsync(defaultFile, ct);
    if (LocaleFile.IsDataOnly(defaultFile, defaultContent, reader))
      return;

    var fileName = Path.GetFileName(defaultFile)!;
    var targetFile = FindMatchingFile(targetDir, fileName)
                     ?? Path.Combine(targetDir, EnsureJsonExtension(fileName));

    JsonObject targetJson;
    string? existingContent = null;
    if (File.Exists(targetFile))
    {
      existingContent = await File.ReadAllTextAsync(targetFile, ct);
      if (LocaleFile.IsDataOnly(targetFile, existingContent, reader))
        return;
      targetJson = LocaleFile.ParseObject(existingContent, targetFile);
    }
    else
    {
      targetJson = new JsonObject();
    }

    var defaultJson = LocaleFile.ParseObject(defaultContent, defaultFile);
    var originalNormalized = targetJson.ToJsonString(WriteOptions);
    var (rebuilt, addedCount) = ReconstructInOrder(defaultJson, targetJson);
    var rebuiltNormalized = rebuilt.ToJsonString(WriteOptions);
    var desired = LineEndings.Apply(rebuiltNormalized, lineEnding);

    // Two reasons to write: the keys moved, or the bytes are not the ones the option asks for.  The
    // second one matters because `gen` already rewrites every file it owns with this ending, so a
    // `sync` that skipped an ending-only difference would leave the two commands disagreeing about
    // the same tree.
    var keysChanged = rebuiltNormalized != originalNormalized;
    if (!keysChanged && existingContent is not null && LineEndings.Uses(existingContent, lineEnding))
      return;

    await File.WriteAllTextAsync(targetFile, desired, ct);

    if (existingContent is null)
      Console.WriteLine($"  {locale}/{fileName}: created ({addedCount} key(s))");
    else if (addedCount > 0)
      Console.WriteLine($"  {locale}/{fileName}: {addedCount} key(s) added");
    else if (keysChanged)
      Console.WriteLine($"  {locale}/{fileName}: re-ordered");
    else
      Console.WriteLine($"  {locale}/{fileName}: line endings normalized to {lineEnding}");
  }

  /// <summary>
  ///   Builds a new <see cref="JsonObject"/> where all keys appear in the same order as
  ///   <paramref name="source"/>, then appends any orphan keys from <paramref name="target"/>
  ///   that are absent from <paramref name="source"/>. Existing translations in
  ///   <paramref name="target"/> are preserved unchanged.
  /// </summary>
  public static (JsonObject result, int added) ReconstructInOrder(JsonObject source, JsonObject target)
  {
    var result = new JsonObject();
    var added = 0;

    foreach (var kvp in source)
    {
      if (kvp.Key.StartsWith('$'))
        continue;

      if (target.TryGetPropertyValue(kvp.Key, out var targetValue))
      {
        if (kvp.Value is JsonObject sourceObj && targetValue is JsonObject targetObj)
        {
          var (nested, nestedAdded) = ReconstructInOrder(sourceObj, targetObj);
          result[kvp.Key] = nested;
          added += nestedAdded;
        }
        else
        {
          result[kvp.Key] = targetValue?.DeepClone();
        }
      }
      else
      {
        result[kvp.Key] = kvp.Value?.DeepClone();
        added++;
      }
    }

    foreach (var kvp in target)
    {
      if (!result.ContainsKey(kvp.Key))
        result[kvp.Key] = kvp.Value?.DeepClone();
    }

    return (result, added);
  }

  private static IEnumerable<string> GetLocaleFiles(string dir)
    => Directory.GetFiles(dir, "*.json")
       .Concat(Directory.GetFiles(dir, "*.jsonc"))
       .OrderBy(f => f);

  private static string? FindMatchingFile(string dir, string sourceFileName)
  {
    var nameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
    foreach (var ext in new[] { ".json", ".jsonc" })
    {
      var candidate = Path.Combine(dir, nameWithoutExt + ext);
      if (File.Exists(candidate))
        return candidate;
    }
    return null;
  }

  private static string EnsureJsonExtension(string fileName)
    => Path.ChangeExtension(fileName, ".json");

  private static IEnumerable<string> GetOtherLocales(LangConfig config)
    => Directory.GetDirectories(config.SourceDir)
       .Select(d => Path.GetFileName(d)!)
       .Where(n => n != config.DefaultLocale);

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
        Console.WriteLine($"Project '{fullPath}' does not declare <Elg>. Nothing to sync.");
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
}
