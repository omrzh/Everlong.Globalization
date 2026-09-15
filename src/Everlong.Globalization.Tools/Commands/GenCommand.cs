using System.CommandLine;
using System.CommandLine.Invocation;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class GenCommand
{
  public static Command Build()
  {
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional; auto-locates by default)");

    var cmd = new Command("gen", "Generate Lang.g.cs from the default locale JSON");
    cmd.AddOption(projectOpt);

    cmd.SetHandler(async (InvocationContext ctx) =>
    {
      var project = ctx.ParseResult.GetValueForOption(projectOpt);
      var service = new LangGenService(new CsprojLocator(), new JsonLangReader(), new CodeGenerator());

      // A skipped project means the batch did not do its job, so the run fails even though the
      // projects around it were generated.
      var skipped = await service.RunAsync(project);
      ctx.ExitCode = skipped > 0 ? 1 : 0;
    });

    return cmd;
  }
}
