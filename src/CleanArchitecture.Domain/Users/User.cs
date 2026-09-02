using CleanArchitecture.Domain.Common;

namespace CleanArchitecture.Domain.Users;

public sealed class User
{
    private readonly List<string> _roles = [];

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = null!;

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public IReadOnlyList<string> Roles => _roles;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public static User Register(string email, string firstName, string lastName, DateTimeOffset nowUtc) =>
        new()
        {
            Id = Ids.New(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = nowUtc,
        };

    // Rebuilds a user already in storage. Roles are passed in rather than looked up here,
    // since nothing about this type can answer that question itself.
    public static User FromStorage(
        Guid id,
        string email,
        string firstName,
        string lastName,
        IEnumerable<string> roles,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? lastLoginAtUtc)
    {
        var user = new User
        {
            Id = id,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedAtUtc = createdAtUtc,
            LastLoginAtUtc = lastLoginAtUtc,
        };

        user._roles.AddRange(roles);

        return user;
    }

    public void RecordLogin(DateTimeOffset atUtc) => LastLoginAtUtc = atUtc;
}
