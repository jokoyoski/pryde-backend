using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Pryde.Api.Extension;

public sealed class KycEndpointDocumentationFilter : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;

        if (path.EndsWith("/kyc/session", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Start or continue KYC";
        }
        else if (path.EndsWith("/kyc/retry", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Retry KYC";
        }
        else if (path.EndsWith("/kyc/mine", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get my KYC status";
        }
        else if (path.Contains(
                     "/kyc/providers/smile-id/callback",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Smile ID callback";
        }
        else if (path.EndsWith(
                     "/kyc/dojah/webhook/debug",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Debug Dojah webhook";
        }
        else if (path.EndsWith(
                     "/kyc/dojah/webhook",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Dojah webhook";
        }
        else if (path.EndsWith(
                     "/kyc/dojah/config",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get Dojah configuration";
        }
        else if (path.EndsWith(
                     "/kyc/dojah/retry",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Retry Dojah KYC";
        }
    }
}