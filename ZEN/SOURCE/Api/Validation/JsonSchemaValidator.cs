using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RichMove.SmartPay.Api.Validation;

public static class JsonSchemaValidator
{
    private static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] BodyField = ["body"];
    private static readonly string[] UnmappedMemberErrors = ["body"];
    private static readonly string[] ValidationErrors = ["body"];

    public static ValidationResult ValidateAndDeserialize<T>(string json, out T? result) where T : class
    {
        result = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return new ValidationResult("Request body cannot be empty", BodyField);
        }

        try
        {
            // First attempt: Strict parsing
            result = JsonSerializer.Deserialize<T>(json, StrictOptions);

            if (result == null)
            {
                return new ValidationResult("Request body resulted in null object", BodyField);
            }

            // Model validation
            var validationContext = new ValidationContext(result);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(result, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage ?? "Validation error").ToArray();
                return new ValidationResult($"Model validation failed: {string.Join(", ", errors)}",
                    validationResults.SelectMany(vr => vr.MemberNames).ToArray());
            }

            return ValidationResult.Success!;
        }
        catch (JsonException ex)
        {
            // Robust JSON parsing error messages
            var errorMessage = ex.Message.Contains("unmapped member", StringComparison.OrdinalIgnoreCase)
                ? "Request contains unknown fields. Only specified properties are allowed."
                : ex.Message.Contains("trailing comma", StringComparison.OrdinalIgnoreCase)
                ? "Trailing commas are not allowed in JSON."
                : $"Invalid JSON format: {ex.Message}";

            return new ValidationResult(errorMessage, UnmappedMemberErrors);
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Deserialization failed: {ex.Message}", ValidationErrors);
        }
    }

    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}