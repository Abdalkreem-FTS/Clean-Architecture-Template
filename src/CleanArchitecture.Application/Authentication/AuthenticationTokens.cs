namespace CleanArchitecture.Application.Authentication;

public sealed record AuthenticationTokens(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken);
