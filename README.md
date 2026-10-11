# Address Book API

SE 4458 – Assignment 2 (Group 1): a REST API for an address book. You can create, edit, update,
delete, search, sort and favorite contacts. There is no frontend; use the Swagger page.

| | Link |
|---|---|
| Source code | https://github.com/Batsy18/SE4458-AddressBook-API |
| Live Swagger | https://addressbook-api-q9k0.onrender.com/swagger |

> The free Render instance sleeps after ~15 minutes without traffic. The first request after that
> can take about a minute.

## Tech stack

- .NET 10 (LTS), ASP.NET Core Web API with controllers
- EF Core InMemory database
- Swashbuckle (Swagger UI)
- xUnit integration tests (`WebApplicationFactory`), 60 tests
- Docker, deployed on Render (free plan, auto-deploy from `main`)

## Design

```
Controllers  →  IContactService / ContactService  →  AddressBookContext (EF Core InMemory)
  (HTTP)              (business logic)                     (data access)
```

- **Controllers** (`ContactsController`, `TagsController`) contain no business logic. They call
  the service and map results to status codes.
- **ContactService** (registered with dependency injection behind `IContactService`) handles
  search, filtering, sorting, paging, trimming input, tag clean-up, timestamps and DTO mapping.
- **DTOs** (`CreateContactDto`, `UpdateContactDto`, `PatchContactDto`, `ContactDto`, `ContactQuery`,
  `PagedResult<T>`) keep the API contract separate from the `Contact` entity. Clients can't set
  `Id`, `CreatedAt` or `UpdatedAt`.
- **Validation** uses data annotations and `[ApiController]`, so invalid input gets an automatic
  400 response.
- **Errors** are ProblemDetails (`application/problem+json`): 400 for validation, 404 for an
  unknown id or route, 500 without internal details.
- **Seed data:** 24 sample contacts (6 favorites) are loaded at startup, so the live page is never
  empty.

```
AddressBook.Api/
├─ Controllers/   ContactsController, TagsController
├─ Services/      IContactService, ContactService
├─ Data/          AddressBookContext, SeedData
├─ Models/        Contact (entity)
├─ Dtos/          request/response types
├─ Common/        TagRules, TextMatching
└─ Program.cs     DI, Swagger, error handling, PORT, forwarded headers
AddressBook.Api.Tests/   integration tests
Dockerfile               multi-stage build used by Render
```

### Endpoints

| Action | Method & route | Success |
|---|---|---|
| List / search / sort / page | `GET /api/contacts?search=&tag=&favorite=&sortBy=lastName&sortOrder=asc&page=1&pageSize=10` | 200 |
| Get one | `GET /api/contacts/{id}` | 200 |
| Create | `POST /api/contacts` | 201 + `Location` |
| Update (full) | `PUT /api/contacts/{id}` | 200 |
| Edit (partial) | `PATCH /api/contacts/{id}` | 200 |
| Delete | `DELETE /api/contacts/{id}` | 204 |
| Bulk delete | `POST /api/contacts/bulk-delete` with `{ "ids": [1, 2] }` | 200 `{ deletedCount }` |
| Favorite | `PUT /api/contacts/{id}/favorite` | 204 |
| Unfavorite | `DELETE /api/contacts/{id}/favorite` | 204 |
| Tag list | `GET /api/tags` | 200 |

**List parameters:**
- `search`: looks in first name, last name, full name, email and tags. It ignores case and
  accents, so `dagdeviren` finds `Dağdeviren` and `ihsan` finds `İhsan`.
- `tag`: exact tag match, ignoring case.
- `favorite`: `true` or `false`.
- `sortBy`: `firstName`, `lastName`, `email` or `createdAt`. Contacts without an email always come last.
- `sortOrder`: `asc` or `desc`. Ties are ordered by id.
- `page` (≥ 1) and `pageSize` (1–100). The response includes `totalCount` and `totalPages`.

Favorite and unfavorite are separate `PUT`/`DELETE` calls rather than a toggle, so repeating a
request is safe (idempotent).

## Assumptions

- Data lives only in memory. It resets whenever the app restarts or the free instance sleeps,
  and the 24 sample contacts come back on every start.
- First and last name are required. Email, phone and tags are optional.
- Limits: names up to 50 characters, email up to 100, phone up to 30, up to 20 tags of at most
  30 characters each.
- Tags are free text with no separate tag table. They are trimmed; blank tags are dropped and
  duplicates removed, ignoring case.
- `PUT` replaces every editable field: optional fields you leave out are cleared, and
  `isFavorite` becomes `false`.
- `PATCH` changes only the fields you send. Sending `null` means "leave unchanged", so you can't
  clear a field with `null`:
  - clear the phone with `""`
  - clear the tags with `[]`
  - remove an email with `PUT`
- Bulk delete ignores unknown and repeated ids, and reports how many contacts were actually deleted.
- There is no authentication, versioning or rate limiting. These were optional in the course and
  are out of scope.

## Issues encountered

- **The .NET 10 template has no Swagger UI.** From .NET 9 on, `dotnet new webapi` only generates
  an OpenAPI document. I added Swashbuckle for the UI.
- **Swagger was only on in Development.** The template enables Swagger only in the Development
  environment, so `/swagger` would return 404 in production. I enabled it in every environment.
- **Hosting.** Azure wasn't available to me, and Vercel can't run ASP.NET Core. I used Render
  with a Docker image instead; the app listens on the port in Render's `PORT` variable.
- **Turkish locale and text comparison (two bugs).** On a Turkish machine, the lowercase of `I`
  is `ı`, not `i`.
  - A naive `ToLower()` search would miss `İhsan` when searching for `ihsan`. Search and sort use
    invariant-culture, accent-insensitive comparison instead.
  - A case-insensitive regex for `sortBy` rejected `FIRSTNAME` on my machine but would have
    accepted it on the Linux server. I replaced it with an ordinal check. A test caught this
    because the tests run on a Turkish-locale machine.
- **`[Produces("application/json")]` hid ProblemDetails.** That attribute forced error responses
  to `application/json` instead of `application/problem+json`. I removed it; JSON is the default
  anyway.
- **http links behind Render's proxy.** Render terminates HTTPS and forwards plain http, so the
  `Location` header of a created contact said `http://`. Fixed with the Forwarded Headers
  middleware (`X-Forwarded-Proto`). I found this while smoke-testing the live site.
- **Windows Event Log in tests.** In a sandboxed runner, the error-handling test failed because
  the host couldn't write to the Windows Event Log. The test host now logs only to the console.
- **EF Core InMemory and filtering.** For culture-independent, accent-insensitive search, the
  small in-memory table is loaded and then filtered in .NET. With a real database, this filtering
  would be pushed into the database query.

## Run locally

Requires the .NET 10 SDK.

```bash
dotnet run --project AddressBook.Api     # then open the printed URL (redirects to /swagger)
dotnet test                              # run the 60 integration tests
```

With Docker:

```bash
docker build -t addressbook-api .
docker run --rm -p 8080:8080 addressbook-api   # http://localhost:8080/swagger
```
