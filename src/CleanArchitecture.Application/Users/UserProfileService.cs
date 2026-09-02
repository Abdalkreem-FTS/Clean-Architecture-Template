using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;
using DomainUser = CleanArchitecture.Domain.Users.User;

namespace CleanArchitecture.Application.Users;

internal sealed class UserProfileService(IUserAccountService accounts, ICurrentUser currentUser) : IUserProfileService
{
    public Task<Result<User>> GetCurrentAsync(CancellationToken cancellationToken) =>
        currentUser.UserId is not { } userId
            ? Task.FromResult<Result<User>>(UserErrors.InvalidCredentials)
            : GetByIdAsync(userId, cancellationToken);

    public async Task<Result<User>> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        Result<DomainUser> user = await accounts.FindByIdAsync(userId, cancellationToken);

        if (user.IsError)
        {
            return user.Errors;
        }

        return ToResponse(user.Value);
    }

    public async Task<Result<Paged<User>>> ListAsync(
        PageQuery page,
        CancellationToken cancellationToken)
    {
        UserPage result = await accounts.ListAsync(page.Skip, page.Size, cancellationToken);

        return new Paged<User>([.. result.Items.Select(ToResponse)], page.Number, page.Size, result.TotalCount);
    }

    public Task<Result<Success>> AssignRoleAsync(Guid userId, string role, CancellationToken cancellationToken) =>
        !Roles.IsKnown(role)
            ? Task.FromResult<Result<Success>>(UserErrors.RoleNotFound)
            : accounts.AssignRoleAsync(userId, role.ToLowerInvariant(), cancellationToken);

    private static User ToResponse(DomainUser user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Roles, user.CreatedAtUtc, user.LastLoginAtUtc);
}
