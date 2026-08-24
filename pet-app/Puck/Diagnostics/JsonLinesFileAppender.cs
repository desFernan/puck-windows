using System.IO;
using System.Text;
using System.Text.Json;

namespace Puck.Diagnostics;

/// logs\puck-YYYY-MM-DD.jsonl — 한 줄이 한 이벤트.
public sealed class JsonLinesFileAppender : IJsonLinesSink
{
    private static readonly HashSet<string> Reserved = ["ts", "level", "category", "message"];

    /// BOM 없는 UTF-8. Encoding.UTF8은 파일이 비어 있을 때 BOM을 먼저 쓰는데,
    /// 그러면 첫 줄이 0xEF로 시작해 한 줄씩 읽는 도구(jq 등)가 깨진다.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly object _gate = new();

    public JsonLinesFileAppender(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Append(LogLevel level, string category, string message,
                       IReadOnlyDictionary<string, object?>? fields)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("ts", DateTimeOffset.UtcNow.ToString("O"));
            writer.WriteString("level", level.ToString().ToLowerInvariant());
            writer.WriteString("category", category);
            writer.WriteString("message", message);
            if (fields is not null)
            {
                foreach (var (key, value) in fields)
                {
                    var name = Reserved.Contains(key) ? $"field.{key}" : key;
                    writer.WritePropertyName(name);
                    JsonSerializer.Serialize(writer, value);
                }
            }
            writer.WriteEndObject();
        }

        var line = Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine;
        lock (_gate)
        {
            File.AppendAllText(CurrentFile(), line, Utf8NoBom);
        }
    }

    public void Flush() { /* AppendAllText은 매 호출마다 닫는다 */ }

    private string CurrentFile()
        => Path.Combine(_directory, $"puck-{DateTime.Now:yyyy-MM-dd}.jsonl");
}
