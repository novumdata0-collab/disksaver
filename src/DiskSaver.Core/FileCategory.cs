namespace DiskSaver.Core;

/// <summary>Группа файлов, которые ищем на диске (документы, фото и т.д.).</summary>
public sealed class FileCategory
{
    public FileCategory(string key, string name, int copyPriority, IEnumerable<string> extensions,
        long minSizeBytes = 0, bool groupByYear = false)
    {
        Key = key;
        Name = name;
        CopyPriority = copyPriority;
        MinSizeBytes = minSizeBytes;
        GroupByYear = groupByYear;
        Extensions = extensions.Select(Normalize).Distinct().ToArray();
    }

    public string Key { get; }
    public string Name { get; }

    /// <summary>Меньше — копируется раньше. Мелкие ценные файлы идут первыми на случай, если диск умирает.</summary>
    public int CopyPriority { get; }

    /// <summary>Файлы меньше этого размера пропускаются (иконки, миниатюры).</summary>
    public long MinSizeBytes { get; }

    /// <summary>При раскладке по категориям класть в подпапку года (фото, видео).</summary>
    public bool GroupByYear { get; }

    /// <summary>Расширения в нижнем регистре с точкой: ".docx".</summary>
    public IReadOnlyList<string> Extensions { get; }

    public override string ToString() => Name;

    internal static string Normalize(string extension)
    {
        var ext = extension.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}
