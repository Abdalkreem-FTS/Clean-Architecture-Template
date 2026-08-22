using CleanArchitecture.Application.Users;

namespace CleanArchitecture.Application.Abstractions;

public interface ITokenService
{
    AccessToken CreateAccessToken(UserResponse user);

    RefreshTokenPair CreateRefreshToken();

    string Hash(string rawRefreshToken);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public sealed record RefreshTokenPair(string Raw, string Hash, DateTimeOffset ExpiresAtUtc);
