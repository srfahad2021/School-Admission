# School Admission System

## Run
docker compose up --build

- Frontend: http://localhost:3000
- Backend:  http://localhost:8080

## Configuration
| Variable | Default | Purpose |
|---|---|---|
| ConnectionStrings__Default | see docker-compose.yml | PostgreSQL connection string |
| HOLD_DURATION_SECONDS | 600 | Seat hold duration |
| VITE_API_URL | http://localhost:8080 | Backend URL used by the frontend (build-time) |