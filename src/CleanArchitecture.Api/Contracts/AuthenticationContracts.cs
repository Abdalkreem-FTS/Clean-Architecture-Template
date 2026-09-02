using CleanArchitecture.Application.Authentication;

namespace CleanArchitecture.Api.Contracts;

// The wire shapes for /api/tokens and /api/users. Requests carry a To… that hands the use case its own
// type; responses carry a From that builds the wire shape out of one. Nothing below Api sees
// either of these records.
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName)
{
    public Registration ToRegistration() => new(Email, Password, FirstName, LastName);
}

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RegisteredResponse(Guid Id);

public sealed record AuthenticationResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken)
{
    public static AuthenticationResponse From(AuthenticationTokens tokens) =>
        new(tokens.UserId, tokens.AccessToken, tokens.AccessTokenExpiresAtUtc, tokens.RefreshToken);
}
