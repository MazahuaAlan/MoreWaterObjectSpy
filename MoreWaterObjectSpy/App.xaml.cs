using System.Windows;
using Wpf.Ui.Appearance;

namespace MoreWaterObjectSpy;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Tema Claro (Fluent/Win11) por defecto; el switch en la UI lo cambia a Oscuro.
        ApplicationThemeManager.Apply(ApplicationTheme.Light);
    }
}
