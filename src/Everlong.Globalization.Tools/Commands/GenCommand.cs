using System.CommandLine;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class GenCommand
{
  public static Command Build()
  {
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional; auto-locates by default)");

    var cmd = new Command("gen", "Generate Lang.g.cs from the default locale JSON");
    cmd.AddOption(projectOpt);

    cmd.SetHandler(async (project) =>
    {
      var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());
      await service.RunAsync(project);
    }, projectOpt);

    return cmd;
  }
}
