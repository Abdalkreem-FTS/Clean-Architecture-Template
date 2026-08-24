namespace CleanArchitecture.Api.Contracts;

// The JSON shape of a page. Application hands back Paged<T>; this is what goes on the wire.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
