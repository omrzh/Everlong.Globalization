using System.Text.Json;
using System.Text.RegularExpressions;

namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   Pins the option table (<see cref="LangOptions" />) to the surfaces that are supposed to be
///   derived from it: <c>init</c>'s scaffold and the config example the docs show.  A key that exists
///   on only one side is exactly the drift this file exists to catch — the README's example had
///   drifted a whole group behind the parser before the table existed.
/// </summary>
public class LangOptionsTests
{
  private static readonly Regex JsoncFence = new(@"```jsonc\r?\n(.*?)```", RegexOptions.Singleline);
  private static readonly Regex JsonKey = new("^\\s*\"(?<name>[A-Za-z][A-Za-z0-9]*)\"\\s*:", RegexOptions.Multiline);

  /// <summary>Every option, as the dotted path a config error names.</summary>
  private static SortedSet<string> TableKeys()
    => new(LangOptions.All.Select(option => option.Path), StringComparer.Ordinal);

  [Fact]
  public void Scaffold_CarriesEveryOption_AtItsOwnDefault()
  {
    var scaffold = LangInitService.BuildFullTemplate("MyApp.Properties", "zh-CN");
    using var document = JsonDocument.Parse(scaffold, new JsonDocumentOptions
    {
      CommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    });

    var root = document.RootElement;

    // Groups and keys, in table order: a key the table gains appears here, one it drops disappears.
    Assert.Equal(LangOptions.Groups.Select(g => g.Name), root.EnumerateObject().Select(g => g.Name));
    foreach (var group in LangOptions.Groups)
    {
      Assert.Equal(
        LangOptions.InGroup(group.Name).Select(option => option.Name),
        root.GetProperty(group.Name).EnumerateObject().Select(key => key.Name));
    }

    // The three values the table cannot state on its own come from the caller.
    Assert.Equal("zh-CN", root.GetProperty("locale").GetProperty("default").GetString());
    Assert.Equal("Properties/i18n", root.GetProperty("locale").GetProperty("sourceDir").GetString());
    Assert.Equal("MyApp.Properties", root.GetProperty("output").GetProperty("namespace").GetString());
  }

  [Fact]
  public void Scaffold_ShowsEachOptionAsTheJsonKindItAccepts()
  {
    var scaffold = LangInitService.BuildFullTemplate("MyApp.Properties", "en");
    using var document = JsonDocument.Parse(scaffold, new JsonDocumentOptions
    {
      CommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    });

    foreach (var option in LangOptions.All)
    {
      var value = document.RootElement.GetProperty(option.Group).GetProperty(option.Name);
      switch (option.Kind)
      {
        case LangOptionKind.Boolean:
          Assert.True(value.ValueKind is JsonValueKind.True or JsonValueKind.False);
          break;
        case LangOptionKind.StringArray:
          Assert.Equal(JsonValueKind.Array, value.ValueKind);
          break;
        default:
          Assert.Equal(JsonValueKind.String, value.ValueKind);
          break;
      }
    }
  }

  [Fact]
  public void ReadmeConfigExample_ListsExactlyTheOptionsTheReaderAccepts()
  {
    var paths = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var block in JsoncBlocks(Path.Combine(RepoRoot(), "README.md")))
    {
      foreach (var group in block.EnumerateObject())
      {
        foreach (var key in group.Value.EnumerateObject())
          paths.Add($"{group.Name}.{key.Name}");
      }
    }

    Assert.Equal(TableKeys(), paths);
  }

  [Theory]
  [InlineData("src/Everlong.Globalization.Tools/readme.md")]
  [InlineData("docs/skills/everlong-globalization/SKILL.md")]
  public void KeysMentionedElsewhere_AreOnesTheReaderKnows(string relativePath)
  {
    // These docs show excerpts rather than the whole config, so the check is one-way: nothing a doc
    // names may have left the table.  A brand-new option is caught by the README check above, which
    // compares the full example both ways.
    var text = File.ReadAllText(Path.Combine(RepoRoot(), relativePath));
    var mentioned = new SortedSet<string>(StringComparer.Ordinal);

    foreach (Match fence in JsoncFence.Matches(text))
    {
      foreach (Match key in JsonKey.Matches(fence.Groups[1].Value))
        mentioned.Add(key.Groups["name"].Value);
    }

    Assert.NotEmpty(mentioned);

    var known = new SortedSet<string>(StringComparer.Ordinal);
    known.UnionWith(LangOptions.All.Select(option => option.Name));
    known.UnionWith(LangOptions.Groups.Select(group => group.Name));

    Assert.Empty(mentioned.Except(known));
  }

  [Fact]
  public void NearestOption_SuggestsTheSameGroupBeforeAnotherOne()
  {
    Assert.Equal("output.lineEnding", LangOptions.NearestOption("output", "lineEndings"));
    Assert.Equal("output.className", LangOptions.NearestOption("types", "className"));
    Assert.Null(LangOptions.NearestOption("output", "zzzzzzzzzzzz"));
  }

  private static IEnumerable<JsonElement> JsoncBlocks(string path)
  {
    foreach (Match fence in JsoncFence.Matches(File.ReadAllText(path)))
    {
      using var document = JsonDocument.Parse(fence.Groups[1].Value, new JsonDocumentOptions
      {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
      });

      yield return document.RootElement.Clone();
    }
  }

  /// <summary>The repository root, found by walking up from the test output folder.</summary>
  private static string RepoRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null &&
           !File.Exists(Path.Combine(directory.FullName, "Everlong.Globalization.slnx")))
    {
      directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException("No repository root above the test output folder.");
  }
}
