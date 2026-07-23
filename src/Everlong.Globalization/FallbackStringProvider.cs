namespace Everlong.Globalization;

/// <summary>
///   An <see cref="IStringProvider" /> that always returns the <c>fallback</c> parameter.
///   Use as the safe default for any <c>_provider</c> field so that callers receive their
///   English default text without any provider configured.
/// </summary>
public sealed class FallbackStringProvider : IStringProvider
{
  /// <summary>The shared singleton instance.</summary>
  public static readonly FallbackStringProvider Instance = new();

  private FallbackStringProvider() { }

  /// <inheritdoc />
  public string GetString(string key, string fallback = "") => fallback;
}
