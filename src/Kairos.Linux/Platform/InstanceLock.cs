namespace Kairos.Platform;

/// <summary>
/// "Only one of these per user": an exclusive lock on a file in the user's runtime directory. The kernel drops it when the
/// process ends, however it ends. (A named mutex would do on Windows; on Linux those are scoped to a login session, so two
/// launches from different sessions would not see each other.)
/// </summary>
public sealed class InstanceLock : IDisposable
{
    readonly FileStream _file;

    InstanceLock(FileStream file) => _file = file;

    /// <summary>The lock, or null when another process holds it.</summary>
    public static InstanceLock? TryAcquire(string name)
    {
        string folder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } run && Directory.Exists(run) ? run : Path.GetTempPath();
        try
        {
            return new InstanceLock(new FileStream(Path.Combine(folder, $"kairos-{name}-{Environment.UserName}.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _file.Dispose();
}
