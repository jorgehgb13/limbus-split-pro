using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LimbusSplitPro.App;

public partial class App : System.Windows.Application
{
    /// <summary>Ruta al Python embebido dentro de la instalación (solo lectura).</summary>
    public static string EmbeddedPythonExePath { get; private set; } = string.Empty;
    public static string EngineScriptPath { get; private set; } = string.Empty;
    public static string UserDataRoot { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var installDir = AppDomain.CurrentDomain.BaseDirectory;
        EmbeddedPythonExePath = Path.Combine(installDir, "engine", "python", "python.exe");
        EngineScriptPath = Path.Combine(installDir, "engine", "limbus_engine", "main.py");

        UserDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Limbus Split Pro");
        Directory.CreateDirectory(Path.Combine(UserDataRoot, "Models"));
        Directory.CreateDirectory(Path.Combine(UserDataRoot, "Cache"));
        Directory.CreateDirectory(Path.Combine(UserDataRoot, "Logs"));

        ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General)
            {
                ApplySystemTheme();
            }
        };
    }

    private static void ApplySystemTheme()
    {
        var isLight = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                isLight = value != 0;
            }
        }
        catch (Exception)
        {
            // Si no se puede leer la preferencia del sistema, se usa el
            // tema claro por defecto en vez de fallar el arranque.
        }

        var dictUri = isLight
            ? new Uri("Themes/Light.xaml", UriKind.Relative)
            : new Uri("Themes/Dark.xaml", UriKind.Relative);

        var current = System.Windows.Application.Current;
        var existing = current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null &&
                                  (d.Source.OriginalString.Contains("Light.xaml") ||
                                   d.Source.OriginalString.Contains("Dark.xaml")));
        if (existing is not null)
        {
            current.Resources.MergedDictionaries.Remove(existing);
        }

        current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = dictUri });
    }
}
