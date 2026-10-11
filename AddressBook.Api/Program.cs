using System.Reflection;
using AddressBook.Api.Data;
using AddressBook.Api.Services;
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
