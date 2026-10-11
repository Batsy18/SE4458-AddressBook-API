using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class ListContactsTests
{
    // Created in this order, so Ids (and CreatedAt) increase down the list.
    private static async Task SeedAsync(HttpClient client)
    {
        await client.CreateContactAsync(NewContact("Alan", "Turing", "alan.turing@auning.com", ["Work"], isFavorite: true));
        await client.CreateContactAsync(NewContact("Marie", "Curie", "marie.curie@mail.com", ["Family"]));
        await client.CreateContactAsync(NewContact("Katherine", "Johnson", "katherine.johnson@abcc.com", ["Phone"], isFavorite: true));
        await client.CreateContactAsync(NewContact("Charles", "Darwin", "charles.darwin@adl.gov", ["Tag"]));
        await client.CreateContactAsync(NewContact("Çağan", "Koyunseven", "c.koyunseven@example.com", ["Work", "Family"]));
        await client.CreateContactAsync(NewContact("İsmail", "Yılmaz", null, ["Gym"]));
    }

    private static async Task<PagedResult<ContactDto>> ListAsync(HttpClient client, string queryString = "")
    {
        var response = await client.GetAsync($"/api/contacts{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>())!;
    }

    private static string[] LastNames(PagedResult<ContactDto> page) =>
        page.Items.Select(c => c.LastName).ToArray();

    [Fact]
    public async Task Default_list_is_first_page_of_10_sorted_by_last_name()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client);

        Assert.Equal(new[] { "Curie", "Darwin", "Johnson", "Koyunseven", "Turing", "Yılmaz" }, LastNames(page));
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Empty_address_book_returns_empty_page()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var page = await ListAsync(client);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Theory]
    [InlineData("marie", "Curie")]                  // first name
    [InlineData("CURIE", "Curie")]                  // last name, any case
    [InlineData("marie cur", "Curie")]              // full name
    [InlineData("abcc.com", "Johnson")]             // email
    [InlineData("family", "Curie,Koyunseven")]      // tag
    [InlineData("  marie  ", "Curie")]              // surrounding spaces ignored
    public async Task Search_matches_name_email_or_tag(string search, string expected)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, $"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(expected.Split(','), LastNames(page));
    }

    [Theory]
    [InlineData("cagan", "Koyunseven")]
    [InlineData("ismail", "Yılmaz")]
    [InlineData("İSMAİL", "Yılmaz")]
    public async Task Search_ignores_case_and_turkish_accents(string search, string expected)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, $"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(new[] { expected }, LastNames(page));
    }

    [Fact]
    public async Task Tag_filter_is_an_exact_case_insensitive_match()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        Assert.Equal(new[] { "Koyunseven", "Turing" }, LastNames(await ListAsync(client, "?tag=work")));
        Assert.Empty((await ListAsync(client, "?tag=wor")).Items);
    }

    [Fact]
    public async Task Favorite_filter_works_both_ways_and_combines_with_tag()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        Assert.Equal(new[] { "Johnson", "Turing" }, LastNames(await ListAsync(client, "?favorite=true")));
        Assert.Equal(4, (await ListAsync(client, "?favorite=false")).TotalCount);
        Assert.Equal(new[] { "Turing" }, LastNames(await ListAsync(client, "?tag=work&favorite=true")));
    }

    [Fact]
    public async Task Sort_by_first_name_in_both_directions()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var asc = await ListAsync(client, "?sortBy=firstName");
        var desc = await ListAsync(client, "?sortBy=FIRSTNAME&sortOrder=DESC");

        Assert.Equal("Alan", asc.Items[0].FirstName);
        Assert.Equal("Marie", desc.Items[0].FirstName);
    }

    [Fact]
    public async Task Sort_by_last_name_descending()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, "?sortBy=lastName&sortOrder=desc");

        Assert.Equal(new[] { "Yılmaz", "Turing", "Koyunseven", "Johnson", "Darwin", "Curie" }, LastNames(page));
    }

    [Fact]
    public async Task Ties_are_ordered_by_id_in_the_sort_direction()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var first = await client.CreateContactAsync(NewContact("Anna", "Smith"));
        var second = await client.CreateContactAsync(NewContact("Bob", "Smith"));
        var third = await client.CreateContactAsync(NewContact("Cem", "Smith"));

        var asc = await ListAsync(client, "?sortBy=lastName");
        var desc = await ListAsync(client, "?sortBy=lastName&sortOrder=desc");

        Assert.Equal(new[] { first.Id, second.Id, third.Id }, asc.Items.Select(c => c.Id));
        Assert.Equal(new[] { third.Id, second.Id, first.Id }, desc.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task Sort_by_email_puts_missing_emails_last()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var asc = await ListAsync(client, "?sortBy=email");
        var desc = await ListAsync(client, "?sortBy=email&sortOrder=desc");

        Assert.Equal("Turing", asc.Items[0].LastName);   // alan.turing@...
        Assert.Equal("Yılmaz", asc.Items[^1].LastName);  // no email
        Assert.Equal("Curie", desc.Items[0].LastName);   // marie.curie@...
        Assert.Equal("Yılmaz", desc.Items[^1].LastName); // still last
    }

    [Fact]
    public async Task Sort_by_created_at_descending_shows_newest_first()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, "?sortBy=createdAt&sortOrder=desc");

        Assert.Equal("Yılmaz", page.Items[0].LastName);
        Assert.Equal("Turing", page.Items[^1].LastName);
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_metadata()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page2 = await ListAsync(client, "?pageSize=4&page=2");

        Assert.Equal(new[] { "Turing", "Yılmaz" }, LastNames(page2));
        Assert.Equal(2, page2.Page);
        Assert.Equal(4, page2.PageSize);
        Assert.Equal(6, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
    }

    [Theory]
    [InlineData("?pageSize=4&page=3")]
    [InlineData("?page=2147483647")]
    public async Task Page_far_past_the_end_returns_empty_items(string queryString)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, queryString);

        Assert.Empty(page.Items);
        Assert.Equal(6, page.TotalCount);
    }

    [Theory]
    [InlineData("?sortBy=age", "sortBy")]
    [InlineData("?sortOrder=up", "sortOrder")]
    [InlineData("?page=0", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?favorite=maybe", "favorite")]
    public async Task Invalid_query_values_return_400(string queryString, string field)
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.GetAsync($"/api/contacts{queryString}");

        AssertValidationError(response, field);
    }
}
