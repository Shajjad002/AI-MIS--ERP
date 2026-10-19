using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AI.MIS.Api.OpenApi;

public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodAttributes = context.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<IAuthorizeData>()
            .ToArray();
        var controllerAttributes = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(inherit: true)
            .OfType<IAuthorizeData>()
            .ToArray() ?? [];
        var allowsAnonymous = context.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) ||
            context.MethodInfo.DeclaringType?.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) == true;

        if (allowsAnonymous || methodAttributes.Length == 0 && controllerAttributes.Length == 0)
        {
            return;
        }

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = []
            }
        ];
    }
}
