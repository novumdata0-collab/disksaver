using System.Windows;
using System.Windows.Threading;

namespace DiskSaver;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Непредвиденная ошибка:\n{e.Exception.Message}", "DiskSaver",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
