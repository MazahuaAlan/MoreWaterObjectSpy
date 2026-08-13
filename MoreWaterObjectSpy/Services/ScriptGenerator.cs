using System.IO;
using System.Text;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Services;

/// <summary>Genera el script Winium/Java a partir de los pasos grabados.</summary>
public static class ScriptGenerator
{
    public static string ToJava(IReadOnlyList<RecordedStep> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ============================================================");
        sb.AppendLine("// Script generado por MoreWater Object Spy (grabador v0.5)");
        sb.AppendLine("// Requiere un WebDriver de Winium/WinAppDriver inicializado como 'driver'.");
        sb.AppendLine("// ============================================================");
        sb.AppendLine();

        if (steps.Count == 0)
        {
            sb.AppendLine("// (sin pasos grabados)");
            return sb.ToString();
        }

        foreach (var s in steps)
        {
            switch (s.Kind)
            {
                case StepKind.Click:
                    if (string.IsNullOrWhiteSpace(s.Locator))
                        sb.AppendLine($"// (sin locator) click en: {s.Target}");
                    else
                        sb.AppendLine($"driver.findElement({s.Locator}).click();  // {s.Target}");
                    break;

                case StepKind.Type:
                    if (string.IsNullOrWhiteSpace(s.Locator))
                        sb.AppendLine($"// (sin locator) escribir: \"{Esc(s.Value)}\"");
                    else
                        sb.AppendLine($"driver.findElement({s.Locator}).sendKeys(\"{Esc(s.Value)}\");  // {s.Target}");
                    break;

                case StepKind.Key:
                    sb.AppendLine($"// tecla {s.Value}   ->   .sendKeys(Keys.{s.Value});");
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Genera el script en estilo Page Object: primero declara los objetos (locators con nombre)
    /// y luego las acciones que los usan. Reutiliza la variable si el mismo locator se repite.
    /// </summary>
    public static string ToJavaPageObject(IReadOnlyList<RecordedStep> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// ============================================================");
        sb.AppendLine("// Script (Page Object) generado por MoreWater Object Spy");
        sb.AppendLine("// Requiere un WebDriver de Winium/WinAppDriver inicializado como 'driver'.");
        sb.AppendLine("// ============================================================");
        sb.AppendLine();

        if (steps.Count == 0) { sb.AppendLine("// (sin pasos grabados)"); return sb.ToString(); }

        // 1) Declaraciones de objetos (una variable By por locator distinto)
        var vars = new Dictionary<string, string>();  // locator -> nombre de variable
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

        // 2) Acciones usando las variables declaradas
        sb.AppendLine("// --- Acciones ---");
        foreach (var s in steps)
        {
            switch (s.Kind)
            {
                case StepKind.Click:
                    sb.AppendLine(string.IsNullOrWhiteSpace(s.Locator)
                        ? $"// (sin locator) click en: {s.Target}"
                        : $"driver.findElement({vars[s.Locator]}).click();");
                    break;
                case StepKind.Type:
                    sb.AppendLine(string.IsNullOrWhiteSpace(s.Locator)
                        ? $"// (sin locator) escribir: \"{Esc(s.Value)}\""
                        : $"driver.findElement({vars[s.Locator]}).sendKeys(\"{Esc(s.Value)}\");");
                    break;
                case StepKind.Key:
                    sb.AppendLine($"// tecla {s.Value}   ->   .sendKeys(Keys.{s.Value});");
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Nombre de variable a partir del tipo + nombre del objeto (p.ej. txtUsuario, btnAceptar).</summary>
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
