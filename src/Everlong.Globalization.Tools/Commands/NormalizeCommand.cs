using System.CommandLine;
using System.CommandLine.Invocation;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class NormalizeCommand
{
  public static Command Build()
  {
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional; auto-locates every project declaring <Elg> by default)");

    var cmd = new Command("normalize", "Rewrite every locale catalog with the configured line endings");
    cmd.AddOption(projectOpt);

    cmd.SetHandler(async (InvocationContext ctx) =>
    {
      var project = ctx.ParseResult.GetValueForOption(projectOpt);
      var service = new LangNormalizeService(new CsprojLocator(), new JsonLangReader());
      var normalized = await service.RunAsync(project);

      if (normalized == 0)
      {
        Console.WriteLine("Every catalog already uses the configured line endings.");
        ctx.ExitCode = 0;
        return;
      }

      Console.WriteLine();
      Console.WriteLine($"{normalized} file(s) rewritten. This commit changes line endings only — to keep");
      Console.WriteLine("`git blame` readable, land it on its own and record it:");
      Console.WriteLine("  git rev-parse HEAD >> .git-blame-ignore-revs");
      Console.WriteLine("  git config blame.ignoreRevsFile .git-blame-ignore-revs");
      Console.WriteLine();
      Console.WriteLine("Run `dotnet elg gen` to rewrite the generated files with the same ending.");
      ctx.ExitCode = 0;
    });

    return cmd;
  }
}
