using System.Text;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Infrastructure;
using MoreWaterObjectSpy.Services;

namespace MoreWaterObjectSpy;

public class MainForm : Form
{
    private const int HOTKEY_TOGGLE = 1; // F8
    private const int HOTKEY_POINT = 2;  // F9

    private readonly CaptureService _service = new();
    private readonly List<CapturedObject> _shown = new();

    private Label _lblEstado = null!;
    private ListBox _lstHistorial = null!;
    private TextBox _txtProps = null!;
    private TextBox _txtLocator = null!;
    private Button _btnToggle = null!;
    private CheckBox _chkTopMost = null!;

    public MainForm()
    {
        BuildUi();
        _service.ObjectCaptured += obj =>
        {
            if (IsHandleCreated) BeginInvoke(() => OnCaptured(obj));
        };
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        Text = "MoreWater Object Spy v0.2 — Winium locators (by MazahuaAlan)";
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 40);
        Size = new Size(1000, 660);
        MinimumSize = new Size(760, 480);
        TopMost = true;
        Font = new Font("Segoe UI", 9f);

        _lblEstado = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Font = new Font("Segoe UI Semibold", 10f),
            Text = "🔴 Detenido — presiona F8 (o el boton) para iniciar la captura global"
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(6, 5, 6, 0),
            WrapContents = false
        };

        _btnToggle = MakeButton("▶ Iniciar captura (F8)", 170, (_, _) => ToggleCapture());
        var btnPoint = MakeButton("🎯 Bajo cursor (F9)", 140, (_, _) => _service.CaptureUnderCursor());
        var btnCopyLoc = MakeButton("📋 Copiar locator", 130, (_, _) => CopySelected(o => o.RecommendedLocator));
        var btnCopyJava = MakeButton("☕ Copiar Java", 120, (_, _) => CopySelected(o => o.JavaSnippet));
        var btnExport = MakeButton("💾 Exportar JSON", 130, (_, _) => ExportJson());
        var btnClear = MakeButton("🗑 Limpiar", 90, (_, _) => ClearHistory());
        _chkTopMost = new CheckBox { Text = "Siempre visible", Checked = true, AutoSize = true, Margin = new Padding(10, 8, 0, 0) };
        _chkTopMost.CheckedChanged += (_, _) => TopMost = _chkTopMost.Checked;

        toolbar.Controls.AddRange(new Control[] { _btnToggle, btnPoint, btnCopyLoc, btnCopyJava, btnExport, btnClear, _chkTopMost });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 280,
            FixedPanel = FixedPanel.Panel1
        };

        _lstHistorial = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            Font = new Font("Consolas", 9f)
        };
        _lstHistorial.SelectedIndexChanged += (_, _) => ShowSelected();

        var lblHist = new Label { Dock = DockStyle.Top, Height = 24, Text = " HISTORIAL DE CAPTURAS", Font = new Font("Segoe UI Semibold", 9f), TextAlign = ContentAlignment.MiddleLeft };
        split.Panel1.Controls.Add(_lstHistorial);
        split.Panel1.Controls.Add(lblHist);

        _txtProps = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.5f),
            BackColor = Color.White
        };

        var locatorPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(6) };
        var lblLoc = new Label { Dock = DockStyle.Top, Height = 20, Text = "LOCATOR RECOMENDADO:", Font = new Font("Segoe UI Semibold", 8.5f) };
        _txtLocator = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(240, 248, 240)
        };
        locatorPanel.Controls.Add(_txtLocator);
        locatorPanel.Controls.Add(lblLoc);

        split.Panel2.Controls.Add(_txtProps);
        split.Panel2.Controls.Add(locatorPanel);

        // Firma del autor (clicable -> GitHub)
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(245, 245, 245) };
        var lnk = new LinkLabel
        {
            Dock = DockStyle.Fill,
            Text = "By MazahuaAlan  ·  github.com/MazahuaAlan",
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = Color.FromArgb(0, 102, 204)
        };
        lnk.Links.Add(3, 11, "https://github.com/MazahuaAlan"); // "MazahuaAlan" en "By MazahuaAlan"
        lnk.Links.Add(20, 20, "https://github.com/MazahuaAlan"); // "github.com/MazahuaAlan"
        lnk.LinkClicked += (_, e) => OpenUrl(e.Link?.LinkData as string ?? "https://github.com/MazahuaAlan");
        footer.Controls.Add(lnk);

        Controls.Add(split);
        Controls.Add(footer);
        Controls.Add(toolbar);
        Controls.Add(_lblEstado);

        _txtProps.Text =
            "MoreWater Object Spy — MVP\r\n\r\n" +
            "1. Presiona F8 para iniciar la captura global.\r\n" +
            "2. Ve a FrameworkXPOS y haz click en cualquier control.\r\n" +
            "   (aunque la ventana cambie o se abra un modal, el snapshot ya quedo tomado)\r\n" +
            "3. Vuelve aqui: veras las propiedades y el locator para Winium.\r\n" +
            "4. F9 captura el objeto bajo el cursor SIN hacer click (util para hovers/menus).\r\n" +
            "5. Exporta todo el historial a JSON en el Escritorio.\r\n";
    }

    private static Button MakeButton(string text, int width, EventHandler onClick)
    {
        var b = new Button { Text = text, Width = width, Height = 30 };
        b.Click += onClick;
        return b;
    }

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sin navegador disponible */ }
    }

    // ------------------------------------------------------------------ Hotkeys

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        bool f8 = NativeMethods.RegisterHotKey(Handle, HOTKEY_TOGGLE, 0, NativeMethods.VK_F8);
        bool f9 = NativeMethods.RegisterHotKey(Handle, HOTKEY_POINT, 0, NativeMethods.VK_F9);
        if (!f8 || !f9)
            _lblEstado.Text = "⚠ No se pudieron registrar F8/F9 globales (¿otra app los usa?). Usa los botones.";
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HOTKEY_TOGGLE: ToggleCapture(); return;
                case HOTKEY_POINT: _service.CaptureUnderCursor(); return;
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_TOGGLE);
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_POINT);
        _service.Dispose();
        base.OnFormClosed(e);
    }

    // ------------------------------------------------------------------ Acciones

    private void ToggleCapture()
    {
        if (_service.IsCapturing)
        {
            _service.Stop();
            _btnToggle.Text = "▶ Iniciar captura (F8)";
            _lblEstado.Text = "🔴 Detenido — F8 para reanudar";
        }
        else
        {
            try
            {
                _service.Start();
                _btnToggle.Text = "⏸ Detener captura (F8)";
                _lblEstado.Text = "🟢 CAPTURANDO — haz click en FrameworkXPOS (F9 = bajo cursor sin click)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo instalar el hook de mouse:\r\n" + ex.Message,
                    "MoreWater Object Spy", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnCaptured(CapturedObject obj)
    {
        _shown.Add(obj);
        _lstHistorial.Items.Add(obj.DisplayName);
        _lstHistorial.SelectedIndex = _lstHistorial.Items.Count - 1; // dispara ShowSelected
    }

    private void ShowSelected()
    {
        var obj = SelectedObject();
        if (obj == null) return;
        _txtProps.Text = FormatProps(obj);
        _txtLocator.Text = obj.RecommendedLocator;
    }

    private CapturedObject? SelectedObject()
    {
        int i = _lstHistorial.SelectedIndex;
        return (i >= 0 && i < _shown.Count) ? _shown[i] : (_shown.Count > 0 ? _shown[^1] : null);
    }

    private void CopySelected(Func<CapturedObject, string> selector)
    {
        var obj = SelectedObject();
        if (obj == null) { _lblEstado.Text = "⚠ No hay capturas todavia"; return; }
        var text = selector(obj);
        if (string.IsNullOrWhiteSpace(text)) { _lblEstado.Text = "⚠ La captura no tiene ese dato"; return; }
        Clipboard.SetText(text);
        _lblEstado.Text = "📋 Copiado: " + (text.Length > 80 ? text[..80] + "..." : text);
    }

    private void ExportJson()
    {
        if (_service.History.Count == 0)
        {
            MessageBox.Show(this, "No hay capturas para exportar.", "MoreWater Object Spy",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var path = _service.ExportJson();
        _lblEstado.Text = "💾 JSON exportado: " + path;
    }

    private void ClearHistory()
    {
        _service.ClearHistory();
        _shown.Clear();
        _lstHistorial.Items.Clear();
        _txtProps.Clear();
        _txtLocator.Clear();
        _lblEstado.Text = _service.IsCapturing ? "🟢 CAPTURANDO — historial limpio" : "🔴 Detenido — historial limpio";
    }

    // ------------------------------------------------------------------ Formato

    private static string FormatProps(CapturedObject o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== CAPTURA #{o.Index}  ({o.Timestamp}) ===");
        sb.AppendLine($"Click en: ({o.Mouse.X}, {o.Mouse.Y})");
        sb.AppendLine();

        sb.AppendLine("--- UI AUTOMATION ---");
        var u = o.UiAutomation;
        if (u.Available)
        {
            sb.AppendLine($"Name:             {u.Name}");
            sb.AppendLine($"AutomationId:     {u.AutomationId}");
            sb.AppendLine($"ClassName:        {u.ClassName}");
            sb.AppendLine($"ControlType:      {u.ControlType}");
            sb.AppendLine($"FrameworkId:      {u.FrameworkId}");
            sb.AppendLine($"BoundingRect:     {u.BoundingRectangle}");
            sb.AppendLine($"RuntimeId:        {u.RuntimeId}");
            sb.AppendLine($"IsEnabled:        {u.IsEnabled}");
            sb.AppendLine($"IsOffscreen:      {u.IsOffscreen}");
            sb.AppendLine($"NativeHandle:     {u.NativeWindowHandle}");
            sb.AppendLine($"Patterns:         {string.Join(", ", u.Patterns)}");
            sb.AppendLine($"SinVentana(WPF):  {u.IsWindowless}");
            if (u.Ancestors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Ancestros (hijo -> padre):");
                for (int i = 0; i < u.Ancestors.Count; i++)
                {
                    var a = u.Ancestors[i];
                    var act = a.Patterns.Exists(p =>
                        p is "Invoke" or "Toggle" or "SelectionItem" or "ExpandCollapse")
                        ? "  ← accionable" : "";
                    sb.AppendLine($"  {i + 1}. {a}{act}");
                }
            }
        }
        else
        {
            sb.AppendLine($"(no disponible) {u.Error}");
        }
        sb.AppendLine();

        sb.AppendLine("--- MSAA (IAccessible — red de seguridad Delphi) ---");
        var m = o.Msaa;
        if (m.Available)
        {
            sb.AppendLine($"Role:             {m.Role}");
            sb.AppendLine($"Name:             {m.Name}");
            sb.AppendLine($"Value:            {m.Value}");
            sb.AppendLine($"State:            {m.State}");
            sb.AppendLine($"Description:      {m.Description}");
            sb.AppendLine($"DefaultAction:    {m.DefaultAction}");
        }
        else
        {
            sb.AppendLine($"(no disponible) {m.Error}");
        }
        sb.AppendLine();

        sb.AppendLine("--- WIN32 (snapshot en el instante del click) ---");
        var w = o.Win32;
        sb.AppendLine($"HWND:             {w.Hwnd}");
        sb.AppendLine($"Parent HWND:      {w.ParentHwnd}");
        sb.AppendLine($"Root HWND:        {w.RootHwnd}");
        sb.AppendLine($"ClassName:        {w.ClassName}");
        sb.AppendLine($"WindowTitle:      {w.WindowTitle}");
        sb.AppendLine($"Ventana (root):   {w.RootTitle}");
        sb.AppendLine($"ProcessId:        {w.ProcessId}");
        sb.AppendLine($"ProcessName:      {w.ProcessName}");
        sb.AppendLine($"ThreadId:         {w.ThreadId}");
        sb.AppendLine($"WindowRect:       {w.WindowRect}");
        sb.AppendLine();

        sb.AppendLine("--- LOCATORS PARA WINIUM (rankeados por estabilidad) ---");
        if (o.Candidates.Count == 0)
        {
            sb.AppendLine("(sin candidatos utilizables)");
        }
        else
        {
            foreach (var c in o.Candidates)
            {
                var star = c.Rank == 1 ? "★ " : "  ";
                sb.AppendLine($"{star}#{c.Rank} [{c.Stability}] {c.Strategy}");
                sb.AppendLine($"     {c.Locator}");
                if (!string.IsNullOrWhiteSpace(c.Warning))
                    sb.AppendLine($"     ⚠ {c.Warning}");
                sb.AppendLine();
            }
            sb.AppendLine("Java (recomendado):");
            sb.AppendLine("  " + o.JavaSnippet);
        }

        return sb.ToString();
    }
}
