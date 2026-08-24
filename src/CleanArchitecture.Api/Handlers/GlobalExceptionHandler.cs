using Microsoft.AspNetCore.Diagnostics;

namespace CleanArchitecture.Api.Handlers;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing {Path}.", httpContext.Request.Path);

        // Nothing can be written once the response is on the wire. Reporting failure lets the
        // host abort the connection instead of appending a problem document to a half-sent body.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Written through IProblemDetailsService rather than serialized here by hand, so the
        // response goes through content negotiation and picks up the traceId and any
        // customization registered with AddProblemDetails.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server error",
                Detail = "An unexpected error occurred.",
                Instance = httpContext.Request.Path,
            },
        });
    }
}
