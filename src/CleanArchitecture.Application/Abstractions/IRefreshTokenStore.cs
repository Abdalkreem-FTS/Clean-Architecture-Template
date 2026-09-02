namespace CleanArchitecture.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task IssueAsync(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    // Who holds this token, if it is still usable.
    Task<Guid?> FindActiveUserIdAsync(string presentedTokenHash, CancellationToken cancellationToken);

    Task<Guid?> RotateAsync(
        string presentedTokenHash,
        string replacementTokenHash,
        DateTimeOffset replacementExpiresAtUtc,
        CancellationToken cancellationToken);

    Task RevokeAsync(string presentedTokenHash, CancellationToken cancellationToken);
}
