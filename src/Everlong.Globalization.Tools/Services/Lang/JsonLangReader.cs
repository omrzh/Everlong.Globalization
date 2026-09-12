using System.Text;
using System.Text.Json;
using Everlong.Globalization.Tools.Services.Lang.Models;

namespace Everlong.Globalization.Tools.Services.Lang;

public class JsonLangReader
{

  private static readonly JsonDocumentOptions JsoncOptions = new()
  {
    CommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  public IReadOnlyList<LangNode> Read(string json)
  {
    using var doc = JsonDocument.Parse(json, JsoncOptions);
    return ParseObject(doc.RootElement);
  }

  /// <summary>
  ///   Parses a JSON/JSONC locale file and extracts both the node tree and any metadata keys
  ///   (<c>$KeyPrefix</c>, <c>$DataOnly</c>). When <paramref name="includeComments"/> is
  ///   <see langword="true"/>, <c>//</c> comments preceding or trailing each string property
  ///   are extracted and attached as <see cref="LangNode.XmlDoc"/> values.
  /// </summary>
  public LangFileData ReadWithMeta(string json, bool includeComments = false)
  {
    using var doc = JsonDocument.Parse(json, JsoncOptions);
    var root = doc.RootElement;

    string? keyPrefix = null;
    var dataOnly = false;

    foreach (var prop in root.EnumerateObject())
    {
      if (prop.Name == "$KeyPrefix" && prop.Value.ValueKind == JsonValueKind.String)
        keyPrefix = prop.Value.GetString();
      else if (prop.Name == "$DataOnly" && prop.Value.ValueKind == JsonValueKind.True)
        dataOnly = true;
    }

    IReadOnlyDictionary<string, string?>? comments = null;
    if (includeComments)
      comments = ExtractComments(json);

    var nodes = ParseObjectWithComments(root, comments, []);
    return new LangFileData(nodes, keyPrefix, dataOnly);
  }

  /// <summary>
  ///   Extracts <c>//</c> comments from a JSONC string, keyed by dot-path (e.g. <c>"Nav.Home"</c>).
  ///   Leading comments (the <c>//</c> line immediately before a property) take priority over
  ///   trailing comments (on the same line as or immediately after the value).
  ///   Block comments (<c>/* */</c>) and <c>$</c>-prefixed keys are ignored.
  /// </summary>
  public static IReadOnlyDictionary<string, string?> ExtractComments(string json)
  {
    var result = new Dictionary<string, string?>();
    var readerOptions = new JsonReaderOptions
    {
      CommentHandling = JsonCommentHandling.Allow,
      AllowTrailingCommas = true
    };
    var bytes = Encoding.UTF8.GetBytes(json);
    var reader = new Utf8JsonReader(bytes, readerOptions);
    ExtractCommentsFromObject(ref reader, [], result);
    return result;
  }

  private static void ExtractCommentsFromObject(
    ref Utf8JsonReader reader,
    string[] path,
    Dictionary<string, string?> result)
  {
    // Called when the reader is positioned just before or at StartObject
    // Advance to consume the StartObject token
    while (reader.TokenType != JsonTokenType.StartObject)
    {
      if (!reader.Read())
        return;
    }

    string? pendingComment = null;
    bool afterValue = false;
    string? currentProp = null;

    while (reader.Read())
    {
      switch (reader.TokenType)
      {
        case JsonTokenType.Comment:
          var commentText = reader.GetComment().Trim();
          // Skip block comments (they start with *) — line comments are plain text
          if (commentText.StartsWith('*'))
            break;

          if (!afterValue)
          {
            // Leading comment: stored for the next property
            pendingComment = commentText;
          }
          else if (currentProp != null)
          {
            // Trailing comment: attach to current property (only if no leading was already stored)
            var dotPath = BuildPath(path, currentProp);
            if (!result.ContainsKey(dotPath))
              result[dotPath] = commentText;
          }
          break;

        case JsonTokenType.PropertyName:
          currentProp = reader.GetString()!;
          afterValue = false;

          if (currentProp.StartsWith('$'))
          {
            pendingComment = null;
            break;
          }

          if (pendingComment != null)
          {
            result[BuildPath(path, currentProp)] = pendingComment;
            pendingComment = null;
          }
          break;

        case JsonTokenType.StartObject:
          // Recurse into nested object
          if (currentProp != null && !currentProp.StartsWith('$'))
          {
            string[] nestedPath = path.Length > 0 ? [.. path, currentProp] : [currentProp];
            ExtractCommentsFromObject(ref reader, nestedPath, result);
            afterValue = true;
          }
          else
          {
            reader.Skip();
            afterValue = true;
          }
          pendingComment = null;
          break;

        case JsonTokenType.String:
        case JsonTokenType.Number:
        case JsonTokenType.True:
        case JsonTokenType.False:
        case JsonTokenType.Null:
          afterValue = true;
          pendingComment = null;
          break;

        case JsonTokenType.StartArray:
          reader.Skip();
          afterValue = true;
          pendingComment = null;
          break;

        case JsonTokenType.EndObject:
          return;
      }
    }
  }

  private static string BuildPath(string[] prefix, string key)
    => prefix.Length > 0 ? string.Join(".", prefix) + "." + key : key;

  /// <summary>
  ///   Flattens a node tree into a dot-separated key → value dictionary.
  ///   ICU MessageFormat placeholders (<c>{name}</c>, <c>{name, select, ...}</c>, etc.)
  ///   are preserved as-is; no normalisation is applied.
  /// </summary>
  public static IReadOnlyDictionary<string, string> Flatten(IReadOnlyList<LangNode> nodes)
  {
    var result = new Dictionary<string, string>();
    FlattenRecursive(nodes, [], result);
    return result;
  }

  private static void FlattenRecursive(IReadOnlyList<LangNode> nodes, string[] prefix, Dictionary<string, string> result)
  {
    foreach (var node in nodes)
    {
      if (node.IsLeaf)
      {
        var key = prefix.Length > 0 ? string.Join(".", prefix) + "." + node.Key : node.Key;
        result[key] = node.Value!;
      }
      else
      {
        FlattenRecursive(node.Children, [.. prefix, node.Key], result);
      }
    }
  }

  private static List<LangNode> ParseObject(JsonElement element)
  {
    var nodes = new List<LangNode>();
    foreach (var prop in element.EnumerateObject())
    {
      if (prop.Name.StartsWith('$'))
        continue;
      nodes.Add(ParseProperty(prop.Name, prop.Value));
    }
    return nodes;
  }

  private static List<LangNode> ParseObjectWithComments(
    JsonElement element,
    IReadOnlyDictionary<string, string?>? comments,
    string[] path)
  {
    var nodes = new List<LangNode>();
    foreach (var prop in element.EnumerateObject())
    {
      if (prop.Name.StartsWith('$'))
        continue;
      nodes.Add(ParsePropertyWithComments(prop.Name, prop.Value, comments, path));
    }
    return nodes;
  }

  private static LangNode ParseProperty(string key, JsonElement value)
  {
    switch (value.ValueKind)
    {
      case JsonValueKind.Object:
        string? keyOverride = null;
        foreach (var meta in value.EnumerateObject())
        {
          if (meta.Name == "$KeyPrefix" && meta.Value.ValueKind == JsonValueKind.String)
          {
            keyOverride = meta.Value.GetString();
            break;
          }
        }
        var nodeKey = keyOverride ?? key;
        return new LangNode(nodeKey, null, ParseObject(value), []);

      case JsonValueKind.String:
        var str = value.GetString()!;
        var formatParams = ExtractFormatParams(str);
        return new LangNode(key, str, [], formatParams);

      default:
        throw new InvalidOperationException(
          $"Unsupported JSON value type at key '{key}': only string and object are supported.");
    }
  }

  private static LangNode ParsePropertyWithComments(
    string key,
    JsonElement value,
    IReadOnlyDictionary<string, string?>? comments,
    string[] path)
  {
    var dotPath = BuildPath(path, key);
    switch (value.ValueKind)
    {
      case JsonValueKind.Object:
        string? keyOverride = null;
        foreach (var meta in value.EnumerateObject())
        {
          if (meta.Name == "$KeyPrefix" && meta.Value.ValueKind == JsonValueKind.String)
          {
            keyOverride = meta.Value.GetString();
            break;
          }
        }
        var nodeKey = keyOverride ?? key;
        string[] childPath = path.Length > 0 ? [.. path, key] : [key];
        return new LangNode(nodeKey, null, ParseObjectWithComments(value, comments, childPath), []);

      case JsonValueKind.String:
        var str = value.GetString()!;
        var formatParams = ExtractFormatParams(str);
        string? xmlDoc = null;
        comments?.TryGetValue(dotPath, out xmlDoc);
        return new LangNode(key, str, [], formatParams, xmlDoc);

      default:
        throw new InvalidOperationException(
          $"Unsupported JSON value type at key '{key}': only string and object are supported.");
    }
  }

  private static IReadOnlyList<string> ExtractFormatParams(string value)
  {
    var names = new List<string>();
    var seen = new HashSet<string>();
    ExtractArgNames(value, 0, names, seen);
    return names;
  }

  private static int ExtractArgNames(string value, int pos, List<string> names, HashSet<string> seen)
  {
    while (pos < value.Length)
    {
      if (value[pos] == '\'')
      {
        pos++;
        while (pos < value.Length && value[pos] != '\'')
          pos++;
        if (pos < value.Length)
          pos++;
        continue;
      }

      if (value[pos] == '{')
      {
        var start = pos + 1;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
          start++;

        if (start >= value.Length || value[start] == '}'
            || (!char.IsLetter(value[start]) && value[start] != '_'))
        {
          pos = SkipToMatchingBrace(value, pos);
          continue;
        }

        var nameStart = start;
        while (start < value.Length && (char.IsLetterOrDigit(value[start]) || value[start] == '_'))
          start++;
        var nameEnd = start;

        if (nameEnd > nameStart)
        {
          var name = value[nameStart..nameEnd];
          if (seen.Add(name))
            names.Add(name);
        }

        while (start < value.Length && char.IsWhiteSpace(value[start]))
          start++;

        if (start < value.Length && value[start] == ',')
        {
          pos = SkipToMatchingBrace(value, pos);
          continue;
        }

        pos = start;
        while (pos < value.Length && value[pos] != '}')
          pos++;
        if (pos < value.Length)
          pos++;
        continue;
      }

      pos++;
    }

    return pos;
  }

  private static int SkipToMatchingBrace(string value, int openPos)
  {
    var depth = 1;
    var i = openPos + 1;
    while (i < value.Length && depth > 0)
    {
      if (value[i] == '{')
        depth++;
      else if (value[i] == '}')
        depth--;
      i++;
    }
    return i;
  }
}
