using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BackEnd.Swagger
{
    // OperationFilter to render IFormFile parameters as file inputs in Swagger UI
    public class FileUploadOperation : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.RequestBody == null)
                return;

            var formFileParams = context.MethodInfo.GetParameters()
                .Where(p => p.ParameterType == typeof(Microsoft.AspNetCore.Http.IFormFile))
                .ToList();

            if (!formFileParams.Any())
                return;

            var content = operation.RequestBody.Content;
            if (!content.ContainsKey("multipart/form-data"))
            {
                // ensure multipart/form-data is present
                content["multipart/form-data"] = new OpenApiMediaType {
                    Schema = new OpenApiSchema { Type = "object", Properties = { } }
                };
            }

            var schema = content["multipart/form-data"].Schema;
            schema.Type = "object";

            foreach (var p in formFileParams)
            {
                schema.Properties[p.Name] = new OpenApiSchema { Type = "string", Format = "binary" };
            }
        }
    }
}
