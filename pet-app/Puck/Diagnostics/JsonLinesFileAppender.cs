using System.IO;
using System.Text;
using System.Text.Json;

namespace Puck.Diagnostics;

/// logs\puck-YYYY-MM-DD.jsonl — 한 줄이 한 이벤트.
public sealed class JsonLinesFileAppender : IJsonLinesSink
{
    private static readonly HashSet<string> Reserved = ["ts", "level", "category", "message"];

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
            File.AppendAllText(CurrentFile(), line, Encoding.UTF8);
        }
    }

    public void Flush() { /* AppendAllText은 매 호출마다 닫는다 */ }

    private string CurrentFile()
        => Path.Combine(_directory, $"puck-{DateTime.Now:yyyy-MM-dd}.jsonl");
}
