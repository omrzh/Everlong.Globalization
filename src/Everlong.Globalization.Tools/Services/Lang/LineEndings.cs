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

  /// <summary>The ending a repository that is normalized on Windows declares.</summary>
  public const string Crlf = "crlf";

  /// <summary>
  ///   The breaks <see cref="string.ReplaceLineEndings(string)" /> recognizes, which is therefore the
  ///   set <see cref="Apply" /> rewrites and the set <see cref="Uses" /> has to look for.  Vertical tab
  ///   (U+000B) is deliberately absent: the framework leaves it alone, so a file holding one still uses
  ///   the configured ending.
  /// </summary>
  private static readonly char[] Breaks = ['\r', '\n', '\f', '\u0085', '\u2028', '\u2029'];

  /// <summary>
  ///   Maps an option already normalized by <see cref="LangConfigFileReader" /> to the sequence to write.
  ///   An unknown token is a programming error — the reader rejects one long before this point — so it
  ///   fails loudly instead of quietly writing the platform's convention.
  /// </summary>
  public static string Resolve(string option) => option switch
  {
    Lf => "\n",
    Crlf => "\r\n",
    Platform => Environment.NewLine,
    _ => throw new ArgumentOutOfRangeException(
      nameof(option), option, $"output.lineEnding must be \"{Lf}\", \"{Crlf}\" or \"{Platform}\".")
  };

  /// <summary>
  ///   Rewrites every line break in <paramref name="content" /> to the configured one.  Values that
  ///   carry a line break of their own are escaped as literals by the generator and by the JSON
  ///   writer, so this only reaches the breaks the writer emitted.
  /// </summary>
  public static string Apply(string content, string option) => content.ReplaceLineEndings(Resolve(option));

  /// <summary>
  ///   Whether <paramref name="content" /> already uses nothing but the configured ending — the test
  ///   that lets <c>sync</c> leave an already-correct file alone, and rewrite one whose keys are in
  ///   sync but whose bytes are not.
  /// </summary>
  public static bool Uses(string content, string option)
    => content.Replace(Resolve(option), "").IndexOfAny(Breaks) < 0;
}
