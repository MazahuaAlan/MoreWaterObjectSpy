namespace MoreWaterObjectSpy.Infrastructure;

/// <summary>
/// Ventana transparente sin bordes que dibuja un rectangulo rojo sobre el elemento capturado,
/// como Inspect.exe. No roba el foco ni intercepta clicks (WS_EX_TRANSPARENT/NOACTIVATE) y se
/// oculta sola tras ~1.6s.
/// </summary>
public sealed class HighlightOverlay : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1600 };

    public HighlightOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Enabled = false; // no interactua
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta; // interior 100% transparente; solo se ve el borde pintado
        _timer.Tick += (_, _) => { _timer.Stop(); Visible = false; };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80;
            const int WS_EX_TRANSPARENT = 0x20;
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(Color.Red, 3);
        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
    }

    /// <summary>Resalta el rectangulo (coordenadas de pantalla) por unos segundos.</summary>
    public void Flash(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        Bounds = new Rectangle(x, y, w, h);
        if (!Visible) Show();
        Invalidate();
        _timer.Stop();
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}