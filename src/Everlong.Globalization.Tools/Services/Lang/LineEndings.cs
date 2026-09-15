namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Resolves the <c>output.lineEnding</c> option to the sequence the tool writes, so one config over
///   one set of source files produces the same bytes on every OS unless the config asks for the
///   running one.
/// </summary>
internal static class LineEndings
{
  /// <summary>The default: the ending an LF-normalized repository declares.</summary>
  public const string Lf = "lf";

  /// <summary>The explicit opt-in of the convention of the running OS.</summary>
  public const string Platform = "platform";

  /// <summary>
  ///   Maps an option already normalized by <see cref="LangConfigFileReader" /> to the sequence to write.
  /// </summary>
  public static string Resolve(string option) => option switch
  {
    Lf => "\n",
    "crlf" => "\r\n",
    _ => Environment.NewLine
  };

  /// <summary>
  ///   Rewrites every line break in <paramref name="content" /> to the configured one. Values that
  ///   carry a line break of their own are escaped as literals by the generator, so this only reaches
  ///   the breaks the writer emitted.
  /// </summary>
  public static string Apply(string content, string option) => content.ReplaceLineEndings(Resolve(option));

  /// <summary>
  ///   Whether <paramref name="content" /> already uses nothing but the configured ending — the test
  ///   that lets <c>sync</c> leave an already-correct file alone, and rewrite one whose keys are in
  ///   sync but whose bytes are not.
  /// </summary>
  public static bool Uses(string content, string option)
  {
    var remainder = content.Replace(Resolve(option), "");
    return !remainder.Contains('\r') && !remainder.Contains('\n');
  }
}
