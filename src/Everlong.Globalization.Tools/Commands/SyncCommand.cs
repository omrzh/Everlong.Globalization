using System.CommandLine;
using Everlong.Globalization.Tools.Services.Lang;

namespace Everlong.Globalization.Tools.Commands;

public static class SyncCommand
{
  public static Command Build()
  {
    var localeArg = new Argument<string?>("locale", () => null, "Locale to sync (optional; syncs all non-default locales when omitted)");
    var projectOpt = new Option<string?>("--project", "Path to .csproj file (optional)");

    var cmd = new Command("sync", "Add missing keys and re-order entries to match the default locale");
    cmd.AddArgument(localeArg);
    cmd.AddOption(projectOpt);

    cmd.SetHandler(async (locale, project) =>
    {
      var service = new LangSyncService(new CsprojLocator(), new JsonLangReader());
      await service.RunAsync(locale, project);
    }, localeArg, projectOpt);

    return cmd;
  }
}
