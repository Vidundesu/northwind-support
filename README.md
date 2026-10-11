# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

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
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Docker API

The API image is built from the repository root and runs as the non-root user included in the
ASP.NET runtime image. It listens only on HTTP port 8080 in the container; Compose publishes it
at http://localhost:5080. TLS belongs at the reverse proxy/load balancer in a deployed environment.

```bash
# Starts SQL Server, then builds the API and waits for SQL Server to be healthy before starting it.
docker compose up --build

# In another terminal: database-aware, unauthenticated health check.
curl -i http://localhost:5080/health

# The React dev server proxies to the containerised API.
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

Compose supplies the SQL Server hostname (`sqlserver`), connection string, environment name, and
JWT signing key as environment variables; none are embedded in the image. Copy `.env.example` to
`.env` and replace `Authentication__Jwt__SigningKey` before using this beyond local development.
When Compose runs with `ASPNETCORE_ENVIRONMENT=Development`, the existing startup behavior applies
EF migrations and seeds the demo data, including on an empty `supportdesk-sql` volume.

For a real deployment, run a reviewed, versioned EF migration job (or generate and have the
deployment platform execute an idempotent SQL migration script) before rolling out the API. That
makes schema changes deliberate, observable, and recoverable rather than allowing every API
replica to race to change production data at startup. Production should not use the Development
environment or automatic demo seed path.

### Docker verification

From a clean local checkout, I verify the container path with:

```bash
docker compose down -v && docker compose up --build
curl -f http://localhost:5080/health
docker compose exec api whoami                 # must not print root
docker image ls                                # compare the API runtime image with dotnet/sdk:10.0
```

After editing one `.cs` file, rerun `docker compose build api`; Docker should report the
`dotnet restore` layer as cached because project files and central build inputs are copied before
the remaining source. With a JWT obtained from `POST /api/auth/login`, `GET /api/tickets` confirms
the seeded data is reachable. On this Apple Silicon build, `northwind-support-api` is **483 MB**;
the SDK is used only in the discarded build stage. Image sizes vary with the base-image digest and
Docker platform, so record your own `docker image ls` result when verifying another environment.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with database-side `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. |
| `POST /api/auth/login` | Validates an active seeded agent and issues a signed JWT. |
| `GET /api/auth/me` | Returns the current agent from the validated token. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create and automatically triage a ticket |
| `POST /api/tickets/{id}/escalate` | Escalate one priority level and record the actor and reason |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

### Task 3 — authentication

- The six existing agents are the login identities. The documented development password is
  `DemoOnly!ChangeMe2026` for seeded agents; Yuki Tanaka is inactive and cannot authenticate.
- Passwords are stored as ASP.NET Core Identity `PasswordHasher<Agent>` hashes. Salt generation and
  verification are handled by the framework; plaintext passwords are not written to the database.
  The API seeder generates hashes at runtime and backfills empty hashes on existing development data.
- `POST /api/auth/login` returns a signed JWT, expiry, and agent identity. Invalid email and password
  share the same generic `401` response. `GET /api/auth/me` returns the validated token identity.
- A fallback authorization policy protects API endpoints centrally. Login is explicitly anonymous;
  Swagger and `GET /health` are explicitly anonymous; the health endpoint returns `503` when SQL
  Server cannot be reached. Escalation history uses the authenticated agent name, never the caller's
  submitted actor string.
- Access tokens expire after 60 minutes. JWT issuer/audience/expiry are configured in application
  settings; the development signing key is explicitly development-only. In non-Development environments,
  provide `Authentication__Jwt__SigningKey` through the process environment with a random secret of at
  least 32 UTF-8 bytes. 

#### Web client (AU-9 to AU-13)

- `/login` has labelled fields, client validation (email format, password required), a pending
  state and one generic error for rejected credentials (the API's own message is shown).
- **Token storage:** `sessionStorage` (`northwind.session`: token, expiry, user). It survives a
  refresh but not closing the tab, and is not shared between tabs. Trade-off: any script running on
  the page (XSS) can read it, which an `HttpOnly` cookie would prevent, at the cost of CSRF handling
  and a cookie-issuing backend. In-memory only would be safer still but signs the agent out on every refresh.
- `apps/web/src/api/client.ts` attaches `Authorization: Bearer <token>` to every call. Login opts
  out (`anonymous: true`) so a wrong password is a form error, not a "session expired".
- A 401 on an authenticated call clears the session; the session store is observable, so
  `RequireAuth` re-renders and redirects to `/login` without the API layer knowing about routing.
  A timer also signs the agent out when the token expires.
- `RequireAuth` guards every route except `/login` and remembers the requested location; after
  sign-in the agent lands there (same-site paths only), otherwise on `/tickets`.
- The header shows the signed-in agent's name and a **Log out** button that clears the session.

### Task 2 decisions

- One domain `SlaStatusRules` expression drives both in-memory ticket projections and the
  `slaStatus` database filter. The filter is applied before count and paging.
- `Tickets.SlaWindowSeconds` preserves the original SLA duration. `ActiveSlaWindowSeconds`
  tracks the duration associated with the current due date, so an escalation can reset the due
  date without changing the original commitment or applying its old duration to the new risk
  threshold.
- Agent workloads are grouped in SQL once, then combined with the agent specialization rows.
  Ties use the lowest agent id; an eligible current owner is retained during escalation.
- `TicketEscalations` uses a composite `(TicketId, EscalatedAtUtc DESC, Id DESC)` index for the
  newest-first history lookup. Ticket and optional agent references use restrictive deletes.
- Escalation history records the authenticated agent name from the JWT claims; the API and UI no longer accept an actor name supplied by the caller.
- The optional SLA summary report is not implemented.

### Task 3 — error contract and known limitations

- **Error shape:** every failure is a JSON problem document with a `traceId`: `400` validation
  (`errors` per field), `401` missing/invalid/expired token or bad login, `404` unknown ticket,
  `409` Critical or Resolved/Closed escalation, `500` generic. A missing or invalid token also gets
  this body (JwtBearer's default is an empty 401), so clients parse one error shape.
- **Login failures:** wrong email, wrong password, inactive agent, blank fields and oversized
  input (email over 254 or password over 128 characters) all return the same `401` and message.
  An unknown email still does one password hash, so response time does not reveal which emails exist.
  Blank fields are `401`, not `400`: the response must not hint which half was wrong.
- **Escalation reason:** validated at the API edge by `EscalateTicketRequestValidator` (400 with a
  `Reason` field error); the handler repeats the check so it is safe when called directly.
- **Swagger:** an Authorize button accepts the token from `POST /api/auth/login`.
- **Known limitations:** a token stays valid for up to 60 minutes after an agent is deactivated;
  there is no concurrency token on tickets, so two simultaneous escalations of one ticket can both
  succeed and write two history rows (a `rowversion` column and a `409` on conflict would fix it);
  no refresh tokens, lockout or rate limiting, and no roles, as the assignment excludes them.
  Docker packaging intentionally does not include Kubernetes/Helm, registry publishing, CI/CD,
  HTTPS inside the container, Docker secrets/vault integration, multi-architecture builds, or a
  containerised React app.

### Migration review

Development startup currently applies migrations and seeds automatically. Do not start the API
against an unverified database. Before manually applying anything, inspect the effective target
and migration history, back up valuable data, and reconcile databases created from `db/schema.sql`
that do not have matching `__EFMigrationsHistory` rows. The hand-maintained schema has legacy FK
constraint names and server defaults that are not emitted by `InitialCreate`; check the actual
objects and defaults instead of assuming the schema script is an exact migration baseline. Do not
blindly apply the full chain to a schema that already contains the tables.

From the repository root, the offline commands are:

```bash
npm run ef -- migrations list --no-connect
npm run ef -- migrations has-pending-model-changes
npm run ef -- migrations script 0 AddTicketEscalations --output /tmp/northwind-task2.sql
npm run ef -- dbcontext info
```

`migrations list --no-connect` lists code migrations without reporting which ones a database has
applied. `dbcontext info` resolves the provider, server and database from `ConnectionStrings:Default`
without opening a database connection; do not share the connection string if separately
inspecting configuration. After manually verifying that target and its history (and backing it
up if it contains data), applying the migrations is a separate operator action:

```bash
npm run ef -- database update
```

The migration chain is `20260926183612_InitialCreate`,
`20261010181404_AddTicketSlaWindowSeconds`, `20261010211205_AddActiveSlaWindowSeconds`,
`20261010211823_AddTicketEscalations`, and `20261011050000_AddAgentPasswordHash`.
`AddActiveSlaWindowSeconds` copies only positive stored windows; `AddTicketEscalations` adds the
history table and `AddAgentPasswordHash` adds credentials. These commands use Infrastructure as the
migrations project and Presentation as the startup project through the root `npm run ef` script.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
