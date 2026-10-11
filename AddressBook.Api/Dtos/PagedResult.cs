namespace AddressBook.Api.Dtos;

/// <summary>One page of results plus the numbers a client needs for "Showing 1–4 of 24".</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
