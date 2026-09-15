namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   <c>normalize</c> is the one-shot migration for <c>output.lineEnding</c>: it rewrites every catalog
///   the tool owns, default locale included, because no other command writes that directory.
/// </summary>
[Collection(ProcessWideStateCollection.Name)]
public class LangNormalizeServiceTests : IDisposable
{
  private readonly List<string> _tempDirs = new();

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static string WriteCsproj(string dir, string sourceDir, string defaultLocale = "en", string? lineEnding = null)
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
        "output": { "dir": "Generated", "namespace": "MyApp.Lang"{{lineEndingOption}} },
        "types": { "classVisibility": "public" },
        "codegen": { "xmlDoc": false }
      }
      """);
    return path;
  }

  private static string WriteCatalog(string dir, string locale, string fileName, string content)
  {
    var localeDir = Path.Combine(dir, "i18n", locale);
    Directory.CreateDirectory(localeDir);
    var path = Path.Combine(localeDir, fileName);
    File.WriteAllText(path, content);
    return path;
  }

  [Fact]
  public async Task NormalizesTheWholeTree_IncludingTheDefaultLocale()
  {
    var dir = CreateTempDir();
    var en = WriteCatalog(dir, "en", "app.json", "{\r\n  \"Title\": \"App\"\r\n}");
    var fr = WriteCatalog(dir, "fr", "app.json", "{\r\n  \"Title\": \"Appli\"\r\n}");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());

    var normalized = await service.RunAsync(csproj, TestContext.Current.CancellationToken);

    Assert.Equal(2, normalized);
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(en, TestContext.Current.CancellationToken), "\n");
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(fr, TestContext.Current.CancellationToken), "\n");
  }

  [Fact]
  public async Task DeclaredCrlf_ConvertsLfCatalogs()
  {
    var dir = CreateTempDir();
    var en = WriteCatalog(dir, "en", "app.json", "{\n  \"Title\": \"App\"\n}");

    var csproj = WriteCsproj(dir, "i18n", lineEnding: "crlf");
    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());

    var normalized = await service.RunAsync(csproj, TestContext.Current.CancellationToken);

    Assert.Equal(1, normalized);
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(en, TestContext.Current.CancellationToken), "\r\n");
  }

  [Fact]
  public async Task AlreadyNormalizedCatalogs_AreLeftAlone()
  {
    var dir = CreateTempDir();
    var en = WriteCatalog(dir, "en", "app.json", "{\n  \"Title\": \"App\"\n}");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());

    var normalized = await service.RunAsync(csproj, TestContext.Current.CancellationToken);

    Assert.Equal(0, normalized);
    Assert.Contains("\"Title\"", await File.ReadAllTextAsync(en, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task DataOnlyCatalogs_AreNormalizedToo()
  {
    // $DataOnly says "contribute keys, generate no class" — the file is still content the repository
    // owns, so it gets the declared ending like any other.
    var dir = CreateTempDir();
    var shared = WriteCatalog(dir, "en", "shared.json", "{\r\n  \"$DataOnly\": true,\r\n  \"Title\": \"App\"\r\n}");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());

    var normalized = await service.RunAsync(csproj, TestContext.Current.CancellationToken);

    Assert.Equal(1, normalized);
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(shared, TestContext.Current.CancellationToken), "\n");
  }

  [Fact]
  public async Task BrokenCatalog_NamesTheFile_AndWritesNothing()
  {
    var dir = CreateTempDir();
    var good = WriteCatalog(dir, "en", "app.json", "{\r\n  \"Title\": \"App\"\r\n}");
    var broken = WriteCatalog(dir, "fr", "app.json", "{\r\n  \"Title\": \"Appli\"");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());

    var ex = await Assert.ThrowsAsync<InvalidLocaleFileException>(
      () => service.RunAsync(csproj, TestContext.Current.CancellationToken));

    Assert.Contains("app.json", ex.Message);
    Assert.Contains(Path.Combine(dir, "i18n", "fr"), ex.Message);
    // Nothing was written: the run validates the whole tree before it rewrites any of it, so a broken
    // catalog leaves the repository unconverted rather than half converted.
    Assert.Contains("\r\n", await File.ReadAllTextAsync(good, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task EveryProjectDeclaringElg_IsNormalized()
  {
    var root = CreateTempDir();
    var appDir = Path.Combine(root, "App");
    var libDir = Path.Combine(root, "Lib");
    Directory.CreateDirectory(appDir);
    Directory.CreateDirectory(libDir);
    var app = WriteCatalog(appDir, "en", "app.json", "{\r\n  \"Title\": \"App\"\r\n}");
    var lib = WriteCatalog(libDir, "en", "app.json", "{\r\n  \"Title\": \"Lib\"\r\n}");
    WriteCsproj(appDir, "i18n");
    WriteCsproj(libDir, "i18n");

    var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());
    var previous = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(root);
    int normalized;
    try
    {
      normalized = await service.RunAsync(null, TestContext.Current.CancellationToken);
    }
    finally
    {
      Directory.SetCurrentDirectory(previous);
    }

    Assert.Equal(2, normalized);
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(app, TestContext.Current.CancellationToken), "\n");
    LineEndingAssertions.AssertOnly(await File.ReadAllTextAsync(lib, TestContext.Current.CancellationToken), "\n");
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
