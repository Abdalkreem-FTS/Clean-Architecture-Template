using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Identity;

internal sealed class UserRepository(
    UserManager<ApplicationUser> usersManager,
    AppDbContext context,
    IOptions<IdentityOptions> identityOptions) : IUserRepository
{
    public async Task<Result<User>> CreateAsync(User user, string password, CancellationToken cancellationToken)
    {
        var applicationUser = new ApplicationUser
        {
            Id = user.Id,
            UserName = user.Email,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAtUtc = user.CreatedAtUtc,
        };

        IdentityResult created;

        try
        {
            created = await usersManager.CreateAsync(applicationUser, password);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return UserErrors.EmailNotUnique;
        }

        if (!created.Succeeded)
        {
            return created.Errors.Any(IsDuplicate) ? UserErrors.EmailNotUnique : Describe(created);
        }

        return user;
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await usersManager.FindByEmailAsync(email);

        return user is null ? null : await ToDomainAsync(user, cancellationToken);
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await usersManager.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return user is null ? null : await ToDomainAsync(user, cancellationToken);
    }

    public async Task<UserPage> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        int total = await context.Users.CountAsync(cancellationToken);

        List<ApplicationUser> pageOfUsers = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        // One query for the whole page rather than a correlated subquery per row.
        ILookup<Guid, string> roles = (await RolesOfAsync([.. pageOfUsers.Select(user => user.Id)], cancellationToken))
            .ToLookup(assignment => assignment.UserId, assignment => assignment.Role);

        List<User> items = [.. pageOfUsers.Select(user => ToDomain(user, roles[user.Id]))];

        return new UserPage(items, total);
    }

    public async Task<bool> VerifyPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        ApplicationUser applicationUser = await RequireAsync(user.Id, cancellationToken);

        if (await usersManager.CheckPasswordAsync(applicationUser, password))
        {
            return true;
        }

        await usersManager.AccessFailedAsync(applicationUser);

        return false;
    }

    public async Task<bool> IsLockedOutAsync(User user, CancellationToken cancellationToken) =>
        await usersManager.IsLockedOutAsync(await RequireAsync(user.Id, cancellationToken));

    public async Task ResetFailedAttemptsAsync(User user, CancellationToken cancellationToken) =>
        await usersManager.ResetAccessFailedCountAsync(await RequireAsync(user.Id, cancellationToken));

    public async Task<bool> RequiresEmailConfirmationAsync(User user, CancellationToken cancellationToken)
    {
        if (!identityOptions.Value.SignIn.RequireConfirmedEmail)
        {
            return false;
        }

        ApplicationUser applicationUser = await RequireAsync(user.Id, cancellationToken);

        return !applicationUser.EmailConfirmed;
    }

    public async Task RecordLoginAsync(User user, DateTimeOffset atUtc, CancellationToken cancellationToken)
    {
        ApplicationUser applicationUser = await RequireAsync(user.Id, cancellationToken);

        applicationUser.LastLoginAtUtc = atUtc;
        await usersManager.UpdateAsync(applicationUser);
    }

    public async Task<Result<Success>> AssignRoleAsync(User user, string role, CancellationToken cancellationToken)
    {
        ApplicationUser applicationUser = await RequireAsync(user.Id, cancellationToken);

        IdentityResult result = await usersManager.AddToRoleAsync(applicationUser, role);

        return result.Succeeded ? Result.Success : Describe(result);
    }

    // UserManager.FindByIdAsync goes through the same DbContext as everything else here, so
    // once a request has loaded a row once, EF's change tracker answers this from memory
    // instead of round-tripping again for every check authentication runs.
    private async Task<ApplicationUser> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await usersManager.FindByIdAsync(id.ToString())
            ?? throw new InvalidOperationException($"User '{id}' was expected to exist.");
    }

    private async Task<User> ToDomainAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        List<RoleAssignment> assignments = await RolesOfAsync([user.Id], cancellationToken);

        return ToDomain(user, assignments.Select(assignment => assignment.Role));
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

    private static User ToDomain(ApplicationUser user, IEnumerable<string> roles) =>
        User.FromStorage(user.Id, user.Email!, user.FirstName, user.LastName, roles, user.CreatedAtUtc, user.LastLoginAtUtc);

    private sealed record RoleAssignment(Guid UserId, string Role);

    private static List<Error> Describe(IdentityResult result) =>
        [.. result.Errors.Select(error => UserErrors.IdentityFailure(error.Code, error.Description))];

    private static bool IsDuplicate(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
