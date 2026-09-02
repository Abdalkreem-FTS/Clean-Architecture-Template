using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Domain.Users;

public interface IUserAccountService
{
    Task<Result<Guid>> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken);

    Task<Result<User>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);

    Task<Result<User>> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserPage> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
}
