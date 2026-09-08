using System.Text.Json;
using AWSConfiguration.Core.Internal;
using AwesomeAssertions;
using Xunit;

namespace AWSConfiguration.Core.Tests.Internal;

public class JsonFlattenerTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData(" { \"Key\": \"Value\" }")]
    [InlineData("\n[1, 2, 3]")]
    public void TryParseJson_should_return_true_for_json_content(string content)
    {
        JsonFlattener.TryParseJson(content, out var element).Should().BeTrue();
        element.Should().NotBeNull();
    }

    [Theory]
    [InlineData("plain string value")]
    [InlineData("{THIS IS NOT AN OBJECT}")]
    [InlineData("[THIS IS NOT AN ARRAY]")]
    [InlineData("")]
    public void TryParseJson_should_return_false_for_non_json_content(string content)
    {
        JsonFlattener.TryParseJson(content, out var element).Should().BeFalse();
        element.Should().BeNull();
    }

    [Fact]
    public void ExtractValues_should_flatten_nested_objects()
    {
        var json = JsonDocument.Parse("""{"Parent": {"Child": "Value"}}""").RootElement.Clone();

        var values = JsonFlattener.ExtractValues(json, "Prefix");

        values.Should().ContainSingle().Which.Should().Be(("Prefix:Parent:Child", "Value"));
    }

    [Fact]
    public void ExtractValues_should_flatten_arrays_with_indices()
    {
        var json = JsonDocument.Parse("""[{"Name": "First"}, {"Name": "Second"}]""").RootElement.Clone();

        var values = JsonFlattener.ExtractValues(json, "Prefix");

        values.Should().BeEquivalentTo(
            new[] { ("Prefix:0:Name", "First"), ("Prefix:1:Name", "Second") });
    }

    [Theory]
    [InlineData("""{"Number": 42}""", "Prefix:Number", "42")]
    [InlineData("""{"Decimal": 4.25}""", "Prefix:Decimal", "4.25")]
    [InlineData("""{"Text": "Hello"}""", "Prefix:Text", "Hello")]
    [InlineData("""{"Truth": true}""", "Prefix:Truth", "True")]
    [InlineData("""{"Falsehood": false}""", "Prefix:Falsehood", "False")]
    [InlineData("""{"Nothing": null}""", "Prefix:Nothing", null)]
    public void ExtractValues_should_map_scalar_kinds(string json, string expectedKey, string? expectedValue)
    {
        var element = JsonDocument.Parse(json).RootElement.Clone();

        var values = JsonFlattener.ExtractValues(element, "Prefix");

        values.Should().ContainSingle().Which.Should().Be((expectedKey, expectedValue));
    }

    [Fact]
    public void ExtractValues_should_yield_nothing_for_null_element()
    {
        JsonFlattener.ExtractValues(null, "Prefix").Should().BeEmpty();
    }

    [Fact]
    public void ExtractValues_should_map_empty_object_and_array_to_prefix()
    {
        var emptyObject = JsonDocument.Parse("{}").RootElement.Clone();
        var emptyArray = JsonDocument.Parse("[]").RootElement.Clone();

        JsonFlattener.ExtractValues(emptyObject, "Prefix").Should().BeEmpty();
        JsonFlattener.ExtractValues(emptyArray, "Prefix").Should().BeEmpty();
    }
}
