namespace VGC.Core.Logging;

public sealed record FileLogOptions(
    string DirectoryPath,
    string FileNamePrefix = "vgc",
    long MaxBytes = 1_048_576,
    int MaxFiles = 3);

public sealed record LogViewerRow(DateTimeOffset Timestamp, string Level, string Category, string Message);

public sealed class FileLogService
{
    private readonly FileLogOptions _options;

    public FileLogService(FileLogOptions options)
    {
        _options = options;
    }

    public string CurrentFilePath => Path.Combine(_options.DirectoryPath, $"{_options.FileNamePrefix}.log");

    public void Write(string level, string category, string message, DateTimeOffset? timestamp = null)
    {
        Directory.CreateDirectory(_options.DirectoryPath);
        RotateIfNeeded();
        var entryTime = timestamp ?? DateTimeOffset.Now;
        File.AppendAllText(
            CurrentFilePath,
            $"{entryTime:O}\t{level}\t{category}\t{message}{Environment.NewLine}");
    }

    public IReadOnlyList<LogViewerRow> ReadRows(int maxRows = 200)
    {
        if (!File.Exists(CurrentFilePath))
        {
            return [];
        }

        return File.ReadLines(CurrentFilePath)
            .Reverse()
            .Take(maxRows)
            .Reverse()
            .Select(ParseRow)
            .Where(static row => row is not null)
            .Cast<LogViewerRow>()
            .ToList();
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(CurrentFilePath);
        if (!file.Exists || file.Length < _options.MaxBytes)
        {
            return;
        }

        for (var index = _options.MaxFiles - 1; index >= 1; index--)
        {
            var source = $"{CurrentFilePath}.{index}";
            var destination = $"{CurrentFilePath}.{index + 1}";
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            if (File.Exists(source))
            {
                File.Move(source, destination);
            }
        }

        File.Move(CurrentFilePath, $"{CurrentFilePath}.1", overwrite: true);
    }

    private static LogViewerRow? ParseRow(string line)
    {
        var parts = line.Split('\t', 4);
        if (parts.Length != 4 || !DateTimeOffset.TryParse(parts[0], out var timestamp))
        {
            return null;
        }

        return new LogViewerRow(timestamp, parts[1], parts[2], parts[3]);
    }
}
