using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IUserService
{
    Task<Result<UserResponse>> GetCurrentAsync(CancellationToken cancellationToken);

    Task<Result<UserResponse>> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PagedResponse<UserResponse>>> ListAsync(PageRequest page, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
}
