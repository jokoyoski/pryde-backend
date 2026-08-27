using Microsoft.OpenApi.Any;
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
            operation.Description = "Provider-neutral frontend endpoint. Returns the current monthly attempt allowance. Returning an existing pending session does not consume another attempt. Creating a new provider attempt consumes one allowance across all KYC providers. Smile ID hosted sessions use flow=IdentityVerification for passenger mixed identity options and flow=DriverLicenseVerification for driver licence verification. The selected ID type and verification method are confirmed only by an authenticated provider callback.";
            AddAttemptLimitResponse(operation);
        }
        else if (path.EndsWith("/kyc/retry", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Retry KYC";
            operation.Description = "Retries an eligible rejected KYC verification and returns the updated monthly attempt allowance. Smile ID passengers may optionally supply selectedIdType to choose another enabled passenger identity option; an omitted body reuses the previous identity type. Attempts are limited across all KYC providers. Returns HTTP 429 without calling the provider or changing KYC state when the monthly allowance is exhausted.";
            AddAttemptLimitResponse(operation);
        }
        else if (path.EndsWith("/kyc/mine", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get my KYC status";
            operation.Description = "Returns provider-confirmed KYC state and the current monthly attempt allowance. CanRetry, including flow-level CanRetry values, is true only when the existing retry rules allow a retry and attemptAllowance.canAttempt is true. Legacy stored Smile attempts are normalized to the neutral IdentityVerification or DriverLicenseVerification flow labels.";
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

    private static void AddAttemptLimitResponse(OpenApiOperation operation)
    {
        operation.Responses["429"] = new OpenApiResponse
        {
            Description = "The configured monthly KYC attempt limit has been reached. No provider call, attempt row, or KYC state change is made.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Example = new OpenApiObject
                    {
                        ["statusCode"] = new OpenApiInteger(429),
                        ["message"] = new OpenApiString(
                            "You have reached your monthly KYC attempt limit."),
                        ["attemptAllowance"] = new OpenApiObject
                        {
                            ["limit"] = new OpenApiInteger(3),
                            ["used"] = new OpenApiInteger(3),
                            ["remaining"] = new OpenApiInteger(0),
                            ["canAttempt"] = new OpenApiBoolean(false),
                            ["resetsAt"] = new OpenApiString(
                                "2026-09-01T00:00:00Z"),
                            ["description"] = new OpenApiString(
                                "You have no KYC attempts remaining this month. You can try again next month.")
                        }
                    }
                }
            }
        };
    }
}
