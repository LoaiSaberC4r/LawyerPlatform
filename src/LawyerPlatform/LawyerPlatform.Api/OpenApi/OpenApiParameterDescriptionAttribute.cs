using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LawyerPlatform.Api.OpenApi;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class OpenApiParameterDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

internal sealed class ParameterDescriptionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var methodParameter in context.MethodInfo.GetParameters())
        {
            var description = methodParameter
                .GetCustomAttributes(typeof(OpenApiParameterDescriptionAttribute), inherit: true)
                .OfType<OpenApiParameterDescriptionAttribute>()
                .SingleOrDefault();
            if (description is null)
            {
                continue;
            }

            var openApiParameter = operation.Parameters
                .OfType<OpenApiParameter>()
                .SingleOrDefault(parameter => string.Equals(
                    parameter.Name,
                    methodParameter.Name,
                    StringComparison.OrdinalIgnoreCase));
            if (openApiParameter is not null)
            {
                openApiParameter.Description = description.Description;
            }
        }
    }
}
