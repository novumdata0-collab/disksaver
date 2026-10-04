using DiskSaver.Core;

namespace DiskSaver.ViewModels;

/// <summary>Редактируемая категория в окне «Расширения файлов».</summary>
public sealed class CategoryEditItem : ObservableObject
{
    private string _name;
    private string _extensionsText;
    private string _minSizeKbText;
    private bool _groupByYear;

    public CategoryEditItem(FileCategory category)
    {
        Key = category.Key;
        CopyPriority = category.CopyPriority;
        _name = category.Name;
        _extensionsText = string.Join(", ", category.Extensions.Select(e => e.TrimStart('.')));
        _minSizeKbText = (category.MinSizeBytes / 1024).ToString();
        _groupByYear = category.GroupByYear;
    }

    public CategoryEditItem()
    {
        Key = CategoryStore.NewKey();
        CopyPriority = 1;
        _name = "Новая категория";
        _extensionsText = "";
        _minSizeKbText = "0";
    }

    public string Key { get; }
    public int CopyPriority { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ExtensionsText
    {
        get => _extensionsText;
        set => SetProperty(ref _extensionsText, value);
    }

    public string MinSizeKbText
    {
        get => _minSizeKbText;
        set => SetProperty(ref _minSizeKbText, value);
    }

    public bool GroupByYear
    {
        get => _groupByYear;
        set => SetProperty(ref _groupByYear, value);
    }
}
