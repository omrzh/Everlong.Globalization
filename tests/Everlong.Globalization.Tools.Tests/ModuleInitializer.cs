using DiffEngine;
using System.Runtime.CompilerServices;

public static class ModuleInitializer
{
  [ModuleInitializer]
  public static void Init()
  {
    DiffRunner.Disabled = true;
  }
}
