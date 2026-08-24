using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Users;

internal sealed class UserService(IIdentityService identityService, ICurrentUser currentUser) : IUserService
{
    public Task<Result<User>> GetCurrentAsync(CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Task.FromResult<Result<User>>(UserErrors.InvalidCredentials)
            : identityService.FindByIdAsync(userId, cancellationToken);

    public Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        identityService.FindByIdAsync(userId, cancellationToken);

    public Task<Result<Paged<User>>> ListAsync(
        PageQuery page,
        CancellationToken cancellationToken) =>
        identityService.ListAsync(page, cancellationToken);

    public Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken) =>
        !Roles.IsKnown(role)
            ? Task.FromResult<Result<Success>>(UserErrors.RoleNotFound)
            : identityService.AssignRoleAsync(userId, role.ToLowerInvariant(), cancellationToken);
}
