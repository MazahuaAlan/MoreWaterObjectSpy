using System.Windows.Automation;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Inspectors;

/// <summary>
/// Inspector UI Automation. Se llama inmediatamente despues del snapshot Win32,
/// en un hilo de fondo. Estrategia doble: FromPoint (mas preciso, detecta elementos
/// sin HWND propio) con fallback a FromHandle del HWND capturado en el hook.
/// </summary>
public static class UIAutomationInspector
{
    public static UIAutomationInfo Inspect(IntPtr hwnd, int x, int y)
    {
        var info = new UIAutomationInfo();
        try
        {
            AutomationElement? el = null;

            try { el = AutomationElement.FromPoint(new System.Windows.Point(x, y)); }
            catch { /* la ventana pudo desaparecer; probamos con el HWND del hook */ }

            if (el == null && hwnd != IntPtr.Zero)
            {
                try { el = AutomationElement.FromHandle(hwnd); }
                catch { }
            }

            if (el == null)
            {
                info.Available = false;
                info.Error = "UI Automation no devolvio elemento (FromPoint y FromHandle fallaron). " +
                             "Usa las propiedades Win32 capturadas en el instante del click.";
                return info;
            }

            return BuildInfo(el);
        }
        catch (Exception ex)
        {
            info.Available = false;
            info.Error = ex.Message;
        }
        return info;
    }

    /// <summary>Construye UIAutomationInfo a partir de un AutomationElement (reusable por el arbol).</summary>
    public static UIAutomationInfo BuildInfo(AutomationElement el)
    {
        var info = new UIAutomationInfo();
        try
        {
            var c = el.Current;
            info.Available = true;
            info.Element = el; // para analisis de unicidad y resaltado
            info.Name = c.Name ?? "";
            info.AutomationId = c.AutomationId ?? "";
            info.ClassName = c.ClassName ?? "";
            info.FrameworkId = c.FrameworkId ?? "";
            info.ControlType = c.ControlType?.ProgrammaticName?.Replace("ControlType.", "") ?? "";

            var r = c.BoundingRectangle;
            if (!r.IsEmpty)
            {
                info.BoundingRectangle = $"{(int)r.X},{(int)r.Y} {(int)r.Width}x{(int)r.Height}";
                info.BoundX = (int)r.X; info.BoundY = (int)r.Y;
                info.BoundW = (int)r.Width; info.BoundH = (int)r.Height;
            }

            try { info.RuntimeId = string.Join(".", el.GetRuntimeId()); } catch { }
            info.IsEnabled = c.IsEnabled;
            info.IsOffscreen = c.IsOffscreen;
            info.NativeWindowHandle = "0x" + c.NativeWindowHandle.ToString("X8");

            try
            {
                foreach (var p in el.GetSupportedPatterns())
                    info.Patterns.Add(p.ProgrammaticName.Replace("PatternIdentifiers.Pattern", ""));
            }
            catch { }

            // Cadena de ancestros (para armar locators por contexto: ventana > panel > boton)
            try
            {
                var walker = TreeWalker.ControlViewWalker;
                var cur = el;
                for (int depth = 0; depth < 6; depth++)
                {
                    var parent = walker.GetParent(cur);
                    if (parent == null || parent == AutomationElement.RootElement) break;
                    var pc = parent.Current;
                    var anc = new AncestorInfo
                    {
                        ControlType = pc.ControlType?.ProgrammaticName?.Replace("ControlType.", "") ?? "?",
                        Name = pc.Name ?? "",
                        ClassName = pc.ClassName ?? "",
                        AutomationId = pc.AutomationId ?? ""
                    };
                    // patterns del ancestro: sirven para saber si es "accionable" (Invoke/Toggle/...)
                    try
                    {
                        foreach (var p in parent.GetSupportedPatterns())
                            anc.Patterns.Add(p.ProgrammaticName.Replace("PatternIdentifiers.Pattern", ""));
                    }
                    catch { }
                    info.Ancestors.Add(anc);
                    cur = parent;
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            info.Available = false;
            info.Error = ex.Message;
        }
        return info;
    }
}
