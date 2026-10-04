using System.Collections.ObjectModel;
using System.Windows;
using DiskSaver.Core;
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
        if (MessageBox.Show($"Удалить категорию «{item.Name}»?", "Расширения файлов",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        var index = _items.IndexOf(item);
        _items.Remove(item);
        CategoryList.SelectedIndex = Math.Min(index, _items.Count - 1);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Вернуть стандартные категории и расширения? Ваши изменения будут потеряны.",
                "Расширения файлов", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
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
                error = "Укажите название категории.";
            else if (!names.Add(name))
                error = $"Категория «{name}» встречается дважды.";
            else if (invalid.Count > 0)
                error = $"Непонятные расширения: {string.Join(", ", invalid)}.\nИспользуйте буквы и цифры, например: docx, pdf.";
            else if (extensions.Count == 0)
                error = "Добавьте хотя бы одно расширение.";
            else if (!long.TryParse(item.MinSizeKbText.Trim(), out var kb) || kb < 0)
                error = "Минимальный размер — целое число КБ, 0 или больше.";
            else
            {
                var duplicate = extensions.FirstOrDefault(owners.ContainsKey);
                if (duplicate is not null)
                    error = $"Расширение {duplicate} уже есть в категории «{owners[duplicate]}».";
                else
                {
                    foreach (var ext in extensions)
                        owners[ext] = name;
                    categories.Add(new FileCategory(item.Key, name, item.CopyPriority, extensions,
                        kb * 1024, item.GroupByYear));
                }
            }

            if (error is not null)
            {
                CategoryList.SelectedItem = item;
                MessageBox.Show(error, $"Категория «{(name.Length > 0 ? name : "без названия")}»",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (categories.Count == 0)
        {
            MessageBox.Show("Нужна хотя бы одна категория.", "Расширения файлов",
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
            MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "Расширения файлов",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Result = catalog;
        DialogResult = true;
    }
}
