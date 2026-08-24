using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> usersManager,
    AppDbContext context,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider clock) : IIdentityService
{
    public async Task<Result<Guid>> RegisterAsync(Registration registration, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Ids.New(),
            UserName = registration.Email,
            Email = registration.Email,
            FirstName = registration.FirstName,
            LastName = registration.LastName,
            CreatedAtUtc = clock.GetUtcNow()
        };

        IdentityResult created;

        try
        {
            created = await usersManager.CreateAsync(user, registration.Password);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return UserErrors.EmailNotUnique;
        }

        if (!created.Succeeded)
        {
            return created.Errors.Any(IsDuplicate) ? UserErrors.EmailNotUnique : Describe(created);
        }

        IdentityResult roleAssigned = await usersManager.AddToRoleAsync(user, Roles.User);

        if (!roleAssigned.Succeeded)
        {
            return Describe(roleAssigned);
        }

        return user.Id;
    }

    public async Task<Result<User>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await usersManager.FindByEmailAsync(email);

        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        if (!await usersManager.CheckPasswordAsync(user, password))
        {
            await usersManager.AccessFailedAsync(user);

            return UserErrors.InvalidCredentials;
        }

        if (await usersManager.IsLockedOutAsync(user))
        {
            return UserErrors.LockedOut;
        }

        await usersManager.ResetAccessFailedCountAsync(user);

        if (identityOptions.Value.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            return UserErrors.EmailNotConfirmed;
        }

        user.LastLoginAtUtc = clock.GetUtcNow();
        await usersManager.UpdateAsync(user);

        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<Result<User>> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await usersManager.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? UserErrors.NotFound : await ToResponseAsync(user, cancellationToken);
    }

    public async Task<Result<Paged<User>>> ListAsync(
        PageQuery page,
        CancellationToken cancellationToken)
    {
        int total = await context.Users.CountAsync(cancellationToken);

        List<ApplicationUser> users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip(page.Skip)
            .Take(page.Size)
            .ToListAsync(cancellationToken);

        // One query for the whole page rather than a correlated subquery per row.
        ILookup<Guid, string> roles = (await RolesOfAsync([.. users.Select(user => user.Id)], cancellationToken))
            .ToLookup(assignment => assignment.UserId, assignment => assignment.Role);

        List<User> items = [.. users.Select(user => ToResponse(user, [.. roles[user.Id]]))];

        return new Paged<User>(items, page.Number, page.Size, total);
    }

    public async Task<Result<Success>> AssignRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await usersManager.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (await usersManager.IsInRoleAsync(user, role))
        {
            return Result.Success;
        }

        IdentityResult result = await usersManager.AddToRoleAsync(user, role);

        if (!result.Succeeded)
        {
            return Describe(result);
        }

        return Result.Success;
    }

    private async Task<User> ToResponseAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        List<RoleAssignment> assignments = await RolesOfAsync([user.Id], cancellationToken);

        return ToResponse(user, [.. assignments.Select(assignment => assignment.Role)]);
    }

    // The one place that knows how a user's roles are read, so changing it is one edit.
    private Task<List<RoleAssignment>> RolesOfAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken) =>
        context.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(
                context.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new RoleAssignment(userRole.UserId, role.Name!))
            .ToListAsync(cancellationToken);

    private static User ToResponse(ApplicationUser user, IReadOnlyList<string> roles) =>
        new(user.Id, user.Email!, user.FirstName, user.LastName, roles, user.CreatedAtUtc, user.LastLoginAtUtc);

    private sealed record RoleAssignment(Guid UserId, string Role);

    private static List<Error> Describe(IdentityResult result) =>
        [.. result.Errors.Select(error => UserErrors.IdentityFailure(error.Code, error.Description))];

    private static bool IsDuplicate(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
