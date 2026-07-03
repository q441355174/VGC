using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace VGC.Core.Logging;

public sealed class AppLogger : IAppLogger
{
    private readonly ObservableCollection<LogEntry> _entries = new();
    private string? _filePath;
    private readonly object _fileLock = new();

    public AppLogger()
    {
        Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);
    }

    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    public bool IsFileLoggingEnabled => _filePath is not null;

    public void EnableFileLogging(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void DisableFileLogging()
    {
        _filePath = null;
    }

    public void Info(string message) => Add("INFO", message);

    public void Warning(string message) => Add("WARN", message);

    public void Error(string message) => Add("ERROR", message);

    private void Add(string level, string message)
    {
        var entry = new LogEntry(DateTimeOffset.Now, level, message);
        if (Dispatcher.UIThread.CheckAccess())
        {
            _entries.Add(entry);
        }
        else
        {
            Dispatcher.UIThread.Post(() => _entries.Add(entry));
        }

        if (_filePath is not null)
        {
            lock (_fileLock)
            {
                File.AppendAllText(_filePath, $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
    }
}
