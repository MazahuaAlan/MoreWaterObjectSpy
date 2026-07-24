using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Hooks;
using MoreWaterObjectSpy.Infrastructure;
using MoreWaterObjectSpy.Inspectors;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Orquesta la captura:
///   hook (hilo rapido) -> snapshot Win32 inmediato -> UIA en hilo de fondo -> historial -> evento a la UI.
/// El snapshot Win32 se toma ANTES de que la app Delphi procese el click, por eso
/// sobrevive aunque la ventana cambie o desaparezca.
/// </summary>
public sealed class CaptureService : IDisposable
{
    private readonly MouseHook _hook = new();
    private readonly List<CapturedObject> _history = new();
    private readonly int _ownPid = Environment.ProcessId;
    private int _counter;

    public bool IsCapturing { get; private set; }

    public IReadOnlyList<CapturedObject> History
    {
        get { lock (_history) return _history.ToList(); }
    }

    /// <summary>Se dispara en un hilo de fondo: la UI debe hacer BeginInvoke.</summary>
    public event Action<CapturedObject>? ObjectCaptured;

    public CaptureService()
    {
        _hook.LeftButtonDown += OnLeftButtonDown;
    }

    /// <summary>Llamar desde el hilo de UI (el hook LL necesita un message pump).</summary>
    public void Start()
    {
        _hook.Install();
        IsCapturing = true;
    }

    public void Stop() => IsCapturing = false;

    private void OnLeftButtonDown(int x, int y, IntPtr hwnd)
    {
        if (!IsCapturing) return;

        // 1) Snapshot Win32 sincrono en el instante del click (la ventana aun existe)
        var win32 = Win32Inspector.Snapshot(hwnd);

        // 2) Ignorar clicks sobre el propio MoreWater Object Spy
        if (win32.ProcessId == _ownPid) return;

        // 3) UIA + locator en hilo de fondo para no bloquear el hook
        Task.Run(() => Complete(x, y, hwnd, win32));
    }

    /// <summary>Captura por hotkey F9: objeto bajo el cursor, sin necesidad de click.</summary>
    public void CaptureUnderCursor()
    {
        NativeMethods.GetCursorPos(out var p);
        var hwnd = NativeMethods.WindowFromPoint(p);
        var win32 = Win32Inspector.Snapshot(hwnd);
        if (win32.ProcessId == _ownPid) return;
        Task.Run(() => Complete(p.X, p.Y, hwnd, win32));
    }

    private void Complete(int x, int y, IntPtr hwnd, Win32Info win32)
    {
        var uia = UIAutomationInspector.Inspect(hwnd, x, y);
        var msaa = MSAAInspector.Inspect(x, y);

        var obj = new CapturedObject
        {
            Index = Interlocked.Increment(ref _counter),
            Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Mouse = new MouseInfo { X = x, Y = y },
            Win32 = win32,
            UiAutomation = uia,
            Msaa = msaa
        };
        LocatorGenerator.Apply(obj);

        lock (_history) _history.Add(obj);
        ObjectCaptured?.Invoke(obj);
    }

    public void ClearHistory()
    {
        lock (_history) _history.Clear();
        _counter = 0;
    }

    public string ExportJson() => JsonExporter.ExportToFile(History);

    public void Dispose() => _hook.Dispose();
}
