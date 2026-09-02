namespace CleanArchitecture.Domain.Users;

public sealed class UserPage(IReadOnlyList<User> items, int totalCount)
{
    public IReadOnlyList<User> Items { get; } = items;

    public int TotalCount { get; } = totalCount;
}
