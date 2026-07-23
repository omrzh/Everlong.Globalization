using VerifyXunit;
using VerifyTests;

namespace Everlong.Globalization.Tools.Tests;

public class CodeGeneratorTests
{
  private static VerifySettings Settings()
  {
    var s = new VerifySettings();
    s.UseDirectory("Verified");
    s.AutoVerify(false, false);
    return s;
  }

  private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> SingleLocale(
    string locale, IReadOnlyList<LangNode> nodes)
    => new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      [locale] = JsonLangReader.Flatten(nodes)
    };

  private static ModuleData FlatModule(IReadOnlyList<LangNode> nodes, string locale = "en")
    => new ModuleData("Default", nodes, SingleLocale(locale, nodes));

  [Fact]
  public Task PlainProperties_NoFormat()
  {
    var nodes = new List<LangNode>
    {
      new("Home", "Home Page", [], []),
      new("About", "About Page", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], SingleLocale("en", nodes));
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task NestedClasses()
  {
    var nodes = new List<LangNode>
    {
      new("Nav", null, new List<LangNode>
      {
        new("Home", "Home", [], []),
        new("About", "About", [], [])
      }, [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], SingleLocale("en", nodes));
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task NamedFormatMethod()
  {
    var nodes = new List<LangNode>
    {
      new("Welcome", "Hello {name}, you have {count} messages", [], new[] { "name", "count" })
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], SingleLocale("en", nodes));
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task PositionalFormatMethod()
  {
    var nodes = new List<LangNode>
    {
      new("RangeHintFormat", "Range: {0} ~ {1}", [], ["arg0", "arg1"])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], SingleLocale("en", nodes));
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task MixedPropertyAndMethod()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], []),
      new("Welcome", "Hello {name}!", [], new[] { "name" })
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], SingleLocale("en", nodes));
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task MultipleLocales()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], []),
      new("Welcome", "Hello {name}!", [], new[] { "name" })
    };
    var allFlatValues = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string>
      {
        ["Title"] = "My App",
        ["Welcome"] = "Hello {0}!"
      },
      ["zh-CN"] = new Dictionary<string, string>
      {
        ["Title"] = "我的应用",
        ["Welcome"] = "你好 {0}!"
      }
    };
    var module = new ModuleData("Default", nodes, allFlatValues);
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], allFlatValues);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task FolderMode_MultipleModules()
  {
    var appNodes = new List<LangNode> { new("Title", "My App", [], []) };
    var navNodes = new List<LangNode> { new("Home", "Home", [], []) };
    var appModule = new ModuleData("App", appNodes, new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App" }
    }, KeyPrefix: "App");
    var navModule = new ModuleData("Nav", navNodes, new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["Nav.Home"] = "Home" }
    }, KeyPrefix: "Nav");
    var allLocaleValues = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App", ["Nav.Home"] = "Home" }
    };
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [appModule, navModule], allLocaleValues);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task FolderMode_WithKeyPrefixOverride()
  {
    // $KeyPrefix overrides the filename-derived prefix (e.g. Nester.json -> "Everlong.Nester")
    var dialogNodes = new List<LangNode>
    {
      new("Dialog", null, new List<LangNode>
      {
        new("Alert", null, new List<LangNode>
        {
          new("Title", "Alert", [], []),
          new("ButtonText", "OK", [], [])
        }, [])
      }, [])
    };
    var module = new ModuleData(
      "Nester",
      dialogNodes,
      new Dictionary<string, IReadOnlyDictionary<string, string>>
      {
        ["en"] = new Dictionary<string, string>
        {
          ["Everlong.Nester.Dialog.Alert.Title"] = "Alert",
          ["Everlong.Nester.Dialog.Alert.ButtonText"] = "OK"
        }
      },
      KeyPrefix: "Everlong.Nester");
    var allLocaleValues = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string>
      {
        ["Everlong.Nester.Dialog.Alert.Title"] = "Alert",
        ["Everlong.Nester.Dialog.Alert.ButtonText"] = "OK"
      }
    };
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [module], allLocaleValues);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task FolderMode_DataOnly_NoClassEmitted()
  {
    var appNodes = new List<LangNode> { new("Title", "My App", [], []) };
    var nesterNodes = new List<LangNode>
    {
      new("Dialog", null, new List<LangNode>
      {
        new("Title", "Alert", [], [])
      }, [])
    };
    var appModule = new ModuleData("App", appNodes,
      new Dictionary<string, IReadOnlyDictionary<string, string>>
      {
        ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App" }
      }, KeyPrefix: "App");
    var nesterModule = new ModuleData("Nester", nesterNodes,
      new Dictionary<string, IReadOnlyDictionary<string, string>>
      {
        ["zh-CN"] = new Dictionary<string, string> { ["Everlong.Nester.Dialog.Title"] = "提示" }
      }, KeyPrefix: "Everlong.Nester", DataOnly: true);
    var allLocaleValues = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App" },
      ["zh-CN"] = new Dictionary<string, string>
      {
        ["Everlong.Nester.Dialog.Title"] = "提示"
      }
    };
    var result = new CodeGenerator().Generate("MyApp.Lang", "en", [appModule, nesterModule], allLocaleValues);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task InternalVisibility_ClassAndMembers()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], []),
      new("Nav", null, new List<LangNode>
      {
        new("Home", "Home", [], [])
      }, [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate(
      "MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      className: "Lang",
      classVisibility: "internal",
      memberVisibility: "internal");
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task CustomClassName_UsedInClassAndProperties()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate(
      "MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      className: "Strings");
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task GenerateXmlDoc_True_NoPragma()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "App", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate(
      "MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      generateXmlDoc: true);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public Task GenerateFormatMethod_False_StillGeneratesTemplateProperty()
  {
    var nodes = new List<LangNode>
    {
      new("RangeHintFormat", "Range: {0} ~ {1}", [], new[] { "arg0", "arg1" })
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate(
      "MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      generateFormatMethod: false);
    return Verifier.Verify(result, Settings());
  }

  [Fact]
  public void GenerateFiles_PartialOptions_ProduceExpectedFileSet()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], [])
    };
    var module = new ModuleData("App", nodes, new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App" }
    }, KeyPrefix: "App");
    var all = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["App.Title"] = "My App" }
    };

    var files = new CodeGenerator().GenerateFiles(
      "MyApp.Properties",
      "en",
      [module],
      all,
      localesInPartialFile: true,
      sectionClassesInPartialFiles: true);

    Assert.Contains(files, f => f.FileName == "Lang.g.cs");
    Assert.Contains(files, f => f.FileName == "Lang.Locales.g.cs");
    Assert.Contains(files, f => f.FileName == "Lang.App.g.cs");
    Assert.Equal(3, files.Count);
  }

  [Fact]
  public void Generate_CustomSectionTypeSuffix_AppliesToSectionTypes()
  {
    var nodes = new List<LangNode>
    {
      new("Dialog", null, new List<LangNode>
      {
        new("Title", "Alert", [], [])
      }, [])
    };
    var module = new ModuleData("Nester", nodes, new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["Nester.Dialog.Title"] = "Alert" }
    }, KeyPrefix: "Nester");
    var all = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
      ["en"] = new Dictionary<string, string> { ["Nester.Dialog.Title"] = "Alert" }
    };

    var result = new CodeGenerator().Generate(
      "MyApp.Properties",
      "en",
      [module],
      all,
      sectionTypeSuffix: "Section");

    Assert.Contains("static NesterSection Nester", result);
    Assert.Contains("sealed class NesterSection", result);
    Assert.Contains("public NesterDialogSection Dialog", result);
    Assert.Contains("sealed class NesterDialogSection", result);
  }

  [Fact]
  public void Generate_MainLangClass_UsesXmlDocInsteadOfHeaderComment()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "My App", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Properties", "en", [module], SingleLocale("en", nodes));

    Assert.DoesNotContain("// <auto-generated>", result);
    Assert.Contains("/// <summary>", result);
    Assert.Contains("This class was generated by", result);
    Assert.Contains("static partial class Lang", result);
  }

  [Fact]
  public void GenerateXmlDoc_False_EmitsPragmaDisableCs1591()
  {
    var nodes = new List<LangNode> { new("Title", "App", [], []) };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      generateXmlDoc: false);

    Assert.Contains("#pragma warning disable CS1591", result);
  }

  [Fact]
  public void GenerateXmlDoc_True_NoPragmaAndHasSummaries()
  {
    var nodes = new List<LangNode>
    {
      new("Title", "App Title", [], [], XmlDoc: "The application title"),
      new("Subtitle", "Subtitle text", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      generateXmlDoc: true);

    Assert.DoesNotContain("#pragma warning disable CS1591", result);
    // XmlDoc from node (leading/trailing comment)
    Assert.Contains("/// <summary>The application title</summary>", result);
    // Falls back to default locale value when no XmlDoc set
    Assert.Contains("/// <summary>Subtitle text</summary>", result);
    // Provider and Manifest get docs too
    Assert.Contains("The global language provider", result);
    Assert.Contains("Returns the language manifest", result);
  }

  [Fact]
  public void GenerateXmlDoc_True_FormatMethod_HasSummary()
  {
    var nodes = new List<LangNode>
    {
      new("RangeHintFormat", "Range: {0} ~ {1}", [], new[] { "arg0", "arg1" })
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate("MyApp.Properties", "en", [module], SingleLocale("en", nodes),
      generateXmlDoc: true);

    Assert.Contains("/// <summary>Formats <see cref=", result);
  }

  [Fact]
  public Task CoordinatorGeneration_WithExternalManifests()
  {
    var nodes = new List<LangNode> { new("Title", "My App", [], []) };
    var module = FlatModule(nodes);
    var files = new CodeGenerator().GenerateFiles(
      "MyApp.Properties",
      "en",
      [module],
      SingleLocale("en", nodes),
      generateCoordinator: true,
      coordinatorManifests: ["Everlong.Nester.Properties.Lang"]);
    var coordFile = files.Single(f => f.FileName == "Lang.Coordinator.g.cs");
    return Verifier.Verify(coordFile.Content, Settings());
  }

  [Fact]
  public Task CoordinatorGeneration_NoManifests()
  {
    var nodes = new List<LangNode> { new("Title", "My App", [], []) };
    var module = FlatModule(nodes);
    var files = new CodeGenerator().GenerateFiles(
      "MyApp.Properties",
      "en",
      [module],
      SingleLocale("en", nodes),
      generateCoordinator: true,
      coordinatorManifests: []);
    var coordFile = files.Single(f => f.FileName == "Lang.Coordinator.g.cs");
    return Verifier.Verify(coordFile.Content, Settings());
  }

  [Fact]
  public void GenerateFiles_WithCoordinator_IncludesCoordinatorFile()
  {
    var nodes = new List<LangNode> { new("Title", "My App", [], []) };
    var module = FlatModule(nodes);

    var files = new CodeGenerator().GenerateFiles(
      "MyApp.Properties",
      "en",
      [module],
      SingleLocale("en", nodes),
      generateCoordinator: true,
      coordinatorManifests: ["Everlong.Nester.Properties.Lang"]);

    Assert.Contains(files, f => f.FileName == "Lang.Coordinator.g.cs");
    Assert.Contains(files, f => f.FileName == "Lang.g.cs");
  }

  [Fact]
  public void GenerateFiles_WithoutCoordinator_DoesNotIncludeCoordinatorFile()
  {
    var nodes = new List<LangNode> { new("Title", "My App", [], []) };
    var module = FlatModule(nodes);

    var files = new CodeGenerator().GenerateFiles(
      "MyApp.Properties",
      "en",
      [module],
      SingleLocale("en", nodes));

    Assert.DoesNotContain(files, f => f.FileName == "Lang.Coordinator.g.cs");
  }

  [Fact]
  public void Generate_MultilineValues_AreEscapedInStringLiteralAndXmlDoc()
  {
    var nodes = new List<LangNode>
    {
      new("Multiline", "Line1\nLine2", [], [])
    };
    var module = FlatModule(nodes);
    var result = new CodeGenerator().Generate(
      "MyApp.Properties",
      "en",
      [module],
      SingleLocale("en", nodes),
      generateXmlDoc: true);

    Assert.Contains("GetString(\"Multiline\", \"Line1\\nLine2\")", result);
    Assert.Contains("[\"Multiline\"] = \"Line1\\nLine2\"", result);
    Assert.Contains("/// <summary>Line1&#10;Line2</summary>", result);
  }
}
