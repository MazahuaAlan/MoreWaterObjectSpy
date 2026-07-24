using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Infrastructure;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(object o) => JsonSerializer.Serialize(o, Opts);

    /// <summary>Exporta el historial completo a un .json en el Escritorio y devuelve la ruta.</summary>
    public static string ExportToFile(IReadOnlyList<CapturedObject> items)
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var path = Path.Combine(dir, $"MoreWaterObjectSpy_capturas_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, ToJson(items));
        return path;
    }
}
