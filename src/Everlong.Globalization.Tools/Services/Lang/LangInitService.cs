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
  ///   and inline comments explaining each option.
  /// </summary>
  public static string BuildFullTemplate(string derivedNamespace, string locale)
    => $$"""
         {
           // --- Locale ---
           "locale": {
             // BCP 47 tag of the neutral (default) locale, e.g. "en", "zh-CN".
             "default": "{{locale}}",
             // Root folder containing per-locale subdirectories. Relative to the .csproj file.
             "sourceDir": "Properties\\i18n"
           },

           // --- Output ---
           "output": {
             // Directory for generated *.g.cs files. Relative to the .csproj file.
             "dir": "Properties",
             // C# namespace of the generated class.
             "namespace": "{{derivedNamespace}}",
             // Root static class name.
             "className": "Lang",
             // Line endings of every file this tool writes: "lf", "crlf", or "platform" (default)
             // to follow the convention of the running OS. Set it to the end_of_line your
             // .editorconfig declares, so a regeneration is byte-identical to the committed file.
             "lineEnding": "platform"
           },

           // --- Type visibility ---
           "types": {
             "classVisibility": "public",   // "public" | "internal"
             "memberVisibility": "public",  // "public" | "internal"
             "localesVisibility": "public", // "public" | "internal"
             // Suffix appended to each per-module string-section class name, e.g. "Strings" → AppStrings.
             "suffix": "Strings"
           },

           // --- Code generation options ---
           "codegen": {
             // Generate XML documentation comments on all public members.
             // When true: full /// <summary> on every property (using JSON value or JSONC comment).
             // When false (default): suppress CS1591 warnings with #pragma disable instead.
             "xmlDoc": false,
             // Emit a Format(\u2026) helper on string entries that contain format placeholders.
             "formattingMethod": true,
             // Namespace of the globalization runtime types (Everlong.Globalization).
             "globalizationNamespace": "Everlong.Globalization",
             // Place the Locales nested class in a separate *.Locales.g.cs partial file.
             "localesPartial": false,
             // Place each string-section class in its own *.{Module}.g.cs partial file.
             "sectionsPartial": false
           }
         }
         """;

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
