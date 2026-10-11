# Address Book API — Design Spec

- **Date:** 2026-10-11
- **Course:** SE 4458, Assignment 2 (Develop/Deploy Simple REST API), Group 1 — Address Book
- **Author:** Mustafa Batu Demir

## 1. Goal

A public REST API for an address book that can create, edit, update, delete, search, sort and
favorite contacts. Data lives in an in-memory database. There is no frontend: the API is used
through its Swagger page.

### Requirements (from the assignment PDF)

- Built on the to-do tutorial template (ASP.NET Core Web API).
- Publicly reachable, with a Swagger page.
- All data in an in-memory database.
- No frontend.
- Deliverables: a GitHub repo whose README covers the source link, design, assumptions and issues
  encountered, plus a link to the live deployment.

### Success criteria

- `https://<service-name>.onrender.com/swagger` loads, and every endpoint works from it.
- The README contains everything listed under deliverables.
- The integration tests pass.

## 2. Technology

| Concern | Choice | Reason |
|---|---|---|
| Framework | ASP.NET Core Web API on **.NET 10 (LTS)**, using controllers | Matches the tutorial and slide 7. .NET 8 support ends Nov 2026 |
| Data | **EF Core InMemory** (`Microsoft.EntityFrameworkCore.InMemory`) | Same as the tutorial and fits "in memory database". Moving to a real database later is a one-line change |
| API docs | **Swashbuckle.AspNetCore** (Swagger UI at `/swagger`) | The .NET 10 template no longer ships a Swagger UI |
| Tests | xUnit + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) | Runs the real HTTP pipeline in memory |
| Hosting | **Render, free web service, Docker** | Free with no credit card. Azure isn't available to the author |
| Container | Multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`) | Render has no native .NET runtime |
| CI/CD | Render auto-deploy from GitHub | Every push to `main` rebuilds and deploys. No workflow file needed |
| Source control | Git, published with GitHub Desktop | User's preferred tool |

## 3. Data model

`Contact` (entity, stored in EF Core InMemory):

| Field | Type | Rules |
|---|---|---|
| `Id` | `int` | Assigned by the server |
| `FirstName` | `string` | Required, max 50 |
| `LastName` | `string` | Required, max 50 |
| `Email` | `string?` | Optional, valid email format, max 100 |
| `Phone` | `string?` | Optional, max 30 |
| `Tags` | `List<string>` | Free text, e.g. `Work`, `Family`. Duplicates (case-insensitive) and blank values removed. Each tag max 30 |
| `IsFavorite` | `bool` | Defaults to `false` |
| `CreatedAt` | `DateTime` (UTC) | Set by the server |
| `UpdatedAt` | `DateTime` (UTC) | Set by the server on every change |

If the InMemory provider can't store `List<string>` directly, `Tags` gets a value converter plus
a value comparer. A test checks that tags round-trip correctly.

## 4. DTOs

Clients never send the entity itself; DTOs prevent over-posting of `Id`, `CreatedAt` and `UpdatedAt`.

| DTO | Used by | Fields |
|---|---|---|
| `CreateContactDto` | `POST` | FirstName\*, LastName\*, Email, Phone, Tags, IsFavorite |
| `UpdateContactDto` | `PUT` | Same as Create; replaces every editable field |
| `PatchContactDto` | `PATCH` | Every field nullable; only fields that are present (non-null) change |
| `ContactDto` | Responses | Every entity field |
| `ContactQuery` | `GET` list | `search`, `tag`, `favorite`, `sortBy`, `sortOrder`, `page`, `pageSize` |
| `PagedResult<T>` | `GET` list | `items`, `page`, `pageSize`, `totalCount`, `totalPages` |
| `BulkDeleteDto` | Bulk delete | `ids: int[]` (at least 1) |

## 5. Endpoints

Base path: `/api`.

| Assignment verb | Method & route | Success | Errors |
|---|---|---|---|
| list / search / sort | `GET /contacts` | 200 `PagedResult<ContactDto>` | 400 for invalid query values |
| get one | `GET /contacts/{id}` | 200 `ContactDto` | 404 |
| create | `POST /contacts` | 201 `ContactDto` + `Location` header | 400 |
| update (full) | `PUT /contacts/{id}` | 200 `ContactDto` | 400, 404 |
| edit (partial) | `PATCH /contacts/{id}` | 200 `ContactDto` | 400, 404 |
| delete | `DELETE /contacts/{id}` | 204 | 404 |
| bulk delete | `POST /contacts/bulk-delete` | 200 `{ deletedCount }` | 400 |
| favorite | `PUT /contacts/{id}/favorite` | 204 | 404 |
| unfavorite | `DELETE /contacts/{id}/favorite` | 204 | 404 |
| tag list | `GET /tags` | 200 `string[]` (distinct, sorted) | — |

### List query parameters

| Parameter | Default | Behaviour |
|---|---|---|
| `search` | — | Case-insensitive "contains" match on first name, last name, full name, email or any tag |
| `tag` | — | Case-insensitive exact match on one tag |
| `favorite` | — | `true` returns only favorites, `false` only non-favorites |
| `sortBy` | `lastName` | One of `firstName`, `lastName`, `email`, `createdAt`. Ties are broken by `Id` |
| `sortOrder` | `asc` | `asc` or `desc` |
| `page` | `1` | At least 1. A page past the end returns an empty `items` list |
| `pageSize` | `10` | Between 1 and 100 |

### Idempotency

- `PUT`, `DELETE`, and favorite `PUT`/`DELETE` are idempotent.
- Favoriting an already-favorite contact returns 204 and changes nothing.
- Bulk delete ignores ids that don't exist and reports how many were actually deleted.

## 6. Architecture

```
Controllers  →  IContactService / ContactService  →  AddressBookContext (EF Core InMemory)
  (HTTP)              (business logic)                     (data access)
```

- **Controllers** (`ContactsController`, `TagsController`): no business logic. They call the
  service and turn its result into a status code.
- **Service** (`IContactService` / `ContactService`, registered as scoped):
  - filtering, sorting and paging with LINQ
  - timestamps
  - normalising tags
  - mapping between the entity and DTOs
  - Returns `null`/`false` for "not found", and the controller turns that into a 404.
- **Data** (`AddressBookContext`): EF Core InMemory with database name `AddressBook`.
  `SeedData` adds about 24 sample contacts on startup when the database is empty.

Folder layout:

```
AddressBook.slnx
AddressBook.Api/          Controllers/ Services/ Data/ Models/ Dtos/ Program.cs
AddressBook.Api.Tests/    integration tests
docs/superpowers/specs/   this spec
README.md
```

## 7. Validation and error handling

- Data annotations on the DTOs: `[Required]`, `[MaxLength]`, `[EmailAddress]`, `[Range]`.
- `[ApiController]` returns 400 `ValidationProblemDetails` automatically.
- `sortBy` and `sortOrder` values outside the allowed set return a 400 `ValidationProblemDetails`.
- `AddProblemDetails()` + `UseExceptionHandler()`: unhandled exceptions return a 500
  ProblemDetails with no stack trace.

## 8. Swagger

- Swashbuckle with XML documentation comments, so every endpoint and parameter is described.
- **Enabled in every environment, including Production.** The template only enables it in
  Development, which would make the live `/swagger` page return 404 on Render.
- `/` redirects to `/swagger`.

## 9. Testing

Integration tests (`WebApplicationFactory<Program>`). Each test gets its own fresh InMemory
database so tests don't share state.

They cover:

- every endpoint's success path and status code
- 404 for an unknown id
- 400 for a missing first or last name, an invalid email, and invalid `sortBy`, `page` or `pageSize`
- search across name, email and tag
- tag filter, favorite filter, sorting in both directions, and paging metadata
- `PATCH` changing only the fields that were sent
- idempotent favorite and unfavorite
- bulk delete with a mix of existing and unknown ids
- tags round-tripping through the InMemory provider

## 10. Deployment

1. Publish the repo to GitHub (public) with GitHub Desktop.
2. Add a multi-stage `Dockerfile` at the repo root plus a `.dockerignore`.
   - The app listens on the port in the `PORT` environment variable (Render's default is 10000)
     and falls back to 8080 when it isn't set.
   - Render terminates HTTPS, so the app does not use `UseHttpsRedirection` in the container.
3. Render dashboard → **New → Web Service** → connect the GitHub repo:
   - Language: **Docker**
   - Branch: `main`
   - Instance type: **Free**
   - Auto-deploy: on
4. Check that `https://<service-name>.onrender.com/swagger` works.

## 11. Assumptions and known limitations (these go in the README)

- Data is in memory. It resets whenever the app restarts, and Render's free plan puts an idle
  service to sleep after about 15 minutes. The seed data comes back on every start.
- A cold start after sleeping takes about a minute. The free plan includes 750 instance-hours a month.
- There is no authentication. JWT and API versioning are optional in the slides and out of scope.
- Tags are free text with no separate tag entity.
- A single app instance, so no concurrency control between instances.

## 12. Out of scope

Frontend, authentication, persistent database, API versioning, rate limiting.
