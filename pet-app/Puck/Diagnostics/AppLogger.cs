namespace Puck.Diagnostics;

public enum LogLevel { Debug, Info, Warning, Error }

public interface IJsonLinesSink
{
    void Append(LogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? fields);
    void Flush();
}

/// 앱 전체의 로깅 진입점. 싱크를 갈아끼울 수 있게 해 둔 이유는 테스트가
/// 파일을 건드리지 않게 하기 위해서다.
public static class AppLogger
{
    private static IJsonLinesSink _sink = new NullSink();

    public static void Configure(IJsonLinesSink sink) => _sink = sink;

    public static void Log(LogLevel level, string category, string message,
                           IReadOnlyDictionary<string, object?>? fields = null)
        => _sink.Append(level, category, message, fields);

    public static void Warning(string category, string message,
                               IReadOnlyDictionary<string, object?>? fields = null)
        => Log(LogLevel.Warning, category, message, fields);

    public static void Error(string category, string message,
                             IReadOnlyDictionary<string, object?>? fields = null)
        => Log(LogLevel.Error, category, message, fields);

    private sealed class NullSink : IJsonLinesSink
    {
        public void Append(LogLevel level, string category, string message, IReadOnlyDictionary<string, object?>? fields) { }
        public void Flush() { }
    }
}
