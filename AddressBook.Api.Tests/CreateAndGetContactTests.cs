using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class CreateAndGetContactTests
{
    [Fact]
    public async Task Create_returns_201_with_location_and_the_new_contact()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts",
            NewContact("Alan", "Turing", "alan.turing@auning.com", ["Work", "Tag"], isFavorite: true, phone: "+1 555 123 4567"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ContactDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Alan", created.FirstName);
        Assert.Equal("Turing", created.LastName);
        Assert.Equal("alan.turing@auning.com", created.Email);
        Assert.Equal("+1 555 123 4567", created.Phone);
        Assert.Equal(new[] { "Work", "Tag" }, created.Tags);
        Assert.True(created.IsFavorite);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Equal($"/api/contacts/{created.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Location_uses_https_when_the_proxy_says_the_request_was_https()
    {
        // Render terminates TLS and forwards plain http with X-Forwarded-Proto: https.
        using var app = new TestApp(onRender: true);

        var response = await PostWithForwardedProtoAsync(app.CreateClient());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    [Fact]
    public async Task Forwarded_headers_are_ignored_when_not_running_on_render()
    {
        // Without Render's proxy in front (e.g. `docker run` locally), anyone could send these headers.
        using var app = new TestApp();

        var response = await PostWithForwardedProtoAsync(app.CreateClient());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("http", response.Headers.Location?.Scheme);
    }

    private static async Task<HttpResponseMessage> PostWithForwardedProtoAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/contacts")
        {
            Content = JsonContent.Create(NewContact()),
        };
        request.Headers.Add("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Get_returns_the_stored_contact_including_tags()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var created = await client.CreateContactAsync(NewContact("Marie", "Curie", tags: ["Family", "Science"]));

        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(created, fetched with { Tags = created.Tags }); // records compare by value
        Assert.Equal(new[] { "Family", "Science" }, fetched.Tags);   // tags round-trip through EF InMemory
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/contacts/12345");

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "Turing")]
    [InlineData("Alan", "")]
    [InlineData("   ", "Turing")]
    public async Task Create_without_first_or_last_name_returns_400(string firstName, string lastName)
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(firstName, lastName));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_invalid_email_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(email: "not-an-email"));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_too_long_tag_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(tags: [new string('x', 31)]));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_trims_text_and_normalizes_tags()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var created = await client.CreateContactAsync(
            NewContact("  Alan ", " Turing  ", tags: [" Work ", "work", "", "   ", "Family"], phone: "   "));

        Assert.Equal("Alan", created.FirstName);
        Assert.Equal("Turing", created.LastName);
        Assert.Null(created.Phone);
        Assert.Equal(new[] { "Work", "Family" }, created.Tags);
    }

    [Fact]
    public async Task Create_ignores_client_supplied_id_and_timestamps()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", new
        {
            id = 999,
            firstName = "Alan",
            lastName = "Turing",
            createdAt = "2000-01-01T00:00:00Z",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.NotEqual(999, created.Id);
        Assert.True(created.CreatedAt > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
