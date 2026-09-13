using System.Runtime.Loader;

namespace ClientData;

public static class Program
{
    private const string LuminaDll = "C:/Users/Knuckles/AppData/Roaming/FFXIVSimpleLauncher/Dalamud/Injector/Lumina.dll";

    public static int Main(string[] args)
    {
        // The launcher's Lumina.dll reports version 0.0.0.0 while Lumina.Excel.dll
        // references 6.0.0.0; Dalamud ignores the mismatch in-game, a console app must not.
        AssemblyLoadContext.Default.Resolving += (context, name) => name.Name == "Lumina" ? context.LoadFromAssemblyPath(LuminaDll) : null;
        return Tool.Run(args);
    }
}
