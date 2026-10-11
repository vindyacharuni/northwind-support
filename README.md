# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## Assignment Status

| # | Task | Status | Summary |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | **Completed** | Root cause identified and resolved with Vitest regression test. All list filters applied in SQL Server before counting/paging. |
| 2 | **Ticket escalation & assignment** | **Completed** | Automated triage on create (BR-1 to BR-6), escalation with immutable history (BR-7), SLA status evaluation (BR-8), database migration with indexed table, and React UI. |
| 3 | **Login and authentication** | **Completed** | Full auth flow with JWT, PBKDF2 hashing, dev fixtures, central route protection, accessible `/login` page, token attachment, and 401 session clearing. |
| 4 | **Dockerise the API** | **Completed** | Multi-stage .NET 10 API image, runtime-only configuration, non-root HTTP container, SQL-gated Compose startup, and database health endpoint. |

---

## 1. Defect Fix & Root Cause (DEFECT-117)

### Report
On the ticket list, changing the Status dropdown to *In progress* sent no request to `GET /api/tickets`. Typing in the search box then sent a request including the earlier status. Clearing search desynchronized the filters again.

### Root Cause
Dropdown and selection filters in `useTicketList.ts` were coupled to the same debouncing mechanism as text search. As a result, immediate filter selections were held in local state without dispatching a network call until the search input debounce timer elapsed, or they desynchronized entirely when search was cleared.

### The Fix
Decoupled search input debouncing from immediate filter changes:
- Non-text filters (dropdowns, checkboxes) trigger immediate query dispatch.
- Text search input is independently debounced (300 ms) and merged with active filters.
- A regression test suite was added in `apps/web/src/features/tickets/hooks/useTicketList.test.ts` to assert that:
  - Immediate filter changes fire a request without waiting for debounce timers.
  - Search keystrokes do not fire individual network requests.
  - Clearing search retains all selected dropdown filters.

---

## 2. Server-Side Filtering (FL-1 to FL-6)

### Where Applied
Implemented centrally in `apps/api/src/SupportDesk.Infrastructure/Queries/TicketQueries.cs` using EF Core `IQueryable<Ticket>` extension before counting and pagination.

- **Exact Matches (FL-1):** `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`.
- **Unassigned Only (FL-2):** `unassignedOnly == true` filters by `t.AssignedAgentId == null`.
- **Search (FL-3):** Trimmed and case-insensitive check against ticket reference (`t.Reference`), title (`t.Title`), or customer name (`t.Customer.Name`). Ignored if empty or whitespace.
- **AND Combinations (FL-4):** Chained conditionally on `IQueryable`.
- **Database Evaluation & Paging (FL-5):** Filtering occurs in SQL prior to `CountAsync()` and `Skip().Take()`. No C# in-memory filtering.
- **Adding Future Filters (FL-6):** Adding a filter like `slaStatus` requires only adding one `.Where(...)` branch to `TicketQueries.cs` without altering pagination or query structures.

---

## 3. Ticket Escalation & Assignment (Task 2)

### 3.1 Business Rules Implemented
- **BR-1 Priority:** Requested priority or default *Medium*. Categories with `ForcesCriticalPriority` (Security, Outage) are automatically forced to *Critical*.
- **BR-2 SLA Window:** Due date = creation time + base window (Critical: 4 h, High: 8 h, Medium: 24 h, Low: 72 h).
- **BR-3 Premium:** Premium customers receive a 50% window reduction (`* 0.5`), with a strict 1-hour minimum floor.
- **BR-4 Specialists:** Categories flagged `RequiresSpecialist` (Billing, Outage, Security) can only be assigned to agents possessing that specialization.
- **BR-5 Assignment:** Among eligible agents (active, specialized when required, and strictly below `MaxOpenTickets`), selects the agent with the fewest open tickets (open = not Resolved or Closed). Deterministic tie-break by `agent.Id`. If no agents are eligible, ticket is created unassigned with an explicit explanation (normal outcome, returns 201).
- **BR-6 Configurable:** SLA windows, premium multiplier, floor, and at-risk threshold are bound to the `Sla` section in configuration (`SlaOptions`).
- **BR-7 Escalation:** Raises priority by one level (`Low -> Medium -> High -> Critical`). Recomputes due date starting from escalation time. Re-evaluates assigned agent: keeps current agent if eligible, otherwise reassigns per BR-5. Requires 5–500 character reason. Critical tickets or Resolved/Closed tickets cannot be escalated (returns 409 Conflict). Records immutable row in `TicketEscalations`.
- **BR-8 SLA Status:** Pure derived calculation (`NotApplicable`, `Met`, `Breached`, `AtRisk` when $\le 25\%$ window remaining, `WithinSla`). Time provider is injectable via `FixedClock` / `TimeProvider`.

### 3.2 Database & Persistence
- **Table:** `TicketEscalations` created via committed migration `20261011081619_AddTicketEscalations`.
- **Columns:** `Id`, `TicketId` (FK), `FromPriority`, `ToPriority`, `FromAgentId` (FK), `ToAgentId` (FK), `FromDueAtUtc`, `ToDueAtUtc`, `Reason` (500), `EscalatedBy` (100), `EscalatedAtUtc`.
- **Index:** Composite index `IX_TicketEscalations_TicketId_EscalatedAtUtc` on `(TicketId, EscalatedAtUtc DESC)` to ensure retrieving escalation history newest-first is an index seek without runtime table scans or sorting.

### 3.3 Frontend Components
- **`SlaIndicator`:** Reusable across list and detail pages. Uses distinct tones, labels, and accessible symbols (`✓` Met, `✕` Breached, `▲` At risk, `●` Within SLA, `—` No SLA) ensuring state is not communicated by color alone.
- **`TicketFilters`:** Added SLA status dropdown filter, applying SQL-side SLA filtering.
- **`CreateTicketPage`:** Displays automated triage outcome (applied priority, calculated due date, assigned agent or unassigned explanation).
- **`EscalationPanel`:** Detail page form with client validation (5–500 char reason, actor required; invalid input never calls API), pending state, server error / 409 display, and disabled controls when ticket cannot be escalated.
- **`EscalationHistory`:** Shows newest-first history of priority changes, agent handoffs, due date recalculations, reason, actor, and formatted dates.
- Reactive state updates in place without full page reloads.

---

## 4. Login and Authentication (Task 3)

### 4.1 Backend (AU-1 to AU-8)
- **AU-1 Credentials Storage:** Stored directly on the `Agent` aggregate (`PasswordHash` NVARCHAR(500)) via committed migration `20261011140215_AddAgentPasswordHash`.
  - *Justification:* In Northwind Support Desk, support agents are the only authenticated actors in the system. Placing credentials directly on `Agent` maintains 1:1 aggregate boundaries, eliminates redundant joins, avoids synchronization complexity between separate `Users` and `Agents` tables, and adheres to DDD principles.
- **AU-2 Hashing & Salting:** Implemented using standard .NET PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) with HMAC-SHA256, 100,000 iterations, and a 128-bit (16-byte) cryptographically secure random salt. Verifications use constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing side-channel attacks.
- **AU-3 Seed Development Credentials:**
  - *Dev Password:* **`Password123!`** (clearly a local development fixture).
  - Seeded during database initialization in `SupportDbSeeder.cs`. All seeded active agents (`alex.turner@northwind-support.example`, `ben.osei@northwind-support.example`, `priya.nair@northwind-support.example`, `sara.lindqvist@northwind-support.example`, `marcus.doyle@northwind-support.example`) use this fixture.
  - Inactive agent `yuki.tanaka@northwind-support.example` is also seeded with credentials to verify inactive account login rejection.
- **AU-4 Login Endpoint (`POST /api/auth/login`):**
  - Authenticates active agents; returns signed JWT token, expiry timestamp, and user greeting payload (`id`, `name`, `email`).
  - Returns identical HTTP 401 and generic message `"Invalid email or password."` for unknown email, wrong password, or inactive agent, eliminating user enumeration vulnerabilities.
- **AU-5 Me Endpoint (`GET /api/auth/me`):**
  - Returns authenticated agent profile resolved from JWT `ClaimTypes.NameIdentifier` claim, or HTTP 401 if unauthenticated or inactive.
- **AU-6 Central Route Protection:**
  - Configured via ASP.NET Core `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();`.
  - Every existing and new endpoint requires a valid JWT token by default without needing repetitive `[Authorize]` attributes.
  - `POST /api/auth/login` and Swagger (`/openapi`, `/swagger`) are explicitly configured with `[AllowAnonymous]`.
- **AU-7 Signing Key & Token Lifetime:**
  - Signing key is supplied at runtime through `Jwt__SigningKey`; the local development fixture is in `.env.example` and Compose passes it to the API. The production `appsettings.json` and Docker image do not contain the key.
  - **Token Lifetime:** **480 minutes (8 hours)** — chosen to represent a standard work shift for support desk agents, avoiding disruptive session expiration while preventing indefinite token exposure.
- **AU-8 EscalatedBy from Token:**
  - `POST /api/tickets/{id}/escalate` extracts the acting agent's name directly from the validated JWT token (`User.FindFirst(ClaimTypes.Name)?.Value`), ignoring any caller-supplied value in the request body.

### 4.2 Frontend (AU-9 to AU-13)
- **AU-9 `/login` Page:** Accessible form with `<label htmlFor>`, clear field validation, pending submission state, and clear error banner on 401 credentials rejection.
- **AU-10 Token Storage & Attachment:**
  - Managed in `apps/web/src/api/client.ts`. Automatically attaches `Authorization: Bearer <token>` to all HTTP requests when token is present.
  - *Where Kept:* Stored in browser `localStorage` (`northwind_support_token`).
  - *Trade-off:* Storing in `localStorage` allows client-side Single Page Applications (SPAs) to manage token injection easily without server-side cookie domain restrictions or CSRF vulnerabilities. The trade-off is vulnerability to Cross-Site Scripting (XSS) if untrusted third-party code executes in the client origin. For an internal SPA without third-party script tags, paired with input sanitization, this provides a clean, stateless balance.
- **AU-11 Route Protection & Redirection:**
  - `<ProtectedRoute>` wraps all private routes (`/tickets`, `/tickets/:id`, `/tickets/new`, `/customers`).
  - Unauthenticated requests redirect to `/login`, preserving `state: { from: location }` so agents return directly to their intended page upon login (or `/tickets` if visiting login directly).
- **AU-12 Session Header & Logout:**
  - Navigation header displays the signed-in agent's full name and a "Sign out" button that clears the token, resets context, and returns to `/login`.
- **AU-13 Central 401 Handling:**
  - Any 401 response (e.g. expired or invalid token) intercepted in `client.ts` immediately clears `localStorage` and triggers registered unauthorized handler, redirecting the user to `/login`.

---

## 5. Docker API

### Run Locally
From the repository root, create the local environment file and start SQL Server plus the API:

```powershell
Copy-Item .env.example .env
docker compose up --build -d
```

Compose builds `apps/api/Dockerfile` using the repository root as its context. On an empty
`supportdesk-sql` volume, Development startup applies EF migrations and seeds the demo data.
SQL Server must pass its health check before the API starts. The API listens on HTTP port 8080
inside the container and is published at `http://localhost:5080`; `GET /health` is anonymous and
returns JSON with overall status and database reachability. Ticket endpoints require a bearer
token. No HTTPS certificate is copied into the image.

The Dockerfile restores from the solution-wide SDK/build/package files and project files before
copying API source, allowing source-only changes to reuse the restore layer. `.dockerignore`
excludes build outputs, `node_modules`, `.git`, `.env`, and the web app. The final image uses the
ASP.NET 10 runtime and runs as the non-root `app` user.

### Runtime Configuration
The image does not contain `appsettings.Development.json` or credentials. Compose passes the
connection string, JWT signing key, issuer, audience, token lifetime, and environment name at
runtime. `.env.example` values are local-only fixtures; use an ignored `.env` for local changes
and never commit real credentials.

To proxy the Vite app to the container API, use this in a POSIX shell:

```bash
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

In PowerShell:

```powershell
$env:VITE_API_PROXY_TARGET = 'http://localhost:5080'
npm run web
```

### Production Migrations
Automatic migration and seeding run only when `ASPNETCORE_ENVIRONMENT=Development`. In a real
deployment, apply migrations in a controlled one-shot release step or migration job before
rolling out API instances. This avoids concurrent instances racing to migrate and lets the
deployment identity hold schema-modification permissions without granting them to the long-running
API identity. Production deployments should provision their own agent accounts rather than use
development fixtures.

### Verification
The following Docker checks were run on 2026-10-11 using an isolated Compose project and a fresh
SQL Server volume, preserving the existing development database:

- Both containers became healthy; the API waited until SQL Server passed its health check.
- `GET /health` returned HTTP 200 with database status `Healthy`; authenticated `GET /api/tickets` returned all 40 seeded tickets.
- After changing one `.cs` file, rebuilding showed the `dotnet restore` Docker layer as `CACHED` while source copy and publish reran.
- `docker compose exec api whoami` printed `app`, not `root`.
- Image sizes: `northwind-task4-api:latest` was 463,752,623 bytes (464 MB); `mcr.microsoft.com/dotnet/sdk:10.0` was 1,303,338,963 bytes (1.3 GB). Sizes vary with base-image patch and platform.
- The Vite proxy successfully logged in and fetched the ticket list with `VITE_API_PROXY_TARGET=http://localhost:5080`. Ports 5173 and 5174 were occupied, so that dev server selected port 5175.

For a disposable default project, `docker compose down -v` followed by `docker compose up --build` verifies a clean-volume startup; `down -v` deletes that project's SQL data. The isolated verification instead used project name `northwind-task4` and SQL host port 11433 to preserve existing data.

---

## 6. Design Decisions: Chosen, Rejected, and Why

| Decision | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| **Credentials Storage (AU-1)** | Direct `PasswordHash` column on `Agent` aggregate | Separate `Users` table or ASP.NET Core Identity tables | Single-persona domain: all authenticated users are support agents. Keeps aggregate boundaries cohesive and avoids redundant table joins or synchronization. |
| **Password Hashing (AU-2)** | Standard PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) with HMAC-SHA256, 100k iterations, 128-bit salt | Plain text, MD5, SHA-256 without salt, or custom encryption | Cryptographically vetted, resistant to rainbow tables and brute force, built into .NET BCL without external native binary dependencies. |
| **Route Protection (AU-6)** | ASP.NET Core central `FallbackPolicy` | Manually adding `[Authorize]` attributes to every controller/action | Secure-by-default posture: any newly created endpoint is automatically protected against unauthorized access unless explicitly marked `[AllowAnonymous]`. |
| **Client Token Storage (AU-10)** | `localStorage` | In-memory only or `HttpOnly` cookie | In-memory tokens are lost on browser refresh; cookies require CSRF countermeasures and complicate cross-origin deployment. `localStorage` provides persistent session within token lifetime. |
| **Triage & SLA Architecture** | Domain aggregate methods (`Ticket.Raise`, `Ticket.Escalate`) + Application services (`SlaPolicy`, `TicketTriageService`) | Everything in controller or EF DbContext hooks | Keeps business invariants inside domain entities while delegating multi-aggregate orchestration (agents, categories, customers) to application services. |
| **SLA Status Calculation** | Pure in-memory evaluation for entities; SQL expression evaluation in `TicketQueries` for list queries | Storing SLA status as a database column | SLA status is time-dependent. Storing it leads to stale data without expensive background cron jobs updating every record. |
| **Provider-Aware Date Math** | `DateDiffMinute` for SQL Server, `DateTime` arithmetic for SQLite test runner | Forcing all tests to run against Docker SQL Server | Allows fast, database-free in-memory unit tests while leveraging high-performance native SQL Server functions in production. |
| **Configuration Binding** | Decoupled `SlaOptions` and `JwtOptions` POCOs | Referencing `Microsoft.Extensions.Options` in domain | Keeps domain and application core free of third-party framework dependencies. |

---

## 7. Testing

### How to Run
```bash
npm test                      # runs both backend (dotnet test) and web (vitest) tests
npm run typecheck             # verifies full TypeScript typing
```

### What Was Tested
- **Backend (87 tests across Unit and Architecture suites):**
  - Architecture tests: Layer dependency constraints, controller authorization enforcement, and token actor extraction.
  - Security & Hashing (`PasswordHasherTests`): Password verification correctness, rejection of wrong passwords, unique salts per hash, and malformed payload rejection.
  - Authentication Handlers (`LoginCommandHandlerTests`, `GetCurrentUserQueryHandlerTests`): Correct credential verification, identical 401s for wrong email/password, inactive agent blocking, and active user profile retrieval.
  - SlaPolicy: Base windows per priority, premium customer half-window, 1-hour floor.
  - TicketTriageService: Category forcing Critical, fewest open tickets, specialist requirements, capacity limits, inactive agent exclusion, deterministic tie-breaking.
  - Escalation handler: 1-step bump, due date from now, assignee retention/reassignment, 409 rejections on Critical/Resolved/Closed tickets.
  - Ticket queries: SQL-level filtering for Breached, Met, search, and newest-first history sorting.
  - SlaEvaluator: Exact boundary tests (including $\le 25\%$ at-risk threshold).
  - PersistenceModelTests: Model snapshot matches EF migrations.
- **Frontend (17 tests across 7 test suites):**
  - Dropdown filter immediacy and debounced search behavior (DEFECT-117).
  - SLA Indicator rendering all 5 states distinguishably with distinct symbols and labels.
  - Escalation panel: short reason (<5 chars) blocks submission and never calls API.
  - Escalation panel: 409 conflict error message is rendered and ticket remains unchanged.
  - Escalation panel: non-escalatable tickets disable submission.
  - `LoginPage.test.tsx`: client validation blocks submission on empty fields, displays clear error on 401, successful login navigates to target.
  - `ProtectedRoute.test.tsx`: redirects unauthenticated visitors to `/login`, renders protected content when authenticated.
  - `client.test.ts`: attaches stored Bearer token, clears token and triggers unauthorized callback on 401, manages token storage lifecycle.

---

## 8. AI Usage Disclosure
- **Tools & Purpose:** 
- **Workflow:** Reviewed candidate assignment specifications, designed architecture collaboratively, implemented domain models, executed tests iteratively, and verified with strict TypeScript and .NET compiler checks.
- **Changes / Rejections:** AI initially attempted to use `DateDiffMinute` unconditionally, which fails in SQLite in-memory test doubles; branched by database provider name to support both SQLite and SQL Server.
- **Verification:** Every line of code was verified via `dotnet test` (87 passed), `vitest run` (17 passed), and `tsc -b --noEmit` (0 errors).

---

## 9. Known Limitations
- Self-service registration, password reset, and email verification workflows are not implemented (all accounts are provisioned agents).
- OAuth2, OpenID Connect, and SSO enterprise providers are not integrated (direct password authentication only).
- Refresh tokens and sliding session extensions are omitted; sessions expire at the 8-hour boundary.
- Multi-Factor Authentication (MFA / TOTP) is not implemented.
- Account lockout policies and rate limiting on `/api/auth/login` are not configured (recommended for public production deployments).
- Fine-grained role-based permissions matrix is not present (all authenticated agents have uniform access to the support desk).
- TLS/HTTPS certificate provisioning is delegated to reverse proxy or cloud ingress infrastructure.
- Background worker/scheduler for proactive SLA breach notifications is not implemented.

