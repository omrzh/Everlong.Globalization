using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Everlong.Globalization.Tools.Commands;
using Everlong.Globalization.Tools.Services.Lang;

var root = new RootCommand("dotnet-elg — Everlong Globalization CLI tools");
root.AddCommand(InitCommand.Build());
root.AddCommand(GenCommand.Build());
root.AddCommand(AddCommand.Build());
root.AddCommand(CheckCommand.Build());
root.AddCommand(SyncCommand.Build());
root.AddCommand(NormalizeCommand.Build());

// The default pipeline ends with `UseExceptionHandler`, which answers every failure with a stack
// trace.  A config the tool cannot act on is a user error — the message is the whole diagnosis — so
// the pipeline is built without it and the known config faults exit with that one line instead.
try
{
  return await new CommandLineBuilder(root)
    .UseVersionOption()
    .UseHelp()
    .UseEnvironmentVariableDirective()
    .UseParseDirective()
    .UseSuggestDirective()
    .RegisterWithDotnetSuggest()
    .UseTypoCorrections()
    .UseParseErrorReporting()
    .CancelOnProcessTermination()
    .Build()
    .InvokeAsync(args);
}
catch (Exception ex) when (ex is InvalidLangConfigException or InvalidLocaleFileException or NoCsprojFoundException or MissingLangConfigException)
{
  Console.Error.WriteLine(ex.Message);
  return 1;
}
