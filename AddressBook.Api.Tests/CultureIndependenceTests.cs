using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

/// <summary>Changes the process-wide culture, so these tests must not run alongside others.</summary>
[CollectionDefinition(nameof(CultureIndependenceTests), DisableParallelization = true)]
public class CultureCollection;

/// <summary>
/// Search, sorting and validation must give the same results whatever the server's culture is.
/// In Turkish, the lowercase of "I" is "ı" (not "i") and the uppercase of "i" is "İ", which
/// breaks naive ToLower()/regex comparisons. Without these tests, the other tests would only
/// cover the culture of whichever machine runs them.
/// </summary>
[Collection(nameof(CultureIndependenceTests))]
public class CultureIndependenceTests
{
    [Theory]
    [InlineData("?search=ismail")]
    [InlineData("?search=İSMAİL")]
    [InlineData("?search=cagan")]
    [InlineData("?search=irmak")]
    [InlineData("?search=IŞIK")]
    [InlineData("?search=isik")]
    [InlineData("?sortBy=FIRSTNAME")]
    [InlineData("?sortBy=firstName&sortOrder=DESC")]
    [InlineData("?sortBy=lastName")]
    [InlineData("?tag=WORK")]
    public async Task Results_are_the_same_in_turkish_and_english_cultures(string queryString)
    {
        var turkish = await ListUnderCultureAsync("tr-TR", queryString);
        var english = await ListUnderCultureAsync("en-US", queryString);

        Assert.NotEmpty(english);
        Assert.Equal(english, turkish);
    }

    private static async Task<string[]> ListUnderCultureAsync(string cultureName, string queryString)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture,
            CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);

        // Set both the current culture and the default for new threads, so the in-process
        // test server sees it no matter which thread handles the request.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        try
        {
            using var app = new TestApp();
            var client = app.CreateClient();
            await client.CreateContactAsync(NewContact("İsmail", "Yılmaz", tags: ["Work"]));
            await client.CreateContactAsync(NewContact("Çağan", "Koyunseven", tags: ["work"]));
            await client.CreateContactAsync(NewContact("Irmak", "Işık"));
            await client.CreateContactAsync(NewContact("ılgın", "İnce"));
            await client.CreateContactAsync(NewContact("Ivan", "Ivanov"));
            await client.CreateContactAsync(NewContact("ida", "ibsen"));

            var response = await client.GetAsync($"/api/contacts{queryString}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = (await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>())!;
            return page.Items.Select(c => $"{c.FirstName} {c.LastName}").ToArray();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture,
                CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture) = saved;
        }
    }
}
