using System.Net;
using System.Text.Json;
using TaskManagement.Application.Common;
using TaskManagement.Application.Common.Exceptions;

namespace TaskManagement.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var statusCode = (int)HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred";

        if (ex is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            message = appEx.Message;
        }
        else
        {
            // Expose base exception message to speed up diagnosing schema/runtime issues in deployed envs.
            // This API is used by trusted clients; without this, we only get a generic 500 on mobile.
            message = ex.GetBaseException().Message;
        }

        var userId = context.User?.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value
                     ?? context.User?.Identity?.Name
                     ?? "anonymous";

        _logger.LogError(
            ex,
            "Unhandled exception. traceId={TraceId} method={Method} path={Path} query={Query} user={User} statusCode={StatusCode} message={Message}",
            context.TraceIdentifier,
            context.Request.Method,
            context.Request.Path.Value,
            context.Request.QueryString.Value,
            userId,
            statusCode,
            ex.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object>.Fail(message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

