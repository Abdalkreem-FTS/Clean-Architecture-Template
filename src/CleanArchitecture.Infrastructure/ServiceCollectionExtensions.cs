using System.Text;
using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.Infrastructure.Identity;
using CleanArchitecture.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchitecture.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string ConnectionStringName = "Database";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        AddPersistence(services, connectionString);
        AddIdentity(services, configuration);
        AddAuthentication(services, configuration);

        services.AddScoped<AdministratorSeeder>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<IdentityOptions>(configuration.GetSection("Identity"));
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();

        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (jwt.SigningKey.Length < JwtOptions.MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} is required and must be at least "
                + $"{JwtOptions.MinimumSigningKeyLength} characters. Supply it from user-secrets, an "
                + "environment variable, or a secret store.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Off, so claims are read under the names they were issued with. The default
                // rewrites "sub" to a long ClaimTypes URI, which leaves the token saying one
                // thing and the code reading another.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.Zero,

                    // Tells ClaimsPrincipal.IsInRole — and therefore RequireRole — which claim
                    // carries the roles, now that it is no longer the ClaimTypes default.
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtOptions.RoleClaimType,
                };
            });

        services.AddAuthorization();
    }
}
