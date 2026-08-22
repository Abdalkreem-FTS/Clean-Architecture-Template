using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/authentication")
            .WithTags("Authentication")
            .AllowAnonymous();

        group.MapPost("/register", Register)
            .WithSummary("Create an account")
            .WithValidation<RegisterRequest>()
            .Produces<RegisteredResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .WithSummary("Exchange credentials for tokens")
            .WithValidation<LoginRequest>()
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/refresh", Refresh)
            .WithSummary("Exchange a refresh token for a new pair")
            .WithDescription("The token presented is revoked, so each refresh token works exactly once.")
            .WithValidation<RefreshRequest>()
            .Produces<AuthenticationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .WithSummary("Revoke a refresh token")
            .WithValidation<RefreshRequest>()
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> Logout(RefreshRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<Success> result = await authenticationService.LogoutAsync(request, cancellationToken);

        return result.Match(_ => Results.NoContent(), errors => errors.ToProblem());
    }

    private static async Task<IResult> Refresh(RefreshRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<AuthenticationResponse> result = await authenticationService.RefreshAsync(request, cancellationToken);

        return result.Match(Results.Ok, errors => errors.ToProblem());
    }

    private static async Task<IResult> Login(LoginRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<AuthenticationResponse> result = await authenticationService.LoginAsync(request, cancellationToken);

        return result.Match(Results.Ok, errors => errors.ToProblem());
    }

    private static async Task<IResult> Register(RegisterRequest request, IAuthenticationService authenticationService, CancellationToken cancellationToken)
    {
        Result<Guid> result = await authenticationService.RegisterAsync(request, cancellationToken);

        return result.Match(id => Results.Created($"/api/users/{id}", new RegisteredResponse(id)), errors => errors.ToProblem());
    }

    private sealed record RegisteredResponse(Guid Id);
}
