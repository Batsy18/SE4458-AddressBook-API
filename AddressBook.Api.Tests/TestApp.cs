using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AddressBook.Api.Tests;

/// <summary>
/// Starts the real API in memory. Every instance gets its own empty database,
/// so tests never see each other's data.
/// </summary>
public class TestApp(bool seed = false, Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": we want to test what Render (Production) will run.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Name", $"tests-{Guid.NewGuid()}");
        builder.UseSetting("Database:Seed", seed ? "true" : "false");

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
