using System.IO;
using System.Text;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Services;

/// <summary>Genera el script Winium/Java a partir de los pasos grabados.</summary>
public static class ScriptGenerator
{
    // ---------- Script plano ----------
    public static string ToJava(IReadOnlyList<RecordedStep> steps)
    {
        var sb = Header("Script");
        if (steps.Count == 0) { sb.AppendLine("// (sin pasos grabados)"); return sb.ToString(); }
        foreach (var s in steps) EmitAction(sb, s, LocatorOf(s));
        return sb.ToString();
    }

    // ---------- Script Page Object (declaraciones + acciones) ----------
    public static string ToJavaPageObject(IReadOnlyList<RecordedStep> steps)
    {
        var sb = Header("Script (Page Object)");
        if (steps.Count == 0) { sb.AppendLine("// (sin pasos grabados)"); return sb.ToString(); }

        var vars = new Dictionary<string, string>();
        var used = new HashSet<string>();
        var decls = new StringBuilder();
        foreach (var s in steps)
        {
            if (s.Kind == StepKind.Key || string.IsNullOrWhiteSpace(s.Locator)) continue;
            if (vars.ContainsKey(s.Locator)) continue;
            var name = UniqueVar(VarName(s), used);
            vars[s.Locator] = name;
            decls.AppendLine($"private final By {name} = {s.Locator};");
        }
        sb.AppendLine("// --- Objetos ---");
        sb.Append(decls);
        sb.AppendLine();
        sb.AppendLine("// --- Acciones ---");
        foreach (var s in steps)
        {
            var by = (s.Kind != StepKind.Key && vars.TryGetValue(s.Locator, out var v)) ? v : s.Locator;
            EmitAction(sb, s, by);
        }
        return sb.ToString();
    }

    // ---------- Emisor de una accion ----------
    private static void EmitAction(StringBuilder sb, RecordedStep s, string byExpr)
    {
        switch (s.Kind)
        {
            case StepKind.Click:
                if (string.IsNullOrWhiteSpace(s.Locator)) { sb.AppendLine($"// (sin locator) click en: {s.Target}"); break; }
                sb.AppendLine($"{Access(s, byExpr)}.click();{IndexNote(s)}");
                break;
            case StepKind.Type:
                if (string.IsNullOrWhiteSpace(s.Locator)) { sb.AppendLine($"// (sin locator) escribir: \"{Esc(s.Value)}\""); break; }
                sb.AppendLine($"{Access(s, byExpr)}.sendKeys(\"{Esc(s.Value)}\");{IndexNote(s)}");
                break;
            case StepKind.Key:
                sb.AppendLine($"// tecla {s.Value}   ->   .sendKeys(Keys.{s.Value});");
                break;
        }
    }

    /// <summary>
    /// Si el locator NO es unico (varias coincidencias y no es XPath indexado), usa
    /// findElements(...).get(indice) con el indice 0-based del objeto capturado.
    /// </summary>
    private static string Access(RecordedStep s, string byExpr)
    {
        var c = s.Candidate;
        if (c != null && !c.Unique && c.MatchCount > 1 && c.MatchIndex > 0)
            return $"driver.findElements({byExpr}).get({c.MatchIndex - 1})";
        return $"driver.findElement({byExpr})";
    }

    private static string IndexNote(RecordedStep s)
    {
        var c = s.Candidate;
        if (c != null && !c.Unique && c.MatchCount > 1 && c.MatchIndex > 0)
            return $"  // objeto #{c.MatchIndex} de {c.MatchCount} coincidencias";
        return "";
    }

    private static string LocatorOf(RecordedStep s) => s.Locator;

    private static StringBuilder Header(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ============================================================");
        sb.AppendLine($"// {title} generado por MoreWater Object Spy");
        sb.AppendLine("// Requiere un WebDriver de Winium/WinAppDriver inicializado como 'driver'.");
        sb.AppendLine("// ============================================================");
        sb.AppendLine();
        return sb;
    }

    // ---------- Nombres de variable ----------
    private static string VarName(RecordedStep s)
    {
        var u = s.Object?.UiAutomation;
        var type = u?.ControlType ?? "";
        var prefix = type switch
        {
            "Button" or "SplitButton" => "btn",
            "Edit" or "Document" => "txt",
            "CheckBox" => "chk",
            "RadioButton" => "rad",
            "ComboBox" => "cbo",
            "List" or "ListItem" => "lst",
            "DataGrid" or "Table" or "DataItem" or "Grid" => "grid",
            "TabItem" or "Tab" => "tab",
            "Text" => "lbl",
            "MenuItem" or "Menu" => "mnu",
            _ => "obj",
        };
        var raw = !string.IsNullOrWhiteSpace(u?.AutomationId) ? u!.AutomationId
                : !string.IsNullOrWhiteSpace(u?.Name) ? u!.Name
                : !string.IsNullOrWhiteSpace(u?.ClassName) ? u!.ClassName : "elem";
        return prefix + Pascal(raw);
    }

    private static string Pascal(string s)
    {
        var sb = new StringBuilder();
        bool up = true;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(up ? char.ToUpper(ch) : ch); up = false; }
            else up = true;
        }
        var r = sb.ToString();
        if (r.Length == 0) return "Elem";
        if (char.IsDigit(r[0])) r = "N" + r;
        return r;
    }

    private static string UniqueVar(string name, HashSet<string> used)
    {
        var n = name; int i = 2;
        while (!used.Add(n)) { n = name + i; i++; }
        return n;
    }

    /// <summary>Guarda el texto de script dado en un .java en el Escritorio y devuelve la ruta.</summary>
    public static string SaveText(string text)
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var path = Path.Combine(dir, $"MoreWaterObjectSpy_script_{DateTime.Now:yyyyMMdd_HHmmss}.java");
        File.WriteAllText(path, text);
        return path;
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
