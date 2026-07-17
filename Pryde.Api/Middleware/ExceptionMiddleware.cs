using System.Text.Json;
using Pryde.Domain.Common.Exceptions;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Api.Middleware;

public class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, exception.Message);

            await HandleExceptionAsync(
                context,
                exception);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            BadRequestException => StatusCodes.Status400BadRequest,
            ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var response = new ErrorResponseDto
        {
            StatusCode = statusCode,
            Message = exception.Message,
            //TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}
