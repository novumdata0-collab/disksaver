using System.IO;
using System.Text.Json;

namespace DiskSaver;

/// <summary>Настройки программы в %APPDATA%\DiskSaver\settings.json.</summary>
public sealed class AppSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskSaver", "settings.json");

    /// <summary>"en" или "ru"; null — по языку Windows.</summary>
    public string? Language { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Не критично: в следующий раз язык снова определится по Windows.
        }
    }
}
