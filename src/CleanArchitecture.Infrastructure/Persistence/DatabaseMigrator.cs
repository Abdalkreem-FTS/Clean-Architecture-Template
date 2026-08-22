using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Identity;
using EvolveDb;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Infrastructure.Persistence;

public sealed class DatabaseMigrator(
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<SeedOptions> seedOptions,
    UserManager<ApplicationUser> users,
    TimeProvider clock,
    ILogger<DatabaseMigrator> logger)
{
    public const string MetadataTableName = "schema_changelog";

    private const string EmbeddedResourcePrefix = "CleanArchitecture.Infrastructure.Migrations";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Migrate();

        await SeedAdministratorAsync(cancellationToken);
    }

    private void Migrate()
    {
        using NpgsqlConnection connection = new(databaseOptions.Value.ConnectionString);

        var evolve = new Evolve(connection, message => logger.LogInformation("Evolve: {Message}", message))
        {
            EmbeddedResourceAssemblies = [typeof(DatabaseMigrator).Assembly],
            EmbeddedResourceFilters = [EmbeddedResourcePrefix],
            MetadataTableName = MetadataTableName,
            EnableClusterMode = true,
            IsEraseDisabled = true,
            MustEraseOnValidationError = false,
        };

        evolve.Migrate();

        logger.LogInformation("Database migration complete. {Applied} script(s) applied.", evolve.NbMigration);
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SeedOptions seed = seedOptions.Value;

        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            logger.LogInformation("No administrator seed configured. Skipping.");

            return;
        }

        if (await users.FindByEmailAsync(seed.AdminEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Ids.New(),
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            CreatedAtUtc = clock.GetUtcNow(),
        };

        IdentityResult created = await users.CreateAsync(admin, seed.AdminPassword);

        if (!created.Succeeded)
        {
            logger.LogError(
                "Could not seed the administrator: {Errors}",
                string.Join(" ", created.Errors.Select(error => error.Description)));

            return;
        }

        await users.AddToRoleAsync(admin, Roles.Admin);

        logger.LogInformation("Seeded administrator {Email}.", seed.AdminEmail);
    }
}

public static class DatabaseMigratorExtensions
{
    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().RunAsync(cancellationToken);
    }
}

public sealed class DatabaseOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}
