namespace Everlong.Globalization;

/// <summary>
///   An <see cref="IStringProvider" /> backed by a pre-built key→value dictionary.
///   Used by generated <c>Lang.Locales.*</c> fields to represent a single locale snapshot.
/// </summary>
public sealed class DictionaryStringProvider : IStringProvider
{
  private readonly IReadOnlyDictionary<string, string> _d;

  /// <summary>Initializes a new instance with the supplied dictionary.</summary>
  public DictionaryStringProvider(IReadOnlyDictionary<string, string> d) => _d = d;

  /// <inheritdoc />
  public string GetString(string key, string fallback = "") => _d.GetValueOrDefault(key, fallback);
}
