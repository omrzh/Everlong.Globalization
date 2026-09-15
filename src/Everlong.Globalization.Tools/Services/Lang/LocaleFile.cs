using System.Text.Json;
using System.Text.Json.Nodes;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   Reads a locale catalog the way the tool needs it: as content whose syntax errors name the file.
///   A malformed locale file is a hand-edited typo far more often than a tool bug, so it fails like a
///   config fault — one message with the path — instead of surfacing as a parser exception that only
///   knows a line and a byte offset.
/// </summary>
internal static class LocaleFile
{
  private static readonly JsonDocumentOptions JsoncOptions = new()
  {
    CommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  /// <summary>
  ///   Parses a locale file into its root object.  A root that is not an object is a broken catalog,
  ///   not an empty one: reading it as empty would let <c>sync</c> overwrite what is in the file.
  /// </summary>
  public static JsonObject ParseObject(string content, string path)
  {
    JsonNode? node;
    try
    {
      node = JsonNode.Parse(content, nodeOptions: null, JsoncOptions);
    }
    catch (JsonException ex)
    {
      throw new InvalidLocaleFileException(path, ex.Message, ex);
    }

    return node as JsonObject
           ?? throw new InvalidLocaleFileException(path, "the root of a locale file must be a JSON object.");
  }

  /// <summary>Reads a locale file's nodes and metadata, or throws naming the file.</summary>
  public static LangFileData Parse(string path, string content, JsonLangReader reader, bool includeComments = false)
  {
    try
    {
      return reader.ReadWithMeta(content, includeComments);
    }
    catch (JsonException ex)
    {
      throw new InvalidLocaleFileException(path, ex.Message, ex);
    }
  }

  /// <summary>Whether a locale file opts out of generation and sync (<c>$DataOnly</c>).</summary>
  public static bool IsDataOnly(string path, string content, JsonLangReader reader)
    => Parse(path, content, reader).DataOnly;
}
