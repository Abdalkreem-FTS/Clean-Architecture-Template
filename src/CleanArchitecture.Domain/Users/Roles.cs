namespace CleanArchitecture.Domain.Users;

public static class Roles
{
    public const string Admin = "admin";

    public const string User = "user";

    public static IReadOnlyList<string> All { get; } = [Admin, User];

    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);
}
