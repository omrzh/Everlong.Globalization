using System.Text;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class CodeGenerator
{
  public sealed record GeneratedSourceFile(string FileName, string Content);

  /// <summary>
  ///   Generates <c>Lang.g.cs</c> source that embeds all locale data and exposes INPC-capable typed accessors.
  /// </summary>
  public string Generate(
    string @namespace,
    string defaultLocale,
    IReadOnlyList<ModuleData> modules,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues,
    string className = "Lang",
    string classVisibility = "public",
    string memberVisibility = "public",
    bool generateXmlDoc = false,
    bool generateFormatMethod = true,
    string? localesVisibility = null,
    string sectionTypeSuffix = "Strings",
    string globalizationNamespace = "Everlong.Globalization")
  {
    return GenerateFiles(
      @namespace,
      defaultLocale,
      modules,
      allLocaleValues,
      className,
      classVisibility,
      memberVisibility,
      generateXmlDoc,
      generateFormatMethod,
      localesVisibility,
      sectionTypeSuffix: sectionTypeSuffix,
      globalizationNamespace: globalizationNamespace).Single().Content;
  }

  public IReadOnlyList<GeneratedSourceFile> GenerateFiles(
    string @namespace,
    string defaultLocale,
    IReadOnlyList<ModuleData> modules,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues,
    string className = "Lang",
    string classVisibility = "public",
    string memberVisibility = "public",
    bool generateXmlDoc = false,
    bool generateFormatMethod = true,
    string? localesVisibility = null,
    bool localesInPartialFile = false,
    bool sectionClassesInPartialFiles = false,
    string sectionTypeSuffix = "Strings",
    bool generateCoordinator = false,
    IReadOnlyList<string>? coordinatorManifests = null,
    string globalizationNamespace = "Everlong.Globalization")
  {
    var files = new List<GeneratedSourceFile>();
    var effectiveLocalesVisibility = localesVisibility ?? memberVisibility;

    var main = new StringBuilder();
    AppendFilePreamble(main, @namespace, generateXmlDoc, globalizationNamespace);
    EmitLangClass(
      main,
      modules,
      defaultLocale,
      allLocaleValues,
      className,
      classVisibility,
      memberVisibility,
      effectiveLocalesVisibility,
      sectionTypeSuffix,
      includeLocales: !localesInPartialFile,
      generateXmlDoc);

    if (!sectionClassesInPartialFiles)
      EmitSectionClasses(main, modules, defaultLocale, memberVisibility, generateFormatMethod, sectionTypeSuffix, generateXmlDoc);

    files.Add(new GeneratedSourceFile($"{className}.g.cs", main.ToString()));

    if (localesInPartialFile)
    {
      var localesPart = new StringBuilder();
      AppendFilePreamble(localesPart, @namespace, generateXmlDoc, globalizationNamespace);
      EmitLocalesPartial(localesPart, className, classVisibility, defaultLocale, allLocaleValues, effectiveLocalesVisibility, generateXmlDoc);
      files.Add(new GeneratedSourceFile($"{className}.Locales.g.cs", localesPart.ToString()));
    }

    if (sectionClassesInPartialFiles)
    {
      foreach (var module in modules)
      {
        if (module.DataOnly)
          continue;

        var sectionPart = new StringBuilder();
        AppendFilePreamble(sectionPart, @namespace, generateXmlDoc, globalizationNamespace);
        EmitModuleSectionClass(sectionPart, module, defaultLocale, memberVisibility, generateFormatMethod, sectionTypeSuffix, generateXmlDoc);
        files.Add(new GeneratedSourceFile($"{className}.{module.ModuleName}.g.cs", sectionPart.ToString()));
      }
    }

    if (generateCoordinator)
    {
      var coordPart = new StringBuilder();
      AppendFilePreamble(coordPart, @namespace, generateXmlDoc, globalizationNamespace);
      EmitCoordinatorPartial(coordPart, className, classVisibility, coordinatorManifests ?? []);
      files.Add(new GeneratedSourceFile($"{className}.Coordinator.g.cs", coordPart.ToString()));
    }

    return files;
  }

  private static void EmitLangClass(
    StringBuilder sb,
    IReadOnlyList<ModuleData> modules,
    string defaultLocale,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues,
    string className,
    string classVisibility,
    string memberVisibility,
    string localesVisibility,
    string sectionTypeSuffix,
    bool includeLocales,
    bool generateXmlDoc = false)
  {
    AppendGeneratedClassDoc(sb);
    sb.AppendLine($"{classVisibility} static partial class {className}");
    sb.AppendLine("{");

    var defaultId = LocaleToIdentifier(defaultLocale);
    if (generateXmlDoc)
      sb.AppendLine("  /// <summary>The global language provider for this assembly.</summary>");
    sb.AppendLine($"  public static readonly LangProvider Provider = new(Locales.{defaultId});");
    sb.AppendLine();

    if (generateXmlDoc)
      sb.AppendLine("  /// <summary>Returns the language manifest describing all supported locales.</summary>");
    sb.AppendLine($"  {classVisibility} static ILangManifest Manifest {{ get; }} = new {className}ManifestImpl();");
    sb.AppendLine();

    foreach (var module in modules)
    {
      if (module.DataOnly)
        continue;
      var name = module.ModuleName;
      var sectionTypeName = BuildSectionTypeName(sectionTypeSuffix, name);
      if (generateXmlDoc)
        sb.AppendLine($"  /// <summary>Strings for the <c>{name}</c> section.</summary>");
      sb.AppendLine($"  {memberVisibility} static {sectionTypeName} {name} {{ get; }} = new {sectionTypeName}(Provider);");
    }

    if (includeLocales)
    {
      sb.AppendLine();
      EmitLocales(sb, defaultLocale, allLocaleValues, localesVisibility, generateXmlDoc);
      sb.AppendLine();
    }
    else
    {
      sb.AppendLine();
    }

    EmitManifestImpl(sb, className, defaultLocale, allLocaleValues);
    sb.AppendLine("}");
  }

  private static void EmitCoordinatorPartial(
    StringBuilder sb,
    string className,
    string classVisibility,
    IReadOnlyList<string> manifests)
  {
    sb.AppendLine("/// <summary>");
    sb.AppendLine("/// This class was generated by <c>dotnet elg gen</c>.");
    sb.AppendLine("/// <para>");
    sb.AppendLine($"/// Extends <see cref=\"{className}\"/> with coordinator helpers for multi-assembly locale switching.");
    sb.AppendLine("/// Call <see cref=\"Initialize\"/> at app startup to register all manifests, then call");
    sb.AppendLine("/// <see cref=\"UseLocale\"/> whenever the user changes the locale.");
    sb.AppendLine("/// </para>");
    sb.AppendLine("/// <para>Do not edit this file manually; it is overwritten on next generation.</para>");
    sb.AppendLine("/// </summary>");
    sb.AppendLine($"{classVisibility} static partial class {className}");
    sb.AppendLine("{");
    sb.AppendLine("  private static LangCoordinator? _coordinator;");
    sb.AppendLine();
    sb.AppendLine("  /// <summary>");
    sb.AppendLine("  /// Creates a <see cref=\"LangCoordinator\"/>, registers all assembly manifests,");
    sb.AppendLine("  /// and stores the instance. Call this once at app startup before the first");
    sb.AppendLine("  /// <see cref=\"UseLocale\"/> call.");
    sb.AppendLine("  /// </summary>");
    sb.AppendLine("  /// <param name=\"defaultLocale\">The default locale to use.</param>");
    sb.AppendLine("  public static void Initialize(string? defaultLocale = null)");
    sb.AppendLine("  {");
    sb.AppendLine("    var coord = new LangCoordinator();");
    foreach (var manifest in manifests)
      sb.AppendLine($"    coord.Register({manifest}.Manifest);");
    sb.AppendLine($"    coord.Register(Manifest);");
    sb.AppendLine("    _coordinator = coord;");
    sb.AppendLine("    if (defaultLocale is not null)");
    sb.AppendLine("      UseLocale(defaultLocale);");
    sb.AppendLine("  }");
    sb.AppendLine();
    sb.AppendLine("  /// <summary>");
    sb.AppendLine("  /// Switches all registered assemblies to the best available locale match.");
    if (manifests.Count > 0)
    {
      sb.AppendLine("  /// When the app supports a locale that an external assembly does not,");
      sb.AppendLine("  /// the app's string provider is chained as primary over the external assembly's");
      sb.AppendLine("  /// fallback (collaborative overlay).");
    }
    sb.AppendLine("  /// </summary>");
    sb.AppendLine("  public static void UseLocale(string locale)");
    sb.AppendLine("  {");
    sb.AppendLine("    _coordinator!.Use(locale);");
    if (manifests.Count > 0)
    {
      sb.AppendLine();
      foreach (var manifest in manifests)
      {
        sb.AppendLine($"    // Collaborative overlay for {manifest}");
        sb.AppendLine($"    ApplyCollaborativeOverlay({manifest}.Manifest, locale);");
      }
    }
    sb.AppendLine("  }");
    if (manifests.Count > 0)
    {
      sb.AppendLine();
      sb.AppendLine("  private static void ApplyCollaborativeOverlay(ILangManifest externalManifest, string locale)");
      sb.AppendLine("  {");
      sb.AppendLine("    if (!externalManifest.SupportedLocales.ContainsKey(locale)");
      sb.AppendLine("        && Manifest.SupportedLocales.TryGetValue(locale, out var appLocale))");
      sb.AppendLine("    {");
      sb.AppendLine("      var fallback = LangCoordinator.Resolve(externalManifest.SupportedLocales, locale);");
      sb.AppendLine("      if (fallback is not null)");
      sb.AppendLine("        externalManifest.Provider.Use(new ChainedStringProvider(appLocale, fallback));");
      sb.AppendLine("    }");
      sb.AppendLine("  }");
    }
    sb.AppendLine("}");
  }

  private static void EmitLocalesPartial(
    StringBuilder sb,
    string className,
    string classVisibility,
    string defaultLocale,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues,
    string localesVisibility,
    bool generateXmlDoc = false)
  {
    sb.AppendLine($"{classVisibility} static partial class {className}");
    sb.AppendLine("{");
    EmitLocales(sb, defaultLocale, allLocaleValues, localesVisibility, generateXmlDoc);
    sb.AppendLine("}");
  }

  private static void EmitSectionClasses(
    StringBuilder sb,
    IReadOnlyList<ModuleData> modules,
    string defaultLocale,
    string memberVisibility,
    bool generateFormatMethod,
    string sectionTypeSuffix,
    bool generateXmlDoc = false)
  {
    foreach (var module in modules)
    {
      if (module.DataOnly)
        continue;

      sb.AppendLine();
      EmitModuleSectionClass(sb, module, defaultLocale, memberVisibility, generateFormatMethod, sectionTypeSuffix, generateXmlDoc);
    }
  }

  private static void EmitModuleSectionClass(
    StringBuilder sb,
    ModuleData module,
    string defaultLocale,
    string memberVisibility,
    bool generateFormatMethod,
    string sectionTypeSuffix,
    bool generateXmlDoc = false)
  {
    var sectionPath = module.ModuleName;
    var neutralFlat = module.AllFlatValues.TryGetValue(defaultLocale, out var mf)
      ? mf
      : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
    var initialKeyPath = module.KeyPrefix?.Split('.') ?? [];
    EmitInpcClass(
      sb,
      sectionPath,
      module.DefaultNodes,
      neutralFlat,
      initialKeyPath,
      memberVisibility,
      generateFormatMethod,
      sectionTypeSuffix,
      generateXmlDoc);
  }

  private static void EmitLocales(
    StringBuilder sb,
    string defaultLocale,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues,
    string memberVisibility,
    bool generateXmlDoc = false)
  {
    if (generateXmlDoc)
      sb.AppendLine("  /// <summary>Compiled string providers for all supported locales.</summary>");
    sb.AppendLine($"  {memberVisibility} static class Locales");
    sb.AppendLine("  {");

    var ordered = new List<(string Locale, IReadOnlyDictionary<string, string> Values)>
    {
      (defaultLocale, allLocaleValues[defaultLocale])
    };
    foreach (var kvp in allLocaleValues.OrderBy(x => x.Key))
    {
      if (kvp.Key != defaultLocale)
        ordered.Add((kvp.Key, kvp.Value));
    }

    foreach (var (locale, values) in ordered)
    {
      var id = LocaleToIdentifier(locale);
      if (generateXmlDoc)
        sb.AppendLine($"    /// <summary>String provider for the <c>{EscapeXmlDoc(locale)}</c> locale.</summary>");
      sb.AppendLine($"    public static readonly IStringProvider {id} = new DictionaryStringProvider(new Dictionary<string, string>");
      sb.AppendLine("    {");
      foreach (var (key, value) in values.OrderBy(kvp => kvp.Key))
        sb.AppendLine($"      [\"{EscapeStringLiteral(key)}\"] = \"{EscapeStringLiteral(value)}\",");
      sb.AppendLine("    });");
    }

    sb.AppendLine("  }");
  }

  private static void EmitManifestImpl(
    StringBuilder sb,
    string className,
    string defaultLocale,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> allLocaleValues)
  {
    sb.AppendLine($"  private sealed class {className}ManifestImpl : ILangManifest");
    sb.AppendLine("  {");
    sb.AppendLine($"    public LangProvider Provider => {className}.Provider;");
    sb.AppendLine("    public IReadOnlyDictionary<string, IStringProvider> SupportedLocales { get; } =");
    sb.AppendLine("      new Dictionary<string, IStringProvider>");
    sb.AppendLine("      {");

    var orderedLocales = new List<string> { defaultLocale };
    orderedLocales.AddRange(allLocaleValues.Keys.Where(k => k != defaultLocale).OrderBy(k => k));
    foreach (var locale in orderedLocales)
    {
      var id = LocaleToIdentifier(locale);
      sb.AppendLine($"        [\"{EscapeStringLiteral(locale)}\"] = Locales.{id},");
    }

    sb.AppendLine("      };");
    sb.AppendLine("  }");
  }

  private static void EmitInpcClass(
    StringBuilder sb,
    string sectionPath,
    IReadOnlyList<LangNode> nodes,
    IReadOnlyDictionary<string, string> neutralFlatValues,
    string[] keyPath,
    string memberVisibility,
    bool generateFormatMethod,
    string sectionTypeSuffix,
    bool generateXmlDoc = false)
  {
    var className = BuildSectionTypeName(sectionTypeSuffix, sectionPath);
    var sectionKeyPrefix = string.Join(".", keyPath);
    var interiorNodes = nodes.Where(n => !n.IsLeaf).ToList();
    var leafNodes = nodes.Where(n => n.IsLeaf).ToList();

    if (generateXmlDoc)
      sb.AppendLine($"/// <summary>String section for the <c>{EscapeXmlDoc(sectionPath)}</c> module.</summary>");
    sb.AppendLine($"{memberVisibility} sealed class {className} : StringSectionBase");
    sb.AppendLine("{");

    if (interiorNodes.Count > 0)
    {
      foreach (var node in interiorNodes)
      {
        if (generateXmlDoc)
          sb.AppendLine($"  /// <summary>The <c>{EscapeXmlDoc(node.Key)}</c> sub-section.</summary>");
        sb.AppendLine($"  public {BuildSectionTypeName(sectionTypeSuffix, sectionPath + node.Key)} {node.Key} {{ get; }}");
      }
      sb.AppendLine();
    }

    sb.AppendLine($"  internal {className}(LangProvider provider) : base(provider, \"{EscapeStringLiteral(sectionKeyPrefix)}\")");
    sb.AppendLine("  {");
    foreach (var node in interiorNodes)
      sb.AppendLine($"    {node.Key} = new {BuildSectionTypeName(sectionTypeSuffix, sectionPath + node.Key)}(provider);");
    sb.AppendLine("  }");

    if (leafNodes.Count > 0)
    {
      sb.AppendLine();
      foreach (var node in leafNodes)
      {
        var fullKey = keyPath.Length > 0
          ? string.Join(".", keyPath) + "." + node.Key
          : node.Key;
        var fallback = neutralFlatValues.GetValueOrDefault(fullKey, "");

        if (node.FormatParams.Count == 0)
        {
          if (generateXmlDoc)
          {
            var docText = node.XmlDoc ?? EscapeXmlDoc(fallback);
            if (!string.IsNullOrWhiteSpace(docText))
              sb.AppendLine($"  /// <summary>{docText}</summary>");
            else
              sb.AppendLine($"  /// <summary>The <c>{EscapeXmlDoc(node.Key)}</c> string.</summary>");
          }
          sb.AppendLine($"  public string {node.Key} => GetString(\"{EscapeStringLiteral(node.Key)}\", \"{EscapeStringLiteral(fallback)}\");");
        }
        else
        {
          var paramList = string.Join(", ", node.FormatParams.Select(p => $"object? {p}"));
          var dictPart = string.Join(", ", node.FormatParams.Select(p => $"[\"{EscapeStringLiteral(p)}\"] = {p}"));
          var stringAccessor = $"GetString(\"{EscapeStringLiteral(node.Key)}\", \"{EscapeStringLiteral(fallback)}\")";
          var propertyName = GetFormatTemplatePropertyName(node.Key);
          if (generateXmlDoc)
          {
            var docText = node.XmlDoc ?? EscapeXmlDoc(fallback);
            if (!string.IsNullOrWhiteSpace(docText))
              sb.AppendLine($"  /// <summary>{docText}</summary>");
            else
              sb.AppendLine($"  /// <summary>The <c>{EscapeXmlDoc(propertyName)}</c> format template.</summary>");
          }
          sb.AppendLine($"  public string {propertyName} => {stringAccessor};");
          if (generateFormatMethod)
          {
            var methodName = GetFormatMethodName(propertyName);
            if (generateXmlDoc)
              sb.AppendLine($"  /// <summary>Formats <see cref=\"{propertyName}\"/>.</summary>");
            sb.AppendLine($"  public string {methodName}({paramList}) => IcuMessageFormatter.Format({propertyName}, new Dictionary<string, object?> {{ {dictPart} }});");
          }
        }
      }
    }

    sb.AppendLine("}");

    foreach (var node in interiorNodes)
    {
      sb.AppendLine();
      EmitInpcClass(
        sb,
        sectionPath + node.Key,
        node.Children,
        neutralFlatValues,
        [.. keyPath, node.Key],
        memberVisibility,
        generateFormatMethod,
        sectionTypeSuffix,
        generateXmlDoc);
    }
  }

  private static void AppendFilePreamble(StringBuilder sb, string @namespace, bool generateXmlDoc, string globalizationNamespace = "Everlong.Globalization")
  {
    sb.AppendLine("#nullable enable");
    if (!generateXmlDoc)
      sb.AppendLine("#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member");
    sb.AppendLine("using System.Collections.Generic;");
    sb.AppendLine($"using {globalizationNamespace};");
    sb.AppendLine();
    sb.AppendLine($"namespace {@namespace};");
    sb.AppendLine();
  }

  private static string GetFormatTemplatePropertyName(string nodeKey)
    => nodeKey.EndsWith("Format", StringComparison.Ordinal) && nodeKey.Length > "Format".Length
      ? nodeKey[..^"Format".Length]
      : nodeKey;

  private static string GetFormatMethodName(string templatePropertyName)
    => $"Format{templatePropertyName}";

  private static string BuildSectionTypeName(string sectionTypeSuffix, string sectionPath)
    => $"{sectionPath}{sectionTypeSuffix}";

  private static void AppendGeneratedClassDoc(StringBuilder sb)
  {
    sb.AppendLine("/// <summary>");
    sb.AppendLine("/// This class was generated by <c>dotnet elg gen</c>.");
    sb.AppendLine("/// <para>Usage:</para>");
    sb.AppendLine("/// <list type=\"bullet\">");
    sb.AppendLine("/// <item><description>Update strings in the JSON files under locale subdirectories.</description></item>");
    sb.AppendLine("/// <item><description>Add a locale by creating a new subdirectory (for example <c>zh-CN\\</c>) with matching JSON files.</description></item>");
    sb.AppendLine("/// <item><description>Regenerate by running <c>dotnet elg gen</c> from the project directory.</description></item>");
    sb.AppendLine("/// <item><description>Configuration file: <c>Properties\\i18n\\i18n.json</c>.</description></item>");
    sb.AppendLine("/// <item><description>Do not edit generated files manually; they are overwritten on next generation.</description></item>");
    sb.AppendLine("/// </list>");
    sb.AppendLine("/// </summary>");
  }

  internal static string LocaleToIdentifier(string locale)
  {
    var parts = locale.Split('-', '_');
    return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
  }

  private static string EscapeStringLiteral(string value)
  {
    if (string.IsNullOrEmpty(value))
      return value;

    var sb = new StringBuilder(value.Length + 8);
    foreach (var ch in value)
    {
      switch (ch)
      {
        case '\\':
          sb.Append("\\\\");
          break;
        case '"':
          sb.Append("\\\"");
          break;
        case '\r':
          sb.Append("\\r");
          break;
        case '\n':
          sb.Append("\\n");
          break;
        case '\t':
          sb.Append("\\t");
          break;
        case '\0':
          sb.Append("\\0");
          break;
        case '\b':
          sb.Append("\\b");
          break;
        case '\f':
          sb.Append("\\f");
          break;
        case '\v':
          sb.Append("\\v");
          break;
        default:
          if (char.IsControl(ch))
            sb.Append($"\\u{(int)ch:x4}");
          else
            sb.Append(ch);
          break;
      }
    }

    return sb.ToString();
  }

  private static string EscapeXmlDoc(string value)
    => value
      .Replace("\r\n", "\n")
      .Replace('\r', '\n')
      .Replace("&", "&amp;")
      .Replace("<", "&lt;")
      .Replace(">", "&gt;")
      .Replace("\n", "&#10;");
}
