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

        Error first = errors[0];

        if (first.Type == ErrorType.Validation)
        {
            return Results.ValidationProblem(
                errors.GroupBy(error => error.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray()),
                title: "One or more validation errors occurred.");
        }

        return Results.Problem(
            title: Title(first.Type),
            detail: first.Description,
            statusCode: StatusCode(first.Type),
            extensions: new Dictionary<string, object?> { ["code"] = first.Code });
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
}
