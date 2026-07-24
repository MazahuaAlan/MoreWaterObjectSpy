using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Genera candidatos de locator rankeados por estabilidad, consciente de WPF vs Win32/Delphi.
///
/// Reglas duras aprendidas en campo:
///  - NUNCA usar el WindowTitle de Win32 como Name del control: en WPF eso es el titulo de la
///    ventana raiz (p.ej. "MainWindow") y produce un locator que apunta a toda la ventana.
///  - En WPF el control no tiene HWND propio (NativeHandle=0) -> HWND/Win32 no sirven como locator.
///  - Sin Name ni AutomationId, la mejor opcion es XPath por ClassName/ControlType, anclado al
///    ancestro identificable para acotar. Advertir siempre que puede haber varios y hay que indexar.
///  - PROMOCION: si capturas un elemento de puro contenido (Text/Image dentro de un Button) sin
///    AutomationId ni pattern accionable, se sube al ancestro accionable (Button con Invoke y/o
///    AutomationId) y se recomienda ESE locator: es lo que realmente quieres clicar en Winium.
///
/// Prioridad: AutomationId > Name (exacto UIA) > XPath anclado > By.className > XPath simple > HWND.
/// </summary>
public static class LocatorGenerator
{
    private static readonly HashSet<string> ActionPatterns =
        new(StringComparer.OrdinalIgnoreCase) { "Invoke", "Toggle", "SelectionItem", "ExpandCollapse" };

    private static readonly HashSet<string> ActionableTypes =
        new(StringComparer.OrdinalIgnoreCase)
        { "Button", "MenuItem", "ListItem", "TabItem", "CheckBox", "RadioButton", "Hyperlink", "SplitButton" };

    // Tipos que suelen ser "solo contenido" (el label/icono dentro de un control accionable)
    private static readonly HashSet<string> ContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Text", "Image", "Separator" };

    public static void Apply(CapturedObject obj)
    {
        var cands = Build(obj);
        obj.Candidates = cands;

        if (cands.Count > 0)
        {
            var best = cands[0];
            obj.LocatorStrategy = best.Strategy + (best.Warning != null ? "  ⚠ " + best.Warning : "");
            obj.RecommendedLocator = best.Locator;
            obj.JavaSnippet = best.Java;
        }
        else
        {
            obj.LocatorStrategy = "Sin propiedades utilizables";
            obj.RecommendedLocator = "// No se pudo derivar un locator";
            obj.JavaSnippet = "// No se pudo derivar un locator para este objeto";
        }
    }

    /// <summary>Objetivo del locator: puede ser el elemento capturado o un ancestro promovido.</summary>
    private sealed class Target
    {
        public string ControlType = "";
        public string Name = "";
        public string AutomationId = "";
        public string ClassName = "";
        public List<string> Patterns = new();
        public List<AncestorInfo> AnchorAncestors = new(); // ancestros para armar XPath anclado
    }

    private static List<LocatorCandidate> Build(CapturedObject obj)
    {
        var list = new List<LocatorCandidate>();
        var u = obj.UiAutomation;
        var w = obj.Win32;

        // --- Caso UIA no disponible: MSAA (Delphi) y luego Win32 real ---
        if (!u.Available)
        {
            var m = obj.Msaa;
            if (m.Available && !string.IsNullOrWhiteSpace(m.Name))
                Add(list, $"MSAA Name (rol {m.Role})", "Media",
                    "UIA no expuso el control; MSAA si. Verificar unicidad del nombre", $"By.name(\"{Esc(m.Name)}\")");
            if (!string.IsNullOrWhiteSpace(w.WindowTitle) && w.Hwnd != w.RootHwnd)
                Add(list, "Win32 WindowText (UIA no disponible)", "Media", null, $"By.name(\"{Esc(w.WindowTitle)}\")");
            if (!string.IsNullOrWhiteSpace(w.ClassName))
                Add(list, "Win32 ClassName", "Baja", "Puede repetirse; verificar unicidad", $"By.className(\"{Esc(w.ClassName)}\")");
            Rank(list);
            return list;
        }

        bool isWpf = u.FrameworkId.Equals("WPF", StringComparison.OrdinalIgnoreCase)
                  || u.FrameworkId.Equals("XAML", StringComparison.OrdinalIgnoreCase)
                  || u.IsWindowless;

        // --- Decidir el objetivo: elemento capturado o ancestro accionable (promocion) ---
        string? promotedFrom = null;
        var target = new Target
        {
            ControlType = u.ControlType,
            Name = u.Name,
            AutomationId = u.AutomationId,
            ClassName = u.ClassName,
            Patterns = u.Patterns,
            AnchorAncestors = u.Ancestors
        };

        if (IsContentLeaf(u))
        {
            int idx = FindActionableAncestor(u.Ancestors, maxDepth: 3);
            if (idx >= 0)
            {
                var a = u.Ancestors[idx];
                promotedFrom = $"{Blank(u.ControlType, "elemento")} '{u.Name}'";
                target = new Target
                {
                    ControlType = a.ControlType,
                    Name = a.Name,
                    AutomationId = a.AutomationId,
                    ClassName = a.ClassName,
                    Patterns = a.Patterns,
                    AnchorAncestors = u.Ancestors.Skip(idx + 1).ToList()
                };
            }
        }

        GenerateFor(list, target, promotedFrom, isWpf, w);

        // Si hubo promocion, dejar el texto/hijo como dato informativo (normalmente NO es el click)
        if (promotedFrom != null && !string.IsNullOrWhiteSpace(u.Name))
            Add(list, "Texto hijo (informativo — normalmente NO es lo que clicas)", "Baja",
                "Es el label/icono dentro del control accionable de arriba", $"By.name(\"{Esc(u.Name)}\")");

        Rank(list);
        return list;
    }

    private static void GenerateFor(List<LocatorCandidate> list, Target t, string? promotedFrom, bool isWpf, Win32Info w)
    {
        string tag = promotedFrom != null ? $" (promovido desde {promotedFrom})" : "";
        // Si el control expone Value pero NO Invoke, la accion natural es escribir, no clicar
        string action = t.Patterns.Any(p => p.Equals("Value", StringComparison.OrdinalIgnoreCase))
                        && !t.Patterns.Any(p => p.Equals("Invoke", StringComparison.OrdinalIgnoreCase))
                        ? "sendKeys(\"...\")" : "click()";

        // 1) AutomationId (lo mas estable de todo)
        if (!string.IsNullOrWhiteSpace(t.AutomationId))
            Add(list, "AutomationId (el mas estable)" + tag, "Alta", null, $"By.id(\"{Esc(t.AutomationId)}\")", action);

        // 2) Name exacto del control (UIA)
        if (!string.IsNullOrWhiteSpace(t.Name))
            Add(list, "Name (caption del control)" + tag, "Alta",
                "Se rompe si cambia el texto/idioma", $"By.name(\"{Esc(t.Name)}\")", action);

        // 3) XPath anclado al ancestro identificable
        var anchor = FindAnchor(t.AnchorAncestors);
        var elTag = XTag(t.ControlType);
        if (anchor != null && !string.IsNullOrWhiteSpace(t.ClassName))
        {
            var ancTag = XTag(anchor.ControlType);
            var ancPred = !string.IsNullOrWhiteSpace(anchor.ClassName)
                ? $"[@ClassName='{Esc(anchor.ClassName)}']"
                : $"[@Name='{Esc(anchor.Name)}']";
            var xp = $"//{ancTag}{ancPred}//{elTag}[@ClassName='{Esc(t.ClassName)}']";
            Add(list, $"XPath anclado a '{anchor.ClassName}'" + tag, "Media",
                "Si hay varios controles iguales dentro de la vista, indexar: (…)[1], (…)[2]",
                $"By.xpath(\"{Esc(xp)}\")", action);
        }

        // 4) By.className directo (mas limpio que XPath; misma selectividad)
        if (!string.IsNullOrWhiteSpace(t.ClassName))
            Add(list, "By.className (alternativa simple)" + tag, "Baja",
                "Devuelve el primero que coincida; si hay varios, usar el XPath anclado", $"By.className(\"{Esc(t.ClassName)}\")", action);

        // 5) XPath simple por ControlType + ClassName
        if (!string.IsNullOrWhiteSpace(t.ClassName))
            Add(list, "XPath por ClassName" + tag, "Baja",
                "Probablemente devuelve varios; combinar con ancestro o indice", $"By.xpath(\"//{elTag}[@ClassName='{Esc(t.ClassName)}']\")", action);

        // 6) HWND: solo Win32 real (no WPF sin ventana) y si no es la ventana raiz
        if (!isWpf && w.Hwnd != "0x0" && w.Hwnd != w.RootHwnd)
            Add(list, "HWND (volatil, solo sesion actual)", "Volatil",
                "El handle cambia en cada ejecucion; no usar en scripts", $"// HWND: {w.Hwnd}", action);
    }

    // --- Heuristicas ---

    /// <summary>El elemento es "puro contenido" (label/icono), no un control accionable.</summary>
    private static bool IsContentLeaf(UIAutomationInfo u)
    {
        bool leafType = ContentTypes.Contains(u.ControlType);
        bool hasActionPattern = u.Patterns.Any(p => ActionPatterns.Contains(p));
        bool hasId = !string.IsNullOrWhiteSpace(u.AutomationId);
        return leafType && !hasActionPattern && !hasId;
    }

    /// <summary>Indice del primer ancestro accionable dentro de maxDepth niveles; -1 si no hay.</summary>
    private static int FindActionableAncestor(List<AncestorInfo> ancestors, int maxDepth)
    {
        for (int i = 0; i < ancestors.Count && i < maxDepth; i++)
        {
            var a = ancestors[i];
            bool byPattern = a.Patterns.Any(p => ActionPatterns.Contains(p));
            bool byType = ActionableTypes.Contains(a.ControlType);
            if (byPattern || byType) return i;
        }
        return -1;
    }

    /// <summary>Ancestro mas cercano con identidad util (una "vista"/panel con ClassName o Name propio).</summary>
    private static AncestorInfo? FindAnchor(List<AncestorInfo> ancestors)
    {
        foreach (var a in ancestors)
        {
            if (a.ControlType.Equals("Window", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(a.ClassName) || !string.IsNullOrWhiteSpace(a.Name))
                return a;
        }
        foreach (var a in ancestors)
            if (a.ControlType.Equals("Window", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.Name))
                return a;
        return null;
    }

    /// <summary>ControlType -> tag XPath de WinAppDriver/Winium (Edit, Button, Window, Custom, Pane...).</summary>
    private static string XTag(string controlType) =>
        string.IsNullOrWhiteSpace(controlType) ? "*" : controlType;

    private static string Blank(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s;

    private static void Add(List<LocatorCandidate> list, string strategy, string stability, string? warn, string locator, string action = "click()")
        => list.Add(new LocatorCandidate
        {
            Strategy = strategy,
            Stability = stability,
            Warning = warn,
            Locator = locator,
            Java = locator.StartsWith("By.")
                ? $"driver.findElement({locator}).{action};"
                : locator   // comentario (p.ej. HWND): se deja tal cual
        });

    private static void Rank(List<LocatorCandidate> list)
    {
        for (int i = 0; i < list.Count; i++) list[i].Rank = i + 1;
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
