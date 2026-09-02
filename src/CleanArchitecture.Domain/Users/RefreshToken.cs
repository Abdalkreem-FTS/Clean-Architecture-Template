using CleanArchitecture.Domain.Common;

namespace CleanArchitecture.Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A refresh token must belong to a user.", nameof(userId));
        }

        if (expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                expiresAtUtc,
                "A refresh token must expire after it is issued.");
        }

        return new RefreshToken
        {
            Id = Ids.New(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
    }
}
