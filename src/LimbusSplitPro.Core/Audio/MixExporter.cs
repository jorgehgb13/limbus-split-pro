using NAudio.Wave;

namespace LimbusSplitPro.Core.Audio;

public sealed record MixExportOptions(string OutputWavPath, bool ApplyLimiterIfClipping = true);

public sealed record MixExportResult(
    string OutputPath,
    float PeakAmplitude,
    bool LimiterWasApplied,
    double LimiterGainReductionDb);

/// <summary>
/// Exporta la mezcla actual (respetando exactamente volumen/mute/solo por
/// pista) de forma offline, con precisión de sample — nunca grabando la
/// salida del dispositivo en tiempo real (sección 17).
/// </summary>
public static class MixExporter
{
    public static MixExportResult Export(IReadOnlyList<TrackChannel> tracks, MixExportOptions options)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No hay pistas para exportar.");
        }

        var format = tracks[0].WaveFormat;
        foreach (var t in tracks)
        {
            if (t.WaveFormat.SampleRate != format.SampleRate || t.WaveFormat.Channels != format.Channels)
            {
                throw new InvalidOperationException(
                    "Las pistas no comparten frecuencia de muestreo/canales; no se puede exportar una mezcla coherente.");
            }
        }

        // Rebobinamos cada pista al inicio (sample 0) para exportar la
        // mezcla completa desde el principio, sin importar dónde estaba el
        // cursor de reproducción cuando el usuario pulsó "Exportar mezcla".
        foreach (var t in tracks)
        {
            t.RequestSeek(0);
        }

        var totalSamples = tracks.Max(t => t.TotalSamples);
        var channels = format.Channels;
        const int blockFrames = 4096;
        var block = new float[blockFrames * channels];
        var mixedBlock = new float[blockFrames * channels];

        float peak = 0f;

        using var writer = new WaveFileWriter(options.OutputWavPath, format);

        long framesWritten = 0;
        while (framesWritten < totalSamples)
        {
            Array.Clear(mixedBlock);
            var framesThisBlock = (int)Math.Min(blockFrames, totalSamples - framesWritten);

            foreach (var track in tracks)
            {
                Array.Clear(block);
                var read = track.Read(block, 0, framesThisBlock * channels);
                for (var i = 0; i < read; i++)
                {
                    mixedBlock[i] += block[i];
                }
            }

            for (var i = 0; i < framesThisBlock * channels; i++)
            {
                peak = Math.Max(peak, Math.Abs(mixedBlock[i]));
            }

            writer.WriteSamples(mixedBlock, 0, framesThisBlock * channels);
            framesWritten += framesThisBlock;
        }

        var limiterApplied = false;
        var gainReductionDb = 0.0;

        if (peak > 1f && options.ApplyLimiterIfClipping)
        {
            // No se normaliza silenciosamente: se documenta cuánta
            // reducción de ganancia se aplicó (sección 17: "No normalices
            // silenciosamente. Documenta cualquier limitador o reducción de
            // ganancia.").
            limiterApplied = true;
            gainReductionDb = 20 * Math.Log10(1f / peak);
            ApplyGainAndRewrite(options.OutputWavPath, 1f / peak);
        }

        return new MixExportResult(options.OutputWavPath, peak, limiterApplied, gainReductionDb);
    }

    private static void ApplyGainAndRewrite(string path, float gain)
    {
        // Segunda pasada simple: relee el WAV recién escrito y reescala.
        // Para archivos muy grandes esto podría optimizarse a un solo paso,
        // pero se prioriza corrección y claridad sobre rendimiento aquí.
        var tempPath = path + ".tmp";
        using (var reader = new AudioFileReader(path))
        using (var writer = new WaveFileWriter(tempPath, reader.WaveFormat))
        {
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    buffer[i] *= gain;
                }

                writer.WriteSamples(buffer, 0, read);
            }
        }

        File.Delete(path);
        File.Move(tempPath, path);
    }
}
