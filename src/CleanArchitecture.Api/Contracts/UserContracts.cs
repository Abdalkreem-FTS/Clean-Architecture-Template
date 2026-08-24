using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;

namespace CleanArchitecture.Api.Contracts;

public sealed record AssignRoleRequest(string Role);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc)
{
    public static UserResponse From(User user) =>
        new(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Roles,
            user.CreatedAtUtc,
            user.LastLoginAtUtc);

    public static PagedResponse<UserResponse> From(Paged<User> page) =>
        new([.. page.Items.Select(From)], page.Page, page.PageSize, page.TotalCount);
}
