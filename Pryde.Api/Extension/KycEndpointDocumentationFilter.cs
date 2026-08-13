using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Pryde.Api.Extension;

public sealed class KycEndpointDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;
        if (!path.Contains("/kyc/", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith("/kyc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (path.EndsWith("/kyc/session", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Start or continue KYC (frontend-facing)";
            operation.Description = "Provider-neutral endpoint for new clients. Returns Dojah compatibility data or Smile ID hosted redirect sessions according to Kyc:ActiveProvider.";
        }
        else if (path.EndsWith("/kyc/retry", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Retry failed KYC flows (frontend-facing)";
            operation.Description = "Provider-neutral retry endpoint. Smile ID creates new single-use links only for failed retryable flows.";
        }
        else if (path.EndsWith("/kyc/mine", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get current KYC and required-flow statuses (frontend-facing)";
            operation.Description = "Returns overall KYC status and provider-neutral per-flow status. Browser redirects never approve KYC.";
        }
        else if (path.Contains("/kyc/providers/smile-id/callback", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Receive authenticated Smile ID results (Smile-only)";
            operation.Description = "Provider callback only; frontend clients must not call this endpoint.";
        }
        else if (path.Contains("/kyc/dojah/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Dojah compatibility endpoint (Dojah-only, legacy)";
            operation.Description = "Preserved for existing Dojah clients and callbacks. New clients should use the provider-neutral session, retry, and mine endpoints.";
        }
    }
}
