namespace CleanArchitecture.Api.IntegrationTests.Configuration;

internal static class SettingKeys
{
    public const string ConnectionString = "ConnectionStrings:Database";

    public const string JwtSigningKey = "Jwt:SigningKey";

    public const string JwtAccessTokenLifetime = "Jwt:AccessTokenLifetime";

    public const string SeedAdminEmail = "Seed:AdminEmail";

    public const string SeedAdminPassword = "Seed:AdminPassword";

    public const string LockoutWindow = "Identity:Lockout:DefaultLockoutTimeSpan";
}
