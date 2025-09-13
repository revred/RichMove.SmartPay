#pragma warning disable CA1515, CA2007, CA1860, xUnit2020
using System.Text.Json;
using NJsonSchema;
using Xunit;

namespace RichMove.SmartPay.Api.Tests.Helpers;

internal static class JsonSchemaAssert
{
    public static async Task ValidatesAgainstAsync(string schemaPath, string json)
    {
        var schema = await JsonSchema.FromJsonAsync(await File.ReadAllTextAsync(schemaPath));
        var errors = schema.Validate(json);
        if (errors.Count > 0)
        {
            var detail = string.Join(Environment.NewLine, errors.Select(e => $"- {e.Path}: {e.Kind}"));
            Assert.Fail($"Schema validation failed for {Path.GetFileName(schemaPath)}:{Environment.NewLine}{detail}{Environment.NewLine}JSON:{Environment.NewLine}{json}");
        }
    }

    public static async Task ValidatesAgainstAsync(string schemaPath, object obj)
        => await ValidatesAgainstAsync(schemaPath, JsonSerializer.Serialize(obj));
}