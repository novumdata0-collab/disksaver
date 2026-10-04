using System.Windows;
using System.Windows.Threading;
using DiskSaver.Localization;

namespace DiskSaver;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Loc.Instance.SetLanguage(AppSettings.Load().Language ?? Loc.SystemLanguage);
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(Loc.F("UnexpectedError", e.Exception.Message), "DiskSaver",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
