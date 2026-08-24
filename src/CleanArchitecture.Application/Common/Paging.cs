namespace CleanArchitecture.Application.Common;

public sealed record PageQuery
{
    public const int DefaultSize = 20;
    public const int MaxSize = 100;

    private PageQuery(int number, int size)
    {
        Number = number;
        Size = size;
    }

    public int Number { get; }

    public int Size { get; }

    public int Skip => (Number - 1) * Size;

    public static PageQuery Of(int? number, int? size) =>
        new(Math.Max(number ?? 1, 1), Math.Clamp(size ?? DefaultSize, 1, MaxSize));
}

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
