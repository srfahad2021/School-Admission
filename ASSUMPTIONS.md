# Assumptions

The brief leaves several behaviors unspecified. These are the choices in this project and why they were made.

## Identity and access

There is no login. The 17-digit birth registration number is the student. The UI stores it in `localStorage` and sends it as `X-Student-Id`. Refreshing or reopening the browser restores that number, and applications are loaded from the database, so an ongoing hold is still visible.

The same page is used by the school admin and by parents. Creating a class does not require the student header. Anyone who can open the site can create a class. That matches “no login” and “all actions from the UI.”

A student id must be exactly 17 digits. Anything else returns HTTP 400. The UI strips non-digits and will not call student APIs until the value is valid.

Knowing the number is enough to view, pay, or cancel that student’s applications. There is no second secret, because the brief defines this header as the identity.

## Classes and shifts

A shift is not a separate record. “Class 6 – Morning Shift” and “Class 6 – Day Shift” are two classes, each with its own name and seat capacity. The admin types the full name when creating the class.

Class name is required and limited to 200 characters. Seat capacity must be a whole number from 1 to 100,000. No academic year, fee amount, or parent name is stored. The brief only requires a name, a capacity, and a successful payment action.

## Holds, payment, and cancellation

`HOLD_DURATION_SECONDS` sets the hold length. If the variable is missing, not a number, or not greater than zero, the duration is 600 seconds.

Applying sets `expiresAt` to the current UTC time plus that duration. Paying calls `POST /holds/{holdId}/confirm` and treats payment as successful. There is no gateway and no amount.

A student may apply to several classes at once. Within one class they may have only one active application, meaning one unexpired `HELD` row or one `CONFIRMED` row. After a hold is `EXPIRED` or `CANCELLED`, they may apply to that class again.

Confirming a hold that is already `CONFIRMED` returns 200 and does not change seat counts. That covers the idempotent-confirmation rule when the same request is sent more than once.

A confirmed admission cannot be cancelled (HTTP 409). The brief’s cancel action is for releasing a hold before payment. Cancelling an already cancelled hold returns 204 and changes nothing.

Confirming or cancelling a hold that belongs to a different student returns 403. An unknown class or hold returns 404. No seats left, or a second active application in the same class, returns 409.

`GET /holds/{holdId}` does not require the student header. The response includes `studentId` in addition to the required fields. Error bodies are `{ "error": "..." }`.

## When a seat comes back

Seat counts treat a `HELD` row as free as soon as `expiresAt` is in the past. The UI and `GET` responses report that hold as `EXPIRED` at the same moment, using the current time. A background job then writes `EXPIRED` onto the row about once a second. Callers do not need to hit an expire endpoint, and a late sweeper does not keep the seat occupied.

The countdown uses the HTTP `Date` header so a laptop clock that is ahead or behind the server does not leave “Pay Admission Fee” enabled after the server has expired the hold.

## Concurrency, including two API processes

Held seats plus confirmed seats must never exceed `totalSeats`, including when two API processes share one database.

Each apply runs in a transaction and locks the class row with `SELECT ... FOR UPDATE`. Other applies for that class, in this process or another, wait until the lock is released. The code then counts unexpired `HELD` rows and `CONFIRMED` rows. It inserts a new hold only when that sum is below `totalSeats`. Confirmation only changes an existing `HELD` row to `CONFIRMED`, so it does not consume an extra seat. PostgreSQL holds the row lock across connections, so a second process on another port is covered by the same lock.

A partial unique index on `(class_id, student_id)` for statuses `HELD` and `CONFIRMED` is a second guard against two active applications for one student in one class. Before a new insert, that student’s own already-expired `HELD` rows are marked `EXPIRED`, so the index does not block a legitimate re-apply.

## Persistence and configuration

EF Core `EnsureCreated` builds the schema on startup. Data lives in the `pgdata` Docker volume, so `docker compose down` followed by `docker compose up` keeps classes and applications. Later model changes are not migrated automatically; the volume would need to be recreated.

The API reads `ConnectionStrings__Default`. In Compose it points at the `postgres` service. `appsettings.json` has a localhost default for running the API outside Docker.

Identifiers look like `c_` or `h_` plus 12 hexadecimal characters.

`VITE_API_URL` is applied when the frontend image is built, because Vite inlines it. Changing the API URL means rebuilding the frontend image. Compose defaults it to `http://localhost:8080`, which is correct for a browser on the host talking to the published API port.

The UI polls classes and the student’s applications every 4 seconds. On top of that, the hold card switches to `EXPIRED` and disables payment as soon as the local countdown reaches zero.
