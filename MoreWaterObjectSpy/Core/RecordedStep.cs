using System.Text.Json.Serialization;

namespace MoreWaterObjectSpy.Core;

public enum StepKind { Click, Type, Key }

/// <summary>Un paso de la grabacion: click sobre un objeto, escritura, o tecla especial.</summary>
public class RecordedStep
{
    public int Index { get; set; }
    public StepKind Kind { get; set; }
    public string Target { get; set; } = "";   // nombre legible del objeto
    public string Locator { get; set; } = "";   // By... del objeto (click/type)
    public string Value { get; set; } = "";      // texto escrito o nombre de tecla

    [JsonIgnore]
    public CapturedObject? Object { get; set; }

    [JsonIgnore]
    public string Display => Kind switch
    {
        StepKind.Click => $"● Click  ·  {Target}",
        StepKind.Type => $"⌨ Escribir  \"{Value}\"  ·  {Target}",
        StepKind.Key => $"⏎ Tecla  {Value}",
        _ => Target
    };
}
