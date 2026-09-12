namespace Everlong.Globalization.Tools.Tests;

public class CsprojLocatorTests : IDisposable
{
  private readonly List<string> _tempDirs = new();

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static string WriteCsproj(string dir, string? extraProperties = null)
  {
    var path = Path.Combine(dir, "Test.csproj");
    File.WriteAllText(path, $"""
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          {extraProperties ?? ""}
        </PropertyGroup>
      </Project>
      """);
    return path;
  }

  private static string WriteNamedCsproj(string dir, string fileName, string? extraProperties = null)
  {
    var path = Path.Combine(dir, fileName);
    File.WriteAllText(path, $"""
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          {extraProperties ?? ""}
        </PropertyGroup>
      </Project>
      """);
    return path;
  }

  private static void WriteI18nConfig(string sourceDir, string ns, string defaultLocale = "en", string outputDir = "Properties",
    string? className = null, string? classVisibility = null, string? memberVisibility = null, bool? generateXmlDoc = null,
    bool? generateFormatMethod = null, bool? localesInPartialFile = null, bool? sectionClassesInPartialFiles = null,
    string? sectionTypeSuffix = null)
  {
    Directory.CreateDirectory(sourceDir);

    var actualClassName = className ?? "Lang";
    var actualClassVis = classVisibility ?? "public";
    var actualMemberVis = memberVisibility ?? "public";
    var actualXmlDoc = generateXmlDoc ?? false;
    var actualFm = generateFormatMethod ?? true;
    var actualLp = localesInPartialFile ?? false;
    var actualSp = sectionClassesInPartialFiles ?? false;
    var actualSuffix = sectionTypeSuffix ?? "Strings";

    File.WriteAllText(Path.Combine(sourceDir, "i18n.json"), $$"""
      {
        "locale": { "default": "{{defaultLocale}}" },
        "output": {
          "dir": "{{outputDir}}",
          "namespace": "{{ns}}",
          "className": "{{actualClassName}}"
        },
        "types": {
          "classVisibility": "{{actualClassVis}}",
          "memberVisibility": "{{actualMemberVis}}",
          "localesVisibility": "{{actualMemberVis}}",
          "suffix": "{{actualSuffix}}"
        },
        "codegen": {
          "xmlDoc": {{(actualXmlDoc ? "true" : "false")}},
          "formattingMethod": {{(actualFm ? "true" : "false")}},
          "localesPartial": {{(actualLp ? "true" : "false")}},
          "sectionsPartial": {{(actualSp ? "true" : "false")}}
        }
      }
      """);
  }

  [Fact]
  public void FindCsproj_InSameDir()
  {
    var dir = CreateTempDir();
    WriteCsproj(dir);

    var locator = new CsprojLocator();
    var result = locator.FindCsproj(dir);

    Assert.EndsWith(".csproj", result);
    Assert.True(File.Exists(result));
  }

  [Fact]
  public void FindCsproj_WalksDown()
  {
    var root = CreateTempDir();
    var child = Path.Combine(root, "src", "App");
    Directory.CreateDirectory(child);
    var csproj = WriteCsproj(child);

    var locator = new CsprojLocator();
    var result = locator.FindCsproj(root);

    Assert.EndsWith(".csproj", result);
    Assert.Equal(csproj, result);
  }

  [Fact]
  public void FindCsproj_NotFound_ReturnsNull()
  {
    var isolated = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(isolated);
    _tempDirs.Add(isolated);

    var locator = new CsprojLocator();
    Assert.Null(locator.FindCsproj(isolated));
  }

  [Fact]
  public void FindCsprojWithElg_SelectsDeclaredProject()
  {
    var root = CreateTempDir();
    var a = Path.Combine(root, "A");
    var b = Path.Combine(root, "B");
    Directory.CreateDirectory(a);
    Directory.CreateDirectory(b);

    WriteNamedCsproj(a, "A.csproj", "<RootNamespace>A</RootNamespace>");
    var target = WriteNamedCsproj(b, "B.csproj", "<Elg>Properties/i18n/i18n.json</Elg>");

    var locator = new CsprojLocator();
    var result = locator.FindCsprojWithElg(root);

    Assert.Equal(target, result);
  }

  [Fact]
  public void ReadConfig_ElgTrue_UsesDefaultSource()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    Assert.EndsWith(Path.Combine("Properties", "i18n"), config.SourceDir);
  }

  [Fact]
  public void ReadConfig_NesterLangAbsent_UsesDefaultSource()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    Assert.EndsWith(Path.Combine("Properties", "i18n"), config.SourceDir);
  }

  [Fact]
  public void ReadConfig_NesterLangCustomPath_UsesCustomSource()
  {
    var dir = CreateTempDir();
    // Custom path is the path TO the i18n.json file, not a directory
    var csproj = WriteCsproj(dir, "<Elg>src/i18n/i18n.json</Elg>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    // sourceDir defaults to the directory containing the specified i18n.json
    Assert.EndsWith(Path.Combine("src", "i18n"), config.SourceDir);
  }

  [Fact]
  public void ReadConfig_I18nJson_OverridesNamespaceAndLocale()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Lang", "zh-CN", "Generated");
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    Assert.Equal("MyApp.Lang", config.Namespace);
    Assert.Equal("zh-CN", config.DefaultLocale);
    Assert.EndsWith("Generated", config.OutputDir);
  }

  [Fact]
  public void ReadConfig_NoProperties_UsesDefaults()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    Assert.Equal("en", config.DefaultLocale);
    Assert.Equal("MyApp.Properties", config.Namespace);
    Assert.EndsWith(Path.Combine("Properties", "i18n"), config.SourceDir);
    Assert.EndsWith("Properties", config.OutputDir);
  }

  [Fact]
  public void ReadConfig_I18nJsonAbsent_UsesAllDefaults()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<Elg>true</Elg><RootNamespace>Acme</RootNamespace>");

    var locator = new CsprojLocator();
    var config = locator.ReadConfig(csproj);

    Assert.Equal("en", config.DefaultLocale);
    Assert.Equal("Acme.Properties", config.Namespace);
  }

  [Fact]
  public void ReadConfig_ClassName_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", className: "Strings");
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("Strings", config.ClassName);
  }

  [Fact]
  public void ReadConfig_DefaultClassName_IsLang()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("Lang", config.ClassName);
  }

  [Fact]
  public void ReadConfig_ClassAndMemberVisibility_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", classVisibility: "internal", memberVisibility: "internal");
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("internal", config.ClassVisibility);
    Assert.Equal("internal", config.MemberVisibility);
  }

  [Fact]
  public void ReadConfig_PublicMembersOnInternalClass_Throws()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", classVisibility: "internal", memberVisibility: "public");
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    Assert.Throws<InvalidLangConfigException>(() => new CsprojLocator().ReadConfig(csproj));
  }

  [Fact]
  public void ReadConfig_GenerateXmlDoc_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", generateXmlDoc: true);
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.True(config.GenerateXmlDoc);
  }

  [Fact]
  public void ReadConfig_DefaultGenerateXmlDoc_IsFalse()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.False(config.GenerateXmlDoc);
  }

  [Fact]
  public void ReadConfig_GenerateFormatMethod_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", generateFormatMethod: false);
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.False(config.GenerateFormatMethod);
  }

  [Fact]
  public void ReadConfig_DefaultGenerateFormatMethod_IsTrue()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.True(config.GenerateFormatMethod);
  }

  [Fact]
  public void ReadConfig_PartialFileOptions_DefaultFalse()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.False(config.LocalesInPartialFile);
    Assert.False(config.SectionClassesInPartialFiles);
    Assert.Equal("Strings", config.SectionTypeSuffix);
  }

  [Fact]
  public void ReadConfig_PartialFileOptions_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", localesInPartialFile: true, sectionClassesInPartialFiles: true);
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.True(config.LocalesInPartialFile);
    Assert.True(config.SectionClassesInPartialFiles);
  }

  [Fact]
  public void ReadConfig_SectionTypeNaming_FromI18nJson()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    WriteI18nConfig(sourceDir, "MyApp.Properties", sectionTypeSuffix: "Section");
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("Section", config.SectionTypeSuffix);
  }

  [Fact]
  public void ReadConfig_GroupedFormat_IsRead()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "Properties", "i18n");
    Directory.CreateDirectory(sourceDir);
    File.WriteAllText(Path.Combine(sourceDir, "i18n.json"), """
      {
        "locale": { "default": "zh-CN" },
        "output": { "namespace": "MyApp.I18n", "dir": "Properties", "className": "Strings" },
        "types": { "suffix": "Lang", "classVisibility": "internal", "memberVisibility": "internal" },
        "codegen": { "xmlDoc": true, "formattingMethod": false, "localesPartial": true, "sectionsPartial": true }
      }
      """);
    var csproj = WriteCsproj(dir, "<Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("zh-CN", config.DefaultLocale);
    Assert.Equal("MyApp.I18n", config.Namespace);
    Assert.Equal("Strings", config.ClassName);
    Assert.Equal("Lang", config.SectionTypeSuffix);
    Assert.Equal("internal", config.ClassVisibility);
    Assert.Equal("internal", config.MemberVisibility);
    Assert.True(config.GenerateXmlDoc);
    Assert.False(config.GenerateFormatMethod);
    Assert.True(config.LocalesInPartialFile);
    Assert.True(config.SectionClassesInPartialFiles);
  }

  [Fact]
  public void ReadConfig_OutputDir_DefaultIsParentOfSourceDir()
  {
    var dir = CreateTempDir();
    // No i18n.json at all → config file doesn't exist → all defaults
    var csproj = WriteCsproj(dir, "<RootNamespace>MyApp</RootNamespace><Elg>true</Elg>");

    var config = new CsprojLocator().ReadConfig(csproj);

    // sourceDir = Properties\i18n, outputDir should be parent = Properties
    Assert.EndsWith("Properties", config.OutputDir);
    Assert.EndsWith(Path.Combine("Properties", "i18n"), config.SourceDir);
  }

  public void Dispose()
  {
    foreach (var dir in _tempDirs)
    {
      try
      { Directory.Delete(dir, recursive: true); }
      catch { /* best-effort cleanup */ }
    }
  }
}
