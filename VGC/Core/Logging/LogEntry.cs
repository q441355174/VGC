namespace VGC.Core.Logging;

public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message);
