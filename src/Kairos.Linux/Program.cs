using System.Runtime.InteropServices;
using Kairos.Native;

namespace Kairos;

public static class Program
{
    public static int Main(string[] args)
    {
        string prefix = Engine.CaptureBackend.Prefix(AppContext.BaseDirectory);
        NativeLibrary.SetDllImportResolver(typeof(Obs).Assembly, (name, _, _) =>
            name == "obs" ? NativeLibrary.Load(Path.Combine(prefix, "lib", "libobs.so.30")) : IntPtr.Zero);

        return args.Length > 0 ? args[0] switch
        {
            "--selftest-record" => SelfTests.Record(),
            "--selftest-hotkeys" => SelfTests.Hotkeys(),
            "--selftest-foreground" => SelfTests.Foreground(),
            _ => Usage(),
        } : Usage();
    }

    static int Usage()
    {
        Console.Error.WriteLine("usage: kairos --selftest-record | --selftest-hotkeys");
        return 2;
    }
}
