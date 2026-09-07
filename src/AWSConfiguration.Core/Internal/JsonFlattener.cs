using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AWSConfiguration.Core.Internal;

/// <summary>
/// Shared helper that flattens JSON documents into configuration key/value pairs.
/// </summary>
internal static class JsonFlattener
{
    internal static bool TryParseJson(string data, out JsonElement? jsonElement)
    {
        jsonElement = null;

        data = data.TrimStart();
        var firstChar = data.FirstOrDefault();

        if (firstChar != '[' && firstChar != '{')
        {
            return false;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(data);
            //  https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-use-dom-utf8jsonreader-utf8jsonwriter?pivots=dotnet-6-0#jsondocument-is-idisposable
            //  Its recommended to return the clone of the root element as the json document will be disposed
            jsonElement = jsonDocument.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static IEnumerable<(string key, string? value)> ExtractValues(JsonElement? jsonElement, string prefix)
    {
        if (jsonElement == null)
        {
            yield break;
        }
        var element = jsonElement.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var currentIndex = 0;
                foreach (var el in element.EnumerateArray())
                {
                    var secretKey = $"{prefix}{ConfigurationPath.KeyDelimiter}{currentIndex}";
                    foreach (var (key, value) in ExtractValues(el, secretKey))
                    {
                        yield return (key, value);
                    }
                    currentIndex++;
                }
                break;
            }
            case JsonValueKind.Number:
            {
                var value = element.GetRawText();
                yield return (prefix, value);
                break;
            }
            case JsonValueKind.String:
            {
                var value = element.GetString() ?? "";
                yield return (prefix, value);
                break;
            }
            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                var value = element.GetBoolean();
                yield return (prefix, value.ToString());
                break;
            }
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    var secretKey = $"{prefix}{ConfigurationPath.KeyDelimiter}{property.Name}";
                    foreach (var (key, value) in ExtractValues(property.Value, secretKey))
                    {
                        yield return (key, value);
                    }
                }
                break;
            }
            case JsonValueKind.Null:
            {
                yield return (prefix, null);
                break;
            }
            case JsonValueKind.Undefined:
            default:
            {
                throw new FormatException("unsupported json token");
            }
        }
    }
}
