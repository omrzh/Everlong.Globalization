using System.CommandLine;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class AddCommand
{
  public static Command Build()
  {
    var localeArg = new Argument<string>("locale", "Locale code to add (e.g. zh-CN, fr)");
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional)");
    var forceOpt = new Option<bool>("--force", "Overwrite existing locale files if they exist");

    var cmd = new Command("add", "Scaffold a new locale by copying the default locale");
    cmd.AddArgument(localeArg);
    cmd.AddOption(projectOpt);
    cmd.AddOption(forceOpt);

    cmd.SetHandler(async (locale, project, force) =>
    {
      var service = new LangAddService(new CsprojLocator());
      await service.RunAsync(locale, project, force);
    }, localeArg, projectOpt, forceOpt);

    return cmd;
  }
}
