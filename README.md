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
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create and automatically triage a ticket |
| `POST /api/tickets/{id}/escalate` | Escalate one priority level and record the actor and reason |
| `GET /api/tickets/{id}/escalations` | Escalation history, newest first |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

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
- Until Task 3 supplies an authenticated identity, escalation requests send `escalatedBy` in the
  body as the assessment specifies. No authentication behavior is included here.
- The optional SLA summary report is not implemented.

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
`20261010211823_AddTicketEscalations`. `AddActiveSlaWindowSeconds` copies only positive stored
windows; `AddTicketEscalations` adds the history table. These commands use Infrastructure as the
migrations project and Presentation as the startup project through the root `npm run ef` script.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
