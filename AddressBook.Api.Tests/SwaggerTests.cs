using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AddressBook.Api.Tests;

public class SwaggerTests
{
    [Fact]
    public async Task Swagger_document_and_ui_are_served_outside_development()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var doc = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
        Assert.Contains("Address Book API", await doc.Content.ReadAsStringAsync());

        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task Root_redirects_to_swagger()
    {
        using var app = new TestApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.OriginalString);
    }
}
