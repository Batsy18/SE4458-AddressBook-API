using System.Net.Http.Json;
using AddressBook.Api.Dtos;

namespace AddressBook.Api.Tests;

public static class TestHelpers
{
    public static CreateContactDto NewContact(
        string firstName = "Alan",
        string lastName = "Turing",
        string? email = null,
        string[]? tags = null,
        bool isFavorite = false,
        string? phone = null) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Phone = phone,
        Tags = tags?.ToList(),
        IsFavorite = isFavorite,
    };

    public static async Task<ContactDto> CreateContactAsync(this HttpClient client, CreateContactDto dto)
    {
        var response = await client.PostAsJsonAsync("/api/contacts", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContactDto>())!;
    }

    public static void AssertProblem(HttpResponseMessage response, System.Net.HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
