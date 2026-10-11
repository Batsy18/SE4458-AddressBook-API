using System.Reflection;
using AddressBook.Api.Data;
using AddressBook.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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

// Swagger is the only UI this API has, so it is enabled in every environment.
// (The template only enables it in Development, which would make /swagger return 404 on Render.)
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();
