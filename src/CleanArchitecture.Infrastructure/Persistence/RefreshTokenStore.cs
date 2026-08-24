using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;

namespace CleanArchitecture.Infrastructure.Persistence;

internal sealed class RefreshTokenStore(AppDbContext context, TimeProvider clock) : IRefreshTokenStore
{
    public async Task IssueAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();

        // Spent rows are of no further use: a revoked token can never be presented again, and an
        // expired one is refused on sight. Clearing both here keeps a long-lived account from
        // accumulating one dead row per sign-in for the whole refresh-token lifetime.
        await context.RefreshTokens
            .Where(token => token.UserId == userId
                && (token.ExpiresAtUtc < now || token.RevokedAtUtc != null))
            .ExecuteDeleteAsync(cancellationToken);

        context.RefreshTokens.Add(RefreshToken.Issue(userId, tokenHash, now, expiresAtUtc));

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> FindActiveUserIdAsync(
        string presentedTokenHash,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();

        return await context.RefreshTokens
            .Where(token => token.TokenHash == presentedTokenHash
                && token.RevokedAtUtc == null
                && token.ExpiresAtUtc > now)
            .Select(token => (Guid?)token.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> RotateAsync(
        string presentedTokenHash,
        string replacementTokenHash,
        DateTimeOffset replacementExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();

        await using NpgsqlCommand command = new(RotateSql());

        var parameters = new NpgsqlParameter[]
        {
            new("now", NpgsqlDbType.TimestampTz) { Value = now },
            new("presented", NpgsqlDbType.Varchar) { Value = presentedTokenHash },
            new("replacement", NpgsqlDbType.Varchar) { Value = replacementTokenHash },
            new("id", NpgsqlDbType.Uuid) { Value = Ids.New() },
            new("expires", NpgsqlDbType.TimestampTz) { Value = replacementExpiresAtUtc }
        };

        command.Parameters.AddRange(parameters);

        await context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            command.Connection = (NpgsqlConnection)context.Database.GetDbConnection();

            object? result = await command.ExecuteScalarAsync(cancellationToken);

            return result is Guid userId ? userId : null;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    public async Task RevokeAsync(string presentedTokenHash, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();

        await context.RefreshTokens
            .Where(token => token.TokenHash == presentedTokenHash && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                token => token.SetProperty(property => property.RevokedAtUtc, now),
                cancellationToken);
    }

    // A replayed token that is already revoked is refused, and nothing more happens. A stricter
    // policy treats a replay as evidence the token was stolen and revokes the whole family for
    // that user. Deliberately out of scope for the template — see the README.
    //
    // Rotation is one statement on purpose: the UPDATE decides the winner, so eight concurrent
    // requests presenting the same token produce exactly one new pair without a transaction or a
    // lock. EF cannot express "update this row and return a column from it", hence the SQL.
    //
    // Table and column names are read out of the EF model rather than spelled out here, so
    // renaming a property or changing the naming convention moves this statement with it instead
    // of leaving it to fail at runtime.
    private string RotateSql()
    {
        IEntityType entity = context.Model.FindEntityType(typeof(RefreshToken))!;
        StoreObjectIdentifier table = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)!.Value;
        string tableName = entity.GetSchemaQualifiedTableName()!;

        return $"""
                WITH consumed AS (
                    UPDATE {tableName}
                    SET {Column(nameof(RefreshToken.RevokedAtUtc))} = @now
                    WHERE {Column(nameof(RefreshToken.TokenHash))} = @presented
                      AND {Column(nameof(RefreshToken.RevokedAtUtc))} IS NULL
                      AND {Column(nameof(RefreshToken.ExpiresAtUtc))} > @now
                    RETURNING {Column(nameof(RefreshToken.UserId))}
                )
                INSERT INTO {tableName} (
                    {Column(nameof(RefreshToken.Id))},
                    {Column(nameof(RefreshToken.UserId))},
                    {Column(nameof(RefreshToken.TokenHash))},
                    {Column(nameof(RefreshToken.CreatedAtUtc))},
                    {Column(nameof(RefreshToken.ExpiresAtUtc))})
                SELECT @id, {Column(nameof(RefreshToken.UserId))}, @replacement, @now, @expires FROM consumed
                RETURNING {Column(nameof(RefreshToken.UserId))}
                """;

        string Column(string property) => entity.FindProperty(property)!.GetColumnName(table)!;
    }
}
