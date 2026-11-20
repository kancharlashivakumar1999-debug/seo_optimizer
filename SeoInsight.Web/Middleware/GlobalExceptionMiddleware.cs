using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SeoInsight.Web.Middleware;

/// <summary>
/// Global middleware providing correlation ID and structured error payload capture (AJAX/Fetch & MVC).
/// Returns exact error message and details in standard ProblemDetails format so the frontend toaster can display them.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public const string CorrelationHeader = "X-Correlation-ID";

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationHeader] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred on path {Path}: {Message}", context.Request.Path, ex.Message);
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Extract most descriptive error message from exception hierarchy
        var exactMessage = exception.InnerException != null 
            ? $"{exception.Message} -> {exception.InnerException.Message}" 
            : exception.Message;

        bool isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.Request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                      || context.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

        if (isAjax)
        {
            context.Response.ContentType = "application/json";
            var errorResponse = new
            {
                success = false,
                title = "Database / Server Error",
                message = exactMessage,
                detail = exception.ToString(),
                path = context.Request.Path.Value,
                correlationId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
        }
        else
        {
            context.Response.Redirect($"/Home/Error?correlationId={Uri.EscapeDataString(correlationId)}");
        }
    }
}
