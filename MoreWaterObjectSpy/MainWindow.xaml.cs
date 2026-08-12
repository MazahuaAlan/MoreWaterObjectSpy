using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Infrastructure;
using MoreWaterObjectSpy.Services;

namespace MoreWaterObjectSpy;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private const int HOTKEY_TOGGLE = 1; // F8
    private const int HOTKEY_POINT = 2;  // F9

    private readonly CaptureService _service = new();
    private readonly HighlightOverlay _overlay = new();
    private readonly DispatcherTimer _cd = new() { Interval = TimeSpan.FromSeconds(1) };

    private CapturedObject? _current;
    private bool _dark;
    private int _cdLeft;
    private string _cdMsg = "";
    private Action? _cdAction;
    private int _treeIndex;
    private IntPtr _hwnd;

    public MainWindow()
    {
        InitializeComponent();
        LstHistorial.DisplayMemberPath = "DisplayName";
        _cd.Tick += OnCountdownTick;
        Tree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnItemExpanded));
        _service.ObjectCaptured += obj => Dispatcher.BeginInvoke(() => OnCaptured(obj));

        TxtProps.Text =
            "MoreWater Object Spy\r\n\r\n" +
            "1. Presiona F8 para iniciar la captura global.\r\n" +
            "2. Haz click en cualquier control de la app objetivo.\r\n" +
            "   (aunque la ventana cambie o se abra un modal, el snapshot ya quedo tomado)\r\n" +
            "3. Aqui veras propiedades (UIA / MSAA / Win32) y los locators para Winium.\r\n" +
            "4. 'Capturar en 3s' o F9: captura sin click (menus/hovers que se cierran).\r\n" +
            "5. 'Árbol de elementos' (izquierda): explora toda la ventana como jerarquia.\r\n";
    }

    // ---------------- Hotkeys / ciclo de vida ----------------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        bool f8 = NativeMethods.RegisterHotKey(_hwnd, HOTKEY_TOGGLE, 0, NativeMethods.VK_F8);
        bool f9 = NativeMethods.RegisterHotKey(_hwnd, HOTKEY_POINT, 0, NativeMethods.VK_F9);
        if (!f8 || !f9) Status("⚠ No se pudieron registrar F8/F9 globales (¿otra app los usa?). Usa los botones.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case HOTKEY_TOGGLE: ToggleCapture(); handled = true; break;
                case HOTKEY_POINT: _service.CaptureUnderCursor(); handled = true; break;
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_TOGGLE);
            NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_POINT);
        }
        _cd.Stop();
        try { _overlay.Close(); } catch { }
        _service.Dispose();
        base.OnClosed(e);
    }

    // ---------------- Estado / tema / navegacion ----------------

    private void Status(string msg)
    {
        bool cap = _service.IsCapturing;
        EstadoBar.Title = cap ? "Capturando" : "Detenido";
        EstadoBar.Message = msg;
        LblEstadoMini.Text = cap ? "Capturando" : "Detenido";
        StatusDot.Fill = (Brush)FindResource(cap ? "AccentBlue" : "AccentRed");
    }

    private void ThemeSwitch_Click(object sender, RoutedEventArgs e)
    {
        _dark = (sender as Wpf.Ui.Controls.ToggleSwitch)?.IsChecked == true;
        ApplicationThemeManager.Apply(_dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        ThemeSwitch.IsChecked = _dark;
        ThemeSwitch2.IsChecked = _dark;
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender == NavExport)
        {
            NavExport.IsChecked = false; NavCapturas.IsChecked = true;
            ShowView("cap"); DoExport(); return;
        }
        ShowView(sender == NavArbol ? "arbol"
               : sender == NavConfig ? "config"
               : sender == NavAcerca ? "about" : "cap");
    }

    private void ShowView(string v)
    {
        CapturePanel.Visibility = v == "cap" ? Visibility.Visible : Visibility.Collapsed;
        TreePanel.Visibility = v == "arbol" ? Visibility.Visible : Visibility.Collapsed;
        ConfigPanel.Visibility = v == "config" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = v == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------- Cuenta regresiva ----------------

    private void StartCountdown(int secs, string msg, Action action)
    {
        _cd.Stop();
        _cdLeft = secs; _cdMsg = msg; _cdAction = action;
        Status($"⏳ {msg} — {secs}s...");
        _cd.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _cdLeft--;
        if (_cdLeft > 0) { Status($"⏳ {_cdMsg} — {_cdLeft}s..."); return; }
        _cd.Stop();
        Status("🎯 Capturando...");
        var a = _cdAction; _cdAction = null; a?.Invoke();
    }

    // ---------------- Captura ----------------

    private void Toggle_Click(object sender, RoutedEventArgs e) => ToggleCapture();

    private void ToggleCapture()
    {
        if (_service.IsCapturing)
        {
            _service.Stop();
            BtnToggle.Content = "Iniciar captura (F8)";
            Status("Detenido — F8 para reanudar");
        }
        else
        {
            try
            {
                _service.Start();
                BtnToggle.Content = "Detener captura (F8)";
                Status("Haz click en la app objetivo (F9 / 'Capturar en 3s' = sin click)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo instalar el hook de mouse:\r\n" + ex.Message,
                    "MoreWater Object Spy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Capture3s_Click(object sender, RoutedEventArgs e)
        => StartCountdown(3, "Pon el mouse sobre el objetivo (menu/hover)", () => _service.CaptureUnderCursor());

    private void OnCaptured(CapturedObject obj)
    {
        LstHistorial.Items.Add(obj);
        LstHistorial.SelectedItem = obj;
        LstHistorial.ScrollIntoView(obj);
    }

    private void Hist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstHistorial.SelectedItem is not CapturedObject obj) return;
        _current = obj;
        TxtProps.Text = FormatProps(obj);
        TxtLocator.Text = obj.RecommendedLocator;
        Highlight(obj);
    }

    private void Highlight(CapturedObject o)
    {
        var u = o.UiAutomation;
        if (u.Available && u.BoundW > 0 && u.BoundH > 0)
            _overlay.Flash(u.BoundX, u.BoundY, u.BoundW, u.BoundH);
    }

    // ---------------- Copiar / exportar ----------------

    private void CopyLoc_Click(object sender, RoutedEventArgs e) => Copy(o => o.RecommendedLocator, "locator");
    private void CopyJava_Click(object sender, RoutedEventArgs e) => Copy(o => o.JavaSnippet, "Java");
    private void CopyAll_Click(object sender, RoutedEventArgs e) => Copy(FormatProps, "toda la data");

    private void Copy(Func<CapturedObject, string> sel, string what)
    {
        if (_current == null) { Status("⚠ No hay objeto seleccionado"); return; }
        var text = sel(_current);
        if (string.IsNullOrWhiteSpace(text)) { Status($"⚠ El objeto no tiene {what}"); return; }
        try { Clipboard.SetText(text); } catch { }
        Status($"📋 Copiado ({what}): " + (text.Length > 60 ? text[..60] + "..." : text));
    }

    private void DoExport()
    {
        if (_service.History.Count == 0)
        {
            MessageBox.Show(this, "No hay capturas para exportar.", "MoreWater Object Spy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var path = _service.ExportJson();
        Status("💾 JSON exportado: " + path);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _service.ClearHistory();
        LstHistorial.Items.Clear();
        TxtProps.Clear();
        TxtLocator.Clear();
        _current = null;
        Status("Historial limpio");
    }

    // ---------------- Arbol ----------------

    private void LoadTree_Click(object sender, RoutedEventArgs e)
        => StartCountdown(3, "Pon el mouse sobre la ventana a explorar", LoadTree);

    private void LoadTree()
    {
        NativeMethods.GetCursorPos(out var p);
        var root = TreeService.RootWindowFromPoint(p.X, p.Y);
        if (root == null) { Status("⚠ No se pudo obtener la ventana bajo el cursor"); return; }
        Tree.Items.Clear();
        var node = MakeNode(root);
        Tree.Items.Add(node);
        node.IsExpanded = true;
        Status($"🌳 Árbol cargado: {node.Header}");
    }

    private TreeViewItem MakeNode(AutomationElement el)
    {
        var n = new TreeViewItem { Header = TreeService.Label(el), Tag = el };
        if (TreeService.HasChildren(el))
            n.Items.Add(new TreeViewItem { Header = "(cargando…)", Tag = null });
        return n;
    }

    private void OnItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item) return;
        if (item.Items.Count == 1 && item.Items[0] is TreeViewItem ph && ph.Tag == null)
        {
            item.Items.Clear();
            if (item.Tag is AutomationElement el)
                foreach (var ch in TreeService.Children(el))
                    item.Items.Add(MakeNode(ch));
            if (item.Items.Count == 0)
                item.Items.Add(new TreeViewItem { Header = "(sin hijos)", Foreground = (Brush)FindResource("TextFillColorSecondaryBrush") });
        }
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not AutomationElement el) return;
        try
        {
            var obj = TreeService.ToCaptured(el, ++_treeIndex);
            _current = obj;
            TxtTreeProps.Text = FormatProps(obj);
            Highlight(obj);
        }
        catch (Exception ex)
        {
            TxtTreeProps.Text = "No se pudo leer el elemento (pudo cambiar la app):\r\n" + ex.Message;
        }
    }

    // ---------------- Formato ----------------

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
        else sb.AppendLine($"(no disponible) {u.Error}");
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
            else sb.AppendLine($"(no disponible) {m.Error}");
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
        if (o.Candidates.Count == 0) sb.AppendLine("(sin candidatos utilizables)");
        else
        {
            foreach (var c in o.Candidates)
            {
                var star = c.Rank == 1 ? "★ " : "  ";
                var uniq = c.Unique ? "  (única ✓)"
                         : c.MatchCount > 1 ? $"  ({c.MatchCount} coincidencias, #{c.MatchIndex})" : "";
                sb.AppendLine($"{star}#{c.Rank} [{c.Stability}]{uniq} {c.Strategy}");
                sb.AppendLine($"     {c.Locator}");
                if (!string.IsNullOrWhiteSpace(c.Alt))
                    sb.AppendLine($"     alt (API, índice 0-based): {c.Alt}");
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
