using System.Diagnostics;

namespace Pryde.Api.Middleware;

public sealed class VehicleMediaRequestTimingMiddleware(
    RequestDelegate next,
    ILogger<VehicleMediaRequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsVehicleMediaRequest(context.Request))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Vehicle media HTTP request started. Method: {Method}, Path: {Path}, ContentLengthBytes: {ContentLengthBytes}, Operation: {Operation}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Request.ContentLength,
            "MultipartRequest");

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Vehicle media HTTP request completed. Method: {Method}, Path: {Path}, ContentLengthBytes: {ContentLengthBytes}, StatusCode: {StatusCode}, Operation: {Operation}, DurationMilliseconds: {DurationMilliseconds}, Success: {Success}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.ContentLength,
                context.Response.StatusCode,
                "MultipartRequest",
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode < StatusCodes.Status500InternalServerError);
        }
    }

    private static bool IsVehicleMediaRequest(HttpRequest request)
    {
        var path = request.Path.Value;
        return HttpMethods.IsPut(request.Method) &&
               path is not null &&
               path.Contains(
                   "/vehicles/",
                   StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith(
                   "/media",
                   StringComparison.OrdinalIgnoreCase);
    }
}
