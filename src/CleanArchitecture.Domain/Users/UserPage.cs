namespace CleanArchitecture.Domain.Users;

public sealed class UserPage
{
    public UserPage(IReadOnlyList<User> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }

    public IReadOnlyList<User> Items { get; }

    public int TotalCount { get; }
}
