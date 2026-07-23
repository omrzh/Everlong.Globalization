namespace Everlong.Globalization.Tools.Tests;

public class LangAddServiceTests : IDisposable
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
  public async Task FolderMode_CreatesLocaleFolder()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(enDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Title": "App" }""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangAddService(new CsprojLocator());

    await service.RunAsync("fr", csproj, force: false);

    var frDir = Path.Combine(sourceDir, "fr");
    Assert.True(Directory.Exists(frDir));
    Assert.True(File.Exists(Path.Combine(frDir, "app.json")));
  }

  [Fact]
  public async Task FolderMode_CopiesAllModuleFiles()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    Directory.CreateDirectory(enDir);
    var appContent = """{ "Title": "App" }""";
    var navContent = """{ "Home": "Home" }""";
    File.WriteAllText(Path.Combine(enDir, "app.json"), appContent);
    File.WriteAllText(Path.Combine(enDir, "nav.json"), navContent);

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangAddService(new CsprojLocator());

    await service.RunAsync("zh-CN", csproj, force: false);

    Assert.Equal(appContent, await File.ReadAllTextAsync(Path.Combine(sourceDir, "zh-CN", "app.json")));
    Assert.Equal(navContent, await File.ReadAllTextAsync(Path.Combine(sourceDir, "zh-CN", "nav.json")));
  }

  [Fact]
  public async Task FolderMode_ExistingFolder_Throws()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), "{}");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangAddService(new CsprojLocator());

    await Assert.ThrowsAsync<InvalidOperationException>(
      () => service.RunAsync("fr", csproj, force: false));
  }

  [Fact]
  public async Task FolderMode_Force_Overwrites()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    var newContent = """{ "Title": "Application" }""";
    File.WriteAllText(Path.Combine(enDir, "app.json"), newContent);
    File.WriteAllText(Path.Combine(frDir, "app.json"), "{}");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangAddService(new CsprojLocator());

    await service.RunAsync("fr", csproj, force: true);

    Assert.Equal(newContent, await File.ReadAllTextAsync(Path.Combine(frDir, "app.json")));
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
