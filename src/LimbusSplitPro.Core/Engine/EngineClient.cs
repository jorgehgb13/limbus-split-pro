using System.Diagnostics;
using System.Text.Json;

namespace LimbusSplitPro.Core.Engine;

/// <summary>
/// Administra el proceso hijo del motor de separación (Python embebido).
/// Reglas duras (sección 8 del prompt original):
///  - Nunca se usa cmd.exe ni se concatena texto del usuario en una cadena
///    de comandos: se usa <see cref="ProcessStartInfo.ArgumentList"/>.
///  - stdout se reserva exclusivamente para eventos JSON Lines; stderr es
///    para logs técnicos libres (se reenvían al logger, nunca a la UI
///    directamente sin traducir).
///  - Cancelación por señal controlada + limpieza de procesos hijos.
///  - Al cerrar la app, se garantiza que no quede ningún python.exe vivo.
/// </summary>
public sealed class EngineClient : IAsyncDisposable
{
    private readonly string _pythonExePath;
    private readonly string _engineScriptPath;
    private Process? _process;

    public event Action<EngineEvent>? EventReceived;
    public event Action<string>? TechnicalLogReceived;

    public EngineClient(string pythonExePath, string engineScriptPath)
    {
        _pythonExePath = pythonExePath;
        _engineScriptPath = engineScriptPath;
    }

    public bool IsRunning => _process is { HasExited: false };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("El motor ya está corriendo.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // ArgumentList evita cualquier problema de escapado con rutas que
        // contienen espacios, comillas o caracteres Unicode.
        startInfo.ArgumentList.Add(_engineScriptPath);

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => HandleStdoutLine(e.Data);
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                TechnicalLogReceived?.Invoke(e.Data);
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("No se pudo iniciar el proceso del motor de separación.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await Task.CompletedTask;
    }

    private void HandleStdoutLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            var evt = JsonSerializer.Deserialize<EngineEvent>(line);
            if (evt is not null)
            {
                EventReceived?.Invoke(evt);
            }
        }
        catch (JsonException)
        {
            // Una línea de stdout que no es JSON válido es, por definición,
            // un bug del motor, no algo que deba mostrarse crudo al
            // usuario. Se registra como log técnico para diagnóstico.
            TechnicalLogReceived?.Invoke($"[stdout no-JSON] {line}");
        }
    }

    public async Task SendCommandAsync(EngineCommand command, CancellationToken cancellationToken)
    {
        if (!IsRunning || _process is null)
        {
            throw new InvalidOperationException("El motor no está corriendo.");
        }

        var json = JsonSerializer.Serialize(command);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    public Task RequestCancelAsync(CancellationToken cancellationToken) =>
        SendCommandAsync(new EngineCommand { Type = "cancel" }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                // Señal controlada primero: le pedimos al motor que salga
                // limpio cerrando stdin, en vez de matarlo a la fuerza.
                try
                {
                    _process.StandardInput.Close();
                }
                catch
                {
                    // ignorar: puede que ya esté cerrado
                }

                var exited = _process.WaitForExit(3000);
                if (!exited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }

        await Task.CompletedTask;
    }
}
