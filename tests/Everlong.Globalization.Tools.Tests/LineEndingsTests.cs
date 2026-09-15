namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   The line-ending decision on its own: what the option resolves to, which breaks <c>Apply</c>
///   rewrites, and what <c>Uses</c> accepts as "the configured ending".  The command-level tests cover
///   who asks; this file covers the table both of them read.
/// </summary>
public class LineEndingsTests
{
  [Theory]
  [InlineData("lf", "\n")]
  [InlineData("crlf", "\r\n")]
  public void Resolve_MapsTheTokenToItsSequence(string option, string expected)
    => Assert.Equal(expected, LineEndings.Resolve(option));

  [Fact]
  public void Resolve_Platform_FollowsTheRunningOs()
    => Assert.Equal(Environment.NewLine, LineEndings.Resolve(LineEndings.Platform));

  /// <summary>
  ///   The reader rejects an unknown token, so reaching this one is a programming error rather than a
  ///   config mistake — and it must not quietly pick the platform's convention instead.
  /// </summary>
  [Fact]
  public void Resolve_UnknownToken_Throws()
  {
    var ex = Assert.Throws<ArgumentOutOfRangeException>(() => LineEndings.Resolve("unix"));

    Assert.Contains("unix", ex.Message);
    Assert.Contains(LineEndings.Platform, ex.Message);
  }

  /// <summary>
  ///   The set <c>ReplaceLineEndings</c> knows is wider than CR/LF — which is why <c>Uses</c> tests the
  ///   same set instead of only looking for <c>\r</c> and <c>\n</c>.
  /// </summary>
  [Theory]
  [InlineData("\r")]
  [InlineData("\n")]
  [InlineData("\r\n")]
  [InlineData("\f")]
  [InlineData("\u0085")]
  [InlineData("\u2028")]
  [InlineData("\u2029")]
  public void Apply_RewritesEveryBreakItRecognizes(string lineBreak)
    => Assert.Equal("A\nB", LineEndings.Apply($"A{lineBreak}B", "lf"));

  [Fact]
  public void Apply_LeavesVerticalTabAlone()
    => Assert.Equal("A\vB", LineEndings.Apply("A\vB", "lf"));

  [Theory]
  [InlineData("lf", "A\nB", true)]
  [InlineData("lf", "AB", true)]
  [InlineData("lf", "A\vB", true)] // Apply does not rewrite a vertical tab, so it is not a violation
  [InlineData("lf", "A\r\nB", false)]
  [InlineData("lf", "A\fB", false)]
  [InlineData("lf", "A\u0085B", false)]
  [InlineData("lf", "A\u2028B", false)]
  [InlineData("crlf", "A\r\nB", true)]
  [InlineData("crlf", "A\nB", false)]
  public void Uses_ReportsWhetherTheContentHoldsOnlyTheConfiguredEnding(
    string option, string content, bool expected)
    => Assert.Equal(expected, LineEndings.Uses(content, option));
}
