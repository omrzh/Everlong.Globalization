using System.CommandLine;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class InitCommand
{
  public static Command Build()
  {
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional; auto-locates by default)");
    var localeOpt = new Option<string?>("--locale", "Neutral locale to scaffold (auto-detected from <NeutralLanguage> when omitted)");

    var cmd = new Command("init", "Scaffold the i18n folder structure under Properties\\i18n\\");
    cmd.AddOption(projectOpt);
    cmd.AddOption(localeOpt);

    cmd.SetHandler(async (project, locale) =>
    {
      var service = new LangInitService(new CsprojLocator());
      await service.RunAsync(project, locale);
    }, projectOpt, localeOpt);

    return cmd;
  }
}
