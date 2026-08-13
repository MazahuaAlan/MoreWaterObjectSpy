using System.Windows.Automation;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Inspectors;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Explora la jerarquia de UI Automation de una ventana con carga perezosa.
/// Es una FOTO del arbol en un instante; si la app cambia, hay que recargar.
/// </summary>
public static class TreeService
{
    /// <summary>Etiqueta corta y legible para un nodo del arbol.</summary>
    public static string Label(AutomationElement el)
    {
        try
        {
            var c = el.Current;
            var type = c.ControlType?.ProgrammaticName?.Replace("ControlType.", "") ?? "?";
            var name = c.Name ?? "";
            var id = c.AutomationId ?? "";
            var cls = c.ClassName ?? "";
            var head = !string.IsNullOrWhiteSpace(name) ? $"\"{Trim(name, 40)}\""
                     : !string.IsNullOrWhiteSpace(id) ? $"#{id}"
                     : !string.IsNullOrWhiteSpace(cls) ? cls
                     : "(sin nombre)";
            return $"{type}: {head}";
        }
        catch { return "(elemento no disponible)"; }
    }

    /// <summary>Hijos directos de un elemento. raw=true usa RawView (arbol completo con contenedores).</summary>
    public static List<AutomationElement> Children(AutomationElement el, bool raw = false)
    {
        var result = new List<AutomationElement>();
        try
        {
            var walker = raw ? TreeWalker.RawViewWalker : TreeWalker.ControlViewWalker;
            var child = walker.GetFirstChild(el);
            int guard = 0;
            while (child != null && guard++ < 4000)
            {
                result.Add(child);
                child = walker.GetNextSibling(child);
            }
        }
        catch { }
        return result;
    }

    public static bool HasChildren(AutomationElement el, bool raw = false)
    {
        try
        {
            var walker = raw ? TreeWalker.RawViewWalker : TreeWalker.ControlViewWalker;
            return walker.GetFirstChild(el) != null;
        }
        catch { return false; }
    }

    /// <summary>Elemento raiz para el arbol: la ventana top-level bajo el punto dado.</summary>
    public static AutomationElement? RootWindowFromPoint(int x, int y)
    {
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (el == null) return null;
            // subir hasta la ventana top-level (hijo directo del desktop)
            var walker = TreeWalker.ControlViewWalker;
            var top = el;
            var root = AutomationElement.RootElement;
            while (true)
            {
                var parent = walker.GetParent(top);
                if (parent == null || parent == root) break;
                top = parent;
            }
            return top;
        }
        catch { return null; }
    }

    /// <summary>Construye un CapturedObject (UIA + locators) desde un nodo del arbol.</summary>
    public static CapturedObject ToCaptured(AutomationElement el, int index)
    {
        var obj = new CapturedObject
        {
            Index = index,
            Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            UiAutomation = UIAutomationInspector.BuildInfo(el)
        };
        LocatorGenerator.Apply(obj);
        return obj;
    }

    private static string Trim(string s, int max) => s.Length > max ? s[..max] + "…" : s;
}
