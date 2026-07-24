using System.Diagnostics;
using System.Text;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Infrastructure;

namespace MoreWaterObjectSpy.Inspectors;

/// <summary>
/// Snapshot Win32 sincrono. Se ejecuta en el hilo del hook, en el instante del click,
/// para garantizar datos aunque la ventana desaparezca despues. Solo llamadas rapidas.
/// </summary>
public static class Win32Inspector
{
    public static Win32Info Snapshot(IntPtr hwnd)
    {
        var info = new Win32Info { Hwnd = Hex(hwnd) };
        if (hwnd == IntPtr.Zero) return info;

        try
        {
            var sb = new StringBuilder(512);

            NativeMethods.GetClassName(hwnd, sb, 512);
            info.ClassName = sb.ToString();

            sb.Clear();
            NativeMethods.GetWindowText(hwnd, sb, 512);
            info.WindowTitle = sb.ToString();

            info.ParentHwnd = Hex(NativeMethods.GetParent(hwnd));

            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            info.RootHwnd = Hex(root);
            if (root != IntPtr.Zero && root != hwnd)
            {
                sb.Clear();
                NativeMethods.GetWindowText(root, sb, 512);
                info.RootTitle = sb.ToString();
            }
            else
            {
                info.RootTitle = info.WindowTitle;
            }

            info.ThreadId = (int)NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            info.ProcessId = (int)pid;
            try { info.ProcessName = Process.GetProcessById((int)pid).ProcessName + ".exe"; }
            catch { info.ProcessName = ""; }

            if (NativeMethods.GetWindowRect(hwnd, out var rc))
                info.WindowRect = $"{rc.Left},{rc.Top} {rc.Right - rc.Left}x{rc.Bottom - rc.Top}";
        }
        catch { /* snapshot best-effort */ }

        return info;
    }

    private static string Hex(IntPtr h) => h == IntPtr.Zero ? "0x0" : "0x" + h.ToInt64().ToString("X8");
}
