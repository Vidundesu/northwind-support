# Northwind Support Desk: Implementation Notes

**Candidate:** Vidun Nethdina Pallegoda · vinethdina77@gmail.com

## 1. What I implemented

DEFECT-117 and server-side filtering (Task 1), triage, assignment, escalation and SLA status (Task 2), JWT authentication (Task 3) and API containerisation (Task 4). Anything unfinished is under Known Limitations.

**Business rules as I understood them**
- **Filtering:** status, priority, category, customer and agent are exact-match; unassigned-only and customer-name search are also supported. All filters combine with AND.
- **Triage:** requested priority defaults to Medium; Security and Outage force Critical. SLA windows depend on priority, are halved for Premium customers, and never go below 1 hour.
- **Assignment:** the agent must be active, under their open-ticket limit, and a specialist if the category requires one. Fewest open tickets wins; ties go to the lowest agent ID. If nobody qualifies the ticket is still created (201), unassigned, with a reason.
- **Escalation:** priority +1, due date recomputed from now, owner re-evaluated, immutable history row written. Critical, Resolved or Closed returns 409; unknown ticket 404; bad reason 400.
- **SLA status** is derived from due date, resolution state and the current time. It is never stored as a status.

## 2. Design decisions

Rules live in the domain, are written once, and are shared by create and escalate. Controllers translate HTTP, application services orchestrate, and persistence only reads and writes data.

**Chosen**
- **Policy objects** (`SlaPolicy`, `PriorityPolicy`, `AgentAssignmentPolicy`): stateless and configuration-driven, so tests call them directly with no mocks.
- **Immutable value objects and domain methods** (`TriageDecision`, `SlaWindow`, `Ticket` applying a decision): a ticket cannot be left half-triaged, and create and escalate consume the same decision.
- **One SLA definition** (`SlaStatusRules`): `SlaEvaluator` delegates to it, so BR-8 exists in one place.

**Rejected**
- A Factory and four overlapping rules (see AI usage).
- Putting triage in SQL. The database filters, counts and pages; business decisions stay in the domain and are unit-tested.
- Mediator libraries, AutoMapper, or an interface per rule class.

**Assumptions (written down as the brief asks)**
- **D1:** `Ticket.SlaWindowSeconds` stores the window promised at triage. It is an input fact, not a status. Without it, "AtRisk" is wrong after an escalation, and later config changes would rewrite old promises. Existing tickets may have it null.
- **D2:** when checking whether the current agent is still eligible on escalation, the ticket itself is excluded from that agent's open count.
- **D3:** if the current agent is no longer eligible and nobody else is, the ticket becomes unassigned (BR-5), and the history row records it.
- A ticket is Breached only when `now > due`; equality is AtRisk. With a missing window it stays WithinSla.

## 3. The defect: DEFECT-117

**Root cause:** the ticket-list effect depended on search and pagination state but omitted the other filters, so changing a filter did not reliably send a request, and a later search change could send stale filter values.

**Fix:** all filters are now effect dependencies, and search is debounced while other filters fire immediately. No forced reloads, remount keys or in-memory filtering.

**Verify:** change each filter and check the request; set a status, then type and clear search and check the status is kept; typing must not send a request per keystroke; the regression test fails on the original code.

## 4. Filtering

Filters are applied in the backend query in SQL, *before* counting and paging, so items, `totalCount` and page info agree. For the Task 1 filters I confirmed in the EF Core SQL log that the `WHERE` clause is applied in the database. To add a filter, add one predicate to the query's filter step, then one entry in the frontend `emptyFilters`/`buildQuery` and one control. The `slaStatus` filter is applied in SQL the same way, but I have not yet inspected its generated SQL (see Known limitations).

## 5. Authentication

- **Passwords:** salted PBKDF2 hashes via `PasswordHasher<Agent>`, stored in a `PasswordHash` column. No plaintext anywhere, including the seeder.
- **Login:** `POST /api/auth/login` returns a JWT, expiry and agent identity. Wrong email, wrong password, inactive agent and blank input all return the same 401. An unknown email still costs one hash, so timing does not reveal which emails exist.
- **Enforcement:** one central fallback policy requires a token on every endpoint. Only login, `/health` and Swagger (in Development) are open.
- **Token:** lifetime **60 minutes**. The signing key comes from configuration (`Authentication__Jwt__SigningKey`, see `.env.example`); outside Development the app refuses to start without one of at least 32 bytes.
- **Frontend:** the token is kept in `sessionStorage` and attached in `api/client.ts`. A 401 clears the session and redirects to `/login`, and a timer signs out at expiry. Trade-off: scripts on the page (XSS) can read `sessionStorage`; an `HttpOnly` cookie avoids that but needs CSRF handling.
- **Actor:** `escalatedBy` comes from the token claims, never the request body.
- **Production:** shorter tokens, rate limiting and lockout, secure storage, and OIDC.

## 6. How to log in

- Email: `alex.turner@northwind-support.example`
- Password: `DemoOnly!ChangeMe2026`

Local development fixtures only. All five active seeded agents share this password; the inactive agent (Yuki Tanaka) cannot sign in.

## 7. Docker

A multi-stage build: the .NET 10 SDK image restores (as its own cached layer), builds and publishes; the ASP.NET runtime image runs the result as a non-root user on port 8080. `.dockerignore` keeps `bin/`, `obj/`, `node_modules/`, `.git/`, `.env` and the web app out of the build context. No secrets are in the image.

```
cp .env.example .env        # fill in local values, including the JWT key
docker compose up --build
docker compose down -v && docker compose up --build   # clean slate, destroys data
```

The API runs on `localhost:5080`. `GET /health` checks database connectivity and needs no token. To use the web app against it: `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`.

**How I verified:** **TODO:** clean `down -v` / `up --build` reaches a healthy `/health` and seeded `/api/tickets`; a one-line `.cs` change reuses the cached restore layer; `docker compose exec api whoami` is not root; image size **TODO: measured size** versus the SDK image.

**In production** I would run migrations as a separate controlled deployment step with its own permissions and rollback plan. Applying them from every API replica on startup risks races and gives the app more database rights than it needs. Here the API migrates and seeds on startup only so `compose up` works from an empty volume.

## 8. Database

Committed EF Core migrations: `AddTicketSlaWindowSeconds` (nullable column, backfilled from created and due dates) and the escalation-history table. `db/schema.sql` is kept in sync. History foreign keys use `Restrict` so audit rows are never cascade-deleted. Reads use `AsNoTracking` and narrow projections.

**Index:** `IX_TicketEscalations_TicketId_EscalatedAtUtc` (date descending) serves the one history query, a ticket's escalations newest first, with no sort step. **TODO:** confirm the index and list any others.

## 9. Testing

Run `dotnet test` from the repo root, and the frontend scripts from `package.json` (`npm test`, `npm run lint`, `npm run typecheck`).

**Tested:** priority and SLA windows for every priority and tier (including the Premium floor); assignment (fewest open, specialists, limits, inactive, ties, nobody eligible); SLA boundaries on a fixed clock; escalation rules and history; filtering with count and paging; authentication failures and protected endpoints; frontend requests, validation and error states.

**Not tested:** load, concurrency (simultaneous assignments or escalations), deployment recovery, end-to-end security. **TODO:** report any failing or unrunnable test.

## 10. AI usage

**Tools and roles**
- **ChatGPT:** understanding the defect and feature, and writing use cases before any code.
- **Claude and Codex:** building code one unit at a time, phase by phase. Claude and ChatGPT were also used back and forth to analyse the triage architecture and refine the algorithm.

**Workflow that worked:** agree a use case, then one bounded patch per unit. I applied each patch by hand, reviewed it, and tested it manually. `dotnet build` and `dotnet test` ran at the end of every patch and phase.

**Rejected or changed**
- **ChatGPT's SLA design:** a Factory with four overlapping rules. I replaced it with one set of configured, reusable rules and polymorphism where it adds value, shared by create, escalate and SLA evaluation.
- **ChatGPT's "the database does all the work":** I rejected it and kept decisions in the domain.

**Bug caught in review:** an AI design snippet for AtRisk had a missing `*` (`SlaWindowSeconds * atRiskPercent / 100`). **TODO:** add one thing you accepted largely as-is.

**Verification:** AI output was a proposal, not proof. Tests, generated SQL and manual runs decided what I merged.

## 11. Known limitations

- **10M tickets and thousands of agents:** the linear agent scan is not enough. It needs a non-linear, indexed or priority-based selection with the same eligibility rules and deterministic ties.
- **Triage flow:** it should be event-driven and idempotent so retries cannot create duplicate or stale tickets. A message queue and background workers would run triage in batches or windows, with explicit transactions and failure recovery to keep ACID guarantees.
- **Authentication:** should be OAuth 2.0 / OpenID Connect compatible, the industry standard. Excluded by the brief.
- The SQL generated for the `slaStatus` filter, and full browser end-to-end verification, are not yet confirmed.
- No concurrency token on tickets, so two simultaneous escalations can both succeed. A `rowversion` plus a 409 would fix it.
- A token stays valid until it expires, even after an agent is deactivated.
- All active seeded agents share one dev password, and there are no roles.
