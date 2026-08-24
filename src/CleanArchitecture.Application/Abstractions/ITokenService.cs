namespace CleanArchitecture.Application.Abstractions;

public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, string email, IReadOnlyList<string> roles);

    RefreshTokenPair CreateRefreshToken();

    string Hash(string rawRefreshToken);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public sealed record RefreshTokenPair(string Raw, string Hash, DateTimeOffset ExpiresAtUtc);
