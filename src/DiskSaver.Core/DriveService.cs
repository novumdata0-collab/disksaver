namespace DiskSaver.Core;

public sealed record DriveEntry(
    string Root,
    string Label,
    string Format,
    long TotalSize,
    long FreeSpace,
    DriveType Type,
    bool IsSystem)
{
    public string Display
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Label) ? CoreText.Get("DriveNoLabel") : Label;
            var kind = Type == DriveType.Removable ? CoreText.Get("DriveRemovable") : "";
            var system = IsSystem ? CoreText.Get("DriveSystem") : "";
            return $"{Root}  {label}  ({SizeFormatter.Format(TotalSize)}, {Format}{kind}){system}";
        }
    }

    /// <summary>Метка для имени папки архива: «E_Seagate».</summary>
    public string SessionName =>
        Root.TrimEnd('\\', ':') + (string.IsNullOrWhiteSpace(Label) ? "" : "_" + Label);
}

public static class DriveService
{
    public static string SystemRoot =>
        Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    /// <summary>Готовые к чтению локальные и съёмные диски. Сетевые и CD пропускаем.</summary>
    public static IReadOnlyList<DriveEntry> GetDrives()
    {
        var result = new List<DriveEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                continue;
            try
            {
                if (!drive.IsReady)
                    continue;
                result.Add(new DriveEntry(
                    drive.RootDirectory.FullName,
                    drive.VolumeLabel,
                    drive.DriveFormat,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    drive.DriveType,
                    IsSameRoot(drive.RootDirectory.FullName, SystemRoot)));
            }
            catch (IOException)
            {
                // Диск отключили, пока мы его читали, или он заблокирован BitLocker.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return result;
    }

    public static bool IsSameRoot(string pathA, string pathB) =>
        string.Equals(
            Path.GetPathRoot(Path.GetFullPath(pathA)),
            Path.GetPathRoot(Path.GetFullPath(pathB)),
            StringComparison.OrdinalIgnoreCase);
}
