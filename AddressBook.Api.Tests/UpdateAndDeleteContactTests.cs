using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class UpdateAndDeleteContactTests
{
    [Fact]
    public async Task Put_replaces_every_editable_field()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(
            NewContact("Alan", "Turing", "alan@x.com", ["Work"], isFavorite: true, phone: "123"));

        var response = await client.PutAsJsonAsync($"/api/contacts/{original.Id}",
            new UpdateContactDto { FirstName = "Ada", LastName = "Lovelace", Tags = ["Family"] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("Ada", updated.FirstName);
        Assert.Equal("Lovelace", updated.LastName);
        Assert.Null(updated.Email);       // omitted in PUT => cleared
        Assert.Null(updated.Phone);
        Assert.False(updated.IsFavorite);
        Assert.Equal(new[] { "Family" }, updated.Tags);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= original.UpdatedAt);
    }

    [Fact]
    public async Task Put_unknown_id_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PutAsJsonAsync("/api/contacts/999",
            new UpdateContactDto { FirstName = "Ada", LastName = "Lovelace" });

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_with_invalid_body_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(NewContact());

        var response = await client.PutAsJsonAsync($"/api/contacts/{original.Id}",
            new UpdateContactDto { FirstName = "", LastName = "Lovelace" });

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_changes_only_the_fields_that_were_sent()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(
            NewContact("Alan", "Turing", "alan@x.com", ["Work"], isFavorite: true));

        var response = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { phone = "+90 555 000 0000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patched = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.Equal("+90 555 000 0000", patched.Phone);
        Assert.Equal("Alan", patched.FirstName);
        Assert.Equal("Turing", patched.LastName);
        Assert.Equal("alan@x.com", patched.Email);
        Assert.Equal(new[] { "Work" }, patched.Tags);
        Assert.True(patched.IsFavorite);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Patch_with_blank_name_returns_400(string blank)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(NewContact());

        var first = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { firstName = blank });
        var last = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { lastName = blank });

        AssertProblem(first, HttpStatusCode.BadRequest);
        AssertProblem(last, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_unknown_id_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PatchAsJsonAsync("/api/contacts/999", new { phone = "1" });

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_contact_and_a_second_delete_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact());

        var first = await client.DeleteAsync($"/api/contacts/{contact.Id}");
        var get = await client.GetAsync($"/api/contacts/{contact.Id}");
        var second = await client.DeleteAsync($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_removes_existing_ids_and_ignores_unknown_and_duplicate_ids()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var a = await client.CreateContactAsync(NewContact("A", "One"));
        var b = await client.CreateContactAsync(NewContact("B", "Two"));
        var c = await client.CreateContactAsync(NewContact("C", "Three"));

        var response = await client.PostAsJsonAsync("/api/contacts/bulk-delete",
            new BulkDeleteDto { Ids = [a.Id, b.Id, b.Id, 9999] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BulkDeleteResult>())!;
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/contacts/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/contacts/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/contacts/{c.Id}")).StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_with_no_ids_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts/bulk-delete", new BulkDeleteDto { Ids = [] });

        AssertProblem(response, HttpStatusCode.BadRequest);
    }
}
