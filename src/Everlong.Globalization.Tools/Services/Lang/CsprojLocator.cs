using System.Text.Json;
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

    var i18nConfig = ReadI18nFileConfig(absConfigFile);

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

    var className = i18nConfig.ClassName ?? "Lang";
    var classVisibility = i18nConfig.ClassVisibility ?? "public";
    var memberVisibility = i18nConfig.MemberVisibility ?? "public";
    var generateXmlDoc = i18nConfig.GenerateXmlDoc ?? false;
    var generateFormatMethod = i18nConfig.GenerateFormatMethod ?? true;
    var localesVisibility = i18nConfig.LocalesVisibility ?? memberVisibility;
    var localesInPartialFile = i18nConfig.LocalesInPartialFile ?? false;
    var sectionClassesInPartialFiles = i18nConfig.SectionClassesInPartialFiles ?? false;
    var sectionTypeSuffix = i18nConfig.SectionTypeSuffix ?? "Strings";
    var generateCoordinator = i18nConfig.GenerateCoordinator ?? false;
    var coordinatorManifests = i18nConfig.CoordinatorManifests ?? [];
    var globalizationNamespace = i18nConfig.GlobalizationNamespace ?? "Everlong.Globalization";

    if (memberVisibility == "public" && classVisibility == "internal")
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{absConfigFile}': " +
        "memberVisibility cannot be \"public\" when classVisibility is \"internal\". " +
        "Public members on an internal class are inaccessible to consumers.");

    if (localesVisibility == "public" && classVisibility == "internal")
      throw new InvalidLangConfigException(
        $"Invalid i18n config in '{absConfigFile}': " +
        "localesVisibility cannot be \"public\" when classVisibility is \"internal\". " +
        "Public members on an internal class are inaccessible to consumers.");

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
      globalizationNamespace);
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

  private static readonly JsonDocumentOptions JsoncOptions = new()
  {
    CommentHandling = JsonCommentHandling.Skip
  };

  internal static I18nFileConfig ReadI18nFileConfig(string path)
  {
    if (!File.Exists(path))
      return new I18nFileConfig();
    using var doc = JsonDocument.Parse(File.ReadAllText(path), JsoncOptions);
    var root = doc.RootElement;

    string? ns = null, defaultLocale = null, outputDir = null, sourceDir = null,
            className = null, classVisibility = null, memberVisibility = null,
            localesVisibility = null, globalizationNamespace = null;
    bool? generateXmlDoc = null, generateFormatMethod = null;
    bool? localesInPartialFile = null, sectionClassesInPartialFiles = null;
    string? sectionTypeSuffix = null;
    bool? generateCoordinator = null;
    IReadOnlyList<string>? coordinatorManifests = null;

    if (root.TryGetProperty("locale", out var localeGroup) && localeGroup.ValueKind == JsonValueKind.Object)
    {
      if (localeGroup.TryGetProperty("default", out var lgDefault))
        defaultLocale = lgDefault.GetString();
      if (localeGroup.TryGetProperty("sourceDir", out var lgSourceDir))
        sourceDir = lgSourceDir.GetString();
    }

    if (root.TryGetProperty("output", out var outputGroup) && outputGroup.ValueKind == JsonValueKind.Object)
    {
      if (outputGroup.TryGetProperty("dir", out var ogDir))
        outputDir = ogDir.GetString();
      if (outputGroup.TryGetProperty("namespace", out var ogNs))
        ns = ogNs.GetString();
      if (outputGroup.TryGetProperty("className", out var ogCn))
        className = ogCn.GetString();
    }

    if (root.TryGetProperty("types", out var typesGroup) && typesGroup.ValueKind == JsonValueKind.Object)
    {
      if (typesGroup.TryGetProperty("classVisibility", out var tgCv))
        classVisibility = tgCv.GetString();
      if (typesGroup.TryGetProperty("memberVisibility", out var tgMv))
        memberVisibility = tgMv.GetString();
      if (typesGroup.TryGetProperty("localesVisibility", out var tgLv))
        localesVisibility = tgLv.GetString();
      if (typesGroup.TryGetProperty("suffix", out var tgSuffix))
        sectionTypeSuffix = tgSuffix.GetString();
    }

    if (root.TryGetProperty("codegen", out var codegenGroup) && codegenGroup.ValueKind == JsonValueKind.Object)
    {
      if (codegenGroup.TryGetProperty("xmlDoc", out var cgXd) && cgXd.ValueKind is JsonValueKind.False or JsonValueKind.True)
        generateXmlDoc = cgXd.GetBoolean();
      if (codegenGroup.TryGetProperty("formattingMethod", out var cgFm) && cgFm.ValueKind is JsonValueKind.False or JsonValueKind.True)
        generateFormatMethod = cgFm.GetBoolean();
      if (codegenGroup.TryGetProperty("localesPartial", out var cgLp) && cgLp.ValueKind is JsonValueKind.False or JsonValueKind.True)
        localesInPartialFile = cgLp.GetBoolean();
      if (codegenGroup.TryGetProperty("sectionsPartial", out var cgSp) && cgSp.ValueKind is JsonValueKind.False or JsonValueKind.True)
        sectionClassesInPartialFiles = cgSp.GetBoolean();
      if (codegenGroup.TryGetProperty("globalizationNamespace", out var cgGn) && cgGn.ValueKind == JsonValueKind.String)
        globalizationNamespace = cgGn.GetString();
    }

    if (root.TryGetProperty("coordinator", out var coordGroup) && coordGroup.ValueKind == JsonValueKind.Object)
    {
      if (coordGroup.TryGetProperty("generate", out var cgGen) && cgGen.ValueKind is JsonValueKind.False or JsonValueKind.True)
        generateCoordinator = cgGen.GetBoolean();
      if (coordGroup.TryGetProperty("manifests", out var cgManifests) && cgManifests.ValueKind == JsonValueKind.Array)
      {
        coordinatorManifests = cgManifests.EnumerateArray()
          .Where(e => e.ValueKind == JsonValueKind.String)
          .Select(e => e.GetString()!)
          .Where(s => !string.IsNullOrWhiteSpace(s))
          .ToList();
      }
    }

    return new I18nFileConfig(
      ns, defaultLocale, outputDir, sourceDir, className, classVisibility, memberVisibility, generateXmlDoc, generateFormatMethod, localesVisibility,
      localesInPartialFile, sectionClassesInPartialFiles, sectionTypeSuffix, generateCoordinator, coordinatorManifests, globalizationNamespace);
  }
}

internal record I18nFileConfig(
  string? Namespace = null,
  string? DefaultLocale = null,
  string? OutputDir = null,
  string? SourceDir = null,
  string? ClassName = null,
  string? ClassVisibility = null,
  string? MemberVisibility = null,
  bool? GenerateXmlDoc = null,
  bool? GenerateFormatMethod = null,
  string? LocalesVisibility = null,
  bool? LocalesInPartialFile = null,
  bool? SectionClassesInPartialFiles = null,
  string? SectionTypeSuffix = null,
  bool? GenerateCoordinator = null,
  IReadOnlyList<string>? CoordinatorManifests = null,
  string? GlobalizationNamespace = null);

public class NoCsprojFoundException(string message) : Exception(message);
public class MissingLangConfigException(string csprojPath, string property)
  : Exception($"Property <{property}> not found in {csprojPath}. Add it to a <PropertyGroup>");
public class InvalidLangConfigException(string message) : Exception(message);
