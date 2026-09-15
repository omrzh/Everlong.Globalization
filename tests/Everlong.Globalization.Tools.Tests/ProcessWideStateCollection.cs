namespace Everlong.Globalization.Tools.Tests;

/// <summary>
///   The collection every test that touches process-wide state belongs to: the console, or the current
///   directory.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="Console" /> is one process-wide sink, and redirecting it is only safe while a single
///     test owns the redirect.  Two at once capture each other's writer as their "previous" value and
///     restore it after its owner has disposed it, so every later <c>Console.WriteLine</c> — in tests
///     that never redirect anything — dies with <c>ObjectDisposedException</c>.
///   </para>
///   <para>
///     The current directory is read by the services that auto-locate a project, so a test that changes
///     it decides what another test's auto-location finds.
///   </para>
///   <para>
///     Running this collection on its own is what keeps one owner of either at a time.
///   </para>
/// </remarks>
[CollectionDefinition(ProcessWideStateCollection.Name, DisableParallelization = true)]
public class ProcessWideStateCollection
{
  public const string Name = "process-wide state";
}
