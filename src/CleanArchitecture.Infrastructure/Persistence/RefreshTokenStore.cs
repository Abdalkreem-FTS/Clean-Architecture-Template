using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using Microsoft.EntityFrameworkCore;
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

        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Ids.New(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
        });

        await context.RefreshTokens
            .Where(token => token.UserId == userId && token.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> RotateAsync(
        string presentedTokenHash,
        string replacementTokenHash,
        DateTimeOffset replacementExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH consumed AS (
                UPDATE refresh_tokens
                SET revoked_at_utc = @now
                WHERE token_hash = @presented
                  AND revoked_at_utc IS NULL
                  AND expires_at_utc > @now
                RETURNING user_id
            )
            INSERT INTO refresh_tokens (id, user_id, token_hash, created_at_utc, expires_at_utc)
            SELECT @id, user_id, @replacement, @now, @expires FROM consumed
            RETURNING user_id
            """;

        DateTimeOffset now = clock.GetUtcNow();

        await using NpgsqlCommand command = new(sql);

        var parameters = new NpgsqlParameter[]
        {
            new("now", NpgsqlDbType.TimestampTz) { Value = now },
            new("presented", NpgsqlDbType.Varchar) { Value = presentedTokenHash },
            new("replacement", NpgsqlDbType.Varchar) { Value = replacementTokenHash },

            // The replacement row's primary key. Assigned here rather than by the database,
            // like every other id in the application.
            new("id", NpgsqlDbType.Uuid) { Value = Ids.New() },
            new("expires", NpgsqlDbType.TimestampTz) { Value = replacementExpiresAtUtc }
        };

        command.Parameters.AddRange(parameters);

        // Runs on the DbContext's own connection, so the command joins whatever connection
        // lifetime EF already has rather than opening a second one. A raw NpgsqlCommand has no
        // connection of its own — without this it throws "Connection property has not been
        // initialized" at execution time.
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
}
