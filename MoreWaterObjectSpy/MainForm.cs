using System.Text;
using System.Windows.Automation;
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

    // Arbol
    private TreeView _tree = null!;
    private TextBox _txtTreeProps = null!;
    private int _treeIndex;

    // Objeto actualmente mostrado (de captura o de arbol) — lo usan los botones Copiar
    private CapturedObject? _currentObj;

    // Cuenta regresiva (para captura bajo cursor y para cargar arbol)
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private int _cdLeft;
    private string _cdMsg = "";
    private Action? _cdAction;

    public MainForm()
    {
        BuildUi();
        _timer.Tick += OnCountdownTick;
        _service.ObjectCaptured += obj =>
        {
            if (IsHandleCreated) BeginInvoke(() => OnCaptured(obj));
        };
    }

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        Text = "MoreWater Object Spy v0.3 — Winium locators (by MazahuaAlan)";
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 40);
        Size = new Size(1040, 680);
        MinimumSize = new Size(820, 500);
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
            WrapContents = false,
            AutoScroll = true
        };

        _btnToggle = MakeButton("▶ Iniciar captura (F8)", 165, (_, _) => ToggleCapture());
        var btnPoint = MakeButton("🎯 Capturar en 3s", 135, (_, _) =>
            StartCountdown(3, "Pon el mouse sobre el objetivo (menu/hover)", () => _service.CaptureUnderCursor()));
        var btnCopyLoc = MakeButton("📋 Locator", 95, (_, _) => CopyCurrent(o => o.RecommendedLocator, "locator"));
        var btnCopyJava = MakeButton("☕ Java", 80, (_, _) => CopyCurrent(o => o.JavaSnippet, "Java"));
        var btnCopyAll = MakeButton("📄 Copiar todo", 115, (_, _) => CopyCurrent(FormatProps, "toda la data"));
        var btnExport = MakeButton("💾 JSON", 85, (_, _) => ExportJson());
        var btnClear = MakeButton("🗑 Limpiar", 90, (_, _) => ClearHistory());
        _chkTopMost = new CheckBox { Text = "Siempre visible", Checked = true, AutoSize = true, Margin = new Padding(10, 8, 0, 0) };
        _chkTopMost.CheckedChanged += (_, _) => TopMost = _chkTopMost.Checked;

        toolbar.Controls.AddRange(new Control[] { _btnToggle, btnPoint, btnCopyLoc, btnCopyJava, btnCopyAll, btnExport, btnClear, _chkTopMost });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildCaptureTab());
        tabs.TabPages.Add(BuildTreeTab());

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
        lnk.Links.Add(3, 11, "https://github.com/MazahuaAlan");
        lnk.Links.Add(20, 20, "https://github.com/MazahuaAlan");
        lnk.LinkClicked += (_, e) => OpenUrl(e.Link?.LinkData as string ?? "https://github.com/MazahuaAlan");
        footer.Controls.Add(lnk);

        Controls.Add(tabs);
        Controls.Add(footer);
        Controls.Add(toolbar);
        Controls.Add(_lblEstado);
    }

    private TabPage BuildCaptureTab()
    {
        var tab = new TabPage("Captura");

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 300, FixedPanel = FixedPanel.Panel1 };

        _lstHistorial = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Consolas", 9f) };
        _lstHistorial.SelectedIndexChanged += (_, _) => ShowSelected();
        var lblHist = new Label { Dock = DockStyle.Top, Height = 24, Text = " HISTORIAL DE CAPTURAS", Font = new Font("Segoe UI Semibold", 9f), TextAlign = ContentAlignment.MiddleLeft };
        split.Panel1.Controls.Add(_lstHistorial);
        split.Panel1.Controls.Add(lblHist);

        _txtProps = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = new Font("Consolas", 9.5f), BackColor = Color.White
        };

        var locatorPanel = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(6) };
        var lblLoc = new Label { Dock = DockStyle.Top, Height = 20, Text = "LOCATOR RECOMENDADO:", Font = new Font("Segoe UI Semibold", 8.5f) };
        _txtLocator = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            BackColor = Color.FromArgb(240, 248, 240)
        };
        locatorPanel.Controls.Add(_txtLocator);
        locatorPanel.Controls.Add(lblLoc);

        split.Panel2.Controls.Add(_txtProps);
        split.Panel2.Controls.Add(locatorPanel);
        tab.Controls.Add(split);

        _txtProps.Text =
            "MoreWater Object Spy\r\n\r\n" +
            "1. Presiona F8 para iniciar la captura global.\r\n" +
            "2. Haz click en cualquier control de la app objetivo.\r\n" +
            "   (aunque la ventana cambie o se abra un modal, el snapshot ya quedo tomado)\r\n" +
            "3. Aqui veras las propiedades (UIA / MSAA / Win32) y los locators para Winium.\r\n" +
            "4. 'Capturar en 3s' o F9: captura sin click (menus/hovers que se cierran).\r\n" +
            "5. Pestaña 'Árbol': explora toda la ventana como jerarquia.\r\n";
        return tab;
    }

    private TabPage BuildTreeTab()
    {
        var tab = new TabPage("Árbol");

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6, 5, 0, 0), WrapContents = false };
        var btnLoad = MakeButton("🌳 Cargar árbol (ventana bajo cursor, 3s)", 300, (_, _) =>
            StartCountdown(3, "Pon el mouse sobre la ventana a explorar", LoadTree));
        bar.Controls.Add(btnLoad);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 380 };

        _tree = new TreeView { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), HideSelection = false };
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.AfterSelect += Tree_AfterSelect;
        split.Panel1.Controls.Add(_tree);

        _txtTreeProps = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = new Font("Consolas", 9.5f), BackColor = Color.White,
            Text = "Presiona 'Cargar árbol', pon el mouse sobre la ventana objetivo y espera 3s.\r\n" +
                   "Luego navega el árbol y selecciona un nodo para ver sus propiedades y locators."
        };
        split.Panel2.Controls.Add(_txtTreeProps);

        tab.Controls.Add(split);
        tab.Controls.Add(bar);
        return tab;
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

    // ------------------------------------------------------------------ Cuenta regresiva

    private void StartCountdown(int secs, string msg, Action action)
    {
        _timer.Stop();
        _cdLeft = secs;
        _cdMsg = msg;
        _cdAction = action;
        _lblEstado.Text = $"⏳ {msg} — {secs}s...";
        _timer.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _cdLeft--;
        if (_cdLeft > 0)
        {
            _lblEstado.Text = $"⏳ {_cdMsg} — {_cdLeft}s...";
            return;
        }
        _timer.Stop();
        _lblEstado.Text = "🎯 Capturando...";
        var a = _cdAction;
        _cdAction = null;
        a?.Invoke();
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
                case HOTKEY_POINT: _service.CaptureUnderCursor(); return; // F9 = instantaneo bajo cursor
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_TOGGLE);
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_POINT);
        _timer.Dispose();
        _service.Dispose();
        base.OnFormClosed(e);
    }

    // ------------------------------------------------------------------ Captura

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
                _lblEstado.Text = "🟢 CAPTURANDO — haz click en la app (F9 / 'Capturar en 3s' = sin click)";
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
        int i = _lstHistorial.SelectedIndex;
        var obj = (i >= 0 && i < _shown.Count) ? _shown[i] : null;
        if (obj == null) return;
        _currentObj = obj;
        _txtProps.Text = FormatProps(obj);
        _txtLocator.Text = obj.RecommendedLocator;
    }

    private void CopyCurrent(Func<CapturedObject, string> selector, string what)
    {
        if (_currentObj == null) { _lblEstado.Text = "⚠ No hay objeto seleccionado"; return; }
        var text = selector(_currentObj);
        if (string.IsNullOrWhiteSpace(text)) { _lblEstado.Text = $"⚠ El objeto no tiene {what}"; return; }
        try { Clipboard.SetText(text); } catch { }
        _lblEstado.Text = $"📋 Copiado ({what}): " + (text.Length > 70 ? text[..70] + "..." : text);
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
        _currentObj = null;
        _lblEstado.Text = _service.IsCapturing ? "🟢 CAPTURANDO — historial limpio" : "🔴 Detenido — historial limpio";
    }

    // ------------------------------------------------------------------ Arbol

    private void LoadTree()
    {
        NativeMethods.GetCursorPos(out var p);
        var root = TreeService.RootWindowFromPoint(p.X, p.Y);
        if (root == null)
        {
            _lblEstado.Text = "⚠ No se pudo obtener la ventana bajo el cursor";
            return;
        }
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var node = MakeNode(root);
        _tree.Nodes.Add(node);
        node.Expand();
        _tree.EndUpdate();
        _lblEstado.Text = $"🌳 Árbol cargado: {node.Text} — expande los nodos y selecciona para ver locators";
    }

    private static TreeNode MakeNode(AutomationElement el)
    {
        var n = new TreeNode(TreeService.Label(el)) { Tag = el };
        if (TreeService.HasChildren(el))
            n.Nodes.Add(new TreeNode("(cargando...)")); // placeholder; se llena al expandir
        return n;
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var n = e.Node;
        if (n == null) return;
        // placeholder pendiente = un unico hijo sin Tag
        if (n.Nodes.Count == 1 && n.Nodes[0].Tag == null)
        {
            n.Nodes.Clear();
            if (n.Tag is AutomationElement el)
                foreach (var ch in TreeService.Children(el))
                    n.Nodes.Add(MakeNode(ch));
            if (n.Nodes.Count == 0) n.Nodes.Add(new TreeNode("(sin hijos)") { ForeColor = Color.Gray });
        }
    }

    private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not AutomationElement el) return;
        try
        {
            var obj = TreeService.ToCaptured(el, ++_treeIndex);
            _currentObj = obj;
            _txtTreeProps.Text = FormatProps(obj);
        }
        catch (Exception ex)
        {
            _txtTreeProps.Text = "No se pudo leer el elemento (pudo cambiar la app):\r\n" + ex.Message;
        }
    }

    // ------------------------------------------------------------------ Formato

    private static string FormatProps(CapturedObject o)
    {
        var sb = new StringBuilder();
        bool fromTree = o.Win32.ProcessId == 0 && o.Mouse.X == 0 && o.Mouse.Y == 0;
        sb.AppendLine(fromTree
            ? $"=== NODO DE ÁRBOL #{o.Index}  ({o.Timestamp}) ==="
            : $"=== CAPTURA #{o.Index}  ({o.Timestamp}) ===");
        if (!fromTree) sb.AppendLine($"Click en: ({o.Mouse.X}, {o.Mouse.Y})");
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

        if (!fromTree)
        {
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
        }

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
