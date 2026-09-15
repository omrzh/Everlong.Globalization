using System.Text.Json;
using System.Text.Json.Nodes;

namespace Everlong.Globalization.Tools.Tests;

/// <summary>Redirects <see cref="Console.Out" /> in places, so it runs in its own collection.</summary>
[Collection(ConsoleOutputCollection.Name)]
public class LangSyncServiceTests : IDisposable
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

  [Fact]
  public async Task MissingKeys_AreAdded_WithDefaultValue()
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
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(zhDir, "app.json")));
    var root = doc.RootElement;
    Assert.Equal("你好", root.GetProperty("Hello").GetString());
    Assert.Equal("Goodbye", root.GetProperty("Bye").GetString()); // filled from default
  }

  [Fact]
  public async Task ExistingTranslations_ArePreserved()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好", "Bye": "再见" }""");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(zhDir, "app.json")));
    var root = doc.RootElement;
    Assert.Equal("你好", root.GetProperty("Hello").GetString());
    Assert.Equal("再见", root.GetProperty("Bye").GetString());
  }

  [Fact]
  public async Task NestedMissingKeys_AreAdded()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Nav": { "Home": "Home", "About": "About" } }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Nav": { "Home": "首页" } }""");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(zhDir, "app.json")));
    var nav = doc.RootElement.GetProperty("Nav");
    Assert.Equal("首页", nav.GetProperty("Home").GetString());
    Assert.Equal("About", nav.GetProperty("About").GetString());
  }

  [Fact]
  public async Task OrphanKeys_ArePreserved()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好", "Extra": "额外" }""");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(zhDir, "app.json")));
    var root = doc.RootElement;
    Assert.Equal("你好", root.GetProperty("Hello").GetString());
    Assert.Equal("额外", root.GetProperty("Extra").GetString());
  }

  [Fact]
  public async Task KeyOrder_Reordered_ToMatchDefault()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "A": "A", "B": "B", "C": "C" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "C": "诶", "A": "啊", "B": "呗" }""");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = File.ReadAllText(Path.Combine(zhDir, "app.json"));
    var idxA = content.IndexOf("\"A\"");
    var idxB = content.IndexOf("\"B\"");
    var idxC = content.IndexOf("\"C\"");
    Assert.True(idxA < idxB, "A should appear before B after reorder");
    Assert.True(idxB < idxC, "B should appear before C after reorder");
  }

  [Fact]
  public async Task MissingTargetFile_IsCreated()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Title": "App" }""");
    // zh-CN dir exists but has no app.json

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var targetFile = Path.Combine(zhDir, "app.json");
    Assert.True(File.Exists(targetFile));
    using var doc = JsonDocument.Parse(File.ReadAllText(targetFile));
    Assert.Equal("App", doc.RootElement.GetProperty("Title").GetString());
  }

  /// <summary>
  ///   A file the tool creates is announced as created.  The empty-source corner is why: with nothing
  ///   to add and nothing to re-order, the old wording called a newly written file a normalization.
  /// </summary>
  [Fact]
  public async Task MissingTargetFile_IsAnnouncedAsCreated()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), "{}");

    var csproj = WriteCsproj(dir, "i18n");
    using var writer = new StringWriter();
    var oldOut = Console.Out;
    Console.SetOut(writer);
    try
    {
      await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);
    }
    finally
    {
      Console.SetOut(oldOut);
    }

    var output = writer.ToString();
    Assert.Contains("created", output);
    Assert.DoesNotContain("normalized", output);
    Assert.True(File.Exists(Path.Combine(zhDir, "app.json")));
  }

  /// <summary>
  ///   A target locale file whose root is not an object is a broken catalog, not an empty one.  Reading
  ///   it as empty is how <c>sync</c> would quietly overwrite whatever the file still holds.
  /// </summary>
  [Fact]
  public async Task TargetFileWithoutAnObjectRoot_IsRejected_NotOverwritten()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    var target = Path.Combine(frDir, "app.json");
    File.WriteAllText(target, "5");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangSyncService(new CsprojLocator(), new JsonLangReader());

    var ex = await Assert.ThrowsAsync<InvalidLocaleFileException>(
      () => service.RunAsync(null, csproj, TestContext.Current.CancellationToken));

    Assert.Contains("app.json", ex.Message);
    Assert.Equal("5", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task MalformedTargetFile_NamesTheFile()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(Path.Combine(frDir, "app.json"), """{ "Hello": "Bonjour""");

    var csproj = WriteCsproj(dir, "i18n");
    var service = new LangSyncService(new CsprojLocator(), new JsonLangReader());

    var ex = await Assert.ThrowsAsync<InvalidLocaleFileException>(
      () => service.RunAsync(null, csproj, TestContext.Current.CancellationToken));

    Assert.Contains("app.json", ex.Message);
    Assert.Contains(frDir, ex.Message);
  }

  [Fact]
  public async Task DataOnly_TargetFiles_AreLeftUntouched()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好" }""");
    var extOriginal = """{ "$DataOnly": true, "$KeyPrefix": "Lib", "Alert": "提示" }""";
    File.WriteAllText(Path.Combine(zhDir, "_ext.json"), extOriginal);

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    Assert.Equal(extOriginal, File.ReadAllText(Path.Combine(zhDir, "_ext.json")));
  }

  [Fact]
  public async Task SpecificLocale_SyncsOnlyThatLocale()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好" }""");
    File.WriteAllText(Path.Combine(frDir, "app.json"), """{ "Hello": "Bonjour" }""");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync("zh-CN", csproj, TestContext.Current.CancellationToken);

    Assert.Contains("Goodbye", File.ReadAllText(Path.Combine(zhDir, "app.json")));
    Assert.DoesNotContain("Goodbye", File.ReadAllText(Path.Combine(frDir, "app.json")));
  }

  [Fact]
  public void ReconstructInOrder_SkipsMetadataKeys_FromSource()
  {
    var source = new JsonObject
    {
      ["$KeyPrefix"] = JsonValue.Create("Foo"),
      ["Hello"] = JsonValue.Create("Hello")
    };
    var target = new JsonObject { ["Hello"] = JsonValue.Create("你好") };

    var (result, added) = LangSyncService.ReconstructInOrder(source, target);

    Assert.Equal(0, added);
    Assert.False(result.ContainsKey("$KeyPrefix"));
    Assert.Equal("你好", result["Hello"]!.GetValue<string>());
  }

  [Fact]
  public void ReconstructInOrder_PreservesOrphan_MetadataInTarget()
  {
    var source = new JsonObject { ["Hello"] = JsonValue.Create("Hello") };
    var target = new JsonObject
    {
      ["$KeyPrefix"] = JsonValue.Create("Bar"),
      ["Hello"] = JsonValue.Create("你好")
    };

    var (result, _) = LangSyncService.ReconstructInOrder(source, target);

    Assert.True(result.ContainsKey("$KeyPrefix"));
    Assert.Equal("你好", result["Hello"]!.GetValue<string>());
  }

  [Fact]
  public void WriteOptions_DoesNotEscapeNonAsciiCharacters()
  {
    // Arrange: source with Chinese characters
    var source = new JsonObject
    {
      ["Title"] = JsonValue.Create("互動實驗室"),
      ["Desc"] = JsonValue.Create("集中展示 Dialog 與通知。")
    };
    var target = new JsonObject();

    // Act
    var (result, _) = LangSyncService.ReconstructInOrder(source, target);
    var opts = new System.Text.Json.JsonSerializerOptions
    {
      WriteIndented = true,
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    var json = result.ToJsonString(opts);

    // Assert: real characters, not \uXXXX escapes
    Assert.Contains("互動實驗室", json);
    Assert.Contains("集中展示", json);
    Assert.DoesNotContain(@"\u", json);
  }

  [Fact]
  public async Task LineEnding_Lf_SyncedFileHasLfOnly()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好" }""");

    var csproj = WriteCsproj(dir, "i18n", lineEnding: "lf");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = await File.ReadAllTextAsync(Path.Combine(zhDir, "app.json"), TestContext.Current.CancellationToken);
    Assert.Contains("Goodbye", content); // the file was rewritten, not skipped
    LineEndingAssertions.AssertOnly(content, "\n");
  }

  [Fact]
  public async Task LineEnding_NotConfigured_SyncedFileUsesLfOnly()
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
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = await File.ReadAllTextAsync(Path.Combine(zhDir, "app.json"), TestContext.Current.CancellationToken);
    LineEndingAssertions.AssertOnly(content, "\n");
  }

  [Fact]
  public async Task LineEnding_Platform_SyncedFileUsesPlatformNewLines()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello", "Bye": "Goodbye" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "你好" }""");

    var csproj = WriteCsproj(dir, "i18n", lineEnding: "platform");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = await File.ReadAllTextAsync(Path.Combine(zhDir, "app.json"), TestContext.Current.CancellationToken);
    LineEndingAssertions.AssertOnly(content, Environment.NewLine);
  }

  /// <summary>
  ///   A file whose keys are already in sync is still rewritten when its bytes are not the ones the
  ///   option asks for: <c>gen</c> owns the ending of everything it writes, and a <c>sync</c> that
  ///   skipped this difference would leave one tree with two answers.
  /// </summary>
  [Fact]
  public async Task LineEnding_Lf_NormalizesAnInSyncFileThatIsCrlf()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(
      Path.Combine(zhDir, "app.json"),
      "{\r\n  \"Hello\": \"你好\"\r\n}");

    var csproj = WriteCsproj(dir, "i18n");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = await File.ReadAllTextAsync(Path.Combine(zhDir, "app.json"), TestContext.Current.CancellationToken);
    Assert.Contains("你好", content); // the translation survived the rewrite
    LineEndingAssertions.AssertOnly(content, "\n");
  }

  /// <summary>
  ///   The other half of the same rule: a file that already holds the keys and the configured ending
  ///   is left alone, so a second <c>sync</c> over an untouched repository writes nothing.
  /// </summary>
  [Fact]
  public async Task LineEnding_Lf_LeavesAnInSyncFileUntouched()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var zhDir = Path.Combine(sourceDir, "zh-CN");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(zhDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");
    File.WriteAllText(Path.Combine(zhDir, "app.json"), """{ "Hello": "\u4f60\u597d" }""");

    var csproj = WriteCsproj(dir, "i18n");
    using var writer = new StringWriter();
    var oldOut = Console.Out;
    Console.SetOut(writer);
    try
    {
      await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);
    }
    finally
    {
      Console.SetOut(oldOut);
    }

    Assert.DoesNotContain("app.json", writer.ToString());
  }

  [Fact]
  public async Task LineEnding_Lf_AdoptFileIsCreatedWithLfOnly()
  {
    var dir = CreateTempDir();
    var sourceDir = Path.Combine(dir, "i18n");
    var enDir = Path.Combine(sourceDir, "en");
    var frDir = Path.Combine(sourceDir, "fr");
    Directory.CreateDirectory(enDir);
    Directory.CreateDirectory(frDir);
    File.WriteAllText(Path.Combine(enDir, "app.json"), """{ "Hello": "Hello" }""");

    var csproj = WriteCsproj(dir, "i18n", lineEnding: "lf");
    await new LangSyncService(new CsprojLocator(), new JsonLangReader()).RunAsync(null, csproj, TestContext.Current.CancellationToken);

    var content = await File.ReadAllTextAsync(Path.Combine(frDir, "app.json"), TestContext.Current.CancellationToken);
    LineEndingAssertions.AssertOnly(content, "\n");
  }

  public void Dispose()
  {
    foreach (var d in _tempDirs)
    {
      try
      { Directory.Delete(d, recursive: true); }
      catch { /* best-effort */ }
    }
  }
}
