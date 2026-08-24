namespace CleanArchitecture.Api.IntegrationTests.Configuration;

internal static class SettingKeys
{
    public const string ConnectionString = "ConnectionStrings:Database";

    public const string JwtIssuer = "Jwt:Issuer";

    public const string JwtAudience = "Jwt:Audience";

    public const string JwtSigningKey = "Jwt:SigningKey";

    public const string JwtAccessTokenLifetime = "Jwt:AccessTokenLifetime";

    public const string JwtRefreshTokenLifetime = "Jwt:RefreshTokenLifetime";

    public const string SeedAdminEmail = "Seed:AdminEmail";

    public const string SeedAdminPassword = "Seed:AdminPassword";
}
