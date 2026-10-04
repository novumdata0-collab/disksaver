using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DiskSaver.Core;
using DiskSaver.Localization;
using Microsoft.Win32;

namespace DiskSaver.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private CategoryCatalog _catalog = CategoryStore.Load();
    private readonly ScanOptions _scanOptions = new();
    private readonly List<FileItemViewModel> _allFiles = [];
    private readonly HashSet<string> _knownRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _deviceChangeDebounce;
    private CancellationTokenSource? _cts;
    private DriveEntry? _scannedDrive;

    private DriveEntry? _selectedDrive;
    private CategoryViewModel? _selectedCategory;
    private IReadOnlyList<FileItemViewModel> _visibleFiles = [];
    private string _searchText = "";
    // Статус хранится как функция, чтобы при смене языка пересобрать текст.
    private Func<string> _status = () => Loc.T("StatusInitial");
    private string _selectionSummary = "";
    private string _destinationPath = "";
    private bool _preserveStructure = true;
    private bool _isScanning;
    private bool _isCopying;
    private double _progressValue;
    private bool _progressIndeterminate;

    public MainViewModel()
    {
        BuildCategoryRows();
        _selectedCategory = Categories[0];

        _deviceChangeDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _deviceChangeDebounce.Tick += (_, _) =>
        {
            _deviceChangeDebounce.Stop();
            RefreshDrives(announceNew: true);
        };

        RefreshDrivesCommand = new RelayCommand(() => RefreshDrives(announceNew: false), () => !IsBusy);
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsBusy && SelectedDrive is not null);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination, () => !IsCopying);
        CopyCommand = new RelayCommand(async () => await CopyAsync(), () => !IsBusy && _allFiles.Any(f => f.IsSelected));
        SelectVisibleCommand = new RelayCommand(() => SetSelected(VisibleFiles, true), () => VisibleFiles.Count > 0);
        UnselectVisibleCommand = new RelayCommand(() => SetSelected(VisibleFiles, false), () => VisibleFiles.Count > 0);
        ShowInExplorerCommand = new RelayCommand(p => ShowInExplorer(p as FileItemViewModel), p => p is FileItemViewModel);
        OpenFileCommand = new RelayCommand(p => OpenFile(p as FileItemViewModel), p => p is FileItemViewModel);

        RefreshDrives(announceNew: false);
        SelectedDrive = Drives.FirstOrDefault(d => !d.IsSystem) ?? Drives.FirstOrDefault();
        RecalculateStats();

        Loc.Instance.LanguageChanged += (_, _) => OnLanguageChanged();
    }

    /// <summary>Подключён новый диск — окно стоит вывести на передний план.</summary>
    public event EventHandler<DriveEntry>? NewDriveDetected;

    public CategoryCatalog Catalog => _catalog;

    public ObservableCollection<DriveEntry> Drives { get; } = [];
    public ObservableCollection<CategoryViewModel> Categories { get; } = [];

    public ICommand RefreshDrivesCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseDestinationCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SelectVisibleCommand { get; }
    public ICommand UnselectVisibleCommand { get; }
    public ICommand ShowInExplorerCommand { get; }
    public ICommand OpenFileCommand { get; }

    public DriveEntry? SelectedDrive
    {
        get => _selectedDrive;
        set => SetProperty(ref _selectedDrive, value);
    }

    public CategoryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ApplyFilter();
        }
    }

    public IReadOnlyList<FileItemViewModel> VisibleFiles
    {
        get => _visibleFiles;
        private set
        {
            if (SetProperty(ref _visibleFiles, value))
                OnPropertyChanged(nameof(ShowPlaceholder));
        }
    }

    public bool ShowPlaceholder => _visibleFiles.Count == 0;

    public string PlaceholderText => Loc.T(IsScanning
        ? "PlaceholderScanning"
        : _allFiles.Count == 0 ? "PlaceholderEmpty" : "PlaceholderNoMatch");

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilter();
        }
    }

    public string StatusText => _status();

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    public bool PreserveStructure
    {
        get => _preserveStructure;
        set
        {
            if (SetProperty(ref _preserveStructure, value))
                OnPropertyChanged(nameof(ByCategory));
        }
    }

    public bool ByCategory
    {
        get => !_preserveStructure;
        set => PreserveStructure = !value;
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
                OnBusyChanged();
        }
    }

    public bool IsCopying
    {
        get => _isCopying;
        private set
        {
            if (SetProperty(ref _isCopying, value))
                OnBusyChanged();
        }
    }

    public bool IsBusy => IsScanning || IsCopying;

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        private set => SetProperty(ref _progressIndeterminate, value);
    }

    /// <summary>Вызывается окном на WM_DEVICECHANGE. Событий приходит пачка, поэтому ждём секунду.</summary>
    public void OnDeviceChanged()
    {
        _deviceChangeDebounce.Stop();
        _deviceChangeDebounce.Start();
    }

    /// <summary>
    /// Применяет новые категории из настроек. Уже найденные файлы переклассифицируются,
    /// а файлы с новыми расширениями появятся только после повторного сканирования.
    /// </summary>
    public void ApplyCatalog(CategoryCatalog catalog)
    {
        var selectedKey = SelectedCategory?.Category?.Key;
        _catalog = catalog;
        BuildCategoryRows();

        var remapped = new List<FileItemViewModel>(_allFiles.Count);
        foreach (var item in _allFiles)
        {
            var category = catalog.Classify(Path.GetExtension(item.Model.FullPath));
            if (category is null || item.Size < category.MinSizeBytes)
                continue;
            var updated = new FileItemViewModel(item.Model with { Category = category }, RecalculateStats);
            updated.SetSelectedQuietly(item.IsSelected);
            remapped.Add(updated);
        }
        var hadResults = _allFiles.Count > 0;
        _allFiles.Clear();
        _allFiles.AddRange(remapped);

        _selectedCategory = Categories.FirstOrDefault(r => r.Category?.Key == selectedKey) ?? Categories[0];
        OnPropertyChanged(nameof(SelectedCategory));
        RecalculateStats();
        ApplyFilter();
        SetStatus(() => Loc.T("CatalogSaved") + (hadResults ? Loc.T("CatalogSavedRescan") : ""));
    }

    private void SetStatus(Func<string> status)
    {
        _status = status;
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnLanguageChanged()
    {
        // Подписи дисков и названия категорий собираются в коде — пересобираем.
        if (!IsBusy)
            RefreshDrives(announceNew: false);
        foreach (var row in Categories)
            row.RefreshTexts();
        foreach (var file in _allFiles)
            file.RefreshTexts();
        RecalculateStats();
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(StatusText));
    }

    private void BuildCategoryRows()
    {
        Categories.Clear();
        Categories.Add(new CategoryViewModel(null, SetCategorySelected));
        foreach (var category in _catalog.Categories)
            Categories.Add(new CategoryViewModel(category, SetCategorySelected));
    }

    private void OnBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(PlaceholderText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshDrives(bool announceNew)
    {
        var drives = DriveService.GetDrives();
        var previous = SelectedDrive?.Root;
        var added = drives.Where(d => !_knownRoots.Contains(d.Root) && !d.IsSystem).ToList();

        _knownRoots.Clear();
        _knownRoots.UnionWith(drives.Select(d => d.Root));
        Drives.Clear();
        foreach (var d in drives)
            Drives.Add(d);

        SelectedDrive = Drives.FirstOrDefault(d => d.Root == previous);

        if (announceNew && added.Count > 0 && !IsBusy)
        {
            var drive = added[0];
            SelectedDrive = drives.First(d => d.Root == drive.Root);
            SetStatus(() => Loc.F("NewDriveConnected", drive.Display));
            NewDriveDetected?.Invoke(this, drive);
        }
    }

    private async Task ScanAsync()
    {
        var drive = SelectedDrive;
        if (drive is null)
            return;
        if (!Directory.Exists(drive.Root))
        {
            MessageBox.Show(Loc.F("DriveUnavailable", drive.Root), "DiskSaver",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshDrives(announceNew: false);
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _scannedDrive = drive;
        _allFiles.Clear();
        VisibleFiles = [];
        IsScanning = true;
        ProgressIndeterminate = true;
        RecalculateStats();

        var scanner = new DiskScanner(_catalog, _scanOptions);
        var queue = new ConcurrentQueue<FoundFile>();
        var stopwatch = Stopwatch.StartNew();

        void Drain()
        {
            while (queue.TryDequeue(out var file))
                _allFiles.Add(new FileItemViewModel(file, RecalculateStats));
            RecalculateStats();
            var found = _allFiles.Count;
            var folders = scanner.Stats.Directories;
            SetStatus(() => Loc.F("ScanProgress", drive.Root, found.ToString("N0"), folders.ToString("N0")));
        }

        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background,
            (_, _) => Drain(), Dispatcher.CurrentDispatcher);

        string resultKey;
        try
        {
            await Task.Run(() =>
            {
                foreach (var file in scanner.Scan(drive.Root, token))
                    queue.Enqueue(file);
            });
            resultKey = "ScanDone";
        }
        catch (OperationCanceledException)
        {
            resultKey = "ScanStopped";
        }
        catch (Exception ex)
        {
            resultKey = "ScanError";
            MessageBox.Show(Loc.F("ScanFailed", ex.Message), "DiskSaver",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            timer.Stop();
            Drain();
            IsScanning = false;
            ProgressIndeterminate = false;
        }

        ApplyFilter();
        var count = _allFiles.Count;
        var totalBytes = _allFiles.Sum(f => f.Size);
        var errors = scanner.Stats.Errors;
        var elapsed = stopwatch.Elapsed.ToString(@"mm\:ss");
        SetStatus(() => Loc.F("ScanSummary", Loc.T(resultKey), elapsed, count.ToString("N0"),
            SizeFormatter.Format(totalBytes), errors > 0 ? Loc.F("ScanUnreadable", errors.ToString("N0")) : ""));
    }

    private async Task CopyAsync()
    {
        if (_scannedDrive is null)
            return;

        var destination = DestinationPath.Trim();
        if (destination.Length == 0 || !Directory.Exists(destination))
        {
            MessageBox.Show(Loc.T("DestinationMissing"), "DiskSaver",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (DriveService.IsSameRoot(destination, _scannedDrive.Root))
        {
            MessageBox.Show(Loc.T("DestinationSameDrive"),
                "DiskSaver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var files = _allFiles.Where(f => f.IsSelected).Select(f => f.Model).ToList();
        if (files.Count == 0)
            return;

        _cts = new CancellationTokenSource();
        IsCopying = true;
        ProgressValue = 0;
        var progress = new Progress<CopyProgress>(p =>
        {
            ProgressValue = p.BytesTotal == 0 ? 100 : p.BytesDone * 100.0 / p.BytesTotal;
            SetStatus(() => Loc.F("CopyProgress", p.FilesDone.ToString("N0"), p.FilesTotal.ToString("N0"),
                                SizeFormatter.Format(p.BytesDone), SizeFormatter.Format(p.BytesTotal)) +
                            (p.Errors > 0 ? Loc.F("CopyProgressErrors", p.Errors) : "") +
                            (p.CurrentFile.Length > 0 ? $" · {p.CurrentFile}" : ""));
        });

        CopyReport report;
        try
        {
            var options = new CopyOptions(destination,
                PreserveStructure ? CopyLayout.PreserveStructure : CopyLayout.ByCategory);
            report = await new CopyEngine().CopyAsync(files, _scannedDrive.SessionName, options, progress, _cts.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(() => Loc.T("CopyNotStarted"));
            MessageBox.Show(ex.Message, "DiskSaver", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            IsCopying = false;
        }

        var total = files.Count;
        SetStatus(() => Loc.F("CopyStatusDone", report.Copied.ToString("N0"), total.ToString("N0")) +
                        (report.Failed > 0 ? Loc.F("CopyStatusFailed", report.Failed.ToString("N0")) : "") +
                        (report.Cancelled ? Loc.T("CopyStatusCancelled") : "") +
                        Loc.F("CopyStatusArchive", report.OutputFolder));

        var message = report.IsComplete
            ? Loc.F("CopyResultOk", report.Copied.ToString("N0")) + "\n\n"
            : Loc.F("CopyResultPartial", report.Copied.ToString("N0"), report.Failed.ToString("N0")) +
              (report.Cancelled ? Loc.T("CopyResultCancelled") : "") +
              (report.Failed > 0 ? Loc.F("CopyResultErrorLog", report.ErrorLogPath) : "") +
              Loc.T("CopyResultDontFormat") + "\n\n";
        message += Loc.F("CopyResultOpenFolder", report.OutputFolder);

        if (MessageBox.Show(message, Loc.T("CopyResultTitle"), MessageBoxButton.YesNo,
                report.IsComplete ? MessageBoxImage.Information : MessageBoxImage.Warning) == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{report.OutputFolder}\"") { UseShellExecute = true });
    }

    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("BrowseTitle") };
        if (Directory.Exists(DestinationPath))
            dialog.InitialDirectory = DestinationPath;
        if (dialog.ShowDialog() == true)
            DestinationPath = dialog.FolderName;
    }

    private void ApplyFilter()
    {
        IEnumerable<FileItemViewModel> query = _allFiles;
        if (SelectedCategory?.Category is { } category)
            query = query.Where(f => f.Model.Category == category);
        var search = SearchText.Trim();
        if (search.Length > 0)
            query = query.Where(f => f.Model.RelativePath.Contains(search, StringComparison.OrdinalIgnoreCase));
        VisibleFiles = query.ToList();
        OnPropertyChanged(nameof(PlaceholderText));
    }

    private void SetCategorySelected(CategoryViewModel row, bool selected) =>
        SetSelected(row.Category is null ? _allFiles : _allFiles.Where(f => f.Model.Category == row.Category), selected);

    private void SetSelected(IEnumerable<FileItemViewModel> items, bool selected)
    {
        foreach (var item in items)
            item.SetSelectedQuietly(selected);
        RecalculateStats();
    }

    private void RecalculateStats()
    {
        var count = new Dictionary<FileCategory, (int Count, long Bytes, int Selected)>();
        int totalSelected = 0;
        long selectedBytes = 0, totalBytes = 0;
        foreach (var file in _allFiles)
        {
            var c = count.GetValueOrDefault(file.Model.Category);
            c.Count++;
            c.Bytes += file.Size;
            totalBytes += file.Size;
            if (file.IsSelected)
            {
                c.Selected++;
                totalSelected++;
                selectedBytes += file.Size;
            }
            count[file.Model.Category] = c;
        }

        foreach (var row in Categories)
        {
            if (row.Category is null)
                row.Update(_allFiles.Count, totalBytes, totalSelected);
            else
            {
                var c = count.GetValueOrDefault(row.Category);
                row.Update(c.Count, c.Bytes, c.Selected);
            }
        }

        SelectionSummary = _allFiles.Count == 0
            ? ""
            : Loc.F("SelectionSummary", totalSelected.ToString("N0"), SizeFormatter.Format(selectedBytes));
    }

    private static void ShowInExplorer(FileItemViewModel? item)
    {
        if (item is not null)
            Process.Start("explorer.exe", $"/select,\"{item.Model.FullPath}\"");
    }

    private static void OpenFile(FileItemViewModel? item)
    {
        if (item is null)
            return;
        try
        {
            Process.Start(new ProcessStartInfo(item.Model.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("OpenFileFailed", ex.Message), "DiskSaver",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
