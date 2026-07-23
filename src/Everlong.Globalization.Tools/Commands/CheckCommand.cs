using System.CommandLine;
using System.CommandLine.Invocation;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class CheckCommand
{
  public static Command Build()
  {
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional)");

    var cmd = new Command("check", "Check key consistency across all locale files");
    cmd.AddOption(projectOpt);

    cmd.SetHandler(async (InvocationContext ctx) =>
    {
      var project = ctx.ParseResult.GetValueForOption(projectOpt);
      var locator = new CsprojLocator();
      var service = new LangCheckService(locator, new JsonLangReader());
      var results = await service.RunAsync(project);

      bool anyIssue = false;
      foreach (var r in results)
      {
        if (r.IsConsistent)
        {
          Console.WriteLine($"{r.Locale,-15} — OK");
        }
        else
        {
          anyIssue = true;
          Console.WriteLine($"{r.Locale,-15} — {r.MissingKeys.Count} missing, {r.OrphanKeys.Count} orphan");
          foreach (var k in r.MissingKeys)
            Console.WriteLine($"  MISSING: {k}");
          foreach (var k in r.OrphanKeys)
            Console.WriteLine($"  ORPHAN:  {k}");
        }
      }

      var satelliteWarnings = service.CheckSatelliteConsistency(project);
      if (satelliteWarnings.Count > 0)
      {
        anyIssue = true;
        Console.WriteLine();
        Console.WriteLine("SatelliteResourceLanguages consistency:");
        foreach (var w in satelliteWarnings)
          Console.WriteLine($"  WARNING: {w}");
      }

      ctx.ExitCode = anyIssue ? 1 : 0;
    });

    return cmd;
  }
}
