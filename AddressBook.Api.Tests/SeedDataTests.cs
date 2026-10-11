using System.Net.Http.Json;
using AddressBook.Api.Data;
using AddressBook.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task App_starts_with_24_sample_contacts_when_seeding_is_on()
    {
        using var app = new TestApp(seed: true);
        var client = app.CreateClient();

        var all = await client.GetFromJsonAsync<PagedResult<ContactDto>>("/api/contacts?pageSize=100");
        var favorites = await client.GetFromJsonAsync<PagedResult<ContactDto>>("/api/contacts?favorite=true");
        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Equal(24, all!.TotalCount);
        Assert.Equal(6, favorites!.TotalCount);
        Assert.NotEmpty(tags!);
    }

    [Fact]
    public void Initialize_does_nothing_when_contacts_already_exist()
    {
        var options = new DbContextOptionsBuilder<AddressBookContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
            .Options;
        using var db = new AddressBookContext(options);

        SeedData.Initialize(db);
        SeedData.Initialize(db);

        Assert.Equal(24, db.Contacts.Count());
    }
}
