using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Domain.Users;

// The business rules around an account: what "registered" means, what order authentication's
// checks run in, which failure wins when more than one applies. IUserRepository is the only
// thing this talks to — it does not know or care what is on the other side of it.
public sealed class UserAccountService(IUserRepository users, TimeProvider clock) : IUserAccountService
{
    public async Task<Result<Guid>> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        User user = User.Register(email, firstName, lastName, clock.GetUtcNow());

        Result<User> created = await users.CreateAsync(user, password, cancellationToken);

        if (created.IsError)
        {
            return created.Errors;
        }

        Result<Success> roleAssigned = await users.AssignRoleAsync(created.Value, Roles.User, cancellationToken);

        if (roleAssigned.IsError)
        {
            return roleAssigned.Errors;
        }

        return created.Value.Id;
    }

    public async Task<Result<User>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        User? user = await users.FindByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        if (!await users.VerifyPasswordAsync(user, password, cancellationToken))
        {
            return UserErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user, cancellationToken))
        {
            return UserErrors.LockedOut;
        }

        await users.ResetFailedAttemptsAsync(user, cancellationToken);

        if (await users.RequiresEmailConfirmationAsync(user, cancellationToken))
        {
            return UserErrors.EmailNotConfirmed;
        }

        DateTimeOffset now = clock.GetUtcNow();

        await users.RecordLoginAsync(user, now, cancellationToken);
        user.RecordLogin(now);

        return user;
    }

    public async Task<Result<User>> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await users.FindByIdAsync(userId, cancellationToken);

        return user is null ? UserErrors.NotFound : user;
    }

    public Task<UserPage> ListAsync(int skip, int take, CancellationToken cancellationToken) =>
        users.ListAsync(skip, take, cancellationToken);

    public async Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        User? user = await users.FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.Roles.Contains(role, StringComparer.Ordinal))
        {
            return Result.Success;
        }

        return await users.AssignRoleAsync(user, role, cancellationToken);
    }
}
