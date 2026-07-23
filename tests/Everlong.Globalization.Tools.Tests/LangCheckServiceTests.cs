namespace Everlong.Globalization.Tools.Tests;

public class LangCheckServiceTests : IDisposable
{
  private readonly List<string> _tempDirs = new();

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static string WriteCsproj(string dir, string sourceDir, string defaultLocale = "en")
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
    File.WriteAllText(Path.Combine(absSourceDir, "i18n.json"), $$"""
      {
        "locale": { "default": "{{defaultLocale}}" },
        "output": { "dir": "Generated", "namespace": "MyApp.Lang" },
        "types": { "classVisibility": "public" },
        "codegen": { "xmlDoc": false }
      }
      """);
    return path;
  }

  [Fact]
  public async Task AllConsistent_Returns_OkResults()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(frDir, "app.json"), """{ "Hello": "Bonjour", "Bye": "Au revoir" }""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangCheckService(new CsprojLocator(), new JsonLangReader());

    var results = await service.RunAsync(csproj);

    Assert.Single(results);
    Assert.Equal("fr", results[0].Locale);
    Assert.True(results[0].IsConsistent);
    Assert.Empty(results[0].MissingKeys);
    Assert.Empty(results[0].OrphanKeys);
  }

  [Fact]
  public async Task MissingKeys_Detected()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好" }""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangCheckService(new CsprojLocator(), new JsonLangReader());

    var results = await service.RunAsync(csproj);

    Assert.Single(results);
    var result = results[0];
    Assert.False(result.IsConsistent);
    Assert.Contains("Bye", result.MissingKeys);
    Assert.Empty(result.OrphanKeys);
  }

  [Fact]
  public async Task OrphanKeys_Detected()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好", "Extra": "Extra" }""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangCheckService(new CsprojLocator(), new JsonLangReader());

    var results = await service.RunAsync(csproj);

    Assert.Single(results);
    var result = results[0];
    Assert.False(result.IsConsistent);
    Assert.Empty(result.MissingKeys);
    Assert.Contains("Extra", result.OrphanKeys);
  }

  [Fact]
  public async Task FolderMode_ChecksAcrossFolders()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);

    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Title": "App" }""");
    File.WriteAllText(Path.Combine(enDir, "nav.json"), """{ "Home": "Home" }""");
    File.WriteAllText(Path.Combine(frDir, "app.json"), """{ "Title": "Application" }""");
    File.WriteAllText(Path.Combine(frDir, "nav.json"), """{ "Home": "Accueil" }""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangCheckService(new CsprojLocator(), new JsonLangReader());

    var results = await service.RunAsync(csproj);

    Assert.Single(results);
    Assert.Equal("fr", results[0].Locale);
    Assert.True(results[0].IsConsistent);
  }

  [Fact]
  public async Task DataOnly_FilesAreExcludedFromKeyComparison()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhTwDir = Path.Combine(sourceDir, "zh-TW");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhTwDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhTwDir, "app.json"), """{ "Hello": "你好", "Bye": "再見" }""");
    // Extension file: $DataOnly + $KeyPrefix — keys should be invisible to lang check
    File.WriteAllText(Path.Combine(zhTwDir, "_ext.json"), """
      {
        "$DataOnly": true,
        "$KeyPrefix": "Some.Lib",
        "Alert": { "Title": "提示", "ButtonText": "確定" }
      }
      """);

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangCheckService(new CsprojLocator(), new JsonLangReader());

    var results = await service.RunAsync(csproj);

    Assert.Single(results);
    var result = results[0];
    Assert.Equal("zh-TW", result.Locale);
    Assert.True(result.IsConsistent);
    Assert.Empty(result.MissingKeys);
    Assert.Empty(result.OrphanKeys);
  }

  public void Dispose()
  {
    foreach (var dir in _tempDirs)
    {
      try { Directory.Delete(dir, recursive: true); }
      catch { /* best-effort cleanup */ }
    }
  }
}
