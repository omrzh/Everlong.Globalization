using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class LangGenService(CsprojLocator locator, JsonLangReader reader, CodeGenerator generator)
{
  public async Task RunAsync(string? projectOverride, CancellationToken ct = default)
  {
    var singleProjectMode = !string.IsNullOrWhiteSpace(projectOverride);
    var csprojPaths = ResolveCsprojPaths(projectOverride);
    if (csprojPaths.Count == 0)
      return;

    foreach (var csprojPath in csprojPaths)
    {
      try
      {
        await GenerateForProjectAsync(csprojPath, ct);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex) when (!singleProjectMode)
      {
        WriteSkipped(csprojPath);
        WriteReason(ex.Message);
      }
      catch (Exception ex)
      {
        WriteFailed(csprojPath);
        WriteReason(ex.Message);
        throw;
      }
    }
  }

  private async Task GenerateForProjectAsync(string csprojPath, CancellationToken ct)
  {
    var config = locator.ReadConfig(csprojPath);

    if (config.GenerateCoordinator && locator.IsLibraryProject(csprojPath))
      WriteWarning(
        $"'coordinator.generate' is set for what appears to be a library project: {csprojPath}. " +
        "The coordinator is intended for executable app projects only. Generating anyway, " +
        "but consumers of this library may be confused by the public Initialize()/UseLocale() methods on Lang.");

    var (modules, allLocaleValues) = ReadFolder(config);

    var generatedFiles = generator.GenerateFiles(
      config.Namespace,
      config.DefaultLocale,
      modules,
      allLocaleValues,
      config.ClassName,
      config.ClassVisibility,
      config.MemberVisibility,
      config.GenerateXmlDoc,
      config.GenerateFormatMethod,
      config.LocalesVisibility,
      config.LocalesInPartialFile,
      config.SectionClassesInPartialFiles,
      config.SectionTypeSuffix,
      config.GenerateCoordinator,
      config.CoordinatorManifests,
      config.GlobalizationNamespace);

    Directory.CreateDirectory(config.OutputDir);
    var outputPaths = generatedFiles
      .Select(f => Path.Combine(config.OutputDir, f.FileName))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);

    DeleteStaleGeneratedFiles(config.OutputDir, config.ClassName, outputPaths);

    foreach (var generatedFile in generatedFiles)
    {
      var outputPath = Path.Combine(config.OutputDir, generatedFile.FileName);

      if (File.Exists(outputPath))
      {
        var attrs = File.GetAttributes(outputPath);
        if (attrs.HasFlag(FileAttributes.ReadOnly))
          File.SetAttributes(outputPath, attrs & ~FileAttributes.ReadOnly);
      }

      await File.WriteAllTextAsync(outputPath, LineEndings.Apply(generatedFile.Content, config.LineEnding), ct);

      File.SetAttributes(outputPath, File.GetAttributes(outputPath) | FileAttributes.ReadOnly);

      WriteGenerated(outputPath);
    }
  }

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
        Console.WriteLine($"Project '{fullPath}' does not declare <Elg>. Nothing to generate.");
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

  private (IReadOnlyList<ModuleData>, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>)
    ReadFolder(LangConfig config)
  {
    var neutralDir = Path.Combine(config.SourceDir, config.DefaultLocale);
    if (!Directory.Exists(neutralDir))
      throw new DirectoryNotFoundException(
        $"Neutral locale directory not found: {neutralDir}. " +
        $"The default locale '{config.DefaultLocale}' must have a corresponding subdirectory.");

    var moduleFiles = Directory.GetFiles(neutralDir, "*.json")
      .Concat(Directory.GetFiles(neutralDir, "*.jsonc"))
      .OrderBy(f => f).ToList();
    var localeDirs = Directory.GetDirectories(config.SourceDir).OrderBy(d => d).ToList();

    var neutralBaseNames = new HashSet<string>(
      moduleFiles.Select(f => Path.GetFileNameWithoutExtension(f)!),
      StringComparer.OrdinalIgnoreCase);

    var modules = new List<ModuleData>();
    foreach (var moduleFile in moduleFiles)
    {
      var moduleName = ToModuleName(Path.GetFileNameWithoutExtension(moduleFile)!);
      var neutralFileData = reader.ReadWithMeta(File.ReadAllText(moduleFile), config.GenerateXmlDoc);

      var keyPrefix = neutralFileData.KeyPrefix ?? moduleName;
      var defaultNodes = neutralFileData.Nodes;

      var neutralFlat = PrefixKeys(JsonLangReader.Flatten(defaultNodes), keyPrefix);
      var allFlatValues = new Dictionary<string, IReadOnlyDictionary<string, string>>
      {
        [config.DefaultLocale] = neutralFlat
      };

      foreach (var localeDir in localeDirs)
      {
        var locale = Path.GetFileName(localeDir)!;
        if (locale == config.DefaultLocale)
          continue;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(moduleFile)!;
        var localeFile = new[] { ".json", ".jsonc" }
          .Select(ext => Path.Combine(localeDir, nameWithoutExt + ext))
          .FirstOrDefault(File.Exists);
        if (localeFile is not null)
        {
          var localeFileData = reader.ReadWithMeta(File.ReadAllText(localeFile));
          var localeKeyPrefix = neutralFileData.KeyPrefix ?? moduleName;
          var localeFlat = PrefixKeys(JsonLangReader.Flatten(localeFileData.Nodes), localeKeyPrefix);
          allFlatValues[locale] = localeFlat;
        }
      }

      modules.Add(new ModuleData(moduleName, defaultNodes, allFlatValues, keyPrefix, neutralFileData.DataOnly));
    }

    var merged = new Dictionary<string, Dictionary<string, string>>();
    foreach (var module in modules)
    {
      foreach (var (locale, flat) in module.AllFlatValues)
      {
        if (!merged.TryGetValue(locale, out var dict))
          merged[locale] = dict = new Dictionary<string, string>();
        foreach (var (k, v) in flat)
          dict[k] = v;
      }
    }

    foreach (var localeDir in localeDirs)
    {
      var locale = Path.GetFileName(localeDir)!;
      if (locale == config.DefaultLocale)
        continue;

      foreach (var extraFile in Directory.GetFiles(localeDir, "*.json")
                 .Concat(Directory.GetFiles(localeDir, "*.jsonc")))
      {
        if (neutralBaseNames.Contains(Path.GetFileNameWithoutExtension(extraFile)!))
          continue;

        var extraFileData = reader.ReadWithMeta(File.ReadAllText(extraFile));
        var extraModuleName = ToModuleName(Path.GetFileNameWithoutExtension(extraFile)!);
        var extraKeyPrefix = extraFileData.KeyPrefix ?? extraModuleName;

        if (!merged.TryGetValue(locale, out var dict))
          merged[locale] = dict = new Dictionary<string, string>();
        foreach (var (k, v) in PrefixKeys(JsonLangReader.Flatten(extraFileData.Nodes), extraKeyPrefix))
          dict[k] = v;
      }
    }

    var allLocaleValues = merged.ToDictionary(
      kvp => kvp.Key,
      kvp => (IReadOnlyDictionary<string, string>)kvp.Value);

    return (modules, allLocaleValues);
  }

  private static IReadOnlyDictionary<string, string> PrefixKeys(
    IReadOnlyDictionary<string, string> flat, string prefix)
  {
    if (string.IsNullOrEmpty(prefix))
      return flat;
    return flat.ToDictionary(kvp => $"{prefix}.{kvp.Key}", kvp => kvp.Value);
  }

  private static string ToModuleName(string filename)
  {
    if (string.IsNullOrEmpty(filename))
      return "Default";
    var parts = filename.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
    return string.Concat(parts.Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
  }

  private static void DeleteStaleGeneratedFiles(
    string outputDir,
    string className,
    IReadOnlySet<string> outputPaths)
  {
    if (!Directory.Exists(outputDir))
      return;

    var filePattern = $"{className}.*.g.cs";
    foreach (var candidate in Directory.GetFiles(outputDir, filePattern, SearchOption.TopDirectoryOnly))
    {
      if (outputPaths.Contains(candidate))
        continue;

      var attrs = File.GetAttributes(candidate);
      if (attrs.HasFlag(FileAttributes.ReadOnly))
        File.SetAttributes(candidate, attrs & ~FileAttributes.ReadOnly);
      File.Delete(candidate);
    }
  }

  private static void WriteGenerated(string outputPath) =>
    WriteColoredLine(ConsoleColor.Green, $"Generated: {outputPath}");

  private static void WriteWarning(string message) =>
    WriteColoredLine(ConsoleColor.Yellow, $"Warning: {message}");

  private static void WriteSkipped(string csprojPath) =>
    WriteColoredLine(ConsoleColor.Yellow, $"Skipped: {csprojPath}");

  private static void WriteFailed(string csprojPath) =>
    WriteColoredLine(ConsoleColor.Red, $"Failed: {csprojPath}");

  private static void WriteReason(string message) =>
    WriteColoredLine(ConsoleColor.DarkYellow, $"Reason: {message}");

  private static void WriteColoredLine(ConsoleColor color, string message)
  {
    var original = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ForegroundColor = original;
  }
}
