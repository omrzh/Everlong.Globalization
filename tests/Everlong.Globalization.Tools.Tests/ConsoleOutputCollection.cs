namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   The collection every test that redirects <see cref="Console.Out" /> belongs to.
/// </summary>
/// <remarks>
///   <see cref="Console" /> is one process-wide sink, and the redirect is only safe while a single test
///   owns it.  Two of them at once capture each other's writer as their "previous" value and restore it
///   after its owner has disposed it, so every later <c>Console.WriteLine</c> — in tests that never
///   redirect anything — dies with <c>ObjectDisposedException</c>.  Running this collection on its own
///   is what keeps one owner at a time.
/// </remarks>
[CollectionDefinition(ConsoleOutputCollection.Name, DisableParallelization = true)]
public class ConsoleOutputCollection
{
  public const string Name = "console output";
}
