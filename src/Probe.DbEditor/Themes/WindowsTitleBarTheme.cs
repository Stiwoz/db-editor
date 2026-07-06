using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Probe.DbEditor.Themes;

public static class WindowsTitleBarTheme
{
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeBeforeWindows10Version2004 = 19;

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        var result = DwmSetWindowAttribute(
            handle,
            UseImmersiveDarkMode,
            ref enabled,
            Marshal.SizeOf<int>());

        if (result != 0)
        {
            _ = DwmSetWindowAttribute(
                handle,
                UseImmersiveDarkModeBeforeWindows10Version2004,
                ref enabled,
                Marshal.SizeOf<int>());
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
