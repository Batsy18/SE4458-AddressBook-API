# Address Book API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy a public ASP.NET Core (.NET 10) REST API for an address book. It can
create, edit, update, delete, search, sort and favorite contacts, keeps its data in EF Core
InMemory, and is used through a live Swagger page on Render.

**Architecture:** Controllers handle HTTP only and delegate to `IContactService`/`ContactService`,
which holds all business logic and talks to `AddressBookContext` (EF Core InMemory). DTOs separate
the API contract from the `Contact` entity. List queries load the (small) in-memory table and then
filter, sort and page in .NET with invariant-culture comparison, so results don't depend on the
machine's language settings.

**Tech Stack:** .NET 10 SDK, ASP.NET Core Web API (controllers), EF Core InMemory, Swashbuckle.AspNetCore, xUnit + Microsoft.AspNetCore.Mvc.Testing, Docker, Render (free web service), Git + GitHub Desktop.

**Spec:** `docs/superpowers/specs/2026-10-11-address-book-api-design.md`

## Global Constraints

- Target framework `net10.0` for both projects. The .NET 10.0.401 SDK is installed.
- Data lives only in EF Core InMemory, database name `AddressBook` by default (config key `Database:Name`).
- Swagger UI at `/swagger` must work in **every** environment, including Production. `/` redirects to `/swagger`.
- Routes are under `/api`: `/api/contacts…` and `/api/tags`.
- Contact limits:
  - FirstName and LastName are required, max 50
  - Email is optional, valid format, max 100
  - Phone is optional, max 30
  - Tags: at most 20, each at most 30 characters
- Tags are trimmed. Blank tags are dropped and duplicates removed, ignoring case.
- List defaults: `sortBy=lastName`, `sortOrder=asc`, `page=1`, `pageSize=10`. `pageSize` must be 1–100 and `page` at least 1.
- `sortBy` must be one of `firstName`, `lastName`, `email`, `createdAt`. `sortOrder` must be `asc` or `desc`.
- Errors are returned as ProblemDetails (`application/problem+json`). A 500 response never includes exception details.
- The app listens on `$PORT` when that variable is set (Render uses 10000), otherwise on the image default, 8080.
- No HTTPS redirection, because Render terminates TLS.
- **Commits must NOT contain a `Co-Authored-By` trailer or any AI attribution.**
- Every task ends with a **Codex checkpoint**:
  1. The user pushes with GitHub Desktop (**Push origin**).
  2. Codex reviews the task's commits, either in the Codex CLI or via `/codex:rescue` from Claude Code, using this prompt:
     > Review the last commit(s) of this repo (`git log -p -n <k>`) against `docs/superpowers/specs/2026-10-11-address-book-api-design.md` and Task N of `docs/superpowers/plans/2026-10-11-address-book-api.md`. Report bugs, spec mismatches, missing tests and risky code, ordered by severity, each with file:line. Don't rewrite the code.
  3. Claude evaluates each finding (superpowers:receiving-code-review). Valid ones get a test and a fix, committed as `fix: address Codex review (Task N)`. Rejected ones are explained to the user.
- Each task has a **"You learn"** line. Explain those concepts to the user while doing the task.

## Review Focus

1. **Turkish characters in search and sort.** On a `tr-TR` machine, `"I".ToLower()` is `"ı"`. Searching `ismail` must find `İsmail` and `cagan` must find `Çağan`. Pinned in Task 5 (`Search_ignores_case_and_turkish_accents`).
2. **Very large `page` values.** `page=2147483647` must return an empty page, not wrap around to the first page through integer overflow. Pinned in Task 5 (`Page_far_past_the_end_returns_empty_items`).
3. **Blank names through PATCH.** `{"firstName": "   "}` or `""` must return 400, not store an empty name. Pinned in Task 3 (`Patch_with_blank_name_returns_400`).
4. **Contacts without email when sorting by email.** They must come last in both directions. Pinned in Task 5 (`Sort_by_email_puts_missing_emails_last`).
5. **Over-posting.** A client that sends `"id": 999` or `"createdAt"` in a POST body must not be able to set them. Pinned in Task 2 (`Create_ignores_client_supplied_id_and_timestamps`).

---

### Task 0 (user): Re-publish the repo

- [ ] Delete the old repo on GitHub: *Settings → Danger Zone → Delete this repository*.
- [ ] Locally the `origin` remote is already removed, so GitHub Desktop now offers **Publish repository**. Publish it as **public**, named `SE4458-AddressBook-API`.
- [ ] Check on GitHub: 2 commits, no co-author shown.

---

### Task 1: Solution scaffold and Swagger in every environment

**You learn:** `dotnet new` templates, `.slnx` solutions, `Program.cs` (the hosting model and the middleware pipeline), why the template only shows Swagger in Development, XML doc comments, and `WebApplicationFactory` integration tests.

**Files:**
- Create: `AddressBook.slnx`
- Create/overwrite: `AddressBook.Api/AddressBook.Api.csproj`, `AddressBook.Api/Program.cs` (replaces the empty file), `AddressBook.Api/appsettings*.json`, `AddressBook.Api/Properties/launchSettings.json` (all from the template)
- Delete: `AddressBook.Api/Controllers/WeatherForecastController.cs`, `AddressBook.Api/WeatherForecast.cs`, `AddressBook.Api/AddressBook.Api.http`, `AddressBook.Api.Tests/UnitTest1.cs`
- Create: `AddressBook.Api.Tests/AddressBook.Api.Tests.csproj`, `AddressBook.Api.Tests/TestApp.cs`, `AddressBook.Api.Tests/SwaggerTests.cs`

**Interfaces:**
- Produces: `TestApp(bool seed = false, Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>`. It gives every test app its own empty InMemory database (`Database:Name` = random GUID) and turns seeding off (`Database:Seed=false`). Its environment is `Testing`, so non-Development behaviour is what gets tested.

- [ ] **Step 1: Scaffold the projects**

Run from the repo root (`C:\Users\mbdem\SE_4458_API_ASSIGNMENT`):
```bash
dotnet new webapi --use-controllers -n AddressBook.Api -o AddressBook.Api --force
dotnet new xunit -n AddressBook.Api.Tests -o AddressBook.Api.Tests
dotnet new sln -n AddressBook
dotnet sln add AddressBook.Api/AddressBook.Api.csproj AddressBook.Api.Tests/AddressBook.Api.Tests.csproj
dotnet add AddressBook.Api.Tests reference AddressBook.Api
dotnet add AddressBook.Api package Swashbuckle.AspNetCore
dotnet remove AddressBook.Api package Microsoft.AspNetCore.OpenApi
dotnet add AddressBook.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
rm AddressBook.Api/Controllers/WeatherForecastController.cs AddressBook.Api/WeatherForecast.cs AddressBook.Api/AddressBook.Api.http AddressBook.Api.Tests/UnitTest1.cs
```
- `dotnet new sln` creates `AddressBook.slnx`, the new XML solution format in .NET 10.
- We remove `Microsoft.AspNetCore.OpenApi` because Swashbuckle generates the OpenAPI document instead. Keeping both would produce two documents.

- [ ] **Step 2: Turn on the XML documentation file**

In `AddressBook.Api/AddressBook.Api.csproj`, add these inside the existing `<PropertyGroup>`:
```xml
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- Not every public member needs a doc comment; don't warn about missing ones. -->
    <NoWarn>$(NoWarn);1591</NoWarn>
```

- [ ] **Step 3: Write the test host and the failing Swagger tests**

`AddressBook.Api.Tests/TestApp.cs`:
```csharp
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
```

`AddressBook.Api.Tests/SwaggerTests.cs`:
```csharp
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
```

- [ ] **Step 4: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~SwaggerTests"`
Expected: FAIL. The template's `Program.cs` has no Swagger, so the requests return 404 instead of 200 or 302.

If instead the build fails with "`Program` is inaccessible", add `public partial class Program { }` as the last line of `Program.cs`. .NET 10 normally makes the generated `Program` class public.

- [ ] **Step 5: Replace `Program.cs`**

`AddressBook.Api/Program.cs`:
```csharp
using System.Reflection;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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
```
If the compiler can't find `OpenApiInfo`, the installed Swashbuckle is older than v10. Replace `using Microsoft.OpenApi;` with `using Microsoft.OpenApi.Models;`.

- [ ] **Step 6: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS (2 tests).

- [ ] **Step 7: Look at it yourself**

Run: `dotnet run --project AddressBook.Api`, then open the `http://localhost:<port>` URL it prints. You should land on Swagger UI (no endpoints yet). Stop it with Ctrl+C.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: scaffold .NET 10 Web API and test project with Swagger in all environments"
```

- [ ] **Step 9: Codex checkpoint** (see Global Constraints)

---

### Task 2: Create and get a contact

**You learn:** entities vs DTOs (over-posting), data annotations and automatic 400s from `[ApiController]`, `IValidatableObject`, `DbContext` with the InMemory provider, scoped dependency injection, and `CreatedAtAction` returning 201 with a `Location` header.

**Files:**
- Create: `AddressBook.Api/Models/Contact.cs`
- Create: `AddressBook.Api/Data/AddressBookContext.cs`
- Create: `AddressBook.Api/Common/TagRules.cs`
- Create: `AddressBook.Api/Dtos/ContactDto.cs`, `AddressBook.Api/Dtos/CreateContactDto.cs`
- Create: `AddressBook.Api/Services/IContactService.cs`, `AddressBook.Api/Services/ContactService.cs`
- Create: `AddressBook.Api/Controllers/ContactsController.cs`
- Modify: `AddressBook.Api/Program.cs` (register the DbContext and service)
- Create: `AddressBook.Api.Tests/TestHelpers.cs`, `AddressBook.Api.Tests/CreateAndGetContactTests.cs`

**Interfaces:**
- Consumes: `TestApp` (Task 1).
- Produces:
  - `Contact` (entity: `Id`, `FirstName`, `LastName`, `Email?`, `Phone?`, `List<string> Tags`, `IsFavorite`, `CreatedAt`, `UpdatedAt`)
  - `AddressBookContext.Contacts`
  - `record ContactDto(int Id, string FirstName, string LastName, string? Email, string? Phone, IReadOnlyList<string> Tags, bool IsFavorite, DateTime CreatedAt, DateTime UpdatedAt)`
  - `class CreateContactDto`
  - `TagRules.Validate(IEnumerable<string?>?)` and `TagRules.Normalize(IEnumerable<string?>?) : List<string>`
  - `IContactService.CreateAsync(CreateContactDto) : Task<ContactDto>` and `GetAsync(int) : Task<ContactDto?>`
  - Private helpers in `ContactService`: `ToDto(Contact)`, `Clean(string?)`
  - Test helpers: `TestHelpers.NewContact(...)` and `HttpClient.CreateContactAsync(CreateContactDto)`

- [ ] **Step 1: Add the EF Core InMemory package**

```bash
dotnet add AddressBook.Api package Microsoft.EntityFrameworkCore.InMemory
```

- [ ] **Step 2: Write the test helpers and the failing tests**

`AddressBook.Api.Tests/TestHelpers.cs`:
```csharp
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
```

`AddressBook.Api.Tests/CreateAndGetContactTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class CreateAndGetContactTests
{
    [Fact]
    public async Task Create_returns_201_with_location_and_the_new_contact()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts",
            NewContact("Alan", "Turing", "alan.turing@auning.com", ["Work", "Tag"], isFavorite: true, phone: "+1 555 123 4567"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ContactDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Alan", created.FirstName);
        Assert.Equal("Turing", created.LastName);
        Assert.Equal("alan.turing@auning.com", created.Email);
        Assert.Equal("+1 555 123 4567", created.Phone);
        Assert.Equal(new[] { "Work", "Tag" }, created.Tags);
        Assert.True(created.IsFavorite);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Equal($"/api/contacts/{created.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Get_returns_the_stored_contact_including_tags()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var created = await client.CreateContactAsync(NewContact("Marie", "Curie", tags: ["Family", "Science"]));

        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(created, fetched with { Tags = created.Tags }); // records compare by value
        Assert.Equal(new[] { "Family", "Science" }, fetched.Tags);   // tags round-trip through EF InMemory
    }

    [Fact]
    public async Task Get_unknown_id_returns_404_problem()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/contacts/12345");

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "Turing")]
    [InlineData("Alan", "")]
    [InlineData("   ", "Turing")]
    public async Task Create_without_first_or_last_name_returns_400(string firstName, string lastName)
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(firstName, lastName));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_invalid_email_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(email: "not-an-email"));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_too_long_tag_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", NewContact(tags: [new string('x', 31)]));

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_trims_text_and_normalizes_tags()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var created = await client.CreateContactAsync(
            NewContact("  Alan ", " Turing  ", tags: [" Work ", "work", "", "   ", "Family"], phone: "   "));

        Assert.Equal("Alan", created.FirstName);
        Assert.Equal("Turing", created.LastName);
        Assert.Null(created.Phone);
        Assert.Equal(new[] { "Work", "Family" }, created.Tags);
    }

    [Fact]
    public async Task Create_ignores_client_supplied_id_and_timestamps()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts", new
        {
            id = 999,
            firstName = "Alan",
            lastName = "Turing",
            createdAt = "2000-01-01T00:00:00Z",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.NotEqual(999, created.Id);
        Assert.True(created.CreatedAt > new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
```

- [ ] **Step 3: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~CreateAndGetContactTests"`
Expected: build FAILS ("The type or namespace name 'Dtos' does not exist").

- [ ] **Step 4: Write the entity, DbContext and tag rules**

`AddressBook.Api/Models/Contact.cs`:
```csharp
namespace AddressBook.Api.Models;

/// <summary>A contact as it is stored in the database. Never sent to or received from clients directly.</summary>
public class Contact
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

`AddressBook.Api/Data/AddressBookContext.cs`:
```csharp
using AddressBook.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Data;

public class AddressBookContext(DbContextOptions<AddressBookContext> options) : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();
}
```

`AddressBook.Api/Common/TagRules.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace AddressBook.Api.Common;

/// <summary>Validation and clean-up rules for contact tags.</summary>
public static class TagRules
{
    public const int MaxTagLength = 30;

    public static IEnumerable<ValidationResult> Validate(IEnumerable<string?>? tags)
    {
        if (tags is null)
        {
            yield break;
        }

        if (tags.Any(t => t is not null && t.Trim().Length > MaxTagLength))
        {
            yield return new ValidationResult(
                $"Each tag must be at most {MaxTagLength} characters.", ["Tags"]);
        }
    }

    /// <summary>Trims tags, drops blank ones and removes duplicates (ignoring case), keeping the first spelling.</summary>
    public static List<string> Normalize(IEnumerable<string?>? tags) =>
        (tags ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
}
```

- [ ] **Step 5: Write the DTOs**

`AddressBook.Api/Dtos/ContactDto.cs`:
```csharp
namespace AddressBook.Api.Dtos;

/// <summary>A contact as returned by the API.</summary>
public record ContactDto(
    int Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Tags,
    bool IsFavorite,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

`AddressBook.Api/Dtos/CreateContactDto.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using AddressBook.Api.Common;

namespace AddressBook.Api.Dtos;

/// <summary>Body for creating a contact. Id and timestamps are always set by the server.</summary>
public class CreateContactDto : IValidatableObject
{
    /// <summary>Required.</summary>
    /// <example>Alan</example>
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Required.</summary>
    /// <example>Turing</example>
    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Optional. Omit or send null for no email.</summary>
    /// <example>alan.turing@auning.com</example>
    [EmailAddress, MaxLength(100)]
    public string? Email { get; set; }

    /// <summary>Optional.</summary>
    /// <example>+1 555 123 4567</example>
    [MaxLength(30)]
    public string? Phone { get; set; }

    /// <summary>Free-text labels such as Work or Family. Up to 20 tags, each up to 30 characters.</summary>
    /// <example>["Work", "Tag"]</example>
    [MaxLength(20)]
    public List<string>? Tags { get; set; }

    /// <summary>Whether the contact is starred.</summary>
    public bool IsFavorite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags);
}
```

- [ ] **Step 6: Write the service**

`AddressBook.Api/Services/IContactService.cs`:
```csharp
using AddressBook.Api.Dtos;

namespace AddressBook.Api.Services;

/// <summary>Business logic for contacts. Methods return null/false when the contact doesn't exist.</summary>
public interface IContactService
{
    Task<ContactDto?> GetAsync(int id);
    Task<ContactDto> CreateAsync(CreateContactDto dto);
}
```

`AddressBook.Api/Services/ContactService.cs`:
```csharp
using AddressBook.Api.Common;
using AddressBook.Api.Data;
using AddressBook.Api.Dtos;
using AddressBook.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Services;

public class ContactService(AddressBookContext db) : IContactService
{
    public async Task<ContactDto?> GetAsync(int id)
    {
        var contact = await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return contact is null ? null : ToDto(contact);
    }

    public async Task<ContactDto> CreateAsync(CreateContactDto dto)
    {
        var now = DateTime.UtcNow;
        var contact = new Contact
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = Clean(dto.Email),
            Phone = Clean(dto.Phone),
            Tags = TagRules.Normalize(dto.Tags),
            IsFavorite = dto.IsFavorite,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    private static ContactDto ToDto(Contact c) => new(
        c.Id, c.FirstName, c.LastName, c.Email, c.Phone,
        c.Tags.ToList(), c.IsFavorite, c.CreatedAt, c.UpdatedAt);

    /// <summary>Trims optional text; blank becomes null.</summary>
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 7: Write the controller**

`AddressBook.Api/Controllers/ContactsController.cs`:
```csharp
using AddressBook.Api.Dtos;
using AddressBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Controllers;

/// <summary>Address book contacts.</summary>
[ApiController]
[Route("api/contacts")]
public class ContactsController(IContactService contacts) : ControllerBase
{
    /// <summary>Gets one contact by id.</summary>
    /// <response code="200">The contact.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> GetById(int id) =>
        await contacts.GetAsync(id) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Creates a contact.</summary>
    /// <response code="201">Created. The Location header points to the new contact.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContactDto>> Create(CreateContactDto dto)
    {
        var created = await contacts.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
```

- [ ] **Step 8: Register the DbContext and service in `Program.cs`**

In `AddressBook.Api/Program.cs`, add these `using`s at the top:
```csharp
using AddressBook.Api.Data;
using AddressBook.Api.Services;
using Microsoft.EntityFrameworkCore;
```
Directly after `builder.Services.AddControllers();`, add:
```csharp
builder.Services.AddProblemDetails();

// The database name is read when the context is created (not at startup),
// so tests can give every test app its own database via "Database:Name".
builder.Services.AddDbContext<AddressBookContext>((services, options) =>
    options.UseInMemoryDatabase(
        services.GetRequiredService<IConfiguration>()["Database:Name"] ?? "AddressBook"));
builder.Services.AddScoped<IContactService, ContactService>();
```

- [ ] **Step 9: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS (all Task 1 and Task 2 tests).

If startup fails with "The property 'Contact.Tags' could not be mapped", the InMemory provider can't store `List<string>` directly. Add this to `AddressBookContext` and run again:
```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Stores the tag list as one string; the comparer lets EF detect changes inside the list.
        modelBuilder.Entity<Contact>()
            .Property(c => c.Tags)
            .HasConversion(
                tags => string.Join('\u001F', tags),
                stored => stored.Length == 0 ? new List<string>() : stored.Split('\u001F', StringSplitOptions.None).ToList(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                    v => v.ToList()));
    }
```

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: create and get contacts with validation and EF Core InMemory"
```

- [ ] **Step 11: Codex checkpoint**

---

### Task 3: Update (PUT), edit (PATCH), delete and bulk delete

**You learn:** the difference between PUT and PATCH, idempotency, why a nullable DTO is used for PATCH, `[MinLength]` + `[RegularExpression]` to reject blank values, and 204 No Content.

**Files:**
- Create: `AddressBook.Api/Dtos/UpdateContactDto.cs`, `AddressBook.Api/Dtos/PatchContactDto.cs`, `AddressBook.Api/Dtos/BulkDeleteDto.cs`
- Modify: `AddressBook.Api/Services/IContactService.cs`, `AddressBook.Api/Services/ContactService.cs`, `AddressBook.Api/Controllers/ContactsController.cs`
- Create: `AddressBook.Api.Tests/UpdateAndDeleteContactTests.cs`

**Interfaces:**
- Consumes: `ContactService.ToDto`, `Clean`, `TagRules.Normalize`, `TestHelpers` (Task 2).
- Produces:
  - `UpdateContactDto : CreateContactDto`, `PatchContactDto`, `BulkDeleteDto { List<int> Ids }`, `record BulkDeleteResult(int DeletedCount)`
  - `IContactService.UpdateAsync(int, UpdateContactDto) : Task<ContactDto?>`
  - `PatchAsync(int, PatchContactDto) : Task<ContactDto?>`
  - `DeleteAsync(int) : Task<bool>`
  - `BulkDeleteAsync(IReadOnlyCollection<int>) : Task<int>`

- [ ] **Step 1: Write the failing tests**

`AddressBook.Api.Tests/UpdateAndDeleteContactTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class UpdateAndDeleteContactTests
{
    [Fact]
    public async Task Put_replaces_every_editable_field()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(
            NewContact("Alan", "Turing", "alan@x.com", ["Work"], isFavorite: true, phone: "123"));

        var response = await client.PutAsJsonAsync($"/api/contacts/{original.Id}",
            new UpdateContactDto { FirstName = "Ada", LastName = "Lovelace", Tags = ["Family"] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("Ada", updated.FirstName);
        Assert.Equal("Lovelace", updated.LastName);
        Assert.Null(updated.Email);       // omitted in PUT => cleared
        Assert.Null(updated.Phone);
        Assert.False(updated.IsFavorite);
        Assert.Equal(new[] { "Family" }, updated.Tags);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= original.UpdatedAt);
    }

    [Fact]
    public async Task Put_unknown_id_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PutAsJsonAsync("/api/contacts/999",
            new UpdateContactDto { FirstName = "Ada", LastName = "Lovelace" });

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_with_invalid_body_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(NewContact());

        var response = await client.PutAsJsonAsync($"/api/contacts/{original.Id}",
            new UpdateContactDto { FirstName = "", LastName = "Lovelace" });

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_changes_only_the_fields_that_were_sent()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(
            NewContact("Alan", "Turing", "alan@x.com", ["Work"], isFavorite: true));

        var response = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { phone = "+90 555 000 0000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var patched = (await response.Content.ReadFromJsonAsync<ContactDto>())!;
        Assert.Equal("+90 555 000 0000", patched.Phone);
        Assert.Equal("Alan", patched.FirstName);
        Assert.Equal("Turing", patched.LastName);
        Assert.Equal("alan@x.com", patched.Email);
        Assert.Equal(new[] { "Work" }, patched.Tags);
        Assert.True(patched.IsFavorite);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Patch_with_blank_name_returns_400(string blank)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var original = await client.CreateContactAsync(NewContact());

        var first = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { firstName = blank });
        var last = await client.PatchAsJsonAsync($"/api/contacts/{original.Id}", new { lastName = blank });

        AssertProblem(first, HttpStatusCode.BadRequest);
        AssertProblem(last, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_unknown_id_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PatchAsJsonAsync("/api/contacts/999", new { phone = "1" });

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_contact_and_a_second_delete_returns_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact());

        var first = await client.DeleteAsync($"/api/contacts/{contact.Id}");
        var get = await client.GetAsync($"/api/contacts/{contact.Id}");
        var second = await client.DeleteAsync($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_removes_existing_ids_and_ignores_unknown_and_duplicate_ids()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var a = await client.CreateContactAsync(NewContact("A", "One"));
        var b = await client.CreateContactAsync(NewContact("B", "Two"));
        var c = await client.CreateContactAsync(NewContact("C", "Three"));

        var response = await client.PostAsJsonAsync("/api/contacts/bulk-delete",
            new BulkDeleteDto { Ids = [a.Id, b.Id, b.Id, 9999] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BulkDeleteResult>())!;
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/contacts/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/contacts/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/contacts/{c.Id}")).StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_with_no_ids_returns_400()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contacts/bulk-delete", new BulkDeleteDto { Ids = [] });

        AssertProblem(response, HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~UpdateAndDeleteContactTests"`
Expected: build FAILS (`UpdateContactDto`, `BulkDeleteDto` and `BulkDeleteResult` don't exist).

- [ ] **Step 3: Write the DTOs**

`AddressBook.Api/Dtos/UpdateContactDto.cs`:
```csharp
namespace AddressBook.Api.Dtos;

/// <summary>
/// Body for PUT (full update). Same fields as create: every editable field is replaced,
/// so optional fields that are omitted are cleared and IsFavorite falls back to false.
/// </summary>
public class UpdateContactDto : CreateContactDto
{
}
```

`AddressBook.Api/Dtos/PatchContactDto.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using AddressBook.Api.Common;

namespace AddressBook.Api.Dtos;

/// <summary>
/// Body for PATCH (partial edit). Only fields that are present (not null) change.
/// Send "" as Phone to clear it. To remove an email, use PUT.
/// </summary>
public class PatchContactDto : IValidatableObject
{
    /// <example>Alan</example>
    [MinLength(1), MaxLength(50), RegularExpression(@".*\S.*", ErrorMessage = "FirstName cannot be blank.")]
    public string? FirstName { get; set; }

    /// <example>Turing</example>
    [MinLength(1), MaxLength(50), RegularExpression(@".*\S.*", ErrorMessage = "LastName cannot be blank.")]
    public string? LastName { get; set; }

    /// <example>alan.turing@auning.com</example>
    [EmailAddress, MaxLength(100)]
    public string? Email { get; set; }

    /// <example>+1 555 123 4567</example>
    [MaxLength(30)]
    public string? Phone { get; set; }

    /// <summary>Replaces the whole tag list when present.</summary>
    [MaxLength(20)]
    public List<string>? Tags { get; set; }

    public bool? IsFavorite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        TagRules.Validate(Tags);
}
```

`AddressBook.Api/Dtos/BulkDeleteDto.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace AddressBook.Api.Dtos;

/// <summary>Ids of the contacts to delete (for example, the rows ticked with checkboxes).</summary>
public class BulkDeleteDto
{
    /// <example>[1, 2, 3]</example>
    [Required, MinLength(1), MaxLength(100)]
    public List<int> Ids { get; set; } = [];
}

/// <summary>How many contacts were actually deleted. Unknown ids are ignored.</summary>
public record BulkDeleteResult(int DeletedCount);
```

- [ ] **Step 4: Extend the service**

Add these four methods to `IContactService`:
```csharp
    Task<ContactDto?> UpdateAsync(int id, UpdateContactDto dto);
    Task<ContactDto?> PatchAsync(int id, PatchContactDto dto);
    Task<bool> DeleteAsync(int id);
    Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids);
```

Add these methods to `ContactService`, after `CreateAsync`:
```csharp
    public async Task<ContactDto?> UpdateAsync(int id, UpdateContactDto dto)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return null;
        }

        contact.FirstName = dto.FirstName.Trim();
        contact.LastName = dto.LastName.Trim();
        contact.Email = Clean(dto.Email);
        contact.Phone = Clean(dto.Phone);
        contact.Tags = TagRules.Normalize(dto.Tags);
        contact.IsFavorite = dto.IsFavorite;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    public async Task<ContactDto?> PatchAsync(int id, PatchContactDto dto)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return null;
        }

        if (dto.FirstName is not null) contact.FirstName = dto.FirstName.Trim();
        if (dto.LastName is not null) contact.LastName = dto.LastName.Trim();
        if (dto.Email is not null) contact.Email = Clean(dto.Email);
        if (dto.Phone is not null) contact.Phone = Clean(dto.Phone);
        if (dto.Tags is not null) contact.Tags = TagRules.Normalize(dto.Tags);
        if (dto.IsFavorite is bool isFavorite) contact.IsFavorite = isFavorite;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(contact);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return false;
        }

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids)
    {
        var idSet = ids.ToHashSet();
        var targets = await db.Contacts.Where(c => idSet.Contains(c.Id)).ToListAsync();

        db.Contacts.RemoveRange(targets);
        await db.SaveChangesAsync();
        return targets.Count;
    }
```

- [ ] **Step 5: Extend the controller**

Add these actions to `ContactsController`, after `Create`:
```csharp
    /// <summary>Updates a contact by replacing every editable field (full update).</summary>
    /// <response code="200">The updated contact.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> Update(int id, UpdateContactDto dto) =>
        await contacts.UpdateAsync(id, dto) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Edits a contact: only the fields present in the body change.</summary>
    /// <response code="200">The edited contact.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactDto>> Patch(int id, PatchContactDto dto) =>
        await contacts.PatchAsync(id, dto) is { } contact ? Ok(contact) : NotFound();

    /// <summary>Deletes a contact.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id) =>
        await contacts.DeleteAsync(id) ? NoContent() : NotFound();

    /// <summary>Deletes several contacts at once. Unknown ids are ignored.</summary>
    /// <response code="200">How many contacts were deleted.</response>
    /// <response code="400">The id list is empty or too long.</response>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkDeleteResult>> BulkDelete(BulkDeleteDto dto) =>
        Ok(new BulkDeleteResult(await contacts.BulkDeleteAsync(dto.Ids)));
```

- [ ] **Step 6: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS (all tests so far).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: update, edit, delete and bulk delete contacts"
```

- [ ] **Step 8: Codex checkpoint**

---

### Task 4: Favorite / unfavorite and the tag list

**You learn:** modelling an action as a sub-resource (`/favorite`) with PUT and DELETE so it stays idempotent, and why a "toggle" endpoint isn't idempotent.

**Files:**
- Modify: `AddressBook.Api/Services/IContactService.cs`, `AddressBook.Api/Services/ContactService.cs`, `AddressBook.Api/Controllers/ContactsController.cs`
- Create: `AddressBook.Api/Controllers/TagsController.cs`
- Create: `AddressBook.Api.Tests/FavoritesAndTagsTests.cs`

**Interfaces:**
- Consumes: `TestHelpers` (Task 2).
- Produces: `IContactService.SetFavoriteAsync(int id, bool isFavorite) : Task<bool>` and `GetTagsAsync() : Task<IReadOnlyList<string>>`.

- [ ] **Step 1: Write the failing tests**

`AddressBook.Api.Tests/FavoritesAndTagsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class FavoritesAndTagsTests
{
    [Fact]
    public async Task Favorite_marks_the_contact_and_repeating_it_changes_nothing()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact());

        var first = await client.PutAsync($"/api/contacts/{contact.Id}/favorite", null);
        var second = await client.PutAsync($"/api/contacts/{contact.Id}/favorite", null);
        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.True(fetched!.IsFavorite);
    }

    [Fact]
    public async Task Unfavorite_clears_the_flag_and_repeating_it_changes_nothing()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        var contact = await client.CreateContactAsync(NewContact(isFavorite: true));

        var first = await client.DeleteAsync($"/api/contacts/{contact.Id}/favorite");
        var second = await client.DeleteAsync($"/api/contacts/{contact.Id}/favorite");
        var fetched = await client.GetFromJsonAsync<ContactDto>($"/api/contacts/{contact.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.False(fetched!.IsFavorite);
    }

    [Fact]
    public async Task Favorite_and_unfavorite_unknown_id_return_404()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        AssertProblem(await client.PutAsync("/api/contacts/999/favorite", null), HttpStatusCode.NotFound);
        AssertProblem(await client.DeleteAsync("/api/contacts/999/favorite"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tags_returns_distinct_tags_sorted_and_ignoring_case()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await client.CreateContactAsync(NewContact("A", "One", tags: ["Work", "Family"]));
        await client.CreateContactAsync(NewContact("B", "Two", tags: ["work", "Gym"]));

        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Equal(new[] { "Family", "Gym", "Work" }, tags);
    }

    [Fact]
    public async Task Tags_is_empty_when_there_are_no_contacts()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Empty(tags!);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~FavoritesAndTagsTests"`
Expected: FAIL with 404/405 responses, because the routes don't exist yet.

- [ ] **Step 3: Extend the service**

Add these to `IContactService`:
```csharp
    Task<bool> SetFavoriteAsync(int id, bool isFavorite);
    Task<IReadOnlyList<string>> GetTagsAsync();
```

Add these to `ContactService`:
```csharp
    public async Task<bool> SetFavoriteAsync(int id, bool isFavorite)
    {
        var contact = await db.Contacts.FindAsync(id);
        if (contact is null)
        {
            return false;
        }

        if (contact.IsFavorite != isFavorite)
        {
            contact.IsFavorite = isFavorite;
            contact.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync()
    {
        var tagLists = await db.Contacts.AsNoTracking().Select(c => c.Tags).ToListAsync();

        return tagLists
            .SelectMany(tags => tags)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Order(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }
```

- [ ] **Step 4: Add the endpoints**

Add these to `ContactsController`:
```csharp
    /// <summary>Marks a contact as favorite. Calling it again changes nothing.</summary>
    /// <response code="204">The contact is a favorite.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpPut("{id:int}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Favorite(int id) =>
        await contacts.SetFavoriteAsync(id, true) ? NoContent() : NotFound();

    /// <summary>Removes a contact from favorites. Calling it again changes nothing.</summary>
    /// <response code="204">The contact is not a favorite.</response>
    /// <response code="404">No contact has this id.</response>
    [HttpDelete("{id:int}/favorite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfavorite(int id) =>
        await contacts.SetFavoriteAsync(id, false) ? NoContent() : NotFound();
```

`AddressBook.Api/Controllers/TagsController.cs`:
```csharp
using AddressBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Controllers;

/// <summary>Tags used by contacts (for the "All Tags" filter).</summary>
[ApiController]
[Route("api/tags")]
public class TagsController(IContactService contacts) : ControllerBase
{
    /// <summary>Lists every tag in use, without duplicates, sorted A–Z.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetAll() =>
        Ok(await contacts.GetTagsAsync());
}
```

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: favorite/unfavorite contacts and list tags"
```

- [ ] **Step 7: Codex checkpoint**

---

### Task 5: List with search, filters, sorting and paging

**You learn:** binding query parameters into an object with `[FromQuery]`, validating query values, LINQ (`Where`/`OrderBy`/`ThenBy`/`Skip`/`Take`), paging metadata, integer overflow, and culture-sensitive string comparison (the Turkish "İ" problem).

**Files:**
- Create: `AddressBook.Api/Common/TextMatching.cs`
- Create: `AddressBook.Api/Dtos/ContactQuery.cs`, `AddressBook.Api/Dtos/PagedResult.cs`
- Modify: `AddressBook.Api/Services/IContactService.cs`, `AddressBook.Api/Services/ContactService.cs`, `AddressBook.Api/Controllers/ContactsController.cs`
- Create: `AddressBook.Api.Tests/ListContactsTests.cs`

**Interfaces:**
- Consumes: `TestHelpers` (Task 2).
- Produces:
  - `ContactQuery` (`Search`, `Tag`, `Favorite`, `SortBy`, `SortOrder`, `Page`, `PageSize`)
  - `record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)`
  - `IContactService.ListAsync(ContactQuery) : Task<PagedResult<ContactDto>>`
  - `TextMatching.Comparer`, `TextMatching.ContainsLoose(string?, string)`

- [ ] **Step 1: Write the failing tests**

`AddressBook.Api.Tests/ListContactsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AddressBook.Api.Dtos;
using static AddressBook.Api.Tests.TestHelpers;

namespace AddressBook.Api.Tests;

public class ListContactsTests
{
    // Created in this order, so Ids (and CreatedAt) increase down the list.
    private static async Task SeedAsync(HttpClient client)
    {
        await client.CreateContactAsync(NewContact("Alan", "Turing", "alan.turing@auning.com", ["Work"], isFavorite: true));
        await client.CreateContactAsync(NewContact("Marie", "Curie", "marie.curie@mail.com", ["Family"]));
        await client.CreateContactAsync(NewContact("Katherine", "Johnson", "katherine.johnson@abcc.com", ["Phone"], isFavorite: true));
        await client.CreateContactAsync(NewContact("Charles", "Darwin", "charles.darwin@adl.gov", ["Tag"]));
        await client.CreateContactAsync(NewContact("Çağan", "Koyunseven", "c.koyunseven@example.com", ["Work", "Family"]));
        await client.CreateContactAsync(NewContact("İsmail", "Yılmaz", null, ["Gym"]));
    }

    private static async Task<PagedResult<ContactDto>> ListAsync(HttpClient client, string queryString = "")
    {
        var response = await client.GetAsync($"/api/contacts{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<ContactDto>>())!;
    }

    private static string[] LastNames(PagedResult<ContactDto> page) =>
        page.Items.Select(c => c.LastName).ToArray();

    [Fact]
    public async Task Default_list_is_first_page_of_10_sorted_by_last_name()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client);

        Assert.Equal(new[] { "Curie", "Darwin", "Johnson", "Koyunseven", "Turing", "Yılmaz" }, LastNames(page));
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Empty_address_book_returns_empty_page()
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var page = await ListAsync(client);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Theory]
    [InlineData("marie", "Curie")]                  // first name
    [InlineData("CURIE", "Curie")]                  // last name, any case
    [InlineData("marie cur", "Curie")]              // full name
    [InlineData("abcc.com", "Johnson")]             // email
    [InlineData("family", "Curie,Koyunseven")]      // tag
    [InlineData("  marie  ", "Curie")]              // surrounding spaces ignored
    public async Task Search_matches_name_email_or_tag(string search, string expected)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, $"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(expected.Split(','), LastNames(page));
    }

    [Theory]
    [InlineData("cagan", "Koyunseven")]
    [InlineData("ismail", "Yılmaz")]
    [InlineData("İSMAİL", "Yılmaz")]
    public async Task Search_ignores_case_and_turkish_accents(string search, string expected)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, $"?search={Uri.EscapeDataString(search)}");

        Assert.Equal(new[] { expected }, LastNames(page));
    }

    [Fact]
    public async Task Tag_filter_is_an_exact_case_insensitive_match()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        Assert.Equal(new[] { "Koyunseven", "Turing" }, LastNames(await ListAsync(client, "?tag=work")));
        Assert.Empty((await ListAsync(client, "?tag=wor")).Items);
    }

    [Fact]
    public async Task Favorite_filter_works_both_ways_and_combines_with_tag()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        Assert.Equal(new[] { "Johnson", "Turing" }, LastNames(await ListAsync(client, "?favorite=true")));
        Assert.Equal(4, (await ListAsync(client, "?favorite=false")).TotalCount);
        Assert.Equal(new[] { "Turing" }, LastNames(await ListAsync(client, "?tag=work&favorite=true")));
    }

    [Fact]
    public async Task Sort_by_first_name_in_both_directions()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var asc = await ListAsync(client, "?sortBy=firstName");
        var desc = await ListAsync(client, "?sortBy=FIRSTNAME&sortOrder=DESC");

        Assert.Equal("Alan", asc.Items[0].FirstName);
        Assert.Equal("Marie", desc.Items[0].FirstName);
    }

    [Fact]
    public async Task Sort_by_last_name_descending()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, "?sortBy=lastName&sortOrder=desc");

        Assert.Equal(new[] { "Yılmaz", "Turing", "Koyunseven", "Johnson", "Darwin", "Curie" }, LastNames(page));
    }

    [Fact]
    public async Task Sort_by_email_puts_missing_emails_last()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var asc = await ListAsync(client, "?sortBy=email");
        var desc = await ListAsync(client, "?sortBy=email&sortOrder=desc");

        Assert.Equal("Turing", asc.Items[0].LastName);   // alan.turing@...
        Assert.Equal("Yılmaz", asc.Items[^1].LastName);  // no email
        Assert.Equal("Curie", desc.Items[0].LastName);   // marie.curie@...
        Assert.Equal("Yılmaz", desc.Items[^1].LastName); // still last
    }

    [Fact]
    public async Task Sort_by_created_at_descending_shows_newest_first()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, "?sortBy=createdAt&sortOrder=desc");

        Assert.Equal("Yılmaz", page.Items[0].LastName);
        Assert.Equal("Turing", page.Items[^1].LastName);
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_metadata()
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page2 = await ListAsync(client, "?pageSize=4&page=2");

        Assert.Equal(new[] { "Turing", "Yılmaz" }, LastNames(page2));
        Assert.Equal(2, page2.Page);
        Assert.Equal(4, page2.PageSize);
        Assert.Equal(6, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
    }

    [Theory]
    [InlineData("?pageSize=4&page=3")]
    [InlineData("?page=2147483647")]
    public async Task Page_far_past_the_end_returns_empty_items(string queryString)
    {
        using var app = new TestApp();
        var client = app.CreateClient();
        await SeedAsync(client);

        var page = await ListAsync(client, queryString);

        Assert.Empty(page.Items);
        Assert.Equal(6, page.TotalCount);
    }

    [Theory]
    [InlineData("?sortBy=age")]
    [InlineData("?sortOrder=up")]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?favorite=maybe")]
    public async Task Invalid_query_values_return_400(string queryString)
    {
        using var app = new TestApp();
        var client = app.CreateClient();

        var response = await client.GetAsync($"/api/contacts{queryString}");

        AssertProblem(response, HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~ListContactsTests"`
Expected: build FAILS (`PagedResult` doesn't exist).

- [ ] **Step 3: Write the text matching rules**

`AddressBook.Api/Common/TextMatching.cs`:
```csharp
using System.Globalization;

namespace AddressBook.Api.Common;

/// <summary>
/// Text comparison rules for search, filtering and sorting. They use the invariant culture,
/// so results don't depend on the server's language settings. (On a Turkish machine,
/// "I".ToLower() is "ı", which breaks a naive ToLower() search.)
/// </summary>
public static class TextMatching
{
    /// <summary>Case-insensitive comparer for equality and sorting.</summary>
    public static readonly StringComparer Comparer = StringComparer.InvariantCultureIgnoreCase;

    private static readonly CompareInfo Invariant = CultureInfo.InvariantCulture.CompareInfo;

    /// <summary>True when <paramref name="value"/> contains <paramref name="term"/>, ignoring case and accents ("cagan" finds "Çağan").</summary>
    public static bool ContainsLoose(string? value, string term) =>
        value is not null &&
        Invariant.IndexOf(value, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
```
Also replace the two `StringComparer.InvariantCultureIgnoreCase` uses with `TextMatching.Comparer`, so there's one source of truth:
- in `TagRules.Normalize` (same namespace, no `using` needed)
- in `ContactService.GetTagsAsync` (add `using AddressBook.Api.Common;` if it's missing)

- [ ] **Step 4: Write the query and paging DTOs**

`AddressBook.Api/Dtos/ContactQuery.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AddressBook.Api.Dtos;

/// <summary>Search, filter, sort and paging options for listing contacts.</summary>
public class ContactQuery
{
    /// <summary>Text to find in first name, last name, full name, email or any tag. Ignores case and accents.</summary>
    [FromQuery(Name = "search")]
    public string? Search { get; set; }

    /// <summary>Only contacts with exactly this tag (case-insensitive).</summary>
    [FromQuery(Name = "tag")]
    public string? Tag { get; set; }

    /// <summary>true = only favorites, false = only non-favorites, empty = all.</summary>
    [FromQuery(Name = "favorite")]
    public bool? Favorite { get; set; }

    /// <summary>firstName, lastName, email or createdAt. Contacts without email always come last.</summary>
    [FromQuery(Name = "sortBy")]
    [RegularExpression("(?i)^(firstName|lastName|email|createdAt)$",
        ErrorMessage = "sortBy must be one of: firstName, lastName, email, createdAt.")]
    public string SortBy { get; set; } = "lastName";

    /// <summary>asc or desc.</summary>
    [FromQuery(Name = "sortOrder")]
    [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "sortOrder must be asc or desc.")]
    public string SortOrder { get; set; } = "asc";

    /// <summary>Page number, starting at 1.</summary>
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; set; } = 1;

    /// <summary>Items per page, 1–100.</summary>
    [FromQuery(Name = "pageSize")]
    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;
}
```

`AddressBook.Api/Dtos/PagedResult.cs`:
```csharp
namespace AddressBook.Api.Dtos;

/// <summary>One page of results plus the numbers a client needs for "Showing 1–4 of 24".</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
```

- [ ] **Step 5: Write `ListAsync`**

Add this to `IContactService`:
```csharp
    Task<PagedResult<ContactDto>> ListAsync(ContactQuery query);
```

Add this to `ContactService` (with `using AddressBook.Api.Common;` at the top):
```csharp
    public async Task<PagedResult<ContactDto>> ListAsync(ContactQuery query)
    {
        // The in-memory table is small, so we load it and filter in .NET.
        // That lets us use culture-independent, accent-insensitive matching (see TextMatching).
        IEnumerable<Contact> contacts = await db.Contacts.AsNoTracking().ToListAsync();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            contacts = contacts.Where(c => MatchesSearch(c, term));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = query.Tag.Trim();
            contacts = contacts.Where(c => c.Tags.Contains(tag, TextMatching.Comparer));
        }

        if (query.Favorite is bool favorite)
        {
            contacts = contacts.Where(c => c.IsFavorite == favorite);
        }

        var matches = Sort(contacts, query.SortBy, query.SortOrder).ToList();

        // long arithmetic: page * pageSize can overflow int for huge page numbers.
        var skip = (long)(query.Page - 1) * query.PageSize;
        List<ContactDto> items = skip >= matches.Count
            ? []
            : matches.Skip((int)skip).Take(query.PageSize).Select(ToDto).ToList();
        var totalPages = (int)Math.Ceiling(matches.Count / (double)query.PageSize);

        return new PagedResult<ContactDto>(items, query.Page, query.PageSize, matches.Count, totalPages);
    }

    private static bool MatchesSearch(Contact c, string term) =>
        TextMatching.ContainsLoose(c.FirstName, term) ||
        TextMatching.ContainsLoose(c.LastName, term) ||
        TextMatching.ContainsLoose($"{c.FirstName} {c.LastName}", term) ||
        TextMatching.ContainsLoose(c.Email, term) ||
        c.Tags.Any(tag => TextMatching.ContainsLoose(tag, term));

    private static IEnumerable<Contact> Sort(IEnumerable<Contact> contacts, string sortBy, string sortOrder)
    {
        var descending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var comparer = TextMatching.Comparer;

        IOrderedEnumerable<Contact> ordered = sortBy.ToLowerInvariant() switch
        {
            "firstname" => descending
                ? contacts.OrderByDescending(c => c.FirstName, comparer)
                : contacts.OrderBy(c => c.FirstName, comparer),
            // Contacts without an email always go last.
            "email" => descending
                ? contacts.OrderBy(c => c.Email is null).ThenByDescending(c => c.Email, comparer)
                : contacts.OrderBy(c => c.Email is null).ThenBy(c => c.Email, comparer),
            "createdat" => descending
                ? contacts.OrderByDescending(c => c.CreatedAt)
                : contacts.OrderBy(c => c.CreatedAt),
            _ => descending
                ? contacts.OrderByDescending(c => c.LastName, comparer)
                : contacts.OrderBy(c => c.LastName, comparer),
        };

        // Ties are broken by Id so the order is stable between requests.
        return descending ? ordered.ThenByDescending(c => c.Id) : ordered.ThenBy(c => c.Id);
    }
```

- [ ] **Step 6: Add the endpoint**

Add this as the first action in `ContactsController`:
```csharp
    /// <summary>Lists contacts with search, tag and favorite filters, sorting and paging.</summary>
    /// <response code="200">One page of contacts.</response>
    /// <response code="400">A query value is invalid.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ContactDto>>> List([FromQuery] ContactQuery query) =>
        Ok(await contacts.ListAsync(query));
```

- [ ] **Step 7: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS.

If only the `İSMAİL`/`cagan` cases fail, check that the test file is saved as UTF-8: Turkish characters can get mangled by an editor. Then run `dotnet test --filter "Search_ignores"` and read the actual result. Don't weaken the assertion; fix the matching.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: list contacts with search, tag/favorite filters, sorting and paging"
```

- [ ] **Step 9: Codex checkpoint**

---

### Task 6: Error handling, seed data and the PORT setting

**You learn:** global exception handling with ProblemDetails, why stack traces must not leak, replacing a service in tests through DI (`ConfigureTestServices`), startup seeding, and reading environment variables.

**Files:**
- Create: `AddressBook.Api/Data/SeedData.cs`
- Modify: `AddressBook.Api/Program.cs` (final version below)
- Create: `AddressBook.Api.Tests/ErrorHandlingTests.cs`, `AddressBook.Api.Tests/SeedDataTests.cs`

**Interfaces:**
- Consumes: `IContactService` (all 9 methods from Tasks 2–5), `TestApp(seed, configureServices)` (Task 1).
- Produces: `SeedData.Initialize(AddressBookContext)`, which adds 24 contacts only when the table is empty. Six of them are favorites.

- [ ] **Step 1: Write the failing tests**

`AddressBook.Api.Tests/ErrorHandlingTests.cs`:
```csharp
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
```

`AddressBook.Api.Tests/SeedDataTests.cs`:
```csharp
using System.Net.Http.Json;
using AddressBook.Api.Data;
using AddressBook.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AddressBook.Api.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task App_starts_with_24_sample_contacts_when_seeding_is_on()
    {
        using var app = new TestApp(seed: true);
        var client = app.CreateClient();

        var all = await client.GetFromJsonAsync<PagedResult<ContactDto>>("/api/contacts?pageSize=100");
        var favorites = await client.GetFromJsonAsync<PagedResult<ContactDto>>("/api/contacts?favorite=true");
        var tags = await client.GetFromJsonAsync<string[]>("/api/tags");

        Assert.Equal(24, all!.TotalCount);
        Assert.Equal(6, favorites!.TotalCount);
        Assert.NotEmpty(tags!);
    }

    [Fact]
    public void Initialize_does_nothing_when_contacts_already_exist()
    {
        var options = new DbContextOptionsBuilder<AddressBookContext>()
            .UseInMemoryDatabase($"seed-{Guid.NewGuid()}")
            .Options;
        using var db = new AddressBookContext(options);

        SeedData.Initialize(db);
        SeedData.Initialize(db);

        Assert.Equal(24, db.Contacts.Count());
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test --filter "FullyQualifiedName~ErrorHandlingTests|FullyQualifiedName~SeedDataTests"`
Expected: build FAILS (`SeedData` doesn't exist).

- [ ] **Step 3: Write the seed data**

`AddressBook.Api/Data/SeedData.cs`:
```csharp
using AddressBook.Api.Models;

namespace AddressBook.Api.Data;

/// <summary>
/// Sample contacts loaded on startup so the live Swagger page is never empty.
/// The data is in memory, so it comes back every time the app restarts.
/// </summary>
public static class SeedData
{
    public static void Initialize(AddressBookContext db)
    {
        if (db.Contacts.Any())
        {
            return;
        }

        Contact[] samples =
        [
            C("Alan", "Turing", "alan.turing@auning.com", "+1 555 123 4567", true, "Work"),
            C("Marie", "Curie", "marie.curie@mail.com", null, false, "Family"),
            C("Katherine", "Johnson", "katherine.johnson@abcc.com", "+1 555 987 6543", true, "Phone"),
            C("Charles", "Darwin", "charles.darwin@adl.gov", null, false, "Tag"),
            C("Ada", "Lovelace", "ada.lovelace@example.com", null, true, "Work", "Friends"),
            C("Grace", "Hopper", "grace.hopper@example.com", "+1 555 222 3333", false, "Work"),
            C("Nikola", "Tesla", "nikola.tesla@example.com", null, false, "Friends"),
            C("Albert", "Einstein", "albert.einstein@example.com", null, false, "Family"),
            C("Isaac", "Newton", "isaac.newton@example.com", null, false, "Work"),
            C("Rosalind", "Franklin", "rosalind.franklin@example.com", null, true, "Friends"),
            C("Cahit", "Arf", "cahit.arf@example.com", "+90 532 000 0001", false, "Work"),
            C("Aziz", "Sancar", "aziz.sancar@example.com", "+90 532 000 0002", true, "Work", "Family"),
            C("Gazi", "Yaşargil", "gazi.yasargil@example.com", null, false, "Doctor"),
            C("Canan", "Dağdeviren", "canan.dagdeviren@example.com", null, false, "Work"),
            C("İhsan", "Doğramacı", "ihsan.dogramaci@example.com", null, false, "Doctor"),
            C("Feryal", "Özel", "feryal.ozel@example.com", null, false, "Friends"),
            C("Linus", "Torvalds", "linus.torvalds@example.com", null, false, "Work"),
            C("Margaret", "Hamilton", "margaret.hamilton@example.com", null, true, "Work"),
            C("Tim", "Berners-Lee", "tim.bernerslee@example.com", null, false, "Friends"),
            C("Hedy", "Lamarr", "hedy.lamarr@example.com", null, false, "Family"),
            C("Richard", "Feynman", "richard.feynman@example.com", null, false, "Friends"),
            C("Dennis", "Ritchie", "dennis.ritchie@example.com", null, false, "Work"),
            C("Barbara", "Liskov", "barbara.liskov@example.com", null, false, "Work"),
            C("Oktay", "Sinanoğlu", "oktay.sinanoglu@example.com", null, false, "Family"),
        ];

        // Space the timestamps a minute apart so sorting by createdAt is meaningful.
        var start = DateTime.UtcNow.AddMinutes(-samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i].CreatedAt = samples[i].UpdatedAt = start.AddMinutes(i);
        }

        db.Contacts.AddRange(samples);
        db.SaveChanges();
    }

    private static Contact C(string firstName, string lastName, string? email, string? phone,
        bool isFavorite, params string[] tags) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Phone = phone,
        IsFavorite = isFavorite,
        Tags = [.. tags],
    };
}
```

- [ ] **Step 4: Write the final `Program.cs`**

Replace `AddressBook.Api/Program.cs` with:
```csharp
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
```
(Keep the `using Microsoft.OpenApi.Models;` variant if Task 1 needed it, and the `public partial class Program { }` line if Task 1 added it.)

- [ ] **Step 5: Run the tests and confirm they pass**

Run: `dotnet test`
Expected: PASS (every test in the solution).

- [ ] **Step 6: Check the PORT setting by hand**

PowerShell:
```powershell
$env:PORT = "5099"; dotnet run --project AddressBook.Api
```
Expected log: `Now listening on: http://0.0.0.0:5099`. Open `http://localhost:5099` → Swagger. Try `GET /api/contacts` → 24 contacts. Stop with Ctrl+C, then run `Remove-Item Env:PORT`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: ProblemDetails error handling, sample seed data and PORT support"
```

- [ ] **Step 8: Codex checkpoint**

---

### Task 7: Docker image and deploy to Render

**You learn:** multi-stage Docker builds, `.dockerignore`, running as a non-root user, how a PaaS assigns ports, and deploying automatically from GitHub.

**Files:**
- Create: `Dockerfile`, `.dockerignore`

- [ ] **Step 1: Write the Docker files**

`Dockerfile`:
```dockerfile
# Build stage: has the full SDK, compiles and publishes the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (only the project file), so this layer is cached when only code changes.
COPY AddressBook.Api/AddressBook.Api.csproj AddressBook.Api/
RUN dotnet restore AddressBook.Api/AddressBook.Api.csproj

COPY AddressBook.Api/ AddressBook.Api/
RUN dotnet publish AddressBook.Api/AddressBook.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage: only the ASP.NET runtime and the published output (much smaller image).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Run as the image's built-in non-root user.
USER $APP_UID

# Render sets PORT; Program.cs listens on it. 8080 is the image default otherwise.
EXPOSE 8080
ENTRYPOINT ["dotnet", "AddressBook.Api.dll"]
```

`.dockerignore`:
```
**/bin/
**/obj/
**/.vs/
**/*.user
.git/
.remember/
AddressBook.Api.Tests/
docs/
```

- [ ] **Step 2: Build and run the image locally (only if Docker is installed)**

Check with `docker --version`. If it isn't installed, skip this step: Render's build log is the test.
```bash
docker build -t addressbook-api .
docker run --rm -p 8080:10000 -e PORT=10000 addressbook-api
```
Expected: `Now listening on: http://0.0.0.0:10000`, and `http://localhost:8080/swagger` works. Stop with Ctrl+C.

- [ ] **Step 3: Commit and push**

```bash
git add Dockerfile .dockerignore
git commit -m "build: add Dockerfile for Render deployment"
```
Then click **Push origin** in GitHub Desktop.

- [ ] **Step 4 (user): Create the Render web service**

1. https://render.com → **Get Started** → **GitHub** sign-in (no card needed).
2. Dashboard → **New +** → **Web Service** → *Build and deploy from a Git repository*. Connect GitHub and grant access to `SE4458-AddressBook-API`.
3. Settings:

   | Field | Value |
   |---|---|
   | Name | `addressbook-api` (part of the URL; Render adds a suffix if it's taken) |
   | Region | Frankfurt (EU Central) |
   | Branch | `main` |
   | Root Directory | *(empty)* |
   | Language | **Docker** |
   | Dockerfile Path | `./Dockerfile` |
   | Instance Type | **Free** |
   | Environment variables | none (Render sets `PORT` itself) |
   | Advanced → Health Check Path | `/swagger/v1/swagger.json` |
   | Advanced → Auto-Deploy | On Commit |

4. **Create Web Service**, then watch the logs. The Docker build takes 2–4 minutes. Expected lines:
   - `Now listening on: http://0.0.0.0:10000`
   - `Your service is live 🎉`

- [ ] **Step 5: Smoke-test the live site**

Open `https://<service>.onrender.com/` → it should redirect to Swagger. In Swagger:
- `GET /api/contacts` → 24 contacts
- `POST /api/contacts` → 201
- `PUT /api/contacts/{id}/favorite` → 204
- `GET /api/contacts?search=cagan` → still works (Linux container, invariant culture)

Write down the URL for the README.

- [ ] **Step 6: Codex checkpoint**

---

### Task 8: README (deliverable)

**You learn:** documenting design decisions, assumptions and trade-offs for a reviewer.

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write `README.md`**

Replace `<RENDER_URL>` with the URL from Task 7, Step 5.
Add any new problems you ran into during Tasks 1–7 to "Issues encountered".

````markdown
# Address Book API

SE 4458 – Assignment 2 (Group 1): a REST API for an address book. You can create, edit, update,
delete, search, sort and favorite contacts. There is no frontend; use the Swagger page.

| | Link |
|---|---|
| Source code | https://github.com/Batsy18/SE4458-AddressBook-API |
| Live Swagger | <RENDER_URL>/swagger |

> The free Render instance sleeps after ~15 minutes without traffic. The first request after that
> can take about a minute.

## Tech stack

- .NET 10 (LTS), ASP.NET Core Web API with controllers
- EF Core InMemory database
- Swashbuckle (Swagger UI)
- xUnit integration tests (`WebApplicationFactory`)
- Docker, deployed on Render (free plan, auto-deploy from `main`)

## Design

```
Controllers  →  IContactService / ContactService  →  AddressBookContext (EF Core InMemory)
  (HTTP)              (business logic)                     (data access)
```

- **Controllers** contain no business logic. They call the service and map results to status codes.
- **ContactService** (registered with dependency injection behind `IContactService`) does
  validation clean-up, search, filtering, sorting, paging, timestamps and DTO mapping.
- **DTOs** (`CreateContactDto`, `UpdateContactDto`, `PatchContactDto`, `ContactDto`, …) keep the
  API contract separate from the `Contact` entity. Clients can't set `Id`, `CreatedAt` or `UpdatedAt`.
- **Errors** are ProblemDetails (`application/problem+json`): 400 for validation, 404 for an
  unknown id, 500 without internal details.

### Endpoints

| Action | Method & route | Success |
|---|---|---|
| List / search / sort / page | `GET /api/contacts?search=&tag=&favorite=&sortBy=lastName&sortOrder=asc&page=1&pageSize=10` | 200 |
| Get one | `GET /api/contacts/{id}` | 200 |
| Create | `POST /api/contacts` | 201 + `Location` |
| Update (full) | `PUT /api/contacts/{id}` | 200 |
| Edit (partial) | `PATCH /api/contacts/{id}` | 200 |
| Delete | `DELETE /api/contacts/{id}` | 204 |
| Bulk delete | `POST /api/contacts/bulk-delete` | 200 `{ deletedCount }` |
| Favorite | `PUT /api/contacts/{id}/favorite` | 204 |
| Unfavorite | `DELETE /api/contacts/{id}/favorite` | 204 |
| Tag list | `GET /api/tags` | 200 |

- `sortBy`: `firstName`, `lastName`, `email` or `createdAt`. `sortOrder`: `asc` or `desc`. `pageSize`: 1–100.
- `search` looks in first name, last name, full name, email and tags. It ignores case and accents
  (`cagan` finds `Çağan`).
- Favorite and unfavorite are separate `PUT`/`DELETE` calls rather than a toggle, so repeating a
  request is safe (idempotent).

## Assumptions

- Data lives only in memory. It resets whenever the app restarts or the free instance sleeps.
  24 sample contacts are loaded on every start.
- First and last name are required. Email, phone and tags are optional.
- Tags are free text. They are trimmed, and duplicates are removed ignoring case.
- `PUT` replaces every editable field (omitted optional fields are cleared). `PATCH` changes only
  the fields that are sent.
- There is no authentication, versioning or rate limiting. These were optional in the course and
  are out of scope.

## Issues encountered

- **.NET 10 template has no Swagger UI.** From .NET 9 on, `dotnet new webapi` only generates an
  OpenAPI document. I added Swashbuckle for the UI.
- **Swagger only in Development.** The template enables Swagger only in the Development
  environment, so `/swagger` would return 404 in production. I enabled it in every environment.
- **Hosting.** Azure wasn't available to me, and Vercel can't run ASP.NET Core. I used Render
  with a Docker image instead; the app reads Render's `PORT` variable.
- **Turkish locale and text comparison.** On a Turkish machine, `"I".ToLower()` is `"ı"`, which
  breaks a naive case-insensitive search. Search and sort use invariant-culture comparison instead.
- **EF Core InMemory and filtering.** For culture-independent, accent-insensitive search, the
  small in-memory table is loaded and then filtered in .NET. With a real database, this filtering
  would be pushed into the query.

## Run locally

```bash
dotnet run --project AddressBook.Api     # then open the printed URL (redirects to /swagger)
dotnet test                              # run the integration tests
```

With Docker:

```bash
docker build -t addressbook-api .
docker run --rm -p 8080:8080 addressbook-api   # http://localhost:8080/swagger
```
````

- [ ] **Step 2: Check the links**

Open both README links in a private browser window. The repo must be public and Swagger must load.

- [ ] **Step 3: Commit and push**

```bash
git add README.md
git commit -m "docs: add README with design, assumptions and issues"
```
Then click **Push origin** in GitHub Desktop.

- [ ] **Step 4: Final Codex checkpoint** — whole-repo review against the spec.
- [ ] **Step 5 (user): Submit** the GitHub link and `<RENDER_URL>/swagger`.
