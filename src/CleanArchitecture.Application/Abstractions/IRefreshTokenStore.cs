namespace CleanArchitecture.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task IssueAsync(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    Task<Guid?> RotateAsync(
        string presentedTokenHash,
        string replacementTokenHash,
        DateTimeOffset replacementExpiresAtUtc,
        CancellationToken cancellationToken);

    Task RevokeAsync(string presentedTokenHash, CancellationToken cancellationToken);
}
