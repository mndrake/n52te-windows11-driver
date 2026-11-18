namespace N52te.Companion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var single = new Mutex(true, @"Local\n52te-companion", out bool first);
        if (!first) return;   // already running in this session
        ApplicationConfiguration.Initialize();
        Application.Run(new CompanionContext());
    }
}
