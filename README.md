# School Admission System
 
An online admission system for a private school. Seats are given **first-come, first-served**. Applying places a temporary hold on a seat. Paying the admission fee confirms it, and an unpaid hold expires automatically and frees the seat.
 
- **Backend:** C# / .NET 10 (minimal API), EF Core, PostgreSQL
- **Frontend:** React 19 + TypeScript (Vite), served by nginx
- **Run with:** Docker Compose (only Docker is required)
## Quick start
 
Requirements: **Docker** with Docker Compose. Nothing else needs to be installed.
 
```bash
docker compose up --build
```
 
The first build takes a few minutes. When the logs settle, open:
 
| Service | URL |
|---|---|
| **Frontend (UI)** | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| PostgreSQL | localhost:5432 (user `school`, password `school_pw`, db `school_admission`) |
 
To run in the background instead, add `-d`:
 
```bash
docker compose up --build -d
docker compose ps            # all services should be running; postgres and backend "healthy"
docker compose logs -f backend
```
 
Stop everything:
 
```bash
docker compose down          # keeps your data
docker compose down -v       # also deletes the database volume (clean slate)
```
 
Data is stored in the named volume `pgdata`, so it **survives** `docker compose down` followed by `docker compose up`.
 
## Using the app
 
1. Type a **17-digit Birth Registration Number** in the box at the top. It is saved in the browser, so it is still there after a refresh.
2. **Create Class (School Admin):** enter a class name (for example `Class 6 - Morning Shift`) and a seat capacity. Each shift is a separate class.
3. **Classes:** shows total, held, confirmed and available seats, refreshed automatically every few seconds. Click **Apply** to hold a seat.
4. **My Applications:** shows each application's status and a live countdown. Click **Pay Admission Fee** (payment is simulated and always succeeds) or **Cancel**.
5. When the countdown reaches zero, the application shows **EXPIRED** without a page reload, Pay is no longer available, and the seat returns to the pool.
Tip: to watch expiry quickly, set `HOLD_DURATION_SECONDS=15` (see below).
 
## Configuration
 
Set these in a `.env` file next to `docker-compose.yml` (see `.env.example`) or in your shell. Everything has a default, so none are required.
 
| Variable | Default | Purpose |
|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | How long a seat hold lasts before it expires |
| `VITE_API_URL` | `http://localhost:8080` | Backend URL used by the frontend. **Build-time**, so re-run with `--build` after changing it |
| `ConnectionStrings__Default` | set in `docker-compose.yml` | PostgreSQL connection string for the backend |
 
Example:
 
```bash
echo "HOLD_DURATION_SECONDS=15" > .env
docker compose up -d --force-recreate backend
curl localhost:8080/health      # {"status":"ok","holdDurationSeconds":15}
```
 
## API
 
Base URL `http://localhost:8080`. JSON in and out. Student identity is passed in the `X-Student-Id` header (17 digits). Errors look like `{ "error": "friendly message" }`.
 
| Method | Endpoint | Description | Notes |
|---|---|---|---|
| POST | `/classes` | Create a class | Body: `name`, `totalSeats` |
| GET | `/classes` | List classes with seat breakdown | |
| GET | `/classes/{classId}` | One class with seat breakdown | |
| POST | `/classes/{classId}/holds` | Apply for a seat hold | Needs header; `409` if no seats or already applied |
| GET | `/holds/{holdId}` | Hold status | `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED` |
| POST | `/holds/{holdId}/confirm` | Pay and confirm | Needs header; idempotent |
| DELETE | `/holds/{holdId}` | Cancel a hold | Needs header; `204` on success |
| GET | `/me/holds` | All applications of the current student | Needs header |
 
Quick try (Git Bash or any shell with curl):
 
```bash
curl -s -X POST localhost:8080/classes -H "Content-Type: application/json" \
  -d '{"name":"Class 6 - Morning Shift","totalSeats":2}'
 
curl -s -X POST localhost:8080/classes/<classId>/holds -H "X-Student-Id: 20152691234567890"
```
 
## Running the tests
 
### Backend tests

Only Docker is needed. This builds a .NET SDK container and runs the tests against a temporary PostgreSQL container:

```bash
docker compose --profile test run --rm backend-tests
docker compose --profile test down     # cleanup
```

(If you have the .NET 10 SDK and Docker installed, you can also run `cd backend && dotnet test SchoolAdmission.Tests`. In that case the tests start their own PostgreSQL container with Testcontainers.)
 
They include:
 
- **Concurrency test:** 200 parallel applications for a 10-seat class. Exactly 10 succeed and 190 get `409`.
- Parallel confirms count as one confirmed seat (idempotent confirm).
- The same student cannot hold two seats in one class, sequentially or in parallel.
- Expiry releases the seat automatically and an expired hold cannot be confirmed.
- Ownership check, cancel, and input validation.
- **Two backend instances** against one database never over-hold (bonus).
### Load test (optional)
 
`scripts/load.sh` fires many concurrent applications with `curl`. Usage: `bash scripts/load.sh BASE_URL CLASS_ID FROM TO`.
 
```bash
CID=$(curl -s -X POST localhost:8080/classes -H "Content-Type: application/json" \
  -d '{"name":"Load Test","totalSeats":50}' | sed -E 's/.*"id":"([^"]+)".*/\1/')
 
bash scripts/load.sh http://localhost:8080 $CID 1 500
curl -s localhost:8080/classes/$CID
```
 
Expected: exactly `50` responses with `201`, `450` with `409`, and the class shows `heldSeats: 50, availableSeats: 0`.
 
To test **two backend processes on one database**, start a second backend and send load to both at once:
 
```bash
docker run -d --name backend2 --network school-admission_default -p 8081:8080 \
  -e "ConnectionStrings__Default=Host=postgres;Port=5432;Database=school_admission;Username=school;Password=school_pw" \
  school-admission-backend
 
bash scripts/load.sh http://localhost:8080 $CID 1 250 &
bash scripts/load.sh http://localhost:8081 $CID 251 500 &
wait
docker rm -f backend2
```
 
The total number of `201` responses across both runs is still exactly the seat count.
 
## Project structure
 
```
.
├── docker-compose.yml          # postgres + backend + frontend
├── .env.example                # configurable variables
├── backend/
│   ├── Dockerfile
│   ├── SchoolAdmission.Api/    # API, EF Core model, hold logic, expiry service
│   └── SchoolAdmission.Tests/  # xUnit + Testcontainers tests
├── frontend/
│   ├── Dockerfile              # build with Node, serve with nginx
│   └── src/                    # React app
├── scripts/load.sh             # bash load test
├── ASSUMPTIONS.md              # decisions and concurrency design
└── AI_USAGE.md                 # how AI tools were used
```
 
## How it works (short version)
 
- **No over-booking:** applying runs in a database transaction that locks the class row (`SELECT ... FOR UPDATE`), counts active seats, and inserts the hold only if there is room. Because the lock is held by PostgreSQL, it also works with several backend processes.
- **Automatic expiry:** expired holds never count as taken (checked on every read), and a background service also marks them `EXPIRED` every second.
- **One active application per student per class:** checked in code and enforced by a partial unique index.
- **Idempotent confirm:** the hold row is locked, and a repeat confirm returns `200` without changing anything.
Details and reasoning are in [ASSUMPTIONS.md](ASSUMPTIONS.md).
 
## Troubleshooting
 
| Problem | Fix |
|---|---|
| `docker compose up` fails immediately | Start Docker Desktop and wait until it is running |
| UI shows "Server is unreachable" right after start | The backend needs a few seconds to connect to the database. Wait for it to become `healthy` in `docker compose ps` |
| UI still calls the wrong API URL | `VITE_API_URL` is baked in at build time. Run `docker compose up --build` |
| Port already in use (3000, 5432 or 8080) | Stop the program using it, or change the left side of the port mapping in `docker-compose.yml` |
| Want a completely fresh database | `docker compose down -v` |
 