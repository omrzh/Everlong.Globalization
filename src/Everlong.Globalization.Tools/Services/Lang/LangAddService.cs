using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class LangAddService(CsprojLocator locator)
{
  public async Task RunAsync(string locale, string? projectOverride, bool force, CancellationToken ct = default)
  {
    var csprojPath = ResolveCsproj(projectOverride);
    if (csprojPath is null) return;

    var config = locator.ReadConfig(csprojPath);
    await AddFolderAsync(locale, config, force, ct);
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
        Console.WriteLine($"Project '{fullPath}' does not declare <Elg>. Nothing to add.");
        return null;
      }

      return fullPath;
    }

    var cwd = Directory.GetCurrentDirectory();
    var located = locator.FindCsprojWithElg(cwd);
    if (located is not null) return located;

    if (locator.FindCsproj(cwd) is null)
      Console.WriteLine($"No .csproj file found walking down from: {cwd}");
    else
      Console.WriteLine($"No project with <Elg> found walking down from: {cwd}");
    return null;
  }

  private static async Task AddFolderAsync(string locale, LangConfig config, bool force, CancellationToken ct)
  {
    var sourceFolder = Path.Combine(config.SourceDir, config.DefaultLocale);
    if (!Directory.Exists(sourceFolder))
      throw new DirectoryNotFoundException($"Default locale folder not found: {sourceFolder}");

    var targetFolder = Path.Combine(config.SourceDir, locale);
    if (Directory.Exists(targetFolder) && !force)
      throw new InvalidOperationException($"Folder already exists: {targetFolder}. Use --force to overwrite.");

    Directory.CreateDirectory(targetFolder);
    foreach (var sourceFile in Directory.GetFiles(sourceFolder, "*.json")
               .Concat(Directory.GetFiles(sourceFolder, "*.jsonc")))
    {
      var targetFile = Path.Combine(targetFolder, Path.GetFileName(sourceFile));
      var content = await File.ReadAllTextAsync(sourceFile, ct);
      await File.WriteAllTextAsync(targetFile, content, ct);
      Console.WriteLine($"Created: {targetFile}");
    }
  }
}
