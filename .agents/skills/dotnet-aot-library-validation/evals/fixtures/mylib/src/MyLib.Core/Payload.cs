using System;
using System.Text.Json;

namespace MyLib.Core;

/// <summary>Payload helpers used by exporters.</summary>
public static class Payload
{
    public static string ToJson(object value) =>
        JsonSerializer.Serialize(value, value.GetType());

    public static T FromJson<T>(string json) where T : new() =>
        JsonSerializer.Deserialize<T>(json) ?? new T();

    public static string DescribeKind(RecordKind kind)
    {
        var names = Enum.GetValues(typeof(RecordKind));
        var best = kind.ToString();
        foreach (var name in names) if (name.ToString() == best) return best;
        return "Unknown";
    }
}

public enum RecordKind
{
    Trace,
    Span,
    Metric
}
