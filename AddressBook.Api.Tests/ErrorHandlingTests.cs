using System.Net;
using AddressBook.Api.Dtos;
using AddressBook.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class ErrorHandlingTests
{
    /// <summary>A service that always fails, to prove the API hides exception details.</summary>
    private sealed class ThrowingContactService : IContactService
    {
        private static Exception Boom() => new InvalidOperationException("Boom: secret internal detail");

        public Task<PagedResult<ContactDto>> ListAsync(ContactQuery query) => throw Boom();
        public Task<ContactDto?> GetAsync(int id) => throw Boom();
        public Task<ContactDto> CreateAsync(CreateContactDto dto) => throw Boom();
        public Task<ContactDto?> UpdateAsync(int id, UpdateContactDto dto) => throw Boom();
        public Task<ContactDto?> PatchAsync(int id, PatchContactDto dto) => throw Boom();
        public Task<bool> DeleteAsync(int id) => throw Boom();
        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids) => throw Boom();
        public Task<bool> SetFavoriteAsync(int id, bool isFavorite) => throw Boom();
        public Task<IReadOnlyList<string>> GetTagsAsync() => throw Boom();
    }

    [Fact]
    public async Task Unhandled_exception_returns_500_problem_without_details()
    {
        using var app = new TestApp(configureServices: services =>
            services.AddScoped<IContactService, ThrowingContactService>());
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/contacts/1");

        AssertProblem(response, HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Boom", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        AssertProblem(response, HttpStatusCode.NotFound);
    }
}
