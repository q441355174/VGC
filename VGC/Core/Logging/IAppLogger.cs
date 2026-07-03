using System.Collections.ObjectModel;

namespace VGC.Core.Logging;

public interface IAppLogger
{
    ReadOnlyObservableCollection<LogEntry> Entries { get; }

    void Info(string message);

    void Warning(string message);

    void Error(string message);
}
