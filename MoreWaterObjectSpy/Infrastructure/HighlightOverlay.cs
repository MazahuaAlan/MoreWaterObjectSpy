using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace MoreWaterObjectSpy.Infrastructure;

/// <summary>
/// Ventana WPF transparente, sin bordes y click-through que dibuja un rectangulo rojo sobre el
/// elemento capturado (como Inspect.exe). No roba el foco ni intercepta clics y se oculta sola.
/// Recibe coordenadas de PANTALLA en pixeles fisicos y las convierte a DIP segun el DPI.
/// </summary>
public sealed class HighlightOverlay : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private readonly System.Windows.Shapes.Rectangle _rect;

    public HighlightOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        IsHitTestVisible = false;
        Focusable = false;

        _rect = new System.Windows.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xEF, 0x4B, 0x4C)),
            StrokeThickness = 3,
            Fill = Brushes.Transparent,
            RadiusX = 3,
            RadiusY = 3
        };
        Content = _rect;

        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Ventana no activable y transparente a clics a nivel Win32
        var hwnd = new WindowInteropHelper(this).Handle;
        const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>Resalta el rectangulo (coordenadas de pantalla en pixeles) por unos segundos.</summary>
    public void Flash(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        if (!IsVisible) Show();

        // px fisicos -> DIP segun DPI del monitor
        double sx = 1, sy = 1;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget != null)
        {
            sx = src.CompositionTarget.TransformToDevice.M11;
            sy = src.CompositionTarget.TransformToDevice.M22;
        }
        Left = x / sx; Top = y / sy; Width = w / sx; Height = h / sy;

        _timer.Stop();
        _timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
