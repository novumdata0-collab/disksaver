namespace DiskSaver.Core;

/// <summary>Набор категорий и быстрый поиск категории по расширению.</summary>
public sealed class CategoryCatalog
{
    private readonly Dictionary<string, FileCategory> _byExtension = new(StringComparer.OrdinalIgnoreCase);

    public CategoryCatalog(IEnumerable<FileCategory> categories)
    {
        Categories = categories.ToArray();
        foreach (var category in Categories)
        foreach (var ext in category.Extensions)
            _byExtension.TryAdd(ext, category);
    }

    public IReadOnlyList<FileCategory> Categories { get; }

    public FileCategory? Classify(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return null;
        if (extension[0] != '.')
            extension = "." + extension;
        return _byExtension.GetValueOrDefault(extension);
    }

    public static CategoryCatalog CreateDefault() => new(
    [
        new FileCategory("documents", "Документы", 0,
            ["doc", "docx", "docm", "odt", "rtf", "pdf", "txt", "djvu"]),
        new FileCategory("spreadsheets", "Таблицы", 0,
            ["xls", "xlsx", "xlsm", "xlsb", "csv", "ods"]),
        new FileCategory("presentations", "Презентации", 1,
            ["ppt", "pptx", "pps", "ppsx", "odp"]),
        new FileCategory("photos", "Фото", 2,
            ["jpg", "jpeg", "png", "heic", "heif", "bmp", "tif", "tiff", "webp", "gif",
             "cr2", "cr3", "nef", "arw", "dng", "orf", "rw2", "raf"],
            minSizeBytes: 30 * 1024, groupByYear: true),
        new FileCategory("archives", "Архивы", 3,
            ["zip", "rar", "7z"]),
        new FileCategory("video", "Видео", 4,
            ["mp4", "mov", "avi", "mkv", "m4v", "3gp", "mts", "wmv"],
            minSizeBytes: 100 * 1024, groupByYear: true),
    ]);
}
