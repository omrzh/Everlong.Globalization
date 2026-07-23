namespace Everlong.Globalization;

/// <summary>
///   An <see cref="IStringProvider" /> that always returns the <c>fallback</c> parameter.
///   Useful for testing or as a safe no-op placeholder before any real locale is configured.
/// </summary>
public sealed class NullStringProvider : IStringProvider
{
  /// <summary>The shared singleton instance.</summary>
  public static readonly NullStringProvider Instance = new();

  private NullStringProvider() { }

  /// <inheritdoc />
  public string GetString(string key, string fallback = "") => fallback;
}
