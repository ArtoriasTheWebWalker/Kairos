using System.Runtime.InteropServices;
using Kairos.Engine;

namespace Kairos;

/// <summary>Tells libobs which windowing system the program runs under, and hands it the X connection.</summary>
static class NixPlatform
{
    const int PlatformX11Egl = 1; // OBS_NIX_PLATFORM_X11_EGL (0 is INVALID)

    static IntPtr _display;

    public static void Prepare()
    {
        if (_display != IntPtr.Zero)
            return;
        XInitThreads(); // libobs uses the connection from its own threads (Qt does this in OBS Studio)
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display == IntPtr.Zero)
            throw new InvalidOperationException("Could not open the X display (is DISPLAY set? Wayland needs XWayland)");
        obs_set_nix_platform(PlatformX11Egl);
        obs_set_nix_platform_display(_display);
        ObsLog.Info("libobs platform: X11/EGL");
    }

    [DllImport("obs")] static extern void obs_set_nix_platform(int platform);
    [DllImport("obs")] static extern void obs_set_nix_platform_display(IntPtr display);
    [DllImport("libX11.so.6")] static extern int XInitThreads();
    [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr name);
}
