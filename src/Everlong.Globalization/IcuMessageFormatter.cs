using System.Text;

namespace Everlong.Globalization;

/// <summary>
///   A minimal ICU MessageFormat formatter that supports simple interpolation
///   (<c>{name}</c>), <c>select</c> variants, and <c>plural</c> variants.
///   Nested placeholders inside variant text are resolved recursively.
/// </summary>
public static class IcuMessageFormatter
{
  /// <summary>
  ///   Formats <paramref name="pattern"/> using ICU MessageFormat syntax,
  ///   substituting named arguments from <paramref name="args"/>.
  /// </summary>
  public static string Format(string pattern, IReadOnlyDictionary<string, object?> args)
  {
    var sb = new StringBuilder(pattern.Length);
    var tokens = Tokenize(pattern);
    var pos = 0;
    while (pos < tokens.Count)
      pos = Evaluate(tokens, pos, args, sb);
    return sb.ToString();
  }

  private enum TokenKind { Text, Open, Comma, Other, Close }

  private sealed record Token(TokenKind Kind, string Value, int Position);

  private static List<Token> Tokenize(string pattern)
  {
    var tokens = new List<Token>();
    var i = 0;

    while (i < pattern.Length)
    {
      if (pattern[i] == '\'' && i + 1 < pattern.Length && pattern[i + 1] == '\'')
      {
        tokens.Add(new Token(TokenKind.Text, "'", i));
        i += 2;
        continue;
      }

      if (pattern[i] == '\'')
      {
        var start = i + 1; // skip opening quote
        i++;
        while (i < pattern.Length && pattern[i] != '\'')
          i++;
        var end = i; // position of closing quote (don't include it)
        if (i < pattern.Length)
          i++; // skip closing quote
        tokens.Add(new Token(TokenKind.Text, pattern[start..end], start));
        continue;
      }

      if (pattern[i] == '{')
      {
        tokens.Add(new Token(TokenKind.Open, "{", i));
        i++;
        continue;
      }

      if (pattern[i] == '}')
      {
        tokens.Add(new Token(TokenKind.Close, "}", i));
        i++;
        continue;
      }

      if (pattern[i] == ',')
      {
        tokens.Add(new Token(TokenKind.Comma, ",", i));
        i++;
        continue;
      }

      var textStart = i;
      while (i < pattern.Length && pattern[i] != '{' && pattern[i] != '}' && pattern[i] != ',' && pattern[i] != '\'')
        i++;
      tokens.Add(new Token(TokenKind.Text, pattern[textStart..i], textStart));
    }

    return tokens;
  }

  private static int Evaluate(
    List<Token> tokens,
    int pos,
    IReadOnlyDictionary<string, object?> args,
    StringBuilder output)
  {
    var token = tokens[pos];

    if (token.Kind == TokenKind.Text)
    {
      output.Append(token.Value);
      return pos + 1;
    }

    if (token.Kind != TokenKind.Open)
    {
      output.Append(token.Value);
      return pos + 1;
    }

    // Expect: Open Text(argument) [Comma Text(type)] [Comma Text(selector)...] Close
    var end = FindMatchingClose(tokens, pos);
    if (end < 0)
    {
      output.Append('{');
      return pos + 1;
    }

    var arg = ReadArgument(tokens, pos + 1);
    if (arg is null)
    {
      output.Append('{');
      return pos + 1;
    }

    var argName = arg.Value.Trim();
    var afterArg = pos + 2; // skip Open + argument name

    // Check for type keyword
    string? type = null;
    if (afterArg < end && tokens[afterArg].Kind == TokenKind.Comma)
    {
      var typeToken = ReadArgument(tokens, afterArg + 1);
      if (typeToken is not null)
      {
        type = typeToken.Value.Trim();
        afterArg += 2; // skip Comma + type
      }
    }

    if (type is null)
    {
      // Simple interpolation: {name}
      var value = args.TryGetValue(argName, out var v) ? v : null;
      output.Append(FormatArg(value));
      return end + 1;
    }

    if (string.Equals(type, "select", StringComparison.OrdinalIgnoreCase))
      return EvaluateSelect(tokens, afterArg, end, argName, args, output);

    if (string.Equals(type, "plural", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "selectordinal", StringComparison.OrdinalIgnoreCase))
      return EvaluatePlural(tokens, afterArg, end, argName, type, args, output);

    // Unknown type: treat as simple interpolation
    var fallback = args.TryGetValue(argName, out var f) ? f : null;
    output.Append(FormatArg(fallback));
    return end + 1;
  }

  private static int EvaluateSelect(
    List<Token> tokens,
    int pos,
    int end,
    string argName,
    IReadOnlyDictionary<string, object?> args,
    StringBuilder output)
  {
    var argValue = args.TryGetValue(argName, out var v) ? v : null;
    var key = argValue?.ToString() ?? "other";

    var branches = ParseBranches(tokens, pos, end);
    var matched = branches.FirstOrDefault(b => b.Key == key);
    if (matched.Key is null && branches.TryGetValue("other", out var other))
      matched = new KeyValuePair<string, List<Token>>("other", other);

    if (matched.Value is not null)
    {
      var innerTokens = StripSurroundingBraces(matched.Value);
      var innerPos = 0;
      while (innerPos < innerTokens.Count)
        innerPos = Evaluate(innerTokens, innerPos, args, output);
    }

    return end + 1;
  }

  private static int EvaluatePlural(
    List<Token> tokens,
    int pos,
    int end,
    string argName,
    string pluralType,
    IReadOnlyDictionary<string, object?> args,
    StringBuilder output)
  {
    var argValue = args.TryGetValue(argName, out var v) ? v : null;
    var num = argValue is double d ? d
              : argValue is decimal m ? (double)m
              : argValue is float f ? (double)f
              : argValue is long l ? (double)l
              : argValue is int i ? (double)i
              : argValue is string s && double.TryParse(s, out var parsed) ? parsed
              : 0.0;

    var branches = ParseBranches(tokens, pos, end);
    var matched = MatchPlural(branches, num, pluralType);
    if (matched is null && branches.TryGetValue("other", out var other))
      matched = other;

    if (matched is not null)
    {
      var innerTokens = StripSurroundingBraces(matched);
      var innerPos = 0;
      while (innerPos < innerTokens.Count)
        innerPos = Evaluate(innerTokens, innerPos, args, output);
    }

    return end + 1;
  }

  private static List<Token>? MatchPlural(
    Dictionary<string, List<Token>> branches,
    double num,
    string pluralType)
  {
    var exactKey = $"={num}";
    if (branches.TryGetValue(exactKey, out var exact))
      return exact;

    var n = Math.Abs(num);
    var mod10 = n % 10;
    var mod100 = n % 100;

    var isOrdinal = string.Equals(pluralType, "selectordinal", StringComparison.OrdinalIgnoreCase);

    string category;
    if (isOrdinal)
    {
      if (mod10 == 1 && mod100 != 11)
        category = "one";
      else if (mod10 == 2 && mod100 != 12)
        category = "two";
      else if (mod10 == 3 && mod100 != 13)
        category = "few";
      else
        category = "other";
    }
    else
    {
      if (n == 1 && mod100 != 11)
        category = "one";
      else if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
        category = "few";
      else if (n == 0 || (mod10 >= 11 && mod10 <= 14) || (mod100 >= 11 && mod100 <= 14))
        category = "many";
      else
        category = "other";
    }

    if (branches.TryGetValue(category, out var plural))
      return plural;
    return null;
  }

  private static Dictionary<string, List<Token>> ParseBranches(
    List<Token> tokens,
    int pos,
    int end)
  {
    var branches = new Dictionary<string, List<Token>>();
    var i = pos;

    while (i < end)
    {
      while (i < end && tokens[i].Kind == TokenKind.Comma)
        i++;

      if (i >= end)
        break;

      var keyToken = tokens[i];
      if (keyToken.Kind != TokenKind.Text)
        break;
      i++;

      if (i >= end)
        break;

      var open = tokens[i];
      if (open.Kind == TokenKind.Open)
      {
        var branchEnd = FindMatchingClose(tokens, i);
        if (branchEnd > i && branchEnd < end)
        {
          var branchTokens = new List<Token>();
          for (var j = i; j <= branchEnd; j++)
            branchTokens.Add(tokens[j]);
          branches[keyToken.Value.Trim()] = branchTokens;
          i = branchEnd + 1;
          continue;
        }
      }

      // Plain text value until next comma/end
      var textEnd = i;
      while (textEnd < end && tokens[textEnd].Kind != TokenKind.Comma)
        textEnd++;
      branches[keyToken.Value.Trim()] = tokens.GetRange(i, textEnd - i);
      i = textEnd;
    }

    return branches;
  }

  private static List<Token> StripSurroundingBraces(List<Token> tokens)
  {
    if (tokens.Count >= 2
        && tokens[0].Kind == TokenKind.Open
        && tokens[^1].Kind == TokenKind.Close)
    {
      var inner = new List<Token>();
      for (var i = 1; i < tokens.Count - 1; i++)
      {
        var t = tokens[i];
        if (t.Kind == TokenKind.Comma)
        {
          // Check if we need to skip: after Open, first Text is the key,
          // next Comma begins the variant content. Skip first key and following comma.
          // Actually at this point we already have branches parsed differently.
          // This is used for the inner content of branches.
        }

        inner.Add(t);
      }

      return inner;
    }

    return tokens;
  }

  private static Token? ReadArgument(List<Token> tokens, int pos)
  {
    if (pos < tokens.Count && tokens[pos].Kind == TokenKind.Text)
      return tokens[pos];
    return null;
  }

  private static int FindMatchingClose(List<Token> tokens, int openPos)
  {
    var depth = 1;
    for (var i = openPos + 1; i < tokens.Count; i++)
    {
      if (tokens[i].Kind == TokenKind.Open)
        depth++;
      else if (tokens[i].Kind == TokenKind.Close)
      {
        depth--;
        if (depth == 0)
          return i;
      }
    }

    return -1;
  }

  private static string FormatArg(object? value)
  {
    return value switch
    {
      null => "",
      string s => s,
      _ => value.ToString() ?? ""
    };
  }
}
