using System.ComponentModel;
using System.Runtime.InteropServices;
using MoreWaterObjectSpy.Infrastructure;

namespace MoreWaterObjectSpy.Hooks;

/// <summary>
/// Hook global de mouse (WH_MOUSE_LL). El callback debe ser rapidisimo:
/// solo lee coordenadas + HWND bajo el punto y dispara el evento.
/// El trabajo pesado (UI Automation) se hace fuera del hilo del hook.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _proc; // referencia viva: evita que el GC recolecte el delegate

    /// <summary>(x, y, hwnd bajo el punto). Se dispara en el hilo del hook: no bloquear.</summary>
    public event Action<int, int, IntPtr>? LeftButtonDown;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled) return;
        _proc = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _proc,
            NativeMethods.GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo instalar el hook de mouse");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            // HWND capturado AQUI, antes de que la app procese el click y la ventana pueda cambiar
            var hwnd = NativeMethods.WindowFromPoint(data.pt);
            try { LeftButtonDown?.Invoke(data.pt.X, data.pt.Y, hwnd); } catch { /* nunca romper el hook */ }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
