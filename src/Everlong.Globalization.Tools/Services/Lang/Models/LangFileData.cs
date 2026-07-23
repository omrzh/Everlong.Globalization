namespace Everlong.Globalization.Tools.Services.Lang.Models;

public record LangFileData(
  IReadOnlyList<LangNode> Nodes,
  string? KeyPrefix = null,
  bool DataOnly = false);
