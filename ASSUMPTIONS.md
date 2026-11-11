# ASSUMPTIONS

Decisions I made where the brief was silent or ambiguous, with the reasoning.

## 1. Stack and setup

- **.NET 10** (the brief allows ".NET 8 or newer"), minimal API, EF Core with the Npgsql provider. The Dockerfile uses the matching `sdk:10.0` / `aspnet:10.0` images.
- **`EnsureCreated` instead of migrations.** It builds the schema automatically on startup, which suits a `docker compose up` workflow. The trade-off is that a later schema change would need `docker compose down -v`. For a one-shot assignment this is acceptable.
- **Startup retry.** The backend retries the database connection (up to 30 times, 2 s apart) so it survives Postgres starting slowly. Compose also has a Postgres healthcheck so the backend start after the database is ready and frontend only starts once the API is ready.
- **Configuration** is via environment variables: `ConnectionStrings__Default`, `HOLD_DURATION_SECONDS` (default 600, invalid or non-positive values fall back to 600) and `VITE_API_URL`. Vite variables are baked in at build time, so `VITE_API_URL` is passed as a Docker build argument. It must be the URL the *browser* uses (`http://localhost:8080`), not the container name.
- **Ports:** frontend `3000`, backend `8080`, Postgres `5432`. Data lives in the named volume `pgdata`, so it survives `docker compose down` (but not `down -v`).

## 2. Identity and validation

- There is no login. The student's identity is the `X-Student-Id` header, taken on trust. Anyone who knows an ID can act as that student. This matches the brief and I did not add authentication.
- `X-Student-Id` must be **exactly 17 digits**, otherwise `400`. The UI enforces the same rule and disables Apply until the ID is valid.
- `POST /classes`: `name` must be non-empty (max 200 characters after trimming) and `totalSeats` a whole number from 1 to 100,000, otherwise `400`. Class names do not have to be unique, and capacity cannot be edited after creation (not in the spec).
- Creating a class needs no admin login. The UI exposes it to everyone, as the brief says admin and parents both use the same UI.
- IDs are a prefix plus 12 random hex characters (`c_...`, `h_...`).

## 3. API behaviour

| Situation | Response |
|---|---|
| Unknown class or hold ID | `404` |
| No seats left on apply | `409` |
| Student already has an active hold or confirmed seat in that class | `409` |
| Confirming or cancelling someone else's hold | `403` |
| Confirming an expired or cancelled hold | `409` |
| Cancelling a confirmed admission | `409` (see below) |
| Missing or invalid student ID, bad body | `400` |

- All errors return JSON in the form `{ "error": "friendly message" }`. Unexpected errors return a generic `500` message without internal details.
- Responses include a few extra fields (for example `studentId` on `GET /holds/{id}`). No specified field was renamed or removed.
- `GET /holds/{id}` and the class endpoints need no student header, as in the spec.
- "Active" means `HELD` and not yet expired, or `CONFIRMED`.
- A student can hold seats in **different** classes at the same time (for example Class 6 Morning and Class 6 Day), because each shift is a separate class. The one-active-application rule is per student per class.
- After an application expires or is cancelled, the same student may apply to that class again.
- **A confirmed admission cannot be cancelled.** The brief doesn't allow or describe refunds, so I treat payment as final.
- **Cancel is idempotent** on an already-cancelled hold. Cancelling an already-expired hold returns `409`.
- Payment is simulated: `POST /holds/{id}/confirm` is the "payment succeeded" event.
- **Confirm is idempotent:** repeating it on a confirmed hold returns `200 CONFIRMED` and changes nothing.

## 4. Concurrency

**The rule:** `heldSeats + confirmedSeats` never exceeds `totalSeats`.

- Applying for a seat runs in one database transaction that starts with `SELECT ... FROM classes WHERE id = @id FOR UPDATE`. This locks the class row. Concurrent applications for the same class queue on that lock. Each one then counts the active seats and inserts its hold only if there is room, so no two requests can both see "one seat left".
- Confirm and cancel lock the **hold** row (`FOR UPDATE`) and then check owner, status and expiry. Parallel confirms are serialised, and only the first one changes the status. That is why `confirmedSeats` rises by exactly one.
- As a second safety net, a **partial unique index** on `holds(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` makes it impossible for one student to have two active holds in a class, even if the application code had a bug. A violation of this index is turned into a `409`.
- Locks apply per class, so applications for different classes don't block each other.

### Bonus: multiple backend processes

The locking is done by **PostgreSQL**, not by application memory (no `lock` statements or in-process queues). Two backend processes pointed at the same database take turns on the same row lock, so the invariant holds across processes as well.

I verified this with:
- an automated test that starts two API instances against one database and sends 200 simultaneous applications, split between them, to a class with 10 seats (exactly 10 succeed);
- a manual load test with two backend containers on ports 8080 and 8081 (results in the README / section 8).

Startup note: two instances starting at the same instant on an empty database may race to create the tables. One may fail its first attempt and succeed on retry, which is one reason the startup retry loop exists.

## 5. Hold expiry

- Hold duration comes from `HOLD_DURATION_SECONDS`. `expiresAt` is set at application time as `now + duration`.
- Two layers make expiry automatic, with no API call needed:
  1. **Correctness (lazy):** every seat count and status check treats a `HELD` row with `expires_at <= now` as expired. A seat is free the instant its hold expires, even if no cleanup has run yet.
  2. **Tidiness (background):** a hosted service runs every second and marks overdue holds as `EXPIRED` using a single idempotent `UPDATE`, so it is safe with several instances running.
- A student's own stale holds are cleared inside the apply transaction so they cannot block the unique index.

## 6. Time handling

- The **backend UTC clock** is the single time source. All expiry decisions are made on the server, and timestamps are returned in UTC ISO format.
- The frontend countdown shows time remaining using *server* time: browser time plus an offset calculated from the `Date` response header. This keeps the timer correct even if the user's PC clock is wrong. The backend exposes the `Date` header through CORS for this (browsers hide it otherwise).
- The `Date` header has one-second precision, so the countdown can be off by up to about a second. The server is always the authority. If the browser thinks a hold expired but the server disagrees (or the reverse), the next refresh corrects the screen, and the server rejects any invalid confirm anyway.

## 7. Frontend

- **Student ID** is stored in `localStorage` and restored on reload. The applications list is fetched from the server using that ID, so closing and reopening the browser still shows ongoing applications.
- **Polling:** classes and "My Applications" refresh every 4 seconds. They also refresh immediately after any action (apply, pay, cancel, create).
- **Countdown** ticks every second. At zero the card shows `EXPIRED` locally, Pay and Cancel are hidden, and a refresh is triggered to confirm with the server.
- Buttons are disabled while a request is in flight to prevent double clicks.
- The Apply button reads "Applied" if the student already has an active application in that class, and "Full" when no seats are available. The server still enforces both rules.
- **Errors** appear as toasts, using the server's friendly message where available. A red banner shows when the server is unreachable, and the page keeps retrying by itself.
- No UI component library or state library was used, to keep the build small. Design is deliberately simple, as the brief says aesthetics aren't critical.

## 8. Test and load results

- Backend tests (`dotnet test`, using a real PostgreSQL container via Testcontainers) cover: 200 parallel applies against 10 seats, parallel idempotent confirm, duplicate application (sequential and parallel), expiry and release of seats, ownership check, cancel, input validation, and the two-instance test.

## 9. Known limitations

- No authentication or authorisation (by design of the brief). IDs are trusted.
- `EnsureCreated` means no migration history.
- Seat counts are computed on read rather than stored in a counter column. This is simple and always consistent, and fine for the expected scale. A very large deployment might cache or denormalise them.
- Polling (4 s) rather than WebSockets, so another user's changes can take up to 4 seconds to appear. The server is still always correct.
