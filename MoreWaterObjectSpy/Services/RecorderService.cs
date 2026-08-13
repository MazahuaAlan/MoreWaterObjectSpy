using System.Text;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Hooks;

namespace MoreWaterObjectSpy.Services;

/// <summary>
/// Grabador de acciones (v0.5). Encadena los clicks capturados y la escritura del teclado
/// en una secuencia de pasos. Regla simple y robusta para el flujo tipico:
///   click en un campo -> el texto que escribas se atribuye a ESE objeto,
///   y al siguiente click (o Enter/Tab) se "cierra" ese texto como un paso de escritura.
/// </summary>
public sealed class RecorderService : IDisposable
{
    private readonly CaptureService _capture;
    private readonly KeyboardHook _kb = new();
    private readonly List<RecordedStep> _steps = new();
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    private CapturedObject? _lastClick; // destino de la escritura (campo clicado)
    private int _counter;
    private bool _prevCapturing;

    public bool IsRecording { get; private set; }

    /// <summary>Se dispara cuando cambian los pasos (marshalar a UI con Dispatcher).</summary>
    public event Action? Changed;

    public RecorderService(CaptureService capture)
    {
        _capture = capture;
        _capture.ObjectCaptured += OnClick;
        _kb.KeyPressed += OnKey;
    }

    public IReadOnlyList<RecordedStep> Steps
    {
        get { lock (_lock) return _steps.ToList(); }
    }

    public void Start()
    {
        if (IsRecording) return;
        _prevCapturing = _capture.IsCapturing;
        if (!_capture.IsCapturing) _capture.Start();
        _kb.Install();
        IsRecording = true;
    }

    public void Stop()
    {
        if (!IsRecording) return;
        lock (_lock) Flush();
        _kb.Uninstall();
        if (!_prevCapturing) _capture.Stop();
        IsRecording = false;
        Changed?.Invoke();
    }

    private void OnClick(CapturedObject obj)
    {
        if (!IsRecording) return;
        lock (_lock)
        {
            Flush();
            _steps.Add(new RecordedStep
            {
                Index = ++_counter,
                Kind = StepKind.Click,
                Target = Short(obj),
                Locator = obj.RecommendedLocator,
                Object = obj,
                Candidate = obj.Candidates.Count > 0 ? obj.Candidates[0] : null
            });
            _lastClick = obj;
        }
        Changed?.Invoke();
    }

    private void OnKey(KeyEvent k)
    {
        if (!IsRecording) return;
        lock (_lock)
        {
            switch (k.Kind)
            {
                case KeyKind.Char: _buffer.Append(k.Text); break;
                case KeyKind.Backspace: if (_buffer.Length > 0) _buffer.Length--; break;
                case KeyKind.Enter: Flush(); AddKey("ENTER"); break;
                case KeyKind.Tab: Flush(); AddKey("TAB"); break;
                case KeyKind.Escape: Flush(); AddKey("ESCAPE"); break;
                default: return; // otras teclas de control se ignoran
            }
        }
        Changed?.Invoke();
    }

    // ---- helpers (llamar bajo _lock) ----

    private void Flush()
    {
        if (_buffer.Length == 0) return;
        var text = _buffer.ToString();
        _buffer.Clear();
        _steps.Add(new RecordedStep
        {
            Index = ++_counter,
            Kind = StepKind.Type,
            Target = _lastClick != null ? Short(_lastClick) : "(campo con foco)",
            Locator = _lastClick?.RecommendedLocator ?? "",
            Value = text,
            Object = _lastClick,
            Candidate = _lastClick != null && _lastClick.Candidates.Count > 0 ? _lastClick.Candidates[0] : null
        });
    }

    private void AddKey(string key)
        => _steps.Add(new RecordedStep { Index = ++_counter, Kind = StepKind.Key, Value = key });

    private static string Short(CapturedObject o)
    {
        var u = o.UiAutomation;
        if (!string.IsNullOrWhiteSpace(u.Name)) return $"{u.Name} [{u.ControlType}]";
        if (!string.IsNullOrWhiteSpace(u.AutomationId)) return $"#{u.AutomationId} [{u.ControlType}]";
        return string.IsNullOrWhiteSpace(u.ClassName) ? $"<{u.ControlType}>" : $"{u.ClassName}";
    }

    public void Clear()
    {
        lock (_lock)
        {
            _steps.Clear();
            _buffer.Clear();
            _counter = 0;
            _lastClick = null;
        }
        Changed?.Invoke();
    }

    public void Dispose() => _kb.Dispose();
}
