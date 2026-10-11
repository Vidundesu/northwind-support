# Northwind Support Desk: Implementation Notes

**Candidate:** Vidun Nethdina Pallegoda · vinethdina77@gmail.com

## 0. Run it

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger (Development only) | https://localhost:7043/swagger |

**Log in** (local fixtures only): `alex.turner@northwind-support.example` / `DemoOnly!ChangeMe2026`. All five active seeded agents share this password; Yuki Tanaka is inactive and cannot sign in. In Swagger, call `POST /api/auth/login`, then use **Authorize** with the returned token.

Tests: `npm test` (backend and web), `npm run lint`, `npm run typecheck`. The Docker path is in section 6.

## 1. What I implemented

DEFECT-117 and server-side filtering (Task 1), triage, assignment, escalation and SLA status (Task 2), JWT authentication (Task 3) and API containerisation (Task 4). Anything unfinished is under Known limitations.

**Business rules as I understood them**
- **Filtering:** status, priority, category, customer and agent are exact-match; unassigned-only and a search over title, reference and customer name are also supported. All supplied filters combine with AND.
- **Triage:** requested priority defaults to Medium; categories flagged `ForcesCriticalPriority` (Security, Outage) force Critical. SLA windows depend on priority, are halved for Premium customers, and never go below 1 hour. All numbers come from the `Sla` configuration section; no category names are hard-coded.
- **Assignment:** the agent must be active, strictly under their open-ticket limit, and a specialist if the category requires one. Fewest open tickets wins; ties go to the lowest agent ID. If nobody qualifies the ticket is still created (201), unassigned, with a reason.
- **Escalation:** priority +1, due date recomputed from now, owner re-evaluated (kept if still eligible), immutable history row written. Critical, Resolved or Closed returns 409; unknown ticket 404; bad reason 400.
- **SLA status** is derived from due date, resolution state and an injectable clock. It is never stored.

## 2. Design decisions

Rules live in the domain, are written once, and are shared by create and escalate. Controllers translate HTTP, handlers orchestrate, and persistence only reads and writes data.

**Chosen**
- **Stateless policy classes** (`SlaPolicy`, `PriorityPolicy`, `AgentAssignmentPolicy`) that take configuration (`SlaSettings`) as input. They never read config, time or the database, so tests call them directly with no mocks. The rules differ in *data*, not behaviour, which is why they are configuration and not a class hierarchy.
- **Immutable value objects** (`TriageDecision`, `SlaWindow`, `AgentAssignmentDecision`): `Ticket` applies a decision, so a ticket cannot be left half-triaged, and create and escalate consume the same decision.
- **One SLA definition** (`SlaStatusRules`): a single expression tree used both in memory (`SlaEvaluator`) and compiled into the SQL predicate for the `slaStatus` filter, so the rule cannot drift between the list badge and the filter.
- **Command/query handlers with a repository and unit of work** (`IUnitOfWork` is the `DbContext`). Interfaces exist only at real seams: repositories, read queries and `IClock`.

**Rejected**
- A Factory with four overlapping rules, which duplicated the same logic per priority.
- Triage in SQL. The database filters, counts and pages; business decisions stay in the domain and are unit-tested.
- A polymorphic strategy per rule, a mediator library, AutoMapper, and an interface per rule class: each adds indirection without a second behaviour to justify it.

**Assumptions (written down as the brief asks)**
- **D1:** `Ticket.SlaWindowSeconds` stores the window promised at triage, and `ActiveSlaWindowSeconds` the window for the current due date. Without them "AtRisk" is wrong after an escalation, and a later config change would rewrite old promises. Existing tickets may have them null.
- **D2:** when checking whether the current agent is still eligible on escalation, the ticket itself is excluded from that agent's open count.
- **D3:** if the current agent is no longer eligible and nobody else is, the ticket becomes unassigned (BR-5), and the history row records it.
- A ticket is Breached only when `now > due`; equality is AtRisk. With a missing window it stays WithinSla.

## 3. The defect: DEFECT-117

**Root cause:** the fetch effect in `useTicketList` depended only on `search`, `page` and `pageSize` (with the lint rule silenced), so changing any other filter never re-ran it. The debounced callback also reused filters from the render that scheduled it, so a later search re-sent the old status.

**Fix:** the effect now depends on the whole applied `filters` object, so every filter requests immediately. Only the search text is debounced (300 ms), applied to the latest filters, and a newer request supersedes an older one. No forced reloads, remount keys or in-memory filtering.

**Verify:** `npm run test --workspace apps/web -- useTicketList` passes (7 tests). Restoring the original hook makes 3 of them fail (status change sends no request; status picked while a search settles; page reset on a filter change). In the app, change Status and watch `GET /api/tickets?status=...` fire at once; typing sends one request after the pause, not one per keystroke.

## 4. Filtering

All filters are one `IQueryable` in `ApplyFilters` (`TicketQueries.cs`), applied *before* `CountAsync` and paging, so items, `totalCount` and page info describe the same filtered set. Nothing filters in C#. I confirmed the Task 1 `WHERE` clause in the EF Core SQL log. The tests in `TicketQueriesFilterTests` run the filters as real SQL on in-memory Sqlite, and `SlaFilterSqlServerTranslationTests` checks that the SLA predicate translates to SQL Server SQL (`DATEADD`, both window columns).

**Adding the next filter:** add a field to `TicketQuery`, one `if` block in `ApplyFilters`; on the web add it to `TicketFilters`, `emptyFilters` and `buildQuery`, and add a control. The hook needs no change because its effect depends on the whole `filters` object. `slaStatus` was added exactly this way.

## 5. Authentication

- **Credentials:** a `PasswordHash` column on `Agent` (committed migration). A separate Users table would add a join and no benefit while agents are the only users.
- **Hashing:** passwords are hashed **and salted** with `PasswordHasher<Agent>` (ASP.NET Core Identity, PBKDF2). Each hash embeds its own random salt, so the shared dev password produces a different stored value for every agent. The database holds only hashes; the dev password appears as a documented fixture constant in the seeder, which hashes it at seed time.
- **Login:** `POST /api/auth/login` returns a JWT, its expiry and the agent's identity. Wrong email, wrong password, inactive agent, blank and oversized input all return the same 401 and message. An unknown email still costs one hash, so timing does not reveal which emails exist. `GET /api/auth/me` returns the current agent or 401.
- **Enforcement:** one central fallback policy requires a token on every endpoint. Open: login, `GET /health` (`AllowAnonymous` on the endpoint) and Swagger (Development only). Missing or invalid tokens get the same JSON problem body as every other error.
- **Token:** HMAC-signed JWT, lifetime **60 minutes** (`Authentication:Jwt:LifetimeMinutes`). The signing key comes from configuration, never a literal in code: set `Authentication__Jwt__SigningKey` (see `.env.example`) and generate one with `openssl rand -base64 48`. Startup fails if the key is under 32 UTF-8 bytes.
- **Frontend:** the token is kept in `sessionStorage` and attached in `api/client.ts`. A 401 on an authenticated call clears the session and redirects to `/login`, and a timer signs out at expiry. Trade-off: scripts on the page (XSS) can read `sessionStorage`; an `HttpOnly` cookie avoids that but needs CSRF handling, and in-memory storage would log the agent out on every refresh.
- **Actor:** `escalatedBy` comes from the token's name claim, never the request body.
- **In production I would:** use OAuth 2.0 / OpenID Connect, shorter-lived access tokens with refresh, an `HttpOnly` cookie or a backend-for-frontend, rate limiting and lockout, per-request checks that the agent is still active, and a signing key from a secret store with rotation.

## 6. Docker

A multi-stage build from the repository root: the .NET 10 SDK image restores (project files and central build files first, so it is a cached layer), builds and publishes; the ASP.NET runtime image runs the result as the image's non-root user on HTTP port 8080, with no certificates. `appsettings.Development.json` is deleted from the image, and `.dockerignore` keeps `bin/`, `obj/`, `node_modules/`, `.git/`, `.env` and the web app out of the build context.

```bash
cp .env.example .env                                   # set the values below
docker compose up --build                              # SQL Server, then the API on localhost:5080
docker compose down -v && docker compose up --build    # clean slate, destroys data
curl -i http://localhost:5080/health                   # database-aware, needs no token
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web   # web app against the container
```

| Variable | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Compose defaults to `Development`, which applies migrations, seeds demo data and enables Swagger. Anything else skips migrate and seed. |
| `ConnectionStrings__Default` | Built by Compose from `MSSQL_SA_PASSWORD`, host `sqlserver` (service name, not `localhost`). |
| `Authentication__Jwt__SigningKey` | At least 32 bytes: `openssl rand -base64 48`. |
| `MSSQL_SA_PASSWORD` | SQL Server password, shared by both containers. |

The API starts only after SQL Server is healthy (`depends_on: condition: service_healthy`). On an empty volume it migrates and seeds before serving.

**How I verified** (run from a clean checkout):

| Check | Result |
| --- | --- |
| `docker compose down -v && docker compose up --build` ends with a healthy `/health` and seeded `/api/tickets` (with a token) | _fill in_ |
| Editing one `.cs` file and running `docker compose build api` reuses the cached restore layer | _fill in_ |
| `docker image ls`: runtime image vs `dotnet/sdk:10.0` | _fill in: ___ MB vs ___ MB_ |
| `docker compose exec api whoami` | _fill in (not root)_ |

**Migrations in a real deployment:** I would run them as a separate, reviewed deployment step before rolling out the API, using an EF migration bundle or an idempotent script (`dotnet ef migrations script --idempotent`) executed by a deployment job with its own database permissions and a rollback plan. Applying them from every replica at startup risks races between replicas, gives the app DDL rights it should not need, and turns a bad migration into a crash loop. Here the API migrates and seeds on startup only so `compose up` works from an empty volume.

## 7. Database

Four committed migrations on top of `InitialCreate`: `AddTicketSlaWindowSeconds` (nullable, backfilled from created and due dates), `AddActiveSlaWindowSeconds`, `AddTicketEscalations`, and `AddAgentPasswordHash`. `db/schema.sql` is kept in sync. `TicketEscalations` has foreign keys to `Tickets` and to both agents with `Restrict`, so audit rows are never cascade-deleted. Reads use `AsNoTracking` and narrow projections.

**Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc_Id` on `(TicketId, EscalatedAtUtc DESC, Id DESC)`. It serves the one history query, a ticket's escalations newest first, without a sort step. It is the only index I added; the existing ticket indexes were already there.

## 8. Testing

Run `npm test` from the repo root (`dotnet test` plus the web tests), `npm run lint` and `npm run typecheck`.

**Tested**
- Backend: priority and SLA windows for every priority and tier (including the Premium floor); assignment (fewest open, specialists, limits, inactive, ties, nobody eligible); SLA boundaries on a fixed clock; escalation rules and history; filtering with count and paging; password hashing and verification; login failures (wrong password, unknown email, inactive agent) and the 401 body; protected endpoints with and without a token; HTTP status mapping for escalate (400, 404, 409), through the real pipeline with mocked repositories.
- Web: filter requests and debounce (the DEFECT-117 regression tests), SLA indicator, escalation form validation and server errors, history display, login validation, token attachment, 401 handling and redirect back to the wanted page.

**Deliberately not tested:** load and concurrency (simultaneous escalations or assignments), case-insensitive search (the filter tests run on Sqlite, which is case-sensitive, unlike the SQL Server default), container behaviour (checked by hand, section 6), and component-level filter bar tests, since the defect was in the hook.

## 9. AI usage

**Tools and what for**
- **ChatGPT:** understanding the defect and the feature, and writing use cases before any code. It also proposed the first SLA architecture, which I rejected (below).
- **Claude:** building the code one unit at a time, phase by phase (Task 2 units, Task 3 web client, error-handling hardening and tests). Claude and ChatGPT were used back and forth to analyse the triage architecture and refine the assignment algorithm.
- **Codex:** also used for code.

**A workflow that worked:** write the use case first, then ask for one bounded patch per unit. I applied each patch by hand, read it, ran it, and ran `dotnet build` and `dotnet test` (and lint, typecheck and web tests) at the end of every patch and phase. Small patches meant a failure pointed at one change.

**Accepted largely as-is:** the Task 3 web client (login page, session store, token handling in `client.ts`, route guard) and the auth and error-handling hardening went in almost exactly as generated. I read them, then relied on lint, typecheck and the new tests to check them.

**Changed or rejected, and why**
- ChatGPT's SLA design used a Factory with four overlapping rules that repeated the same logic. I replaced it with one set of configured, reusable policies shared by create, escalate and SLA evaluation.
- ChatGPT's "the database does all the work" would have put triage decisions in SQL where they are hard to unit-test. I kept filtering, counting and paging in SQL and decisions in the domain.

**Bugs AI introduced or I caught**
- A design snippet for AtRisk had a missing `*` in the formula (`SlaWindowSeconds * atRiskPercent / 100`); I caught it in review.
- An early version of the ticket-list hook called `setState` inside an effect; ESLint rejected it.
- The first API tests Claude wrote for Task 3 all failed with "Connection string 'Default' is missing", and so did the older auth tests: `Program.cs` reads configuration while the factory's in-memory settings are applied later. I found it by running `dotnet test`, and the fix was to supply the test settings as environment variables.

**How I verified:** AI output was a proposal, not proof. Tests (the defect tests fail on the original hook), typecheck and lint, the EF SQL log and a SQL translation test for the filters, and manual runs decided what I merged.

## 10. Known limitations

- **10M tickets and thousands of agents:** the assignment scans and sorts every agent, which is not enough. It needs a non-linear selection (an indexed or priority-queue structure) with the same eligibility rules and deterministic ties.
- **Triage flow at that scale:** it should be event-driven and idempotent so retries cannot create duplicate or stale tickets, with a message queue and background workers running triage in batches or windows, explicit transactions and failure recovery to keep ACID guarantees.
- **Authentication:** should be OAuth 2.0 / OIDC compatible, the industry standard. Excluded by the brief. A token stays valid up to 60 minutes after an agent is deactivated, and there are no roles: every signed-in agent can use every endpoint.
- **No concurrency token on tickets:** two simultaneous escalations can both succeed and write two history rows. A `rowversion` plus a 409 would fix it.
- **`slaStatus` filter:** its SQL translation is checked by a test, and it runs on Sqlite, but I have not executed it against SQL Server or read its SQL log. Full browser end-to-end verification is also not done.
- **Search** is a contains match (`LIKE '%term%'`), which cannot use an index.
- **Compose is a local setup:** it runs `Development` (Swagger on, auto-migrate), falls back to a placeholder signing key and SA password if `.env` is missing, connects to SQL Server with `Encrypt=False`, and has no health check on the API service.
- The optional SLA summary report is not implemented.
