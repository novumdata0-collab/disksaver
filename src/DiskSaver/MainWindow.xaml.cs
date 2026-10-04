using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using DiskSaver.ViewModels;

namespace DiskSaver;

public partial class MainWindow : Window
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.NewDriveDetected += (_, _) => BringToFront();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Windows рассылает WM_DEVICECHANGE с томами всем окнам верхнего уровня.
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DEVICECHANGE && wParam.ToInt32() is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE)
            _viewModel.OnDeviceChanged();
        return IntPtr.Zero;
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void Categories_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy)
            return;
        var window = new CategoriesWindow(_viewModel.Catalog) { Owner = this };
        if (window.ShowDialog() == true && window.Result is not null)
            _viewModel.ApplyCatalog(window.Result);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsCopying &&
            MessageBox.Show("Идёт копирование. Прервать и выйти?", "DiskSaver",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _viewModel.CancelCommand.Execute(null);
        base.OnClosing(e);
    }
}
