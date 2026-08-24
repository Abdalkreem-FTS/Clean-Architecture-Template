using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Api.Extensions;

public static class ProblemExtensions
{
    public static IResult ToProblem(this List<Error> errors)
    {
        if (errors.Count == 0)
        {
            return Results.Problem();
        }

        if (errors.TrueForAll(error => error.Type == ErrorType.Validation))
        {
            return Results.ValidationProblem(
                errors.GroupBy(error => error.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray()),
                title: "One or more validation errors occurred.");
        }

        Error primary = errors.First(error => error.Type != ErrorType.Validation);

        var extensions = new Dictionary<string, object?> { ["code"] = primary.Code };

        if (errors.Count > 1)
        {
            extensions["errors"] = errors
                .Select(error => new ProblemError(error.Code, error.Description))
                .ToArray();
        }

        return Results.Problem(
            title: Title(primary.Type),
            detail: primary.Description,
            statusCode: StatusCode(primary.Type),
            extensions: extensions);
    }

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string Title(ErrorType type) => type switch
    {
        ErrorType.Validation => "Bad request",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        _ => "Server error",
    };

    public sealed record ProblemError(string Code, string Description);
}
