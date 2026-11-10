# AI usage

Cursor, with Grok 4.7, was used on this repository for two things:

- Comparing the existing backend, frontend, and Docker setup with the assignment brief.
- Writing `ASSUMPTIONS.md` and this file from the behavior already implemented in the code.

The application code was already committed before that pass. These documents describe that code. They do not replace it, and this session did not change the API, the seat-locking logic, or the UI.

## What was checked by hand

The review read the endpoint mappings, `AdmissionService`, the EF model, the expiry background job, `docker-compose.yml`, and the React screens. Claims in `ASSUMPTIONS.md` were taken from those files: the class row lock, the 600-second default, the 17-digit check, the partial unique index, the named Postgres volume, and the 4-second poll.

The gap that review found was in the deliverables, not in those behaviors. There were no automated backend tests, and these two markdown files were absent. `README.md` also does not yet say how to run tests, because there is no test project to run.

## Mistakes

No application code was generated in this session, so there was no AI code change to compile, run, and correct here.

One documentation risk did come up. It would have been easy to invent a story about bugs the model made while building the API. That was not written down. This file only records the review and the writing of the assumption notes. If an earlier session with another model produced the C# or React code, that session is not described here, because its prompts and corrections were not available while writing this file.
