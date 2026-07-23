namespace Everlong.Globalization;

/// <summary>
///   An <see cref="IStringProvider" /> that chains a primary and a secondary provider.
///   For each key, the primary is consulted first; if it returns an empty string the secondary
///   is used instead. Propagates <see cref="INotifyStringProviderChanged.ProviderChanged" />
///   from both inner providers so that any <see cref="LangProvider" /> wrapping a chain
///   stays live when either inner switches.
/// </summary>
public sealed class ChainedStringProvider : IStringProvider, INotifyStringProviderChanged
{
  private readonly IStringProvider _primary;
  private readonly IStringProvider _secondary;

  /// <summary>
  ///   Initializes a new <see cref="ChainedStringProvider" />.
  /// </summary>
  /// <param name="primary">Consulted first. An empty-string result is treated as "not found".</param>
  /// <param name="secondary">Consulted when the primary returns an empty string.</param>
  public ChainedStringProvider(IStringProvider primary, IStringProvider secondary)
  {
    _primary = primary;
    _secondary = secondary;

    if (primary is INotifyStringProviderChanged pn)
      pn.ProviderChanged += OnInnerChanged;
    if (secondary is INotifyStringProviderChanged sn)
      sn.ProviderChanged += OnInnerChanged;
  }

  private void OnInnerChanged(object? sender, EventArgs e) =>
    ProviderChanged?.Invoke(this, EventArgs.Empty);

  /// <inheritdoc />
  public string GetString(string key, string fallback = "")
  {
    var result = _primary.GetString(key, "");
    return string.IsNullOrEmpty(result) ? _secondary.GetString(key, fallback) : result;
  }

  /// <inheritdoc />
  public event ProviderChangedEventHandler? ProviderChanged;
}
