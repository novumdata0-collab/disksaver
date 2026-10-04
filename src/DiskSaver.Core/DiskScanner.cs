using System.IO.Enumeration;

namespace DiskSaver.Core;

/// <summary>Счётчики, которые можно читать из UI во время сканирования.</summary>
public sealed class ScanStats
{
    private int _directories;
    private int _errors;

    public int Directories => Volatile.Read(ref _directories);

    /// <summary>Ошибки чтения папок (битые сектора, нет доступа и т.п.).</summary>
    public int Errors => Volatile.Read(ref _errors);

    internal void AddDirectory() => Interlocked.Increment(ref _directories);
    internal void AddError() => Interlocked.Increment(ref _errors);
}

/// <summary>
/// Обходит диск и возвращает файлы нужных категорий.
/// Недоступные и повреждённые папки пропускаются, обход продолжается.
/// </summary>
public sealed class DiskScanner(CategoryCatalog catalog, ScanOptions options)
{
    public ScanStats Stats { get; } = new();

    public IEnumerable<FoundFile> Scan(string rootPath, CancellationToken ct = default)
    {
        var root = Path.GetFullPath(rootPath);
        var enumOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        using var enumerator = new Enumerator(root, catalog, options, Stats, enumOptions);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            bool moved;
            try
            {
                moved = enumerator.MoveNext();
            }
            catch (IOException)
            {
                // Корень стал недоступен (диск отключили) — заканчиваем.
                Stats.AddError();
                yield break;
            }

            if (!moved)
                yield break;
            yield return enumerator.Current;
        }
    }

    private sealed class Enumerator(
        string rootPath,
        CategoryCatalog catalog,
        ScanOptions options,
        ScanStats stats,
        EnumerationOptions enumOptions)
        : FileSystemEnumerator<FoundFile>(rootPath, enumOptions)
    {
        private readonly string root = rootPath;
        private FileCategory? _pending;

        protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry)
        {
            if (options.IsExcluded(root, entry.Directory.ToString(), entry.FileName.ToString()))
                return false;
            stats.AddDirectory();
            return true;
        }

        protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        {
            if (entry.IsDirectory)
                return false;

            var category = catalog.Classify(Path.GetExtension(entry.FileName).ToString());
            if (category is null || entry.Length < category.MinSizeBytes)
                return false;

            _pending = category;
            return true;
        }

        protected override FoundFile TransformEntry(ref FileSystemEntry entry)
        {
            var fullPath = entry.ToFullPath();
            return new FoundFile(
                fullPath,
                Path.GetRelativePath(root, fullPath),
                _pending!,
                entry.Length,
                entry.LastWriteTimeUtc.UtcDateTime);
        }

        protected override bool ContinueOnError(int error)
        {
            stats.AddError();
            return true;
        }
    }
}
