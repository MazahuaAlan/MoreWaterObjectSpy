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

    /// <summary>Exporta el script a un .java en el Escritorio y devuelve la ruta.</summary>
    public static string ExportToFile(IReadOnlyList<RecordedStep> steps)
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var path = Path.Combine(dir, $"MoreWaterObjectSpy_script_{DateTime.Now:yyyyMMdd_HHmmss}.java");
        File.WriteAllText(path, ToJava(steps));
        return path;
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
