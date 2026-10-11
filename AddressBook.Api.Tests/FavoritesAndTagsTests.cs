using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class FavoritesAndTagsTests
{
    [Fact]
    public async Task Favorite_marks_the_contact_and_repeating_it_changes_nothing()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact());

        var first = await client.PutAsync($"/api/contacts/{contact.Id}/favorite", null);
        var second = await client.PutAsync($"/api/contacts/{contact.Id}/favorite", null);
        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.True(fetched!.IsFavorite);
    }

    [Fact]
    public async Task Unfavorite_clears_the_flag_and_repeating_it_changes_nothing()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact(isFavorite: true));

        var first = await client.DeleteAsync($"/api/contacts/{contact.Id}/favorite");
        var second = await client.DeleteAsync($"/api/contacts/{contact.Id}/favorite");
        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.False(fetched!.IsFavorite);
    }

    [Fact]
    public async Task Favorite_and_unfavorite_unknown_id_return_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        AssertProblem(await client.PutAsync("/api/contacts/999/favorite", null), HttpStatusCode.NotFound);
        AssertProblem(await client.DeleteAsync("/api/contacts/999/favorite"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tags_returns_distinct_tags_sorted_and_ignoring_case()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await client.CreateContactAsync(NewContact("A", "One", tags: ["Work", "Family"]));
        await client.CreateContactAsync(NewContact("B", "Two", tags: ["work", "Gym"]));

        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Equal(new[] { "Family", "Gym", "Work" }, tags);
    }

    [Fact]
    public async Task Tags_is_empty_when_there_are_no_contacts()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Empty(tags!);
    }
}
