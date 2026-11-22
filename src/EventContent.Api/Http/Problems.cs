using System.Diagnostics;
using EventContent.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace EventContent.Api.Http;

/// <summary>Translates domain/application results into RFC 9457 ProblemDetails with stable error codes.</summary>
public static class Problems
{
    public static IResult ToProblem(this Error error, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = error.Code,
            ["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
        };
        if (error.FieldErrors is { Count: > 0 })
        {
            extensions["errors"] = error.FieldErrors;
        }

        return TypedResults.Problem(detail: error.Message, statusCode: status, title: Title(status), extensions: extensions);
    }

    public static IResult ToHttp<T>(this Result<T> result, HttpContext context, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem(context);
    }

    public static IResult ToHttp(this Result result, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem(context);
    }

    private static string Title(int status) => status switch
    {
        StatusCodes.Status422UnprocessableEntity => "Validation failed",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        _ => "Bad request",
    };
}

/// <summary>Last-resort handler: logs the exception and returns a generic 500 without internals.</summary>
public sealed partial class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;
            await TypedResults.Problem(
                    title: "Bad request",
                    detail: "The request could not be read.",
                    statusCode: badRequest.StatusCode,
                    extensions: new Dictionary<string, object?> { ["code"] = "common.bad_request" })
                .ExecuteAsync(httpContext);
            return true;
        }

        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        await TypedResults.Problem(
                title: "Unexpected error",
                detail: "Something went wrong. Try again, and contact support with the trace id if it persists.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "common.unexpected",
                    ["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
                })
            .ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path);
}
