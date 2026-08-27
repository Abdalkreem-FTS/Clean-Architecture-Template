using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Authentication;

internal sealed class AuthenticationService(
    IUserAccountService accounts,
    ITokenService tokenService,
    IRefreshTokenStore refreshTokenService) : IAuthenticationService
{
    public Task<Result<Guid>> RegisterAsync(Registration registration, CancellationToken cancellationToken) =>
        accounts.RegisterAsync(
            registration.Email,
            registration.Password,
            registration.FirstName,
            registration.LastName,
            cancellationToken);

    public async Task<Result<AuthenticationTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        Result<User> user = await accounts.AuthenticateAsync(email, password, cancellationToken);

        if (user.IsError)
        {
            return user.Errors;
        }

        RefreshTokenPair refreshToken = tokenService.CreateRefreshToken();

        await refreshTokenService.IssueAsync(
            user.Value.Id,
            refreshToken.Hash,
            refreshToken.ExpiresAtUtc,
            cancellationToken);

        return Respond(user.Value, refreshToken);
    }

    public async Task<Result<AuthenticationTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string presented = tokenService.Hash(refreshToken);

        Guid? holder = await refreshTokenService.FindActiveUserIdAsync(presented, cancellationToken);

        if (holder is null)
        {
            return UserErrors.InvalidRefreshToken;
        }

        Result<User> user = await accounts.FindByIdAsync(holder.Value, cancellationToken);

        if (user.IsError)
        {
            return UserErrors.InvalidRefreshToken;
        }

        RefreshTokenPair replacement = tokenService.CreateRefreshToken();

        Guid? rotated = await refreshTokenService.RotateAsync(
            presented,
            replacement.Hash,
            replacement.ExpiresAtUtc,
            cancellationToken);

        if (rotated is null)
        {
            return UserErrors.InvalidRefreshToken;
        }

        return Respond(user.Value, replacement);
    }

    public async Task<Result<Success>> LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(tokenService.Hash(refreshToken), cancellationToken);

        return Result.Success;
    }

    private AuthenticationTokens Respond(User user, RefreshTokenPair refreshToken)
    {
        AccessToken accessToken = tokenService.CreateAccessToken(user.Id, user.Email, user.Roles);

        return new AuthenticationTokens(user.Id, accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.Raw);
    }
}
