using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DiskSaver.Core;

public enum CopyLayout
{
    /// <summary>Повторить структуру папок исходного диска.</summary>
    PreserveStructure,

    /// <summary>Разложить по папкам категорий (фото и видео — ещё и по годам).</summary>
    ByCategory,
}

public enum CopyStatus { Copied, Failed, VerifyFailed, Cancelled }

public sealed record CopyOptions(string DestinationRoot, CopyLayout Layout, bool Verify = true);

public sealed record CopyProgress(
    int FilesDone, int FilesTotal, long BytesDone, long BytesTotal, string CurrentFile, int Errors);

public sealed record CopyResult(FoundFile Source, string? Destination, CopyStatus Status, string? Hash, string? Error);

public sealed class CopyReport(string outputFolder, IReadOnlyList<CopyResult> results, bool cancelled)
{
    public string OutputFolder { get; } = outputFolder;
    public IReadOnlyList<CopyResult> Results { get; } = results;
    public bool Cancelled { get; } = cancelled;
    public int Copied => Results.Count(r => r.Status == CopyStatus.Copied);
    public int Failed => Results.Count(r => r.Status is CopyStatus.Failed or CopyStatus.VerifyFailed);

    /// <summary>Все выбранные файлы скопированы и проверены — диск можно форматировать.</summary>
    public bool IsComplete => !Cancelled && Failed == 0;

    public string ManifestPath => Path.Combine(OutputFolder, CopyEngine.ManifestFileName);
    public string ErrorLogPath => Path.Combine(OutputFolder, CopyEngine.ErrorLogFileName);
}

/// <summary>Копирует найденные файлы в архив, считает MD5 и проверяет копию повторным чтением.</summary>
public sealed class CopyEngine
{
    public const string ManifestFileName = "manifest.csv";
    public const string ErrorLogFileName = "errors.log";
    private const int BufferSize = 1 << 20;

    public Task<CopyReport> CopyAsync(
        IReadOnlyList<FoundFile> files,
        string sessionName,
        CopyOptions options,
        IProgress<CopyProgress>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Copy(files, sessionName, options, progress, ct), CancellationToken.None);

    public CopyReport Copy(
        IReadOnlyList<FoundFile> files,
        string sessionName,
        CopyOptions options,
        IProgress<CopyProgress>? progress = null,
        CancellationToken ct = default)
    {
        var ordered = files
            .OrderBy(f => f.Category.CopyPriority)
            .ThenBy(f => f.Size)
            .ToList();
        long bytesTotal = ordered.Sum(f => f.Size);

        EnsureFreeSpace(options.DestinationRoot, bytesTotal);

        var outputFolder = Path.Combine(options.DestinationRoot,
            $"{SanitizeName(sessionName)}_{DateTime.Now:yyyy-MM-dd_HHmm}");
        Directory.CreateDirectory(outputFolder);

        var results = new List<CopyResult>(ordered.Count);
        long bytesDone = 0;
        int errors = 0;
        var throttle = Stopwatch.StartNew();
        bool cancelled = false;

        void Report(string current, bool force = false)
        {
            if (progress is null || (!force && throttle.ElapsedMilliseconds < 100))
                return;
            throttle.Restart();
            progress.Report(new CopyProgress(results.Count, ordered.Count, bytesDone, bytesTotal, current, errors));
        }

        foreach (var file in ordered)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            Report(file.RelativePath, force: true);
            var fileStartBytes = bytesDone;
            string? destination = null;
            try
            {
                destination = ReserveDestination(outputFolder, file, options.Layout);
                var hash = CopyFile(file.FullPath, destination, n =>
                {
                    bytesDone += n;
                    Report(file.RelativePath);
                }, ct);

                File.SetCreationTimeUtc(destination, File.GetCreationTimeUtc(file.FullPath));
                File.SetLastWriteTimeUtc(destination, file.ModifiedUtc);

                if (options.Verify && !string.Equals(hash, HashFile(destination, ct), StringComparison.Ordinal))
                {
                    errors++;
                    results.Add(new CopyResult(file, destination, CopyStatus.VerifyFailed, hash,
                        "Копия не совпадает с оригиналом"));
                    continue;
                }

                results.Add(new CopyResult(file, destination, CopyStatus.Copied, hash, null));
            }
            catch (OperationCanceledException)
            {
                TryDelete(destination);
                results.Add(new CopyResult(file, null, CopyStatus.Cancelled, null, "Отменено"));
                cancelled = true;
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(destination);
                errors++;
                bytesDone = fileStartBytes + file.Size;
                results.Add(new CopyResult(file, null, CopyStatus.Failed, null, ex.Message));
            }
        }

        Report("", force: true);
        var report = new CopyReport(outputFolder, results, cancelled);
        WriteManifest(report);
        WriteErrorLog(report);
        return report;
    }

    private static void EnsureFreeSpace(string destinationRoot, long bytesNeeded)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(destinationRoot));
        if (string.IsNullOrEmpty(root))
            return;
        var drive = new DriveInfo(root);
        if (drive.IsReady && drive.AvailableFreeSpace < bytesNeeded)
            throw new IOException(
                $"Недостаточно места на {root}: нужно {SizeFormatter.Format(bytesNeeded)}, " +
                $"свободно {SizeFormatter.Format(drive.AvailableFreeSpace)}.");
    }

    internal static string ReserveDestination(string outputFolder, FoundFile file, CopyLayout layout)
    {
        string target = layout switch
        {
            CopyLayout.PreserveStructure => Path.Combine(outputFolder, file.RelativePath),
            _ when file.Category.GroupByYear => Path.Combine(outputFolder, file.Category.Name,
                file.ModifiedUtc.ToLocalTime().Year.ToString(), file.Name),
            _ => Path.Combine(outputFolder, file.Category.Name, file.Name),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (!File.Exists(target))
            return target;

        var dir = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    /// <summary>Копирует файл и попутно считает MD5 оригинала.</summary>
    private static string CopyFile(string source, string destination, Action<long> onBytes, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.SequentialScan);
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, BufferSize, FileOptions.SequentialScan);
            int read;
            while ((read = input.Read(buffer, 0, BufferSize)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
                output.Write(buffer, 0, read);
                onBytes(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string HashFile(string path, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, FileOptions.SequentialScan);
            int read;
            while ((read = input.Read(buffer, 0, BufferSize)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void WriteManifest(CopyReport report)
    {
        // «;» и UTF-8 с BOM — чтобы русский Excel открыл файл без настроек.
        var sb = new StringBuilder();
        sb.AppendLine("Статус;Категория;Источник;Копия;Размер;Изменён;MD5;Ошибка");
        foreach (var r in report.Results)
        {
            sb.AppendJoin(';',
                Csv(r.Status.ToString()),
                Csv(r.Source.Category.Name),
                Csv(r.Source.FullPath),
                Csv(r.Destination is null ? "" : Path.GetRelativePath(report.OutputFolder, r.Destination)),
                r.Source.Size,
                r.Source.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                r.Hash ?? "",
                Csv(r.Error ?? ""));
            sb.AppendLine();
        }
        File.WriteAllText(report.ManifestPath, sb.ToString(), new UTF8Encoding(true));
    }

    private static void WriteErrorLog(CopyReport report)
    {
        var failed = report.Results.Where(r => r.Status is CopyStatus.Failed or CopyStatus.VerifyFailed).ToList();
        if (failed.Count == 0)
            return;
        File.WriteAllLines(report.ErrorLogPath,
            failed.Select(r => $"{r.Status}\t{r.Source.FullPath}\t{r.Error}"), new UTF8Encoding(true));
    }

    private static string Csv(string value) =>
        value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private static void TryDelete(string? path)
    {
        if (path is null)
            return;
        try { File.Delete(path); } catch { /* копия всё равно не годится */ }
    }

    internal static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.', '_');
        return cleaned.Length == 0 ? "Disk" : cleaned;
    }
}
