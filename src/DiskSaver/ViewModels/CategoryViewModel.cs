using DiskSaver.Core;
using DiskSaver.Localization;

namespace DiskSaver.ViewModels;

/// <summary>Строка в левой панели. Category == null — «Все файлы».</summary>
public sealed class CategoryViewModel(FileCategory? category, Action<CategoryViewModel, bool> onCheck) : ObservableObject
{
    private int _count;
    private long _bytes;
    private int _selectedCount;

    public FileCategory? Category { get; } = category;
    public string Name => Category?.Name ?? Loc.T("AllFiles");

    public string Details => _count == 0
        ? "—"
        : $"{_count:N0} · {SizeFormatter.Format(_bytes)}" + (_selectedCount == _count ? "" : Loc.F("CategorySelectedPart", _selectedCount.ToString("N0")));

    /// <summary>true — выбраны все, false — ни одного, null — часть.</summary>
    public bool? IsChecked
    {
        get => _count == 0 || _selectedCount == 0 ? false : _selectedCount == _count ? true : null;
        set
        {
            onCheck(this, value == true);
            OnPropertyChanged();
        }
    }

    public void Update(int count, long bytes, int selectedCount)
    {
        if (_count == count && _bytes == bytes && _selectedCount == selectedCount)
            return;
        _count = count;
        _bytes = bytes;
        _selectedCount = selectedCount;
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(IsChecked));
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Details));
    }
}
