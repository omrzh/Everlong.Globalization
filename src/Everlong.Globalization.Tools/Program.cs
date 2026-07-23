using System.CommandLine;
using Everlong.Globalization.Tools.Commands;

var root = new RootCommand("dotnet-elg — Everlong Globalization CLI tools");
root.AddCommand(InitCommand.Build());
root.AddCommand(GenCommand.Build());
root.AddCommand(AddCommand.Build());
root.AddCommand(CheckCommand.Build());
root.AddCommand(SyncCommand.Build());
return await root.InvokeAsync(args);
