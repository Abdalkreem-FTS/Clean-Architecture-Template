
using System;

namespace CleanArchitecture.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "cleanarchitecture-api";

    public string Audience { get; set; } = "cleanarchitecture-client";

    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}
