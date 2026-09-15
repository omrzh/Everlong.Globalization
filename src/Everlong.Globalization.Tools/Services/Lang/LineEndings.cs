namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Resolves the <c>output.lineEnding</c> option to the sequence the tool writes, so one config over
///   one set of source files produces the same bytes on every OS when the option is set.
/// </summary>
internal static class LineEndings
{
  /// <summary>The value a config that does not choose one gets: the convention of the running OS.</summary>
  public const string Platform = "platform";

  /// <summary>
  ///   Maps an option already normalized by <see cref="CsprojLocator" /> to the sequence to write.
  /// </summary>
  public static string Resolve(string option) => option switch
  {
    "lf" => "\n",
    "crlf" => "\r\n",
    _ => Environment.NewLine
  };

  /// <summary>
  ///   Rewrites every line break in <paramref name="content" /> to the configured one. Values that
  ///   carry a line break of their own are escaped as literals by the generator, so this only reaches
  ///   the breaks the writer emitted.
  /// </summary>
  public static string Apply(string content, string option) => content.ReplaceLineEndings(Resolve(option));
}
