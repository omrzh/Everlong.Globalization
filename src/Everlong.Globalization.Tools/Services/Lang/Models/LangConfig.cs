namespace Everlong.Globalization.Tools.Services.Lang.Models;

public record LangConfig(
  string CsprojPath,
  string SourceDir,
  string OutputDir,
  string Namespace,
  string DefaultLocale,
  string ClassName,
  string ClassVisibility,
  string MemberVisibility,
  bool GenerateXmlDoc,
  bool GenerateFormatMethod,
  string LocalesVisibility,
  bool LocalesInPartialFile,
  bool SectionClassesInPartialFiles,
  string SectionTypeSuffix,
  bool GenerateCoordinator,
  IReadOnlyList<string> CoordinatorManifests,
  string GlobalizationNamespace = "Everlong.Globalization",
  string LineEnding = LineEndings.Lf);
