using System.Text.Json;

namespace DiskSaver.Core;

/// <summary>Сохраняет пользовательские категории и расширения в %APPDATA%\DiskSaver\categories.json.</summary>
public static class CategoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskSaver", "categories.json");

    /// <summary>Загружает настройки. Если файла нет или он повреждён — категории по умолчанию.</summary>
    public static CategoryCatalog Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
                return CategoryCatalog.CreateDefault();
            var dtos = JsonSerializer.Deserialize<List<CategoryDto>>(File.ReadAllText(path), JsonOptions);
            var categories = dtos?
                .Where(d => d.Extensions.Count > 0)
                .Select(FromDto)
                .OfType<FileCategory>()
                .ToList();
            return categories is { Count: > 0 } ? new CategoryCatalog(categories) : CategoryCatalog.CreateDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CategoryCatalog.CreateDefault();
        }
    }

    public static void Save(CategoryCatalog catalog, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dtos = catalog.Categories.Select(c => new CategoryDto
        {
            Key = c.Key,
            Name = c.CustomName,
            CopyPriority = c.CopyPriority,
            MinSizeBytes = c.MinSizeBytes,
            GroupByYear = c.GroupByYear,
            Extensions = c.Extensions.Select(e => e.TrimStart('.')).ToList(),
        });
        File.WriteAllText(path, JsonSerializer.Serialize(dtos, JsonOptions));
    }

    public static void Reset(string? path = null)
    {
        path ??= DefaultPath;
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// Разбирает строку вида «docx, .PDF; txt» в [".docx", ".pdf", ".txt"].
    /// Возвращает расширения, которые не удалось разобрать, в <paramref name="invalid"/>.
    /// </summary>
    public static IReadOnlyList<string> ParseExtensions(string text, out IReadOnlyList<string> invalid)
    {
        var valid = new List<string>();
        var bad = new List<string>();
        foreach (var token in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var ext = token.TrimStart('*').TrimStart('.');
            if (ext.Length is 0 or > 15 || !ext.All(char.IsLetterOrDigit))
            {
                bad.Add(token);
                continue;
            }
            var normalized = FileCategory.Normalize(ext);
            if (!valid.Contains(normalized))
                valid.Add(normalized);
        }
        invalid = bad;
        return valid;
    }

    public static string NewKey() => "custom-" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Название, которое нужно сохранить как пользовательское: null, если это стандартное название
    /// стандартной категории (тогда оно будет переводиться при смене языка).
    /// </summary>
    public static string? ToCustomName(string key, string name) =>
        CoreText.BuiltInCategoryName(key) is not null && CoreText.IsBuiltInCategoryName(key, name) ? null : name.Trim();

    private static FileCategory? FromDto(CategoryDto d)
    {
        var key = string.IsNullOrWhiteSpace(d.Key) ? NewKey() : d.Key;
        var name = string.IsNullOrWhiteSpace(d.Name) ? null : ToCustomName(key, d.Name);
        // Без названия допустимы только стандартные категории.
        if (name is null && CoreText.BuiltInCategoryName(key) is null)
            return null;
        return new FileCategory(key, name, d.CopyPriority, d.Extensions, Math.Max(0, d.MinSizeBytes), d.GroupByYear);
    }

    private sealed class CategoryDto
    {
        public string Key { get; set; } = "";
        public string? Name { get; set; }
        public int CopyPriority { get; set; }
        public long MinSizeBytes { get; set; }
        public bool GroupByYear { get; set; }
        public List<string> Extensions { get; set; } = [];
    }
}
