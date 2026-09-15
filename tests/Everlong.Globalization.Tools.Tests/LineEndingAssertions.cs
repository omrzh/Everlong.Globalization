namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   Asserts the byte-level property the <c>output.lineEnding</c> option exists to control: the file
///   uses exactly one line-breaking sequence. The Verify snapshots cannot express it — their
///   comparison is line-ending insensitive, so LF and CRLF content verify the same.
/// </summary>
internal static class LineEndingAssertions
{
  public static void AssertOnly(string content, string expected)
  {
    Assert.Contains(expected, content);

    // Nothing of the other sequences may survive stripping the expected one.
    var remainder = content.Replace(expected, "");
    Assert.DoesNotContain("\r", remainder);
    Assert.DoesNotContain("\n", remainder);
  }
}
