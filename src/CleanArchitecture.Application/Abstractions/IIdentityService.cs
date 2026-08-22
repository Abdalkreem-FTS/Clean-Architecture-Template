using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;

namespace CleanArchitecture.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<Guid>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<UserResponse>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<Result<UserResponse>> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<PagedResponse<UserResponse>>> ListAsync(PageRequest page, CancellationToken cancellationToken);

    Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken);
}
