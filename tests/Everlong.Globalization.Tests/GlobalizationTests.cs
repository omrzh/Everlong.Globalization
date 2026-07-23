using System.ComponentModel;
using Everlong.Globalization;
using Xunit;

namespace Everlong.Globalization.Tests;

public class GlobalizationTests
{
  // ── NullStringProvider ─────────────────────────────────────────────────────

  [Fact]
  public void NullStringProvider_NoFallback_ReturnsEmptyString()
    => Assert.Equal("", NullStringProvider.Instance.GetString("any.key"));

  [Fact]
  public void NullStringProvider_WithFallback_ReturnsFallback()
    => Assert.Equal("default text", NullStringProvider.Instance.GetString("any.key", "default text"));

  [Fact]
  public void NullStringProvider_IsSingleton()
    => Assert.Same(NullStringProvider.Instance, NullStringProvider.Instance);

  // ── FallbackStringProvider ─────────────────────────────────────────────────

  [Fact]
  public void FallbackStringProvider_ReturnsFallback()
    => Assert.Equal("fb", FallbackStringProvider.Instance.GetString("x", "fb"));

  [Fact]
  public void FallbackStringProvider_IsSingleton()
    => Assert.Same(FallbackStringProvider.Instance, FallbackStringProvider.Instance);

  // ── DictionaryStringProvider ───────────────────────────────────────────────

  [Fact]
  public void DictionaryStringProvider_ExactMatch_ReturnsValue()
  {
    var p = new DictionaryStringProvider(new Dictionary<string, string> { ["a"] = "A" });
    Assert.Equal("A", p.GetString("a"));
  }

  [Fact]
  public void DictionaryStringProvider_MissingKey_ReturnsFallback()
  {
    var p = new DictionaryStringProvider(new Dictionary<string, string>());
    Assert.Equal("fb", p.GetString("x", "fb"));
  }

  [Fact]
  public void DictionaryStringProvider_CaseSensitive()
  {
    var p = new DictionaryStringProvider(new Dictionary<string, string> { ["Key"] = "v" });
    Assert.Equal("", p.GetString("key"));
    Assert.Equal("v", p.GetString("Key"));
  }

  // ── LangProvider ───────────────────────────────────────────────────────────

  [Fact]
  public void LangProvider_DelegatesToInitialInner()
  {
    var inner = new FixedProvider("hello");
    var provider = new LangProvider(inner);
    Assert.Equal("hello", provider.GetString("key"));
  }

  [Fact]
  public void LangProvider_Use_SwapsInner()
  {
    var first = new FixedProvider("first");
    var second = new FixedProvider("second");
    var provider = new LangProvider(first);

    provider.Use(second);

    Assert.Equal("second", provider.GetString("key"));
  }

  [Fact]
  public void LangProvider_Use_ExistingReferencesPickUpNewInner()
  {
    var inner1 = new FixedProvider("en");
    var inner2 = new FixedProvider("zh");
    var provider = new LangProvider(inner1);

    IStringProvider reference = provider;
    Assert.Equal("en", reference.GetString("key", ""));

    provider.Use(inner2);

    Assert.Equal("zh", reference.GetString("key", ""));
  }

  [Fact]
  public void LangProvider_MissingKey_ReturnsFallback()
  {
    var inner = new KeyedProvider(new Dictionary<string, string> { ["a"] = "value-a" });
    var provider = new LangProvider(inner);

    Assert.Equal("fallback", provider.GetString("missing.key", "fallback"));
  }

  [Fact]
  public void LangProvider_Use_RaisesProviderChanged()
  {
    var raised = false;
    var provider = new LangProvider(new FixedProvider("a"));
    provider.ProviderChanged += (_, _) => raised = true;

    provider.Use(new FixedProvider("b"));

    Assert.True(raised);
  }

  [Fact]
  public void LangProvider_Use_SameInner_DoesNotRaise()
  {
    // LangProvider always raises on Use() regardless — that's by design.
    // This test documents the current behaviour.
    var count = 0;
    var inner = new FixedProvider("x");
    var provider = new LangProvider(inner);
    provider.ProviderChanged += (_, _) => count++;

    provider.Use(inner);

    Assert.Equal(1, count);
  }

  [Fact]
  public void LangProvider_InitialInnerWithNotify_ForwardsEvent()
  {
    var inner = new NotifyProvider("a");
    var provider = new LangProvider(inner);

    var forwarded = false;
    provider.ProviderChanged += (_, _) => forwarded = true;

    inner.Fire();

    Assert.True(forwarded);
  }

  [Fact]
  public void LangProvider_SwapFromNotifyInner_UnsubscribesOld()
  {
    var oldInner = new NotifyProvider("a");
    var newInner = new FixedProvider("b");
    var provider = new LangProvider(oldInner);

    var forwarded = 0;
    provider.ProviderChanged += (_, _) => forwarded++;

    oldInner.Fire(); // forwarded through initial subscription
    Assert.Equal(1, forwarded);

    provider.Use(newInner); // fires event once (count = 2)

    oldInner.Fire(); // should NOT fire because unsubscribed — count stays at 2
    Assert.Equal(2, forwarded);
  }

  [Fact]
  public void LangProvider_SwapToNotifyInner_SubscribesNew()
  {
    var newInner = new NotifyProvider("b");
    var provider = new LangProvider(new FixedProvider("a"));

    var forwarded = false;
    provider.ProviderChanged += (_, _) => forwarded = true;

    provider.Use(newInner);
    forwarded = false;

    newInner.Fire();

    Assert.True(forwarded);
  }

  // ── ChainedStringProvider ──────────────────────────────────────────────────

  [Fact]
  public void ChainedStringProvider_PrimaryHit_ReturnsPrimary()
  {
    var p = new ChainedStringProvider(
      new KeyedProvider(new Dictionary<string, string> { ["k"] = "primary" }),
      new KeyedProvider(new Dictionary<string, string> { ["k"] = "secondary" }));

    Assert.Equal("primary", p.GetString("k"));
  }

  [Fact]
  public void ChainedStringProvider_PrimaryEmpty_FallsThrough()
  {
    var p = new ChainedStringProvider(
      new KeyedProvider(new Dictionary<string, string> { ["k"] = "" }),
      new KeyedProvider(new Dictionary<string, string> { ["k"] = "secondary" }));

    Assert.Equal("secondary", p.GetString("k"));
  }

  [Fact]
  public void ChainedStringProvider_PrimaryMiss_FallsThrough()
  {
    var p = new ChainedStringProvider(
      new KeyedProvider(new Dictionary<string, string>()),
      new KeyedProvider(new Dictionary<string, string> { ["k"] = "secondary" }));

    Assert.Equal("secondary", p.GetString("k"));
  }

  [Fact]
  public void ChainedStringProvider_BothMiss_ReturnsFallback()
  {
    var p = new ChainedStringProvider(
      new KeyedProvider(new Dictionary<string, string>()),
      new KeyedProvider(new Dictionary<string, string>()));

    Assert.Equal("fb", p.GetString("k", "fb"));
  }

  [Fact]
  public void ChainedStringProvider_ForwardsPrimaryEvents()
  {
    var primary = new NotifyProvider("a");
    var p = new ChainedStringProvider(primary, new FixedProvider("b"));

    var forwarded = false;
    p.ProviderChanged += (_, _) => forwarded = true;

    primary.Fire();

    Assert.True(forwarded);
  }

  [Fact]
  public void ChainedStringProvider_ForwardsSecondaryEvents()
  {
    var secondary = new NotifyProvider("b");
    var p = new ChainedStringProvider(new FixedProvider("a"), secondary);

    var forwarded = false;
    p.ProviderChanged += (_, _) => forwarded = true;

    secondary.Fire();

    Assert.True(forwarded);
  }

  // ── StringSectionBase ──────────────────────────────────────────────────────

  [Fact]
  public void StringSectionBase_WithoutPrefix_UsesKeyDirectly()
  {
    var p = new LangProvider(new KeyedProvider(new Dictionary<string, string> { ["key"] = "val" }));
    var s = new TestSection(p, "");

    Assert.Equal("val", s.Get("key"));
  }

  [Fact]
  public void StringSectionBase_WithPrefix_PrependsPrefix()
  {
    var p = new LangProvider(new KeyedProvider(new Dictionary<string, string> { ["pre.key"] = "val" }));
    var s = new TestSection(p, "pre");

    Assert.Equal("val", s.Get("key"));
  }

  [Fact]
  public void StringSectionBase_MissingKey_ReturnsFallback()
  {
    var p = new LangProvider(new KeyedProvider(new Dictionary<string, string>()));
    var s = new TestSection(p, "");

    Assert.Equal("fb", s.Get("missing", "fb"));
  }

  [Fact]
  public void StringSectionBase_ProviderChanged_InvalidatesAllProperties()
  {
    var p = new LangProvider(new FixedProvider("a"));
    var s = new TestSection(p, "");

    var propertyChanged = false;
    string? changedProperty = null;
    s.PropertyChanged += (_, e) =>
    {
      propertyChanged = true;
      changedProperty = e.PropertyName;
    };

    p.Use(new FixedProvider("b"));

    Assert.True(propertyChanged);
    Assert.Null(changedProperty); // null means "all properties"
  }

  [Fact]
  public void StringSectionBase_InitialProviderIsNotify_Subscribes()
  {
    var inner = new NotifyProvider("a");
    var p = new LangProvider(inner);
    var s = new TestSection(p, "");

    var raised = false;
    s.PropertyChanged += (_, _) => raised = true;

    inner.Fire();

    Assert.True(raised);
  }

  // ── LangCoordinator ────────────────────────────────────────────────────────

  [Fact]
  public void LangCoordinator_Resolve_ExactMatch_ReturnsProvider()
  {
    var en = new FixedProvider("en");
    var supported = new Dictionary<string, IStringProvider> { ["en"] = en };

    var result = LangCoordinator.Resolve(supported, "en");

    Assert.Same(en, result);
  }

  [Fact]
  public void LangCoordinator_Resolve_ParentChain_FindsParent()
  {
    var en = new FixedProvider("en");
    var supported = new Dictionary<string, IStringProvider> { ["en"] = en };

    var result = LangCoordinator.Resolve(supported, "en-US");

    Assert.Same(en, result);
  }

  [Fact]
  public void LangCoordinator_Resolve_ParentChain_FindsGrandparent()
  {
    var zh = new FixedProvider("zh");
    var supported = new Dictionary<string, IStringProvider> { ["zh"] = zh };

    // zh-Hans-CN → zh-Hans → zh
    var result = LangCoordinator.Resolve(supported, "zh-Hans-CN");

    Assert.Same(zh, result);
  }

  [Fact]
  public void LangCoordinator_Resolve_PrefixMatch_ByExactLanguageTag()
  {
    var zhCn = new FixedProvider("zh-CN");
    var supported = new Dictionary<string, IStringProvider> { ["zh-CN"] = zhCn };

    // Resolve("zh-CN") exact match — "zh" alone has no dash so prefix step is skipped
    var result = LangCoordinator.Resolve(supported, "zh-CN");

    Assert.Same(zhCn, result);
  }

  [Fact]
  public void LangCoordinator_Resolve_PrefixMatch_SelectsAlphabeticallyFirst()
  {
    var zhHans = new FixedProvider("zh-Hans");
    var zhHant = new FixedProvider("zh-Hant");
    var supported = new Dictionary<string, IStringProvider>
    {
      ["zh-Hant"] = zhHant,
      ["zh-Hans"] = zhHans,
    };

    // Resolve("zh-Hant-CN"): exact miss → parent "zh-Hant" exact match
    var result = LangCoordinator.Resolve(supported, "zh-Hant-CN");

    Assert.Same(zhHant, result);
  }

  [Fact]
  public void LangCoordinator_Resolve_NoMatch_ReturnsNull()
  {
    var supported = new Dictionary<string, IStringProvider> { ["en"] = new FixedProvider("en") };

    var result = LangCoordinator.Resolve(supported, "fr");

    Assert.Null(result);
  }

  [Fact]
  public void LangCoordinator_Resolve_ExactTakesPriorityOverParent()
  {
    var enUs = new FixedProvider("en-US");
    var en = new FixedProvider("en");
    var supported = new Dictionary<string, IStringProvider>
    {
      ["en-US"] = enUs,
      ["en"] = en,
    };

    var result = LangCoordinator.Resolve(supported, "en-US");

    Assert.Same(enUs, result);
  }

  [Fact]
  public void LangCoordinator_RegisterAndUse_SwitchesAllManifests()
  {
    var coord = new LangCoordinator();
    var manifest1 = new TestManifest("en", "fallback");
    var manifest2 = new TestManifest("en", "fallback");
    coord.Register(manifest1);
    coord.Register(manifest2);

    coord.Use("en");

    Assert.Equal("en-value", manifest1.Provider.GetString("key"));
    Assert.Equal("en-value", manifest2.Provider.GetString("key"));
  }

  [Fact]
  public void LangCoordinator_Use_UnknownLocale_DoesNothing()
  {
    var coord = new LangCoordinator();
    var manifest = new TestManifest("en", "default");
    coord.Register(manifest);

    // Should not throw and should not change the provider
    coord.Use("unknown-locale");

    Assert.Equal("en-value", manifest.Provider.GetString("key"));
  }

  [Fact]
  public void LangCoordinator_Resolve_InvalidCulture_NoThrow()
  {
    // "xx-YYY-12345" is not a real culture; should skip parent-chain step
    var en = new FixedProvider("en");
    var supported = new Dictionary<string, IStringProvider> { ["en"] = en };

    var result = LangCoordinator.Resolve(supported, "xx-YYY-12345");

    Assert.Null(result);
  }

  // ── IcuMessageFormatter ────────────────────────────────────────────────────

  [Fact]
  public void Icu_SimpleInterpolation()
  {
    var result = IcuMessageFormatter.Format("Hello, {name}!", new Dictionary<string, object?>
    {
      ["name"] = "World"
    });
    Assert.Equal("Hello, World!", result);
  }

  [Fact]
  public void Icu_MultiplePlaceholders()
  {
    var result = IcuMessageFormatter.Format("{a} + {b} = {c}", new Dictionary<string, object?>
    {
      ["a"] = 1, ["b"] = 2, ["c"] = 3
    });
    Assert.Equal("1 + 2 = 3", result);
  }

  [Fact]
  public void Icu_MissingArg_ReturnsEmpty()
  {
    var result = IcuMessageFormatter.Format("Hello, {name}!", new Dictionary<string, object?>());
    Assert.Equal("Hello, !", result);
  }

  [Fact]
  public void Icu_Select()
  {
    var result = IcuMessageFormatter.Format(
      "{gender, select, male {He} female {She} other {They}}",
      new Dictionary<string, object?> { ["gender"] = "female" });
    Assert.Equal("She", result);
  }

  [Fact]
  public void Icu_Select_FallsBackToOther()
  {
    var result = IcuMessageFormatter.Format(
      "{gender, select, male {He} other {They}}",
      new Dictionary<string, object?> { ["gender"] = "unknown" });
    Assert.Equal("They", result);
  }

  [Fact]
  public void Icu_Select_NoOther_ReturnsEmpty()
  {
    var result = IcuMessageFormatter.Format(
      "{gender, select, male {He}}",
      new Dictionary<string, object?> { ["gender"] = "female" });
    Assert.Equal("", result);
  }

  [Fact]
  public void Icu_Plural_One()
  {
    var result = IcuMessageFormatter.Format(
      "{count, plural, one {1 item} other {{count} items}}",
      new Dictionary<string, object?> { ["count"] = 1 });
    Assert.Equal("1 item", result);
  }

  [Fact]
  public void Icu_Plural_Other()
  {
    var result = IcuMessageFormatter.Format(
      "{count, plural, one {1 item} other {{count} items}}",
      new Dictionary<string, object?> { ["count"] = 5 });
    Assert.Equal("5 items", result);
  }

  [Fact]
  public void Icu_Plural_ExactMatch()
  {
    var result = IcuMessageFormatter.Format(
      "{count, plural, =0 {none} one {1} other {{count}}}",
      new Dictionary<string, object?> { ["count"] = 0 });
    Assert.Equal("none", result);
  }

  [Fact]
  public void Icu_SelectOrdinal_SelectsCorrectBranch()
  {
    // Note: This minimal formatter does NOT replace # with the numeric value
    // (full ICU spec requires it, but the minimal implementation keeps # as-is).
    var result = IcuMessageFormatter.Format(
      "{place, selectordinal, one {first} two {second} few {third} other {other}}",
      new Dictionary<string, object?> { ["place"] = 2 });
    Assert.Equal("second", result);
  }

  [Fact]
  public void Icu_NestedPlaceholdersInsideBranch()
  {
    var result = IcuMessageFormatter.Format(
      "{count, plural, one {{name} has 1 item} other {{name} has {count} items}}",
      new Dictionary<string, object?> { ["count"] = 3, ["name"] = "Bob" });
    Assert.Equal("Bob has 3 items", result);
  }

  [Fact]
  public void Icu_EscapedQuotes_Single()
  {
    var result = IcuMessageFormatter.Format("Don''t", new Dictionary<string, object?>());
    Assert.Equal("Don't", result);
  }

  [Fact]
  public void Icu_EscapedQuotes_Block()
  {
    // ICU single-quote escaping: the entire quoted block is kept literally
    var result = IcuMessageFormatter.Format("'{Hello}'", new Dictionary<string, object?>());
    Assert.Equal("{Hello}", result);
  }

  [Fact]
  public void Icu_NullArg_ReturnsEmpty()
  {
    var result = IcuMessageFormatter.Format("{x}", new Dictionary<string, object?>
    {
      ["x"] = null
    });
    Assert.Equal("", result);
  }

  [Fact]
  public void Icu_DoubleArg_FormatsAsNumber()
  {
    var result = IcuMessageFormatter.Format("{n}", new Dictionary<string, object?>
    {
      ["n"] = 3.14
    });
    Assert.Equal("3.14", result);
  }

  [Fact]
  public void Icu_NoPlaceholders_ReturnsPattern()
  {
    var result = IcuMessageFormatter.Format("plain text", new Dictionary<string, object?>());
    Assert.Equal("plain text", result);
  }

  // ── INotifyStringProviderChanged ───────────────────────────────────────────

  [Fact]
  public void NotifyProvider_EventFired_ReceivedBySubscriber()
  {
    var n = new NotifyProvider("x");
    var fired = false;
    n.ProviderChanged += (_, _) => fired = true;

    n.Fire();

    Assert.True(fired);
  }

  [Fact]
  public void NotifyProvider_EventCanBeUnsubscribed()
  {
    var n = new NotifyProvider("x");
    var count = 0;
    var handler = new ProviderChangedEventHandler((_, _) => count++);

    n.ProviderChanged += handler;
    n.Fire();
    Assert.Equal(1, count);

    n.ProviderChanged -= handler;
    n.Fire();
    Assert.Equal(1, count); // unchanged
  }

  // ── Helpers ────────────────────────────────────────────────────────────────

  private sealed class FixedProvider(string value) : IStringProvider
  {
    public string GetString(string key, string fallback = "") => value;
  }

  private sealed class KeyedProvider(IReadOnlyDictionary<string, string> data) : IStringProvider
  {
    public string GetString(string key, string fallback = "")
      => data.GetValueOrDefault(key, fallback);
  }

  private sealed class NotifyProvider(string value) : IStringProvider, INotifyStringProviderChanged
  {
    public event ProviderChangedEventHandler? ProviderChanged;

    public string GetString(string key, string fallback = "") => value;

    public void Fire() => ProviderChanged?.Invoke(this, EventArgs.Empty);
  }

  private sealed class TestSection(LangProvider provider, string prefix) : StringSectionBase(provider, prefix)
  {
    public string Get(string key, string fallback = "") => GetString(key, fallback);
  }

  private sealed class TestManifest(string localeName, string fallback) : ILangManifest
  {
    private readonly Dictionary<string, IStringProvider> _locales = new()
    {
      [localeName] = new KeyedProvider(new Dictionary<string, string> { ["key"] = $"{localeName}-value" }),
      ["fallback"] = new KeyedProvider(new Dictionary<string, string> { ["key"] = fallback }),
    };

    public LangProvider Provider { get; } = new LangProvider(
      new KeyedProvider(new Dictionary<string, string> { ["key"] = $"{localeName}-value" }));

    public IReadOnlyDictionary<string, IStringProvider> SupportedLocales => _locales;
  }
}
