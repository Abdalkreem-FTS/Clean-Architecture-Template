using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;

namespace CleanArchitecture.Application.Authentication;

internal sealed class AuthenticationService(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenStore refreshTokenService) : IAuthenticationService
{
    public Task<Result<Guid>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        identityService.RegisterAsync(request, cancellationToken);

    public async Task<Result<AuthenticationResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        Result<UserResponse> user = await identityService.AuthenticateAsync(
            request.Email,
            request.Password,
            cancellationToken);

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

    public async Task<Result<AuthenticationResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        RefreshTokenPair replacement = tokenService.CreateRefreshToken();

        Guid? userId = await refreshTokenService.RotateAsync(
            tokenService.Hash(request.RefreshToken),
            replacement.Hash,
            replacement.ExpiresAtUtc,
            cancellationToken);

        if (userId is null)
        {
            return UserErrors.InvalidRefreshToken;
        }

        Result<UserResponse> user = await identityService.FindByIdAsync(userId.Value, cancellationToken);

        if (user.IsError)
        {
            return UserErrors.InvalidRefreshToken;
        }

        return Respond(user.Value, replacement);
    }

    public async Task<Result<Success>> LogoutAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(tokenService.Hash(request.RefreshToken), cancellationToken);

        return Result.Success;
    }

    private AuthenticationResponse Respond(UserResponse user, RefreshTokenPair refreshToken)
    {
        AccessToken accessToken = tokenService.CreateAccessToken(user);

        return new AuthenticationResponse(user.Id, accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.Raw);
    }
}
