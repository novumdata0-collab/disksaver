namespace DiskSaver.Core;

/// <summary>Найденный на диске файл.</summary>
/// <param name="RelativePath">Путь относительно корня диска — не зависит от буквы.</param>
public sealed record FoundFile(
    string FullPath,
    string RelativePath,
    FileCategory Category,
    long Size,
    DateTime ModifiedUtc)
{
    public string Name => Path.GetFileName(RelativePath);

    public string Folder => Path.GetDirectoryName(RelativePath) ?? "";
}
