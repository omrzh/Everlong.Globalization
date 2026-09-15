namespace Everlong.Globalization.Tools.Tests;

public class JsonLangReaderTests
{
  private readonly JsonLangReader _reader = new();

  [Fact]
  public void FlatJson_ReturnsSingleLevelNodes()
  {
    var json = """{ "Home": "Home Page" }""";
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    var node = nodes[0];
    Assert.Equal("Home", node.Key);
    Assert.Equal("Home Page", node.Value);
    Assert.True(node.IsLeaf);
    Assert.Empty(node.Children);
    Assert.Empty(node.FormatParams);
  }

  [Fact]
  public void NestedJson_ReturnsNestedNodes()
  {
    var json = """{ "App": { "Title": "My App" } }""";
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    var app = nodes[0];
    Assert.Equal("App", app.Key);
    Assert.False(app.IsLeaf);
    Assert.Single(app.Children);

    var title = app.Children[0];
    Assert.Equal("Title", title.Key);
    Assert.Equal("My App", title.Value);
    Assert.True(title.IsLeaf);
  }

  [Fact]
  public void DeeplyNestedJson_Works()
  {
    var json = """{ "A": { "B": { "C": "deep value" } } }""";
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    var a = nodes[0];
    Assert.False(a.IsLeaf);

    var b = a.Children[0];
    Assert.Equal("B", b.Key);
    Assert.False(b.IsLeaf);

    var c = b.Children[0];
    Assert.Equal("C", c.Key);
    Assert.Equal("deep value", c.Value);
    Assert.True(c.IsLeaf);
  }

  [Fact]
  public void EmptyObject_ReturnsEmptyList()
  {
    var json = "{}";
    var nodes = _reader.Read(json);
    Assert.Empty(nodes);
  }

  [Fact]
  public void NamedPlaceholder_ExtractsFormatParams()
  {
    var json = """{ "Greeting": "Hello {name}!" }""";
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    Assert.Equal(["name"], nodes[0].FormatParams);
  }

  [Fact]
  public void MultiNamedPlaceholders_ExtractsInOrder()
  {
    var json = """{ "Message": "Hi {name}, you have {count}" }""";
    var nodes = _reader.Read(json);

    Assert.Equal(["name", "count"], nodes[0].FormatParams);
  }

  [Fact]
  public void PositionalPlaceholder_EverlonggerRecognized()
  {
    var json = """{ "Greeting": "Hi {0}!" }""";
    var nodes = _reader.Read(json);

    Assert.Empty(nodes[0].FormatParams);
  }

  [Fact]
  public void MultiPositionalPlaceholders_EverlonggerRecognized()
  {
    var json = """{ "Message": "Hi {0} and {1}" }""";
    var nodes = _reader.Read(json);

    Assert.Empty(nodes[0].FormatParams);
  }

  [Fact]
  public void IcuSelectPlaceholder_ExtractsArgName()
  {
    var json = """{ "Message": "{gender, select, male {He} female {She} other {They}}" }""";
    var nodes = _reader.Read(json);

    Assert.Equal(["gender"], nodes[0].FormatParams);
  }

  [Fact]
  public void IcuPluralPlaceholder_ExtractsArgName()
  {
    var json = """{ "Message": "{count, plural, =0 {none} one {one} other {{count} items}}" }""";
    var nodes = _reader.Read(json);

    Assert.Equal(["count"], nodes[0].FormatParams);
  }

  [Fact]
  public void MixedInterpolationAndIcu_ExtractsAllArgNames()
  {
    var json = """{ "Message": "Hello {name}, {count, plural, =1 {1 item} other {{count} items}}" }""";
    var nodes = _reader.Read(json);

    Assert.Equal(["name", "count"], nodes[0].FormatParams);
  }

  [Fact]
  public void UnsupportedValueType_Throws()
  {
    var json = """{ "Count": 42 }""";
    var ex = Assert.Throws<InvalidOperationException>(() => _reader.Read(json));
    Assert.Contains("Count", ex.Message);
    Assert.Contains("only string and object are supported", ex.Message);
  }

  [Fact]
  public void MetadataKeys_AreSkippedByRead()
  {
    var json = """{ "$KeyPrefix": "App", "Title": "My App" }""";
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    Assert.Equal("Title", nodes[0].Key);
  }

  [Fact]
  public void ReadWithMeta_ExtractsKeyPrefix()
  {
    var json = """{ "$KeyPrefix": "Everlong.Nester", "Dialog": { "Title": "Alert" } }""";
    var result = _reader.ReadWithMeta(json);

    Assert.Equal("Everlong.Nester", result.KeyPrefix);
    Assert.False(result.DataOnly);
    Assert.Single(result.Nodes);
    Assert.Equal("Dialog", result.Nodes[0].Key);
  }

  [Fact]
  public void ReadWithMeta_ExtractsDataOnly()
  {
    var json = """{ "$DataOnly": true, "$KeyPrefix": "Acme", "Key": "Value" }""";
    var result = _reader.ReadWithMeta(json);

    Assert.True(result.DataOnly);
    Assert.Equal("Acme", result.KeyPrefix);
    Assert.Single(result.Nodes);
    Assert.Equal("Key", result.Nodes[0].Key);
  }

  [Fact]
  public void ReadWithMeta_NoMetadata_ReturnsNullPrefixAndFalseDataOnly()
  {
    var json = """{ "Title": "Hello" }""";
    var result = _reader.ReadWithMeta(json);

    Assert.Null(result.KeyPrefix);
    Assert.False(result.DataOnly);
    Assert.Single(result.Nodes);
  }

  /// <summary>
  ///   The opposite contract to <c>i18n.jsonc</c>: a locale file holds content, so any key is a
  ///   legitimate string entry and a key that looks like a config option is just a string.
  /// </summary>
  [Fact]
  public void ReadWithMeta_UnknownKeys_AreEntries_NotConfiguration()
  {
    var json = """{ "$KeyPrefix": "App", "unknownOption": "text", "lineEndings": "unix" }""";
    var result = _reader.ReadWithMeta(json);

    Assert.Equal("App", result.KeyPrefix);
    Assert.Equal(2, result.Nodes.Count);
    Assert.Equal("text", result.Nodes.Single(node => node.Key == "unknownOption").Value);
    Assert.Equal("unix", result.Nodes.Single(node => node.Key == "lineEndings").Value);
  }

  [Fact]
  public void NestedObject_WithKeyPrefixOverride_RenamesNode()
  {
    var json = """
      {
        "Buttons": {
          "$KeyPrefix": "Button",
          "OK": "OK",
          "Cancel": "Cancel"
        }
      }
      """;
    var nodes = _reader.Read(json);

    Assert.Single(nodes);
    var node = nodes[0];
    // $KeyPrefix inside the object overrides the property name as the node key
    Assert.Equal("Button", node.Key);
    Assert.False(node.IsLeaf);
    Assert.Equal(2, node.Children.Count);
    Assert.Equal("OK", node.Children[0].Key);
    Assert.Equal("Cancel", node.Children[1].Key);
  }

  [Fact]
  public void NestedObject_WithKeyPrefixOverride_DoesNotAffectSiblings()
  {
    var json = """
      {
        "Title": "App",
        "Nav": {
          "$KeyPrefix": "Navigation",
          "Home": "Home"
        }
      }
      """;
    var nodes = _reader.Read(json);

    Assert.Equal(2, nodes.Count);
    Assert.Equal("Title", nodes[0].Key);
    Assert.Equal("Navigation", nodes[1].Key);
  }

  [Fact]
  public void JsoncComments_AreIgnored_ByRead()
  {
    var jsonc = """
      {
        // This is a comment
        "Title": "My App", // inline comment
        /* block comment */
        "Name": "Nester"
      }
      """;
    var nodes = _reader.Read(jsonc);

    Assert.Equal(2, nodes.Count);
    Assert.Equal("Title", nodes[0].Key);
    Assert.Equal("My App", nodes[0].Value);
    Assert.Equal("Name", nodes[1].Key);
  }

  [Fact]
  public void JsoncComments_AreIgnored_ByReadWithMeta()
  {
    var jsonc = """
      {
        // DataOnly marker for extension files
        "$DataOnly": true,
        "$KeyPrefix": "Lib", // override prefix
        "Alert": "提示"
      }
      """;
    var result = _reader.ReadWithMeta(jsonc);

    Assert.True(result.DataOnly);
    Assert.Equal("Lib", result.KeyPrefix);
    Assert.Single(result.Nodes);
    Assert.Equal("Alert", result.Nodes[0].Key);
  }

  [Fact]
  public void TrailingCommas_AreHandled()
  {
    var jsonc = """
      {
        "A": "first",
        "B": "second",
      }
      """;
    var nodes = _reader.Read(jsonc);

    Assert.Equal(2, nodes.Count);
    Assert.Equal("A", nodes[0].Key);
    Assert.Equal("B", nodes[1].Key);
  }

  [Fact]
  public void ExtractComments_LeadingComment_IsAssociated()
  {
    var jsonc = """
      {
        // Greeting text shown on the home page
        "Home": "Home Page"
      }
      """;
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.True(comments.TryGetValue("Home", out var doc));
    Assert.Equal("Greeting text shown on the home page", doc);
  }

  [Fact]
  public void ExtractComments_TrailingComment_IsAssociated()
  {
    var jsonc = """
      {
        "Home": "Home Page" // welcome screen
      }
      """;
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.True(comments.TryGetValue("Home", out var doc));
    Assert.Equal("welcome screen", doc);
  }

  [Fact]
  public void ExtractComments_LeadingTakesPriorityOverTrailing()
  {
    var jsonc = """
      {
        // leading wins
        "Home": "Home Page" // trailing loses
      }
      """;
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.Equal("leading wins", comments["Home"]);
  }

  [Fact]
  public void ExtractComments_NestedProperty_UsedDotPath()
  {
    var jsonc = """
      {
        "Nav": {
          // Go to home
          "Home": "Home"
        }
      }
      """;
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.False(comments.ContainsKey("Home"));
    Assert.True(comments.TryGetValue("Nav.Home", out var doc));
    Assert.Equal("Go to home", doc);
  }

  [Fact]
  public void ExtractComments_MetadataKeySkipped()
  {
    var jsonc = """
      {
        // this should be ignored
        "$KeyPrefix": "App",
        "Title": "App"
      }
      """;
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.False(comments.ContainsKey("$KeyPrefix"));
    Assert.False(comments.ContainsKey("Title")); // no comment on Title
  }

  [Fact]
  public void ExtractComments_NoComment_KeyAbsent()
  {
    var jsonc = """{ "Title": "App", "Subtitle": "Sub" }""";
    var comments = JsonLangReader.ExtractComments(jsonc);

    Assert.Empty(comments);
  }

  [Fact]
  public void ReadWithMeta_IncludeComments_AttachesXmlDocToNodes()
  {
    var jsonc = """
      {
        // The main title
        "Title": "App",
        "Subtitle": "Sub" // short sub
      }
      """;
    var result = _reader.ReadWithMeta(jsonc, includeComments: true);

    Assert.Equal("The main title", result.Nodes[0].XmlDoc);
    Assert.Equal("short sub", result.Nodes[1].XmlDoc);
  }

  [Fact]
  public void ReadWithMeta_NoComments_XmlDocNull()
  {
    var json = """{ "Title": "App" }""";
    var result = _reader.ReadWithMeta(json, includeComments: false);

    Assert.Null(result.Nodes[0].XmlDoc);
  }
}
