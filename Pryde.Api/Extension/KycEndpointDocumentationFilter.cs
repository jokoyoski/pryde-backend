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
            operation.Description = "Provider-neutral frontend endpoint. Smile ID hosted sessions use flow=IdentityVerification for passenger mixed identity options and flow=DriverLicenseVerification for driver licence verification. The selected ID type and verification method are confirmed only by an authenticated provider callback.";
        }
        else if (path.EndsWith("/kyc/retry", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Retry KYC";
        }
        else if (path.EndsWith("/kyc/mine", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get my KYC status";
            operation.Description = "Returns provider-confirmed KYC state using the neutral IdentityVerification or DriverLicenseVerification flow labels. Legacy stored Smile attempts are normalized to these labels.";
        }
        else if (path.Contains(
                     "/kyc/providers/smile-id/callback",
                     StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Smile ID callback";
            operation.Description = "Smile ID only. Authenticated callback results determine the selected ID type and verification method; browser redirects and session flow labels never approve KYC.";
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
