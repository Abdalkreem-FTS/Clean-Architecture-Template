using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Common;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> users,
    AppDbContext context,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider clock) : IIdentityService
{
    public async Task<Result<Guid>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(request.Email) is not null)
        {
            return UserErrors.EmailNotUnique;
        }

        var user = new ApplicationUser
        {
            Id = Ids.New(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAtUtc = clock.GetUtcNow()
        };

        IdentityResult created = await users.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            return Describe(created);
        }

        IdentityResult roleAssigned = await users.AddToRoleAsync(user, Roles.User);

        return roleAssigned.Succeeded ? user.Id : Describe(roleAssigned);
    }

    public async Task<Result<UserResponse>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return UserErrors.LockedOut;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);

            return await users.IsLockedOutAsync(user)
                ? UserErrors.LockedOut
                : UserErrors.InvalidCredentials;
        }

        if (identityOptions.Value.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
        {
            return UserErrors.EmailNotConfirmed;
        }

        await users.ResetAccessFailedCountAsync(user);

        user.LastLoginAtUtc = clock.GetUtcNow();
        await users.UpdateAsync(user);

        return await ToResponseAsync(user, cancellationToken);
    }

    public async Task<Result<UserResponse>> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null ? UserErrors.NotFound : await ToResponseAsync(user, cancellationToken);
    }

    public async Task<Result<PagedResponse<UserResponse>>> ListAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        int total = await context.Users.CountAsync(cancellationToken);

        List<UserResponse> items = await context.Users
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip(page.Skip)
            .Take(page.Size)
            .Select(user => new UserResponse(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                context.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                    .ToList(),
                user.CreatedAtUtc,
                user.LastLoginAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>(items, page.Number, page.Size, total);
    }

    public async Task<Result<Success>> AssignRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await users.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (await users.IsInRoleAsync(user, role))
        {
            return Result.Success;
        }

        IdentityResult result = await users.AddToRoleAsync(user, role);

        return result.Succeeded ? Result.Success : Describe(result);
    }

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        List<string> roles = await context.UserRoles
            .Where(userRole => userRole.UserId == user.Id)
            .Join(context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
            .ToListAsync(cancellationToken);

        return new UserResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles,
            user.CreatedAtUtc,
            user.LastLoginAtUtc);
    }

    private static Error Describe(IdentityResult result) =>
        UserErrors.IdentityFailure(string.Join(" ", result.Errors.Select(error => error.Description)));
}
