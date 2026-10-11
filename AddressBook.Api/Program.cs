using System.Reflection;
using AddressBook.Api.Data;
using AddressBook.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Render tells the app which port to listen on through the PORT environment variable.
// Without it, the default applies (8080 in the Docker image, launchSettings.json locally).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Render terminates HTTPS at its proxy and forwards plain http with X-Forwarded-Proto.
// Trust those headers so generated links (e.g. the Location header) use https.
// The proxy's address isn't fixed, so the known-proxy lists are cleared. That is only safe when
// Render's proxy is the sole way in, so the middleware is enabled only on Render (see below).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// The database name is read when the context is created (not at startup),
// so tests can give every test app its own database via "Database:Name".
builder.Services.AddDbContext<AddressBookContext>((services, options) =>
    options.UseInMemoryDatabase(
        services.GetRequiredService<IConfiguration>()["Database:Name"] ?? "AddressBook"));
builder.Services.AddScoped<IContactService, ContactService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Address Book API",
        Version = "v1",
        Description = "SE 4458 Assignment 2 (Group 1): create, edit, update, delete, search, sort " +
                      "and favorite contacts. Data is kept in an in-memory database."
    });

    // Shows the /// comments from controllers and DTOs in Swagger.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:Seed", true))
{
    using var scope = app.Services.CreateScope();
    SeedData.Initialize(scope.ServiceProvider.GetRequiredService<AddressBookContext>());
}

// Render sets RENDER=true on its servers. Elsewhere (e.g. `docker run` locally) any client could
// send X-Forwarded-* headers, so they are not trusted. Must run first so later middleware sees https.
if (app.Configuration.GetValue("RENDER", false))
{
    app.UseForwardedHeaders();
}

// Unhandled exceptions become a 500 ProblemDetails with no stack trace.
app.UseExceptionHandler();
// Empty error responses (e.g. an unknown route) get a ProblemDetails body too.
app.UseStatusCodePages();

// Swagger is the only UI this API has, so it is enabled in every environment.
// (The template only enables it in Development, which would make /swagger return 404 on Render.)
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();
