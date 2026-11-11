# AI_USAGE

## Tools used

- **Claude (Anthropic), chat interface:** Used throughout the project as a pair-programming and planning assistant.
- **Cursor:** Used for the final check that everything is managed according to the requirement or not. 

## Where AI was used

| Area | How AI helped |
|---|---|
| Planning | Broke the assignment into phases and settled the hard design decisions first: row locking for concurrency, lazy plus background expiry, a partial unique index, and idempotent confirm |
| Backend | Drafted the EF Core model, the hold logic (`AdmissionService`), the expiry background service and the minimal API endpoints |
| Docker | Drafted the Dockerfiles, nginx config, `docker-compose.yml` and the healthchecks |
| Frontend | Drafted the API client, polling hook, toasts and the React components (class list, create form, applications with countdown) |
| Tests | Drafted the xUnit tests with Testcontainers, including the 200-request concurrency test and the two-instance test |
| Docs | Drafted `README.md`, `ASSUMPTIONS.md` and this file, from the actual code and results of the project |

**I ran every command myself, read the output, and reported errors back. The AI never ran anything against my machine. I decided what to keep and what to change.**

## Where the AI made mistakes, and how I caught and fixed them

1. **Wrong .NET target for my machine.** The AI first told me to create the project with `-f net8.0`. My .NET 10 SDK only offers `net10.0`, and the command failed. *Caught:* the CLI error. *Fix:* I used .NET 10 (the brief allows 8 or newer) and updated the Dockerfile images to `10.0`.

2. **Docker build failed after adding EF Core.** The build log showed `Microsoft.EntityFrameworkCore` and `Npgsql` not found, while the Dockerfile still used the `8.0` images and the project targeted a different version. *Caught:* the compiler errors in the Docker build output. *Fix:* added the `Npgsql.EntityFrameworkCore.PostgreSQL` package to the `.csproj` and made the Dockerfile images match the project's target framework.

3. **Configuration was read too early, so tests could not override it.** The first `Program.cs` read the connection string and hold duration at the very top of startup. The test host could not change them. *Caught:* while planning the tests, I saw the tests needed their own database and a 2-second hold. *Fix:* moved both reads into lazy service registrations (`AddDbContext` with a service provider and a `HoldOptions` factory) and updated `/health` to use the injected options.

4. **Test code did not compile.** The AI used `PostAsJsonAsync` without the `System.Net.Http.Json` using, and used the now-obsolete parameterless `PostgreSqlBuilder()` constructor. *Caught:* compiler error and warning from `dotnet test`. *Fix:* added the using and passed the image name to the builder constructor.

5. **Manual test steps raced with backend startup.** The AI's instructions had me send requests right after restarting the backend. The API was not ready yet, so `curl -s` printed nothing, and I could not tell whether expiry worked. *Caught:* by noticing empty output and an unchanged seat count that proved nothing. *Fix:* added a short wait to the steps, and later a backend healthcheck in `docker-compose.yml` so the frontend only starts once the API is ready.

6. **Test infrastructure leaked database connections.** The first test setup created a new connection pool (up to 100 connections) for each test database and never released it, while Postgres allows 100 connections by default. After the large concurrency test, the next test's app could not connect and kept retrying. *Caught:* the test log showed repeated "Database not ready (n/30)" warnings after a test finished. *Fix:* raised `max_connections` in the test container, lowered the pool size, cleared pools when each test factory is disposed, and turned off SQL logging in tests. TODO: confirm all backend tests pass after this fix, and edit this item if the real cause turned out to be different.

## Problems the AI flagged before they happened

- Browsers hide the `Date` response header from cross-origin JavaScript unless the server exposes it, so the frontend's clock-drift correction would have silently not worked. The backend CORS policy now exposes it.
- Vite environment variables are baked in at build time, so `VITE_API_URL` is passed to Docker as a build argument.
- An expired but not yet cleaned-up hold must never count as a taken seat, so every count and status check applies the expiry time directly rather than relying only on the background job.

## How I verified the AI's work

- Tested every endpoint by hand with `curl` against the running Docker stack: success cases, `400`, `403`, `404`, `409`, idempotent confirm and cancel.
- Checked the seat arithmetic after each step, for example the class counts after a confirm and after a cancel.
- Confirmed automatic expiry by watching `heldSeats` fall by itself with a 15-second hold duration, with no API calls.
- Checked persistence with `docker compose down` followed by `docker compose up`.
- Ran a clean start with `docker compose down -v` and read the startup log: schema creation, the partial unique index, and the startup order.
- Wrote automated concurrency tests (200 parallel requests, parallel confirms, two backend instances).
- TODO: temporarily remove `FOR UPDATE` from `ApplyAsync`, confirm that the concurrency test **fails**, then restore it. This proves the test really detects over-holding. If you do not do this, delete this line.
- TODO: add the manual UI checks and load test results once done (two browsers fighting over the last seat, `scripts/load.sh`).