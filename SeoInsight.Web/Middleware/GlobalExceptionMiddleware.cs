using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SeoInsight.Web.Middleware;

/// <summary>
/// Global middleware providing correlation ID and friendly Error view (MVC) or ProblemDetails (AJAX).
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
                _logger.LogError(ex, "Unhandled exception occurred on path {Path}", context.Request.Path);
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

        bool isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.Request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));

        if (isAjax)
        {
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "An error occurred while processing your request.",
                Detail = exception.Message,
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
        else
        {
            // For standard browser requests, redirect to Error view
            context.Response.Redirect($"/Home/Error?correlationId={Uri.EscapeDataString(correlationId)}");
        }
    }
}
