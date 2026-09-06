using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LimbusSplitPro.App.ViewModels;

namespace LimbusSplitPro.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as MainViewModel)?.Dispose();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Espacio = play/pause, pero NUNCA si el foco está en un campo de
    /// texto o si hay un diálogo modal abierto (sección 15: "El atajo de
    /// espacio no debe activarse al escribir en campos de texto ni
    /// mientras un diálogo modal está abierto").
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        if (Keyboard.FocusedElement is TextBox or PasswordBox or ComboBox)
        {
            return;
        }

        if (System.Windows.Application.Current.Windows.Cast<Window>().Any(w => w != this && w.IsVisible && w.Owner == this))
        {
            // Hay un diálogo modal (OpenFileDialog/OpenFolderDialog son
            // diálogos nativos y no aparecen aquí, pero cualquier Window
            // propio que abramos como modal sí queda cubierto).
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.TogglePlayPauseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
