using CleanArchitecture.Api.Contracts;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Api.Filters;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tokens")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("", Login)
            .WithSummary("Exchange credentials for tokens")
            .WithValidation<LoginRequest>()
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("", Refresh)
            .WithSummary("Exchange a refresh token for a new pair")
            .WithDescription("The token presented is revoked, so each refresh token works exactly once.")
            .WithValidation<RefreshRequest>()
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("", Logout)
            .WithSummary("Revoke a refresh token")
            .WithValidation<RefreshRequest>()
            .Produces(StatusCodes.Status204NoContent);
    }

    // DELETE does not infer a complex parameter as the body the way POST/PUT do, so this has to
    // be explicit or the app fails to start.
    private static async Task<IResult> Logout(
        [FromBody] RefreshRequest request,
        IAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        Result<Success> result = await authenticationService.LogoutAsync(request.RefreshToken, cancellationToken);

        return result.Match(_ => Results.NoContent(), errors => errors.ToProblem());
    }

    private static async Task<IResult> Refresh(RefreshRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<AuthenticationTokens> result =
            await authenticationService.RefreshAsync(request.RefreshToken, cancellationToken);

        return result.Match(
            tokens => Results.Ok(AuthenticationResponse.From(tokens)),
            errors => errors.ToProblem());
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<AuthenticationTokens> result =
            await authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);

        return result.Match(
            tokens => Results.Ok(AuthenticationResponse.From(tokens)),
            errors => errors.ToProblem());
    }
}
