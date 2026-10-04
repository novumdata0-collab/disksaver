namespace DiskSaver.Core;

/// <summary>Какие папки не сканировать.</summary>
public sealed class ScanOptions
{
    /// <summary>Пропускаются на любой глубине.</summary>
    public HashSet<string> ExcludedNamesAnywhere { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "System Volume Information", "$WinREAgent", "$SysReset",
        "$Windows.~BT", "$Windows.~WS", "node_modules", ".git",
    };

    /// <summary>
    /// Системные папки: пропускаются только в корне диска и внутри Windows.old,
    /// чтобы пользовательская папка «Windows» в документах не потерялась.
    /// </summary>
    public HashSet<string> ExcludedSystemNames { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData",
        "Recovery", "PerfLogs", "MSOCache", "Intel", "AMD", "NVIDIA",
    };

    /// <summary>Хвосты путей (кэши, временные файлы). Сравниваются с концом полного пути.</summary>
    public List<string> ExcludedPathSuffixes { get; } =
    [
        @"AppData\Local\Temp",
        @"AppData\Local\Packages",
        @"AppData\Local\CrashDumps",
        @"AppData\Local\D3DSCache",
        @"AppData\Local\Microsoft\Windows\INetCache",
        @"AppData\Local\Microsoft\Windows\Explorer",
        @"AppData\Local\Microsoft\Edge\User Data",
        @"AppData\Local\Google\Chrome\User Data",
        @"AppData\Local\Yandex\YandexBrowser\User Data",
        @"AppData\Local\Mozilla",
    ];

    /// <summary>Решает, нужно ли заходить в папку.</summary>
    /// <param name="parentPath">Полный путь родительской папки.</param>
    /// <param name="name">Имя папки.</param>
    public bool IsExcluded(string rootPath, string parentPath, string name)
    {
        if (ExcludedNamesAnywhere.Contains(name))
            return true;

        if (ExcludedSystemNames.Contains(name))
        {
            var parent = Path.TrimEndingDirectorySeparator(parentPath);
            var root = Path.TrimEndingDirectorySeparator(rootPath);
            if (parent.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(parent).Equals("Windows.old", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var fullPath = Path.Combine(parentPath, name);
        foreach (var suffix in ExcludedPathSuffixes)
        {
            if (fullPath.EndsWith(@"\" + suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
