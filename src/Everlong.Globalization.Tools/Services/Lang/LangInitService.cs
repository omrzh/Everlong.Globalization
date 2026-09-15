using System.Text.Json;
using System.Xml.Linq;

namespace Everlong.Globalization.Tools.Services.Lang;

public class LangInitService(CsprojLocator locator)
{
  private const string SampleContent =
    """
    {
      "Title": "My Application",
      "Welcome" : "Hello, {name}!"
    }
    """;

  public Task RunAsync(string? projectOverride, string? locale, CancellationToken ct = default)
  {
    var csprojPath = ResolveCsproj(projectOverride);
    if (csprojPath is null)
      return Task.CompletedTask;

    var resolvedLocale = locale ?? ReadNeutralLanguage(csprojPath) ?? "en";

    var csprojDir = Path.GetDirectoryName(csprojPath)!;
    var i18NDir = Path.Combine(csprojDir, "Properties", "i18n");

    if (Directory.Exists(i18NDir))
    {
      Console.WriteLine($"Warning: '{i18NDir}' already exists. Skipping init.");
      return Task.CompletedTask;
    }

    Directory.CreateDirectory(i18NDir);

    var ns = DeriveNamespace(csprojPath);
    var i18NConfig = BuildFullTemplate(ns, resolvedLocale);
    var configFile = Path.Combine(i18NDir, "i18n.jsonc");
    File.WriteAllText(configFile, i18NConfig);
    Console.WriteLine($"Created: {configFile}");

    var localeDir = Path.Combine(i18NDir, resolvedLocale);
    Directory.CreateDirectory(localeDir);
    var sampleFile = Path.Combine(localeDir, "App.json");
    File.WriteAllText(sampleFile, SampleContent);
    Console.WriteLine($"Created: {sampleFile}");

    Console.WriteLine();
    Console.WriteLine("Next step: dotnet elg gen");
    return Task.CompletedTask;
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
      return fullPath;
    }

    var cwd = Directory.GetCurrentDirectory();
    var located = locator.FindCsproj(cwd);
    if (located is not null)
      return located;

    Console.WriteLine($"No .csproj file found walking down from: {cwd}");
    return null;
  }

  /// <summary>
  ///   Returns a complete i18n.jsonc with every configurable key shown at its default value
  ///   and inline comments explaining each option.  The key set comes from
  ///   <see cref="Models.LangOptions" />, so the scaffold cannot fall behind the parser, and the three
  ///   values the table cannot state on its own — the neutral locale, the locale folder <c>init</c>
  ///   just created, and the derived namespace — are supplied here.
  /// </summary>
  public static string BuildFullTemplate(string derivedNamespace, string locale)
    => LangConfigTemplate.Render(new Dictionary<string, string>
    {
      ["locale.default"] = JsonSerializer.Serialize(locale),
      // Forward slash: the same value resolves on every OS, which a committed config needs.
      ["locale.sourceDir"] = JsonSerializer.Serialize("Properties/i18n"),
      ["output.namespace"] = JsonSerializer.Serialize(derivedNamespace)
    });

  private static string DeriveNamespace(string csprojPath)
  {
    var xml = XDocument.Load(csprojPath);
    return CsprojLocator.DeriveNamespace(csprojPath, xml);
  }

  private static string? ReadNeutralLanguage(string csprojPath)
  {
    try
    {
      var xml = XDocument.Load(csprojPath);
      var value = xml.Descendants("PropertyGroup")
        .Elements("NeutralLanguage").FirstOrDefault()?.Value?.Trim();
      return string.IsNullOrEmpty(value) ? null : value;
    }
    catch
    {
      return null;
    }
  }
}
