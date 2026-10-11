using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AddressBook.Api.Tests;

/// <summary>
/// Starts the real API in memory. Every instance gets its own empty database,
/// so tests never see each other's data.
/// </summary>
public class TestApp(
    bool seed = false,
    Action<IServiceCollection>? configureServices = null,
    bool onRender = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": we want to test what Render (Production) will run.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Name", $"tests-{Guid.NewGuid()}");
        builder.UseSetting("Database:Seed", seed ? "true" : "false");

        // Render sets RENDER=true on its servers; the app only trusts proxy headers there.
        if (onRender)
        {
            builder.UseSetting("RENDER", "true");
        }

        // On Windows the host also logs to the Windows Event Log. Sandboxed runners (e.g. Codex)
        // may not be allowed to write there, which made the 500 test fail. Tests log to the console only.
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
