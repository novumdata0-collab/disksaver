using System.Collections.ObjectModel;
using System.Windows;
using DiskSaver.Core;
using DiskSaver.Localization;
using DiskSaver.ViewModels;

namespace DiskSaver;

public partial class CategoriesWindow : Window
{
    private readonly ObservableCollection<CategoryEditItem> _items;

    public CategoriesWindow(CategoryCatalog current)
    {
        InitializeComponent();
        _items = new ObservableCollection<CategoryEditItem>(current.Categories.Select(c => new CategoryEditItem(c)));
        CategoryList.ItemsSource = _items;
        CategoryList.SelectedIndex = 0;
    }

    /// <summary>Новый набор категорий после «Сохранить».</summary>
    public CategoryCatalog? Result { get; private set; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var item = new CategoryEditItem();
        _items.Add(item);
        CategoryList.SelectedItem = item;
        CategoryList.ScrollIntoView(item);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not CategoryEditItem item)
            return;
        if (MessageBox.Show(Loc.F("CatConfirmRemove", item.Name), Loc.T("CatTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var index = _items.IndexOf(item);
        _items.Remove(item);
        CategoryList.SelectedIndex = Math.Min(index, _items.Count - 1);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Loc.T("CatConfirmReset"), Loc.T("CatTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _items.Clear();
        foreach (var category in CategoryCatalog.CreateDefault().Categories)
            _items.Add(new CategoryEditItem(category));
        CategoryList.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var categories = new List<FileCategory>();
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _items)
        {
            var name = item.Name.Trim();
            string? error = null;
            var extensions = CategoryStore.ParseExtensions(item.ExtensionsText, out var invalid);

            if (name.Length == 0)
                error = Loc.T("CatErrName");
            else if (!names.Add(name))
                error = Loc.F("CatErrDuplicateName", name);
            else if (invalid.Count > 0)
                error = Loc.F("CatErrInvalidExt", string.Join(", ", invalid));
            else if (extensions.Count == 0)
                error = Loc.T("CatErrNoExt");
            else if (!long.TryParse(item.MinSizeKbText.Trim(), out var kb) || kb < 0)
                error = Loc.T("CatErrMinSize");
            else
            {
                var duplicate = extensions.FirstOrDefault(owners.ContainsKey);
                if (duplicate is not null)
                    error = Loc.F("CatErrDuplicateExt", duplicate, owners[duplicate]);
                else
                {
                    foreach (var ext in extensions)
                        owners[ext] = name;
                    // Стандартное название не сохраняем — оно будет переводиться вместе с интерфейсом.
                    categories.Add(new FileCategory(item.Key, CategoryStore.ToCustomName(item.Key, name),
                        item.CopyPriority, extensions, kb * 1024, item.GroupByYear));
                }
            }

            if (error is not null)
            {
                CategoryList.SelectedItem = item;
                MessageBox.Show(error, Loc.F("CatErrTitle", name.Length > 0 ? name : Loc.T("CatUnnamed")),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (categories.Count == 0)
        {
            MessageBox.Show(Loc.T("CatErrNone"), Loc.T("CatTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var catalog = new CategoryCatalog(categories);
        try
        {
            CategoryStore.Save(catalog);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Loc.F("CatSaveFailed", ex.Message), Loc.T("CatTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Result = catalog;
        DialogResult = true;
    }
}
