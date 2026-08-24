using System.IO;
using System.Text.Json;
using Puck.Diagnostics;

namespace PuckTests.Diagnostics;

public class JsonLinesFileAppenderTests
{
    [Fact]
    public void WritesOneJsonObjectPerLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var appender = new JsonLinesFileAppender(dir);
            appender.Append(LogLevel.Warning, "avatar", "missing clip", new Dictionary<string, object?> { ["clip"] = "walk" });
            appender.Append(LogLevel.Error, "overlay", "no display", null);
            appender.Flush();

            var file = Directory.GetFiles(dir, "*.jsonl").Single();
            var lines = File.ReadAllLines(file);
            Assert.Equal(2, lines.Length);

            var first = JsonDocument.Parse(lines[0]).RootElement;
            Assert.Equal("warning", first.GetProperty("level").GetString());
            Assert.Equal("avatar", first.GetProperty("category").GetString());
            Assert.Equal("missing clip", first.GetProperty("message").GetString());
            Assert.Equal("walk", first.GetProperty("clip").GetString());
            Assert.True(first.TryGetProperty("ts", out _));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void FieldNameCollidingWithReservedKeyIsPrefixed()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var appender = new JsonLinesFileAppender(dir);
            appender.Append(LogLevel.Info, "x", "y", new Dictionary<string, object?> { ["message"] = "shadow" });
            appender.Flush();

            var line = File.ReadAllLines(Directory.GetFiles(dir, "*.jsonl").Single()).Single();
            var obj = JsonDocument.Parse(line).RootElement;
            Assert.Equal("y", obj.GetProperty("message").GetString());
            Assert.Equal("shadow", obj.GetProperty("field.message").GetString());
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
