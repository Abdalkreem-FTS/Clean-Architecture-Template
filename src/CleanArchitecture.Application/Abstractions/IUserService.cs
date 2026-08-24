using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IUserService
{
    Task<Result<User>> GetCurrentAsync(CancellationToken cancellationToken);

    Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<Paged<User>>> ListAsync(PageQuery page, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
}
