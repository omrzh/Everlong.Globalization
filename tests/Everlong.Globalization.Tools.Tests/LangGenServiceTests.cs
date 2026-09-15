namespace Everlong.Globalization.Tools.Tests;

public class LangGenServiceTests : IDisposable
{
  private readonly List<string> _tempDirs = new();

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static string WriteCsproj(
    string dir,
    string sourceDir,
    string output,
    string ns,
    string defaultLocale = "en",
    bool localesPartial = false,
    bool sectionsPartial = false,
    string? sectionTypeSuffix = null,
    string? lineEnding = null)
  {
    var i18nJsonRelPath = Path.Combine(sourceDir, "i18n.json");
    var path = Path.Combine(dir, "Test.csproj");
    File.WriteAllText(path, $"""
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          <Elg>{i18nJsonRelPath}</Elg>
        </PropertyGroup>
      </Project>
      """);
    var absSourceDir = Path.GetFullPath(Path.Combine(dir, sourceDir));
    Directory.CreateDirectory(absSourceDir);
    var lineEndingOption = lineEnding is null ? "" : $", \"lineEnding\": \"{lineEnding}\"";
    File.WriteAllText(Path.Combine(absSourceDir, "i18n.json"), $$"""
      {
        "locale": { "default": "{{defaultLocale}}" },
        "output": { "dir": "{{output}}", "namespace": "{{ns}}"{{lineEndingOption}} },
        "types": { "classVisibility": "public", "suffix": "{{sectionTypeSuffix ?? "Strings"}}" },
        "codegen": {
          "xmlDoc": false,
          "localesPartial": {{(localesPartial ? "true" : "false")}},
          "sectionsPartial": {{(sectionsPartial ? "true" : "false")}}
        }
      }
      """);
    return path;
  }

  private static string WriteCsprojWithoutElg(string dir)
  {
    var path = Path.Combine(dir, "NoNesterLang.csproj");
    File.WriteAllText(path, """
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          <RootNamespace>MyApp</RootNamespace>
        </PropertyGroup>
      </Project>
      """);
    return path;
  }

  [Fact]
  public async Task FolderMode_GeneratesPerModuleProperties()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);

    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      { "Title": "My App" }
      """);
    File.WriteAllText(Path.Combine(localeDir, "nav.json"), """
      { "Home": "Home Page" }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    await service.RunAsync(csproj);

    var outputPath = Path.Combine(dir, "Generated", "Lang.g.cs");
    Assert.True(File.Exists(outputPath));
    var content = await File.ReadAllTextAsync(outputPath);
    Assert.Contains("public static AppStrings App", content);
    Assert.Contains("public static NavStrings Nav", content);
    Assert.Contains("class AppStrings", content);
    Assert.Contains("class NavStrings", content);
    Assert.Contains("GetString(\"Title\",", content);
    Assert.Contains("GetString(\"Home\",", content);
  }

  [Fact]
  public async Task FolderMode_MultiLocale_EmbedsBothLocales()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Title": "My App" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Title": "我的应用" }""");

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    Assert.Contains("Locales.En", content);
    Assert.Contains("IStringProvider ZhCn", content);
    Assert.Contains("\"My App\"", content);
    Assert.Contains("\"我的应用\"", content);
    Assert.Contains("class AppStrings", content);
    Assert.Contains("StringSectionBase", content);
  }

  [Fact]
  public async Task FolderMode_AutoPrefixFromFilename()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);

    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      { "Title": "My App" }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    Assert.Contains("\"App.Title\"", content);
    Assert.Contains("GetString(\"Title\",", content);
    Assert.DoesNotContain("AppStringsStrings", content);
  }

  [Fact]
  public async Task FolderMode_KeyPrefixOverride_UsedInDict()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);

    File.WriteAllText(Path.Combine(localeDir, "Nester.json"), """
      {
        "$KeyPrefix": "Everlong.Nester",
        "Dialog": { "Title": "Alert" }
      }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    Assert.Contains("\"Everlong.Nester.Dialog.Title\"", content);
    Assert.DoesNotContain("\"Nester.Dialog.Title\"", content);
  }

  [Fact]
  public async Task FolderMode_DataOnly_NoClassEmitted()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var neutralDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(neutralDir);
    Directory.CreateDirectory(zhDir);

    File.WriteAllText(Path.Combine(neutralDir, "app.json"), """{ "Title": "My App" }""");
    File.WriteAllText(Path.Combine(neutralDir, "nester.json"), """
      {
        "$KeyPrefix": "Everlong.Nester",
        "$DataOnly": true,
        "Dialog": { "Title": "Alert" }
      }
      """);
    File.WriteAllText(Path.Combine(zhDir, "nester.json"), """
      {
        "$KeyPrefix": "Everlong.Nester",
        "$DataOnly": true,
        "Dialog": { "Title": "提示" }
      }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    Assert.DoesNotContain("class LangNester", content);
    Assert.DoesNotContain("public static LangNester Nester", content);
    Assert.Contains("\"Everlong.Nester.Dialog.Title\"", content);
    Assert.Contains("\"提示\"", content);
  }

  [Fact]
  public async Task FolderMode_GeneratedFile_IsReadOnly()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """{ "Title": "App" }""");

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var outputPath = Path.Combine(dir, "Generated", "Lang.g.cs");
    var attrs = File.GetAttributes(outputPath);
    Assert.True(attrs.HasFlag(FileAttributes.ReadOnly));
  }

  [Fact]
  public async Task FolderMode_Regen_ClearsReadOnlyAndOverwrites()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """{ "Title": "App" }""");

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj); // first gen
    await service.RunAsync(csproj); // second gen — must not throw

    var outputPath = Path.Combine(dir, "Generated", "Lang.g.cs");
    Assert.True(File.Exists(outputPath));
    Assert.True(File.GetAttributes(outputPath).HasFlag(FileAttributes.ReadOnly));
  }

  [Fact]
  public async Task FolderMode_PartialFileOptions_GeneratesSplitFiles()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """{ "Title": "App" }""");

    var csproj = WriteCsproj(
      dir,
      "i18n",
      "Generated",
      "MyApp.Lang",
      localesPartial: true,
      sectionsPartial: true);
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var mainPath = Path.Combine(dir, "Generated", "Lang.g.cs");
    var localesPath = Path.Combine(dir, "Generated", "Lang.Locales.g.cs");
    var appPath = Path.Combine(dir, "Generated", "Lang.App.g.cs");

    Assert.True(File.Exists(mainPath));
    Assert.True(File.Exists(localesPath));
    Assert.True(File.Exists(appPath));

    var main = await File.ReadAllTextAsync(mainPath);
    var locales = await File.ReadAllTextAsync(localesPath);
    var app = await File.ReadAllTextAsync(appPath);

    Assert.DoesNotContain("static class Locales", main);
    Assert.Contains("static class Locales", locales);
    Assert.Contains("class AppStrings", app);
  }

  [Fact]
  public async Task FolderMode_PartialFileOptions_Disabled_RemovesStaleSplitFiles()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """{ "Title": "App" }""");

    var csproj = WriteCsproj(
      dir,
      "i18n",
      "Generated",
      "MyApp.Lang",
      localesPartial: true,
      sectionsPartial: true);
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var splitLocalesPath = Path.Combine(dir, "Generated", "Lang.Locales.g.cs");
    var splitSectionPath = Path.Combine(dir, "Generated", "Lang.App.g.cs");
    Assert.True(File.Exists(splitLocalesPath));
    Assert.True(File.Exists(splitSectionPath));

    var i18nPath = Path.Combine(sourceDir, "i18n.json");
    File.WriteAllText(i18nPath, """
      {
        "locale": { "default": "en" },
        "output": { "dir": "Generated", "namespace": "MyApp.Lang" },
        "types": { "classVisibility": "public" },
        "codegen": { "xmlDoc": false, "localesPartial": false, "sectionsPartial": false }
      }
      """);

    await service.RunAsync(csproj);

    Assert.False(File.Exists(splitLocalesPath));
    Assert.False(File.Exists(splitSectionPath));
    Assert.True(File.Exists(Path.Combine(dir, "Generated", "Lang.g.cs")));
  }

  [Fact]
  public async Task FolderMode_SectionTypeSuffix_ChangesGeneratedTypeNames()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var localeDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      {
        "Dialog": {
          "Title": "Alert"
        }
      }
      """);

    var csproj = WriteCsproj(
      dir,
      "i18n",
      "Generated",
      "MyApp.Lang",
      sectionTypeSuffix: "Section");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    Assert.Contains("public static AppSection App", content);
    Assert.Contains("sealed class AppSection", content);
    Assert.Contains("public AppDialogSection Dialog", content);
    Assert.Contains("sealed class AppDialogSection", content);
  }

  [Fact]
  public async Task LineEnding_Lf_WritesLfOnly()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      { "Title": "My App", "Nav": { "Home": "Home" } }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang", lineEnding: "lf");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    LineEndingAssertions.AssertOnly(content, "\n");
  }

  [Fact]
  public async Task LineEnding_Crlf_WritesCrlfOnly()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      { "Title": "My App", "Nav": { "Home": "Home" } }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang", lineEnding: "crlf");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    LineEndingAssertions.AssertOnly(content, "\r\n");
  }

  [Fact]
  public async Task LineEnding_NotConfigured_WritesPlatformNewLines()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    Directory.CreateDirectory(localeDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """
      { "Title": "My App", "Nav": { "Home": "Home" } }
      """);

    var csproj = WriteCsproj(dir, "i18n", "Generated", "MyApp.Lang");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var content = await File.ReadAllTextAsync(Path.Combine(dir, "Generated", "Lang.g.cs"));
    LineEndingAssertions.AssertOnly(content, Environment.NewLine);
  }

  [Fact]
  public async Task LineEnding_Lf_AppliesToEveryGeneratedFile()
  {
    var dir = CreateTempDir();
    var localeDir = Path.Combine(dir, "i18n", "en");
    var zhDir = Path.Combine(dir, "i18n", "zh-CN");
    Directory.CreateDirectory(localeDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(localeDir, "app.json"), """{ "Title": "My App" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Title": "我的应用" }""");

    var csproj = WriteCsproj(
      dir,
      "i18n",
      "Generated",
      "MyApp.Lang",
      localesPartial: true,
      sectionsPartial: true,
      lineEnding: "lf");
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    await service.RunAsync(csproj);

    var generatedDir = Path.Combine(dir, "Generated");
    var files = Directory.GetFiles(generatedDir, "*.g.cs");
    Assert.Equal(3, files.Length); // main + Locales + per-module section
    foreach (var file in files)
      LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(file), "\n");
  }

  [Fact]
  public async Task ProjectWithoutNesterLang_DoesNotThrow_AndPrintsMessage()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsprojWithoutElg(dir);
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    using var sw = new StringWriter();
    var oldOut = Console.Out;
    Console.SetOut(sw);
    try
    {
      await service.RunAsync(csproj);
    }
    finally
    {
      Console.SetOut(oldOut);
    }

    Assert.Contains("does not declare <Elg>", sw.ToString());
    Assert.False(File.Exists(Path.Combine(dir, "Properties", "Lang.g.cs")));
  }

  [Fact]
  public async Task NoProjectFound_DoesNotThrow_AndPrintsMessage()
  {
    var dir = CreateTempDir();
    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    using var sw = new StringWriter();
    var oldOut = Console.Out;
    var oldCwd = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(dir);
    Console.SetOut(sw);
    try
    {
      await service.RunAsync(projectOverride: null);
    }
    finally
    {
      Console.SetOut(oldOut);
      Directory.SetCurrentDirectory(oldCwd);
    }

    Assert.Contains("No .csproj file found walking down from", sw.ToString());
  }

  [Fact]
  public async Task AutoLocate_MultipleElgProjects_GeneratesForAll()
  {
    var root = CreateTempDir();

    var aDir = Path.Combine(root, "A");
    var bDir = Path.Combine(root, "B");
    Directory.CreateDirectory(aDir);
    Directory.CreateDirectory(bDir);

    _ = WriteCsproj(aDir, "i18n", "Generated", "AppA.Lang");
    _ = WriteCsproj(bDir, "i18n", "Generated", "AppB.Lang");

    Directory.CreateDirectory(Path.Combine(aDir, "i18n", "en"));
    Directory.CreateDirectory(Path.Combine(bDir, "i18n", "en"));
    File.WriteAllText(Path.Combine(aDir, "i18n", "en", "app.json"), """{ "Title": "App A" }""");
    File.WriteAllText(Path.Combine(bDir, "i18n", "en", "app.json"), """{ "Title": "App B" }""");

    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

    var oldCwd = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(root);
    try
    {
      await service.RunAsync(projectOverride: null);
    }
    finally
    {
      Directory.SetCurrentDirectory(oldCwd);
    }

    Assert.True(File.Exists(Path.Combine(aDir, "Generated", "Lang.g.cs")));
    Assert.True(File.Exists(Path.Combine(bDir, "Generated", "Lang.g.cs")));
  }

  [Fact]
  public async Task AutoLocate_WhenOneProjectInvalid_ContinuesOtherProjects()
  {
    var root = CreateTempDir();

    var okDir = Path.Combine(root, "A");
    var invalidDir = Path.Combine(root, "B");
    Directory.CreateDirectory(okDir);
    Directory.CreateDirectory(invalidDir);

    _ = WriteCsproj(okDir, "i18n", "Generated", "AppA.Lang");

    // <Elg>true</Elg> -> default source is Properties\i18n; keep missing on purpose.
    File.WriteAllText(Path.Combine(invalidDir, "B.csproj"), """
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          <Elg>true</Elg>
        </PropertyGroup>
      </Project>
      """);

    Directory.CreateDirectory(Path.Combine(okDir, "i18n", "en"));
    File.WriteAllText(Path.Combine(okDir, "i18n", "en", "app.json"), """{ "Title": "App A" }""");

    var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
    using var sw = new StringWriter();
    var oldOut = Console.Out;
    var oldCwd = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(root);
    Console.SetOut(sw);
    try
    {
      await service.RunAsync(projectOverride: null);
    }
    finally
    {
      Console.SetOut(oldOut);
      Directory.SetCurrentDirectory(oldCwd);
    }

    Assert.True(File.Exists(Path.Combine(okDir, "Generated", "Lang.g.cs")));
    Assert.Contains("Skipped:", sw.ToString());
    Assert.Contains(Path.Combine(invalidDir, "B.csproj"), sw.ToString());
  }

  public void Dispose()
  {
    foreach (var dir in _tempDirs)
    {
      try
      {
        // Clear ReadOnly on generated files so Directory.Delete succeeds
        foreach (var f in Directory.GetFiles(dir, "*.g.cs", SearchOption.AllDirectories))
        {
          var attrs = File.GetAttributes(f);
          if (attrs.HasFlag(FileAttributes.ReadOnly))
            File.SetAttributes(f, attrs & ~FileAttributes.ReadOnly);
        }
        Directory.Delete(dir, recursive: true);
      }
      catch { /* best-effort cleanup */ }
    }
  }
}
