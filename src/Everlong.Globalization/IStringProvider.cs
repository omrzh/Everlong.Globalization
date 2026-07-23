namespace Everlong.Globalization;

/// <summary>Provides string lookup by key with an optional fallback.</summary>
public interface IStringProvider
{
  /// <summary>Returns a string for <paramref name="key" />.</summary>
  /// <param name="key">The resource key to look up.</param>
  /// <param name="fallback">The value to return when the key is not found.</param>
  /// <returns>The string when found; otherwise <paramref name="fallback" />.</returns>
  string GetString(string key, string fallback);
}
