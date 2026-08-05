using System.Windows.Automation;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Genera candidatos de locator rankeados por estabilidad y UNICIDAD, consciente de WPF vs Win32/Delphi.
///
/// Reglas duras:
///  - NUNCA usar el WindowTitle de Win32 como Name del control (en WPF es el titulo de la ventana raiz).
///  - En WPF el control no tiene HWND propio (NativeHandle=0) -> HWND/Win32 no sirven como locator.
///  - AutomationId puramente NUMERICO -> normalmente autogenerado/inestable: se degrada y se prefiere Name.
///  - PROMOCION: si capturas contenido (Text/Image) dentro de un Button, se sube al control accionable.
///  - UNICIDAD (A1): se cuenta cuantos elementos matchean cada locator dentro de la ventana. Si hay varios
///    iguales, se genera un XPath INDEXADO con el indice real del elemento capturado -> coincidencia unica.
///
/// Ranking final: primero los UNICOS, luego por estabilidad (Alta > Media > Baja > Volatil).
/// </summary>
public static class LocatorGenerator
{
    private static readonly HashSet<string> ActionPatterns =
        new(StringComparer.OrdinalIgnoreCase) { "Invoke", "Toggle", "SelectionItem", "ExpandCollapse" };

    private static readonly HashSet<string> ActionableTypes =
        new(StringComparer.OrdinalIgnoreCase)
        { "Button", "MenuItem", "ListItem", "TabItem", "CheckBox", "RadioButton", "Hyperlink", "SplitButton" };

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

    private sealed class Target
    {
        public string ControlType = "";
        public string Name = "";
        public string AutomationId = "";
        public string ClassName = "";
        public List<string> Patterns = new();
        public List<AncestorInfo> AnchorAncestors = new();
    }

    private sealed class Uniq
    {
        public UniquenessService.Match Name, Id, Cls;
    }

    private static List<LocatorCandidate> Build(CapturedObject obj)
    {
        var list = new List<LocatorCandidate>();
        var u = obj.UiAutomation;
        var w = obj.Win32;
        var el = u.Element as AutomationElement;

        // --- UIA no disponible: MSAA (Delphi) y luego Win32 real ---
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

        // --- Objetivo: elemento capturado o ancestro accionable (promocion) ---
        string? promotedFrom = null;
        var target = new Target
        {
            ControlType = u.ControlType, Name = u.Name, AutomationId = u.AutomationId,
            ClassName = u.ClassName, Patterns = u.Patterns, AnchorAncestors = u.Ancestors
        };
        var targetEl = el;

        if (IsContentLeaf(u))
        {
            int idx = FindActionableAncestor(u.Ancestors, maxDepth: 3);
            if (idx >= 0)
            {
                var a = u.Ancestors[idx];
                promotedFrom = $"{Blank(u.ControlType, "elemento")} '{u.Name}'";
                target = new Target
                {
                    ControlType = a.ControlType, Name = a.Name, AutomationId = a.AutomationId,
                    ClassName = a.ClassName, Patterns = a.Patterns,
                    AnchorAncestors = u.Ancestors.Skip(idx + 1).ToList()
                };
                targetEl = ClimbParents(el, idx + 1);
            }
        }

        // --- A1: analisis de unicidad del objetivo dentro de la ventana ---
        Uniq? uniq = null;
        if (targetEl != null)
        {
            var root = UniquenessService.RootOf(targetEl);
            if (root != null)
            {
                uniq = new Uniq();
                if (!string.IsNullOrWhiteSpace(target.Name))
                    uniq.Name = UniquenessService.ByProperty(root, targetEl, AutomationElement.NameProperty, target.Name);
                if (!string.IsNullOrWhiteSpace(target.AutomationId))
                    uniq.Id = UniquenessService.ByProperty(root, targetEl, AutomationElement.AutomationIdProperty, target.AutomationId);
                if (!string.IsNullOrWhiteSpace(target.ClassName))
                    uniq.Cls = UniquenessService.ByProperty(root, targetEl, AutomationElement.ClassNameProperty, target.ClassName);
            }
        }

        GenerateFor(list, target, promotedFrom, isWpf, w, uniq);

        if (promotedFrom != null && !string.IsNullOrWhiteSpace(u.Name))
            Add(list, "Texto hijo (informativo — normalmente NO es lo que clicas)", "Baja",
                "Es el label/icono dentro del control accionable de arriba", $"By.name(\"{Esc(u.Name)}\")");

        RankByQuality(list);
        return list;
    }

    private static void GenerateFor(List<LocatorCandidate> list, Target t, string? promotedFrom, bool isWpf, Win32Info w, Uniq? uniq)
    {
        string tag = promotedFrom != null ? $" (promovido desde {promotedFrom})" : "";
        string action = t.Patterns.Any(p => p.Equals("Value", StringComparison.OrdinalIgnoreCase))
                        && !t.Patterns.Any(p => p.Equals("Invoke", StringComparison.OrdinalIgnoreCase))
                        ? "sendKeys(\"...\")" : "click()";
        var elTag = XTag(t.ControlType);

        // 1) AutomationId — degradado si es puramente numerico (autogenerado)
        if (!string.IsNullOrWhiteSpace(t.AutomationId))
        {
            bool numeric = IsNumeric(t.AutomationId);
            var c = Add(list,
                (numeric ? "AutomationId numerico (poco fiable)" : "AutomationId (el mas estable)") + tag,
                numeric ? "Baja" : "Alta",
                numeric ? "AutomationId numerico: probablemente autogenerado/inestable — prefiere Name" : null,
                $"By.id(\"{Esc(t.AutomationId)}\")", action);
            Annotate(c, uniq?.Id);
        }

        // 2) Name exacto del control
        if (!string.IsNullOrWhiteSpace(t.Name))
        {
            var c = Add(list, "Name (caption del control)" + tag, "Alta",
                "Se rompe si cambia el texto/idioma", $"By.name(\"{Esc(t.Name)}\")", action);
            Annotate(c, uniq?.Name);
        }

        // 3) XPath anclado al ancestro identificable (unicidad no verificada)
        var anchor = FindAnchor(t.AnchorAncestors);
        if (anchor != null && !string.IsNullOrWhiteSpace(t.ClassName))
        {
            var ancTag = XTag(anchor.ControlType);
            var ancPred = !string.IsNullOrWhiteSpace(anchor.ClassName)
                ? $"[@ClassName='{Esc(anchor.ClassName)}']" : $"[@Name='{Esc(anchor.Name)}']";
            var xp = $"//{ancTag}{ancPred}//{elTag}[@ClassName='{Esc(t.ClassName)}']";
            Add(list, $"XPath anclado a '{anchor.ClassName}'" + tag, "Media",
                "Si hay varios controles iguales dentro de la vista, indexar: (…)[1], (…)[2]",
                $"By.xpath(\"{Esc(xp)}\")", action);
        }

        // 4) By.className directo
        if (!string.IsNullOrWhiteSpace(t.ClassName))
        {
            var c = Add(list, "By.className (alternativa simple)" + tag, "Baja",
                "Devuelve el primero que coincida; si hay varios, usar el indice", $"By.className(\"{Esc(t.ClassName)}\")", action);
            Annotate(c, uniq?.Cls);
        }

        // 5) INDICE DETERMINISTICO — solo si NINGUN atributo es unico por si solo.
        //    Se indexa sobre el atributo mas selectivo/estable (menor nº de coincidencias;
        //    a igualdad, AutomationId > Name > ClassName). Se muestran ambas formas: XPath [n]
        //    (1-based) y findElements(...).get(n-1) (0-based).
        AddDeterministicIndex(list, t, uniq, tag, action);

        // 6) HWND: solo Win32 real (no WPF sin ventana) y si no es la ventana raiz
        if (!isWpf && w.Hwnd != "0x0" && w.Hwnd != w.RootHwnd)
            Add(list, "HWND (volatil, solo sesion actual)", "Volatil",
                "El handle cambia en cada ejecucion; no usar en scripts", $"// HWND: {w.Hwnd}", action);
    }

    /// <summary>Genera el locator indexado usando la mejor base disponible (menos coincidencias + mas estable).</summary>
    private static void AddDeterministicIndex(List<LocatorCandidate> list, Target t, Uniq? uniq, string tag, string action)
    {
        if (uniq == null) return;

        // (atributo XPath, metodo By, valor, count, indice 1-based, peso estabilidad: menor = mejor)
        var bases = new List<(string attr, string by, string val, int count, int index, int weight)>();
        if (!string.IsNullOrWhiteSpace(t.AutomationId) && uniq.Id.Count > 0 && uniq.Id.Index > 0)
            bases.Add(("AutomationId", "id", t.AutomationId, uniq.Id.Count, uniq.Id.Index, IsNumeric(t.AutomationId) ? 3 : 0));
        if (!string.IsNullOrWhiteSpace(t.Name) && uniq.Name.Count > 0 && uniq.Name.Index > 0)
            bases.Add(("Name", "name", t.Name, uniq.Name.Count, uniq.Name.Index, 1));
        if (!string.IsNullOrWhiteSpace(t.ClassName) && uniq.Cls.Count > 0 && uniq.Cls.Index > 0)
            bases.Add(("ClassName", "className", t.ClassName, uniq.Cls.Count, uniq.Cls.Index, 2));

        // Si alguno ya es unico por si solo, no hace falta indexar (ese candidato ya gana el ranking).
        if (bases.Count == 0 || bases.Any(b => b.count == 1)) return;

        // Mejor base: menos coincidencias; a igualdad, el mas estable (menor peso).
        var best = bases.OrderBy(b => b.count).ThenBy(b => b.weight).First();
        var elTag = XTag(t.ControlType);
        var xp = $"(//{elTag}[@{best.attr}='{Esc(best.val)}'])[{best.index}]";
        var alt = $"driver.findElements(By.{best.by}(\"{Esc(best.val)}\")).get({best.index - 1});";

        var c = Add(list, $"Índice determinístico sobre {best.attr} (#{best.index} de {best.count}, único)" + tag,
            "Alta", null, $"By.xpath(\"{Esc(xp)}\")", action);
        c.Unique = true; c.MatchCount = 1; c.MatchIndex = best.index;
        c.Alt = alt; // forma 0-based con la API de Selenium/Winium
    }

    /// <summary>Anota conteo/indice de coincidencias y ajusta estabilidad/advertencia.</summary>
    private static void Annotate(LocatorCandidate c, UniquenessService.Match? match)
    {
        if (match == null || match.Value.Count == 0) return;
        c.MatchCount = match.Value.Count;
        c.MatchIndex = match.Value.Index;
        if (match.Value.Count == 1)
        {
            c.Unique = true;
            if (c.Stability == "Baja") c.Stability = "Media";
            if (c.Warning != null && c.Warning.Contains("coincid")) c.Warning = null;
        }
        else
        {
            c.Unique = false;
            c.Warning = $"{match.Value.Count} coincidencias; este es #{match.Value.Index} — usa el XPath indexado";
        }
    }

    // --- Heuristicas ---

    private static bool IsNumeric(string s) => s.Length > 0 && s.All(char.IsDigit);

    private static bool IsContentLeaf(UIAutomationInfo u)
    {
        bool leafType = ContentTypes.Contains(u.ControlType);
        bool hasActionPattern = u.Patterns.Any(p => ActionPatterns.Contains(p));
        bool hasId = !string.IsNullOrWhiteSpace(u.AutomationId);
        return leafType && !hasActionPattern && !hasId;
    }

    private static int FindActionableAncestor(List<AncestorInfo> ancestors, int maxDepth)
    {
        for (int i = 0; i < ancestors.Count && i < maxDepth; i++)
        {
            var a = ancestors[i];
            if (a.Patterns.Any(p => ActionPatterns.Contains(p)) || ActionableTypes.Contains(a.ControlType))
                return i;
        }
        return -1;
    }

    private static AutomationElement? ClimbParents(AutomationElement? el, int levels)
    {
        if (el == null) return null;
        try
        {
            var walker = TreeWalker.ControlViewWalker;
            var cur = el;
            for (int i = 0; i < levels && cur != null; i++)
                cur = walker.GetParent(cur);
            return cur;
        }
        catch { return null; }
    }

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

    private static string XTag(string controlType) =>
        string.IsNullOrWhiteSpace(controlType) ? "*" : controlType;

    private static string Blank(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s;

    private static LocatorCandidate Add(List<LocatorCandidate> list, string strategy, string stability, string? warn, string locator, string action = "click()")
    {
        var c = new LocatorCandidate
        {
            Strategy = strategy,
            Stability = stability,
            Warning = warn,
            Locator = locator,
            Java = locator.StartsWith("By.") ? $"driver.findElement({locator}).{action};" : locator
        };
        list.Add(c);
        return c;
    }

    private static void Rank(List<LocatorCandidate> list)
    {
        for (int i = 0; i < list.Count; i++) list[i].Rank = i + 1;
    }

    /// <summary>Ordena: primero UNICOS, luego por estabilidad; conserva orden de insercion en empates.</summary>
    private static void RankByQuality(List<LocatorCandidate> list)
    {
        int UniqScore(LocatorCandidate c) => c.Unique ? 2 : (c.MatchCount > 1 ? 0 : 1);
        int StabScore(LocatorCandidate c) => c.Stability switch
        {
            "Alta" => 3, "Media" => 2, "Baja" => 1, _ => 0
        };
        var ordered = list
            .Select((c, i) => (c, i))
            .OrderByDescending(t => UniqScore(t.c))
            .ThenByDescending(t => StabScore(t.c))
            .ThenBy(t => t.i)
            .Select(t => t.c)
            .ToList();
        list.Clear();
        list.AddRange(ordered);
        Rank(list);
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
