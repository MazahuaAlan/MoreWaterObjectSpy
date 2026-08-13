using System.Text.Json.Serialization;

namespace MoreWaterObjectSpy.Core;

public class MouseInfo
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class Win32Info
{
    public string Hwnd { get; set; } = "0x0";
    public string ParentHwnd { get; set; } = "0x0";
    public string RootHwnd { get; set; } = "0x0";
    public string ClassName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string RootTitle { get; set; } = "";
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = "";
    public int ThreadId { get; set; }
    public string WindowRect { get; set; } = "";
}

public class AncestorInfo
{
    public string ControlType { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public List<string> Patterns { get; set; } = new();

    public override string ToString() =>
        $"{ControlType} | Name='{Name}' | Class='{ClassName}' | AutomationId='{AutomationId}'";
}

public class UIAutomationInfo
{
    public bool Available { get; set; }
    public string? Error { get; set; }
    public string Name { get; set; } = "";
    public string AutomationId { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string FrameworkId { get; set; } = "";
    public string ControlType { get; set; } = "";
    public string BoundingRectangle { get; set; } = "";
    public string RuntimeId { get; set; } = "";
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public string NativeWindowHandle { get; set; } = "0x0";
    public List<string> Patterns { get; set; } = new();
    public List<AncestorInfo> Ancestors { get; set; } = new();

    // Bounding rectangle numerico (coordenadas de pantalla) para resaltar en pantalla
    public int BoundX { get; set; }
    public int BoundY { get; set; }
    public int BoundW { get; set; }
    public int BoundH { get; set; }

    /// <summary>Referencia viva al AutomationElement (para analisis de unicidad). No se serializa.</summary>
    [JsonIgnore]
    public object? Element { get; set; }

    /// <summary>true si es un control sin ventana propia (WPF/UWP): NativeHandle == 0.</summary>
    public bool IsWindowless =>
        Available && (NativeWindowHandle == "0x00000000" || NativeWindowHandle == "0x0");
}

/// <summary>
/// MSAA (IAccessible / oleacc). Red de seguridad para Delphi/VCL: a veces expone Role/Name/State
/// que UI Automation no muestra bien. Se consulta por punto de pantalla (AccessibleObjectFromPoint).
/// </summary>
public class MSAAInfo
{
    public bool Available { get; set; }
    public string? Error { get; set; }
    public string Role { get; set; } = "";
    public string State { get; set; } = "";
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Description { get; set; } = "";
    public string DefaultAction { get; set; } = "";
}

/// <summary>Un candidato de locator con su nivel de confianza y advertencias.</summary>
public class LocatorCandidate
{
    public int Rank { get; set; }
    public string Strategy { get; set; } = "";
    public string Locator { get; set; } = "";     // p.ej. By.id("...") / By.xpath("...")
    public string Java { get; set; } = "";         // driver.findElement(...)...
    public string Stability { get; set; } = "";    // Alta / Media / Baja / Volatil
    public string? Warning { get; set; }
    public string? Alt { get; set; }               // forma alternativa (p.ej. findElements(...).get(i))

    // Analisis de unicidad (A1). MatchCount 0 = no analizado.
    public int MatchCount { get; set; }
    public int MatchIndex { get; set; }   // posicion 1-based del elemento capturado entre las coincidencias
    public bool Unique { get; set; }

    [JsonIgnore]
    public string Header
    {
        get
        {
            var star = Rank == 1 ? "★ " : "";
            var uniq = Unique ? " (única ✓)"
                     : MatchCount > 1 ? $" ({MatchCount} coincid., #{MatchIndex})" : "";
            return $"{star}#{Rank} [{Stability}]{uniq} {Strategy}";
        }
    }
}

/// <summary>
/// Snapshot inmutable de un objeto capturado. Se arma en el momento del click
/// para que sobreviva aunque la ventana Delphi desaparezca despues.
/// </summary>
public class CapturedObject
{
    public int Index { get; set; }
    public string Timestamp { get; set; } = "";
    public MouseInfo Mouse { get; set; } = new();
    public UIAutomationInfo UiAutomation { get; set; } = new();
    public Win32Info Win32 { get; set; } = new();
    public MSAAInfo Msaa { get; set; } = new();
    public string LocatorStrategy { get; set; } = "";
    public string RecommendedLocator { get; set; } = "";
    public string JavaSnippet { get; set; } = "";
    public List<LocatorCandidate> Candidates { get; set; } = new();

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            // Solo el Name del CONTROL (UIA). No caer al titulo de la ventana Win32:
            // en WPF eso seria el titulo de la ventana raiz (p.ej. "MainWindow") para todos los controles.
            var name = !string.IsNullOrWhiteSpace(UiAutomation.Name) ? UiAutomation.Name
                     : !string.IsNullOrWhiteSpace(UiAutomation.AutomationId) ? "#" + UiAutomation.AutomationId
                     : "";
            var cls = !string.IsNullOrWhiteSpace(UiAutomation.ClassName) ? UiAutomation.ClassName : Win32.ClassName;
            var type = !string.IsNullOrWhiteSpace(UiAutomation.ControlType) ? UiAutomation.ControlType : "?";
            if (name.Length > 32) name = name[..32] + "...";
            var label = string.IsNullOrWhiteSpace(name) ? $"<{type}>" : name;
            return $"{Index}. {label} [{cls}]";
        }
    }
}
