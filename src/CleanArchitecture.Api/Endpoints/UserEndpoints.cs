using CleanArchitecture.Api.Contracts;
using CleanArchitecture.Api.Extensions;
using CleanArchitecture.Api.Filters;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapPost("", async (
                RegisterRequest request,
                IAuthenticationService authenticationService,
                CancellationToken cancellationToken) =>
            {
                Result<Guid> result =
                    await authenticationService.RegisterAsync(request.ToRegistration(), cancellationToken);

                return result.Match(
                    id => Results.Created($"/api/users/{id}", new RegisteredResponse(id)),
                    errors => errors.ToProblem());
            })
            .WithSummary("Create an account")
            .WithValidation<RegisterRequest>()
            .AllowAnonymous()
            .Produces<RegisteredResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/me", async (IUserService users, CancellationToken cancellationToken) =>
            {
                Result<User> result = await users.GetCurrentAsync(cancellationToken);

                return result.Match(
                    user => Results.Ok(UserResponse.From(user)),
                    errors => errors.ToProblem());
            })
            .WithSummary("The signed-in user's own record")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (
                int? page,
                int? pageSize,
                IUserService userService,
                CancellationToken cancellationToken) =>
            {
                Result<Paged<User>> result = await userService.ListAsync(
                    PageQuery.Of(page, pageSize),
                    cancellationToken);

                return result.Match(
                    users => Results.Ok(UserResponse.From(users)),
                    errors => errors.ToProblem());
            })
            .WithSummary("A page of users")
            .WithDescription(
                "Paged rather than unbounded: page defaults to 1 and pageSize to 20, capped at 100. "
                + "Out-of-range values are clamped rather than rejected.")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .Produces<PagedResponse<UserResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IUserService users,
                CancellationToken cancellationToken) =>
            {
                Result<User> result = await users.GetByIdAsync(id, cancellationToken);

                return result.Match(
                    user => Results.Ok(UserResponse.From(user)),
                    errors => errors.ToProblem());
            })
            .WithSummary("One user by id")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/roles", async (
                Guid id,
                AssignRoleRequest request,
                IUserService users,
                CancellationToken cancellationToken) =>
            {
                Result<Success> result = await users.AssignRoleAsync(id, request.Role, cancellationToken);

                return result.Match(_ => Results.NoContent(), errors => errors.ToProblem());
            })
            .WithSummary("Grant a role")
            .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
            .WithValidation<AssignRoleRequest>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
