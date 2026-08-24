
using System;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumSigningKeyLength = 32;

    // Short on purpose. ClaimTypes.Role is a ~60-byte URI, and it is repeated once per role in
    // every access token the API issues.
    public const string RoleClaimType = "role";

    public string Issuer { get; set; } = "cleanarchitecture-api";

    public string Audience { get; set; } = "cleanarchitecture-client";

    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}
