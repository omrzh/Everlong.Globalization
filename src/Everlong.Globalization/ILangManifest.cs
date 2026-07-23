namespace Everlong.Globalization;

/// <summary>
///   Exposes an assembly's locale catalog so that a <see cref="LangCoordinator" /> can drive
///   unified locale switching across multiple assemblies.
/// </summary>
/// <remarks>
///   Each generated <c>Lang</c> class exposes a <c>Manifest</c> property that implements this
///   interface. Register those manifests with a shared <see cref="LangCoordinator" /> at app
///   startup; then call <see cref="LangCoordinator.Use" /> once to switch all assemblies.
/// </remarks>
public interface ILangManifest
{
  /// <summary>The assembly's mutable locale provider.</summary>
  LangProvider Provider { get; }

  /// <summary>
  ///   All locales shipped by this assembly, keyed by their BCP 47 locale name
  ///   (e.g. <c>"en"</c>, <c>"zh-Hans-CN"</c>).
  /// </summary>
  IReadOnlyDictionary<string, IStringProvider> SupportedLocales { get; }
}
