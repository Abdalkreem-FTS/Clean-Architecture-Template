using CleanArchitecture.Domain.Common;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Persistence;

// Creating the first administrator cannot go through the API, since granting the admin role
// needs an admin. Leave Seed:AdminPassword empty to skip it.
public sealed class AdministratorSeeder(
    IOptions<SeedOptions> seedOptions,
    UserManager<ApplicationUser> users,
    TimeProvider clock,
    ILogger<AdministratorSeeder> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
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

public static class AdministratorSeederExtensions
{
    public static async Task SeedAdministratorAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<AdministratorSeeder>().RunAsync(cancellationToken);
    }
}
