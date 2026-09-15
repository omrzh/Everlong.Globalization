namespace Everlong.Globalization.Tools.Tests;

public class LangInitServiceTests : IDisposable
{
  private readonly List<string> _tempDirs = new();

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static string WriteCsproj(string dir)
  {
    var path = Path.Combine(dir, "Test.csproj");
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
  public async Task Init_CreatesLocaleDir_AndAppJson()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir);
    var service = new LangInitService(new CsprojLocator());

    await service.RunAsync(csproj, "en", TestContext.Current.CancellationToken);

    var i18nDir = Path.Combine(dir, "Properties", "i18n");
    var appJson = Path.Combine(i18nDir, "en", "App.json");
    Assert.True(Directory.Exists(i18nDir));
    Assert.True(File.Exists(appJson));
    var content = await File.ReadAllTextAsync(appJson, TestContext.Current.CancellationToken);
    Assert.Contains("Title", content);
  }

  [Fact]
  public async Task Init_CreatesI18nJsonWithConfig()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir);
    var service = new LangInitService(new CsprojLocator());

    await service.RunAsync(csproj, "en", TestContext.Current.CancellationToken);

    var configFile = Path.Combine(dir, "Properties", "i18n", "i18n.jsonc");
    Assert.True(File.Exists(configFile));
    var content = await File.ReadAllTextAsync(configFile, TestContext.Current.CancellationToken);
    Assert.Contains("namespace", content);
    Assert.Contains("MyApp.Properties", content);
    Assert.Contains("locale", content);
    Assert.Contains("\"en\"", content);
    Assert.Contains("output", content);
    Assert.Contains("types", content);
    Assert.Contains("codegen", content);
    Assert.Contains("suffix", content);
    Assert.Contains("\"Strings\"", content);
  }

  [Fact]
  public void BuildFullTemplate_ContainsAllKeys()
  {
    var template = LangInitService.BuildFullTemplate("MyApp.Properties", "zh-CN");

    Assert.Contains("\"locale\"", template);
    Assert.Contains("\"default\"", template);
    Assert.Contains("zh-CN", template);
    Assert.Contains("\"output\"", template);
    Assert.Contains("\"namespace\"", template);
    Assert.Contains("MyApp.Properties", template);
    Assert.Contains("\"className\"", template);
    Assert.Contains("\"Lang\"", template);
    Assert.Contains("\"types\"", template);
    Assert.Contains("\"classVisibility\"", template);
    Assert.Contains("\"memberVisibility\"", template);
    Assert.Contains("\"localesVisibility\"", template);
    Assert.Contains("\"suffix\"", template);
    Assert.Contains("\"Strings\"", template);
    Assert.Contains("\"codegen\"", template);
    Assert.Contains("\"xmlDoc\"", template);
    Assert.Contains("\"formattingMethod\"", template);
    Assert.Contains("\"localesPartial\"", template);
    Assert.Contains("\"sectionsPartial\"", template);
    Assert.Contains("\"lineEnding\"", template);
    Assert.Contains("\"platform\"", template);
    Assert.Contains("true", template);
    Assert.Contains("false", template);
  }

  [Fact]
  public async Task Init_TemplateConfig_IsReadableByTheLocator()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir);
    var service = new LangInitService(new CsprojLocator());

    await service.RunAsync(csproj, "en", TestContext.Current.CancellationToken);

    // The template ships every key, so it must survive the parser the tool itself uses.
    var config = new CsprojLocator().ReadConfig(csproj);

    Assert.Equal("MyApp.Properties", config.Namespace);
    Assert.Equal("en", config.DefaultLocale);
    Assert.Equal("platform", config.LineEnding);
  }

  [Fact]
  public async Task AlreadyExists_DoesNotOverwrite()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir);
    var i18nDir = Path.Combine(dir, "Properties", "i18n");
    Directory.CreateDirectory(i18nDir);
    var sentinel = Path.Combine(i18nDir, "existing.txt");
    File.WriteAllText(sentinel, "kept");

    var service = new LangInitService(new CsprojLocator());
    await service.RunAsync(csproj, "en", TestContext.Current.CancellationToken);

    Assert.True(File.Exists(sentinel));
    Assert.False(Directory.Exists(Path.Combine(i18nDir, "en")));
  }

  [Fact]
  public async Task CustomLocale_CreatesNamedDir()
  {
    var dir = CreateTempDir();
    var csproj = WriteCsproj(dir);
    var service = new LangInitService(new CsprojLocator());

    await service.RunAsync(csproj, "zh-CN", TestContext.Current.CancellationToken);

    Assert.True(Directory.Exists(Path.Combine(dir, "Properties", "i18n", "zh-CN")));
    Assert.True(File.Exists(Path.Combine(dir, "Properties", "i18n", "zh-CN", "App.json")));
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
