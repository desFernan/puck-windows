using System.Text.Json;
using System.Text.Json.Serialization;

namespace Puck.Avatar;

/// 클립 테이블의 값. 문자열이면 매니페스트 옆 파일의 **스템**이다
/// ("idle" -> idle.png). video 아바타만 {"in":초,"out":초} 시간 구간을 쓴다.
public abstract record ClipReference
{
    public sealed record Stem(string Value) : ClipReference;
    public sealed record TimeRange(double In, double Out) : ClipReference;
}

public sealed class ClipReferenceConverter : JsonConverter<ClipReference>
{
    public override ClipReference Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new ClipReference.Stem(reader.GetString()!);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("클립 값은 문자열이거나 {in,out} 객체여야 합니다");

        double? start = null, end = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            var name = reader.GetString();
            reader.Read();
            if (name == "in") start = reader.GetDouble();
            else if (name == "out") end = reader.GetDouble();
        }

        if (start is null || end is null)
            throw new JsonException("시간 구간 클립에는 in과 out이 모두 있어야 합니다");
        return new ClipReference.TimeRange(start.Value, end.Value);
    }

    public override void Write(Utf8JsonWriter writer, ClipReference value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ClipReference.Stem s:
                writer.WriteStringValue(s.Value);
                break;
            case ClipReference.TimeRange t:
                writer.WriteStartObject();
                writer.WriteNumber("in", t.In);
                writer.WriteNumber("out", t.Out);
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"알 수 없는 클립 값 종류: {value.GetType().Name}");
        }
    }
}
