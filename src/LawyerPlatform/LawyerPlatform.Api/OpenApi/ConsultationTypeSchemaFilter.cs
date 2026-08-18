using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LawyerPlatform.Api.OpenApi;

internal sealed class ConsultationTypeSchemaFilter : ISchemaFilter
{
    private static readonly JsonNode?[] AllowedValues =
        [JsonValue.Create("Online"), JsonValue.Create("Onsite")];

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties is null)
        {
            return;
        }

        var consultationType = schema.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, "consultationType", StringComparison.OrdinalIgnoreCase)).Value;
        if (consultationType is not OpenApiSchema mutableSchema)
        {
            return;
        }

        mutableSchema.Enum ??= [];
        mutableSchema.Enum.Clear();
        foreach (var allowedValue in AllowedValues)
        {
            if (allowedValue is not null)
            {
                mutableSchema.Enum.Add(allowedValue);
            }
        }
        mutableSchema.Description = "Required consultation type: Online or Onsite.";
    }
}
