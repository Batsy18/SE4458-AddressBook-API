using System.Net.Http.Json;
using System.Text.Json;
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

    /// <summary>Checks the status code, the ProblemDetails content type, and that the body really is ProblemDetails.</summary>
    public static void AssertProblem(HttpResponseMessage response, System.Net.HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = ReadJson(response);
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
    }

    /// <summary>Checks for a 400 ValidationProblemDetails whose "errors" mention <paramref name="field"/>.</summary>
    public static void AssertValidationError(HttpResponseMessage response, string field)
    {
        AssertProblem(response, System.Net.HttpStatusCode.BadRequest);
        using var body = ReadJson(response);
        var keys = body.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).ToList();
        Assert.True(
            keys.Any(k => k.Equals(field, StringComparison.OrdinalIgnoreCase) ||
                          k.EndsWith("." + field, StringComparison.OrdinalIgnoreCase)),
            $"Expected a validation error for '{field}', got: {string.Join(", ", keys)}");
    }

    // The test server buffers responses, so reading synchronously here is safe.
    private static JsonDocument ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
}
