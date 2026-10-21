using System.Runtime.InteropServices;
using Kairos.Engine;
using Kairos.Native;
using Kairos.Settings;

namespace Kairos;

public static class Program
{
    public static int Main(string[] args)
    {
        string app = AppContext.BaseDirectory;
        string prefix = CaptureBackend.Prefix(app);
        NativeLibrary.SetDllImportResolver(typeof(Obs).Assembly, (name, _, _) =>
            name == "obs" ? NativeLibrary.Load(Path.Combine(prefix, "lib", "libobs.so.30")) : IntPtr.Zero);

        string work = Path.Combine(Path.GetTempPath(), "kairos-spike");
        string clips = Path.Combine(work, "clips");
        Directory.CreateDirectory(work);
        var settings = new AppSettings();
        settings.Output.Folder = clips;
        settings.Replay.Seconds = 15;
        settings.Video.Fps = 30;
        settings.Video.Resolution = OutputResolution.P1080;
        ObsHost.Start(app, work, work, new VideoConfig(1920, 1080, 1920, 1080, 30), settings.Video.Codec);
        Console.WriteLine("failed modules: " + string.Join(",", ObsHost.FailedModules));
        var pipeline = new CapturePipeline(settings);
        pipeline.Build();
        Console.WriteLine($"built: {pipeline.Choice!.Id} {pipeline.Output.W}x{pipeline.Output.H}@{pipeline.Fps}");
        var saved = new ManualResetEventSlim();
        string? path = null;
        pipeline.ReplaySaved += (p, _) => { path = p; saved.Set(); };
        Console.WriteLine("buffer started: " + pipeline.SetBufferActive(true) + " " + pipeline.LastError);
        Thread.Sleep(8000);
        Console.WriteLine("save requested: " + pipeline.RequestSave(false));
        Console.WriteLine("saved signal: " + saved.Wait(15000) + " -> " + path);
        pipeline.SetBufferActive(false);
        pipeline.Dispose();
        ObsHost.Shutdown();
        return path != null && File.Exists(path) ? 0 : 1;
    }
}
