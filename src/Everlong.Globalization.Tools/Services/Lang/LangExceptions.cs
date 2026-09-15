namespace Everlong.Globalization.Tools.Services.Lang;

/// <summary>
///   The failures the tool answers with a message instead of a stack trace: a project it cannot find,
///   a config it cannot read, and the two files it treats as content rather than configuration — the
///   locale catalogs and their config.  Each says which file it is about, because that is the part a
///   command line cannot guess.
/// </summary>
public class NoCsprojFoundException(string message) : Exception(message);

public class MissingLangConfigException(string csprojPath, string property)
  : Exception($"Property <{property}> not found in {csprojPath}. Add it to a <PropertyGroup>");

/// <summary>
///   A project's <c>i18n.jsonc</c> is not something the tool can act on.  The reader rejects an unknown
///   group, an unknown key, a wrong JSON kind and a value outside the option's allowed set, so a config
///   written for a newer tool fails here instead of being half-read.
/// </summary>
public class InvalidLangConfigException(string message) : Exception(message);

/// <summary>
///   A locale catalog the tool cannot read: the file is not the JSON/JSONC object it must be.  The
///   message carries the page the parser only knows a line and a byte offset of.
/// </summary>
public class InvalidLocaleFileException(string path, string reason, Exception? inner = null)
  : Exception($"Invalid locale file '{path}': {reason}", inner);
