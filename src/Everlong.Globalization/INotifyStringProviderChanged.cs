namespace Everlong.Globalization;

/// <summary>
///   Implemented by <see cref="IStringProvider" /> wrappers that can signal when their active
///   inner provider has changed — enabling live subscriptions across nested providers.
/// </summary>
public interface INotifyStringProviderChanged
{
  /// <summary>Raised after the active inner provider has been replaced.</summary>
  event ProviderChangedEventHandler? ProviderChanged;
}
