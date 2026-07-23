namespace Everlong.Globalization;

/// <summary>
///   A mutable <see cref="IStringProvider" /> wrapper that delegates every lookup to an inner
///   provider. Swap the active locale at runtime by calling <see cref="Use" />; any code that
///   already holds a reference to this <see cref="LangProvider" /> will immediately read from
///   the new locale without re-wiring.
/// </summary>
public sealed class LangProvider : IStringProvider, INotifyStringProviderChanged
{
  private IStringProvider _inner;

  /// <summary>Initializes a new instance with <paramref name="initial" /> as the active locale.</summary>
  public LangProvider(IStringProvider initial)
  {
    _inner = initial;
    if (initial is INotifyStringProviderChanged notify)
      notify.ProviderChanged += OnInnerProviderChanged;
  }

  private void OnInnerProviderChanged(object? sender, EventArgs e) =>
    ProviderChanged?.Invoke(this, EventArgs.Empty);

  /// <inheritdoc />
  public string GetString(string key, string fallback = "") => _inner.GetString(key, fallback);

  /// <summary>
  ///   Raised after <see cref="Use" /> replaces the active provider.
  /// </summary>
  public event ProviderChangedEventHandler? ProviderChanged;

  /// <summary>Replaces the active locale with <paramref name="locale" /> and notifies all subscribers.</summary>
  public void Use(IStringProvider locale)
  {
    if (_inner is INotifyStringProviderChanged oldNotify)
      oldNotify.ProviderChanged -= OnInnerProviderChanged;

    if (locale is INotifyStringProviderChanged newNotify)
      newNotify.ProviderChanged += OnInnerProviderChanged;

    _inner = locale;
    ProviderChanged?.Invoke(this, EventArgs.Empty);
  }
}

/// <summary>Delegate for <see cref="INotifyStringProviderChanged.ProviderChanged" />.</summary>
public delegate void ProviderChangedEventHandler(object? sender, EventArgs e);
