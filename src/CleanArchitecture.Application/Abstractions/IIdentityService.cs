using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<Guid>> RegisterAsync(Registration registration, CancellationToken cancellationToken);

    Task<Result<User>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<Result<User>> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<Paged<User>>> ListAsync(PageQuery page, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
}
