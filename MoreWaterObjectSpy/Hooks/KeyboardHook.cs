using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using MoreWaterObjectSpy.Infrastructure;

namespace MoreWaterObjectSpy.Hooks;

/// <summary>Tipo de tecla capturada por el grabador.</summary>
public enum KeyKind { Char, Enter, Tab, Backspace, Escape, Other }

public sealed class KeyEvent
{
    public KeyKind Kind { get; init; }
    public string Text { get; init; } = ""; // caracter(es) si Kind == Char
    public uint VkCode { get; init; }
}

/// <summary>
/// Hook global de teclado (WH_KEYBOARD_LL) para el grabador de acciones.
/// Traduce cada tecla a texto (respetando Shift/CapsLock/layout) con ToUnicodeEx.
/// El callback es rapido y NO consume la tecla (deja que llegue a la app).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _proc; // misma firma que el hook de mouse

    public event Action<KeyEvent>? KeyPressed;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled) return;
        _proc = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo instalar el hook de teclado");
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
        if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            try { KeyPressed?.Invoke(Translate(data.vkCode, data.scanCode)); } catch { /* nunca romper el hook */ }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static KeyEvent Translate(uint vk, uint scan)
    {
        switch (vk)
        {
            case NativeMethods.VK_RETURN: return new KeyEvent { Kind = KeyKind.Enter, VkCode = vk };
            case NativeMethods.VK_TAB: return new KeyEvent { Kind = KeyKind.Tab, VkCode = vk };
            case NativeMethods.VK_BACK: return new KeyEvent { Kind = KeyKind.Backspace, VkCode = vk };
            case NativeMethods.VK_ESCAPE: return new KeyEvent { Kind = KeyKind.Escape, VkCode = vk };
        }

        // Traducir a caracter segun el estado del teclado (Shift/CapsLock) y el layout activo
        var keyState = new byte[256];
        NativeMethods.GetKeyboardState(keyState);
        var hkl = NativeMethods.GetKeyboardLayout(0);
        var sb = new StringBuilder(8);
        int rc = NativeMethods.ToUnicodeEx(vk, scan, keyState, sb, sb.Capacity, 0, hkl);
        if (rc > 0)
        {
            var s = sb.ToString();
            if (!string.IsNullOrEmpty(s) && !char.IsControl(s[0]))
                return new KeyEvent { Kind = KeyKind.Char, Text = s, VkCode = vk };
        }
        return new KeyEvent { Kind = KeyKind.Other, VkCode = vk };
    }

    public void Dispose() => Uninstall();
}
