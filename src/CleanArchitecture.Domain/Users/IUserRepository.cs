using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Domain.Users;

// The port Infrastructure adapts to. Everything here speaks in Domain terms only — no
// ApplicationUser, no UserManager, no EF — so the account service on the other side of it
// never has to know which store or which identity framework is behind it.
public interface IUserRepository
{
    Task<Result<User>> CreateAsync(User user, string password, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserPage> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<bool> VerifyPasswordAsync(User user, string password, CancellationToken cancellationToken);

    Task<bool> IsLockedOutAsync(User user, CancellationToken cancellationToken);

    Task ResetFailedAttemptsAsync(User user, CancellationToken cancellationToken);

    Task<bool> RequiresEmailConfirmationAsync(User user, CancellationToken cancellationToken);

    Task RecordLoginAsync(User user, DateTimeOffset atUtc, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(User user, string role, CancellationToken cancellationToken);
}
