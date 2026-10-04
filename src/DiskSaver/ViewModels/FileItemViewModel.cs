using DiskSaver.Core;

namespace DiskSaver.ViewModels;

public sealed class FileItemViewModel(FoundFile model, Action onSelectionChanged) : ObservableObject
{
    private bool _isSelected = true;

    public FoundFile Model { get; } = model;
    public string Name => Model.Name;
    public string Folder => Model.Folder;
    public string CategoryName => Model.Category.Name;
    public long Size => Model.Size;
    public string SizeText => SizeFormatter.Format(Model.Size);
    public DateTime Modified => Model.ModifiedUtc.ToLocalTime();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                onSelectionChanged();
        }
    }

    /// <summary>Для массовых операций: без пересчёта итогов на каждый файл.</summary>
    public void SetSelectedQuietly(bool value)
    {
        if (_isSelected == value)
            return;
        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }
}
