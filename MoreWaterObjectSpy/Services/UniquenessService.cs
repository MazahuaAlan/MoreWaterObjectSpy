using System.Windows.Automation;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Analiza cuantos elementos matchean un locator dentro de la ventana, y en que posicion
/// (1-based) esta el elemento capturado. Permite: (a) marcar un locator como UNICO, y
/// (b) cuando hay varios iguales, dar el INDICE correcto para un XPath indexado.
/// </summary>
public static class UniquenessService
{
    public struct Match { public int Count; public int Index; } // Index 1-based; 0 = no encontrado

    /// <summary>Ventana top-level que contiene al elemento (scope de busqueda).</summary>
    public static AutomationElement? RootOf(AutomationElement el)
    {
        try
        {
            var walker = TreeWalker.ControlViewWalker;
            var root = AutomationElement.RootElement;
            var cur = el;
            while (true)
            {
                var parent = walker.GetParent(cur);
                if (parent == null || parent == root) break;
                cur = parent;
            }
            return cur;
        }
        catch { return null; }
    }

    /// <summary>Cuenta coincidencias de una propiedad bajo root y ubica al elemento objetivo.</summary>
    public static Match ByProperty(AutomationElement root, AutomationElement target, AutomationProperty prop, string value)
    {
        var m = new Match();
        try
        {
            var cond = new PropertyCondition(prop, value);
            var col = root.FindAll(TreeScope.Descendants | TreeScope.Element, cond);
            m.Count = col.Count;
            for (int i = 0; i < col.Count; i++)
            {
                if (Automation.Compare(col[i], target)) { m.Index = i + 1; break; }
            }
        }
        catch { }
        return m;
    }
}
