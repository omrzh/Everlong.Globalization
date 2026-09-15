using System.Xml.Linq;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class CsprojLocator
{
  /// <summary>
  ///   Returns <see langword="true" /> when the project appears to be a NuGet-packaged library
  ///   (i.e. <c>&lt;IsPackable&gt;true&lt;/IsPackable&gt;</c> or <c>OutputType</c> is absent/Library).
  /// </summary>
  public bool IsLibraryProject(string csprojPath)
  {
    try
    {
      var xml = XDocument.Load(csprojPath);

      var isPackable = xml.Descendants("PropertyGroup")
        .Elements("IsPackable")
        .FirstOrDefault()?.Value?.Trim();
      if (isPackable?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        return true;

      var outputType = xml.Descendants("PropertyGroup")
        .Elements("OutputType")
        .FirstOrDefault()?.Value?.Trim();
      return string.IsNullOrEmpty(outputType)
             || outputType.Equals("Library", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  public string? FindCsproj(string startDirectory)
  {
    if (!Directory.Exists(startDirectory))
      return null;

    return Directory.EnumerateFiles(startDirectory, "*.csproj", SearchOption.AllDirectories)
      .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
      .FirstOrDefault();
  }

  public string? FindCsprojWithElg(string startDirectory)
  {
    if (!Directory.Exists(startDirectory))
      return null;

    return Directory.EnumerateFiles(startDirectory, "*.csproj", SearchOption.AllDirectories)
      .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
      .FirstOrDefault(HasElgDeclaration);
  }

  public IReadOnlyList<string> FindAllCsprojWithElg(string startDirectory)
  {
    if (!Directory.Exists(startDirectory))
      return [];

    return Directory.EnumerateFiles(startDirectory, "*.csproj", SearchOption.AllDirectories)
      .Where(HasElgDeclaration)
      .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

  public bool HasElgDeclaration(string csprojPath)
  {
    if (!File.Exists(csprojPath))
      return false;

    try
    {
      var xml = XDocument.Load(csprojPath);
      return xml.Descendants("PropertyGroup")
        .Elements("Elg")
        .Any(e => !string.IsNullOrWhiteSpace(e.Value));
    }
    catch
    {
      return false;
    }
  }

  public LangConfig ReadConfig(string csprojPath)
  {
    var xml = XDocument.Load(csprojPath);
    var csprojDir = Path.GetDirectoryName(csprojPath)!;

    var elgLang = xml.Descendants("PropertyGroup")
      .Elements("Elg").FirstOrDefault()?.Value?.Trim();

    // "true" or absent → probe for i18n.jsonc first, fall back to i18n.json
    string relConfigFile;
    if (string.IsNullOrEmpty(elgLang) || elgLang.Equals("true", StringComparison.OrdinalIgnoreCase))
    {
      var jsoncCandidate = Path.GetFullPath(Path.Combine(csprojDir, "Properties", "i18n", "i18n.jsonc"));
      relConfigFile = File.Exists(jsoncCandidate)
        ? Path.Combine("Properties", "i18n", "i18n.jsonc")
        : Path.Combine("Properties", "i18n", "i18n.json");
    }
    else
    {
      relConfigFile = elgLang;
    }

    var absConfigFile = Path.GetFullPath(Path.Combine(csprojDir, relConfigFile));
    var configFileDir = Path.GetDirectoryName(absConfigFile)!;

    var i18nConfig = LangConfigFileReader.Read(absConfigFile);

    var neutralLanguage = xml.Descendants("PropertyGroup")
      .Elements("NeutralLanguage").FirstOrDefault()?.Value?.Trim();

    var defaultLocale = i18nConfig.DefaultLocale
                        ?? neutralLanguage
                        ?? "en";

    var absSource = i18nConfig.SourceDir is { } srcOverride
      ? Path.GetFullPath(Path.Combine(csprojDir, srcOverride))
      : configFileDir;

    var absOutput = i18nConfig.OutputDir is { } outOverride
      ? Path.GetFullPath(Path.Combine(csprojDir, outOverride))
      : Path.GetDirectoryName(absSource)
        ?? Path.GetDirectoryName(csprojPath)!;

    var relOutput = Path.GetRelativePath(csprojDir, absOutput);
    var ns = i18nConfig.Namespace ?? DeriveNamespace(csprojPath, xml, relOutput);

    var className = i18nConfig.ClassName ?? LangOptions.Text("output.className");
    var classVisibility = i18nConfig.ClassVisibility ?? LangOptions.Text("types.classVisibility");
    var memberVisibility = i18nConfig.MemberVisibility ?? LangOptions.Text("types.memberVisibility");
    var generateXmlDoc = i18nConfig.GenerateXmlDoc ?? LangOptions.Flag("codegen.xmlDoc");
    var generateFormatMethod = i18nConfig.GenerateFormatMethod ?? LangOptions.Flag("codegen.formattingMethod");
    var localesVisibility = i18nConfig.LocalesVisibility ?? memberVisibility;
    var localesInPartialFile = i18nConfig.LocalesInPartialFile ?? LangOptions.Flag("codegen.localesPartial");
    var sectionClassesInPartialFiles = i18nConfig.SectionClassesInPartialFiles ?? LangOptions.Flag("codegen.sectionsPartial");
    var sectionTypeSuffix = i18nConfig.SectionTypeSuffix ?? LangOptions.Text("types.suffix");
    var generateCoordinator = i18nConfig.GenerateCoordinator ?? LangOptions.Flag("coordinator.generate");
    var coordinatorManifests = i18nConfig.CoordinatorManifests ?? [];
    var globalizationNamespace = i18nConfig.GlobalizationNamespace ?? LangOptions.Text("codegen.globalizationNamespace");
    var lineEnding = i18nConfig.LineEnding ?? LangOptions.Text("output.lineEnding");

    return new LangConfig(
      csprojPath,
      absSource,
      absOutput,
      ns,
      defaultLocale,
      className,
      classVisibility,
      memberVisibility,
      generateXmlDoc,
      generateFormatMethod,
      localesVisibility,
      localesInPartialFile,
      sectionClassesInPartialFiles,
      sectionTypeSuffix,
      generateCoordinator,
      coordinatorManifests,
      globalizationNamespace,
      lineEnding);
  }

  internal static string DeriveNamespace(string csprojPath, XDocument xml, string outputDir = "Properties")
  {
    var rootNs = xml.Descendants("PropertyGroup")
      .Elements("RootNamespace").FirstOrDefault()?.Value?.Trim();
    var baseNs = !string.IsNullOrEmpty(rootNs)
      ? rootNs
      : Path.GetFileNameWithoutExtension(csprojPath);

    var nsSuffix = outputDir
      .Replace('\\', '.')
      .Replace('/', '.')
      .Trim('.');
    return string.IsNullOrEmpty(nsSuffix) ? baseNs : baseNs + "." + nsSuffix;
  }

  /// <summary>
  ///   Returns the list of satellite locales declared in <c>&lt;SatelliteResourceLanguages&gt;</c>,
  ///   or an empty list when the property is absent or the file cannot be read.
  /// </summary>
  public IReadOnlyList<string> ReadSatelliteLanguages(string csprojPath)
  {
    try
    {
      var xml = XDocument.Load(csprojPath);
      var value = xml.Descendants("PropertyGroup")
        .Elements("SatelliteResourceLanguages")
        .FirstOrDefault()?.Value?.Trim();
      if (string.IsNullOrEmpty(value))
        return [];
      return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .ToList();
    }
    catch
    {
      return [];
    }
  }
}

public class NoCsprojFoundException(string message) : Exception(message);
public class MissingLangConfigException(string csprojPath, string property)
  : Exception($"Property <{property}> not found in {csprojPath}. Add it to a <PropertyGroup>");
public class InvalidLangConfigException(string message) : Exception(message);
