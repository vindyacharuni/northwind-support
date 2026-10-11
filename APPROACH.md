# Approach & Thought Process

## Task 1: Defect Fix & Server-Side Filtering

### 1.1 DEFECT-117: Filters Sometimes Do Not Take Effect

#### Problem Analysis & Root Cause
In `apps/web/src/features/tickets/hooks/useTicketList.ts`, filters were managed with a combination of debounced and non-debounced effects. Specifically:
- A single debounced timer delay was applied across all filter state updates, or state updates from dropdowns like Status were being batched or swallowed by debouncing logic intended strictly for search text input.
- When a user changed the Status dropdown to *In progress*, the state update did not trigger an immediate network request.
- When typing in the search box, the debounced search effect finally executed, packaging whatever state was currently in the hook (which included the stale or previously ignored status change).
- Clearing the search input reset the debounced term, causing the hook's request trigger to fall out of sync with the dropdown filter state again.

#### Root Cause Statement
Dropdown and selection filters were coupled to the same debouncing mechanism as text search, preventing immediate dispatch on dropdown changes and causing queries to only fire after search input debounce elapsed or to desynchronize when search was cleared.

#### The Fix
- Decoupled search input debouncing from immediate filter changes (dropdowns, checkboxes).
- Immediate filters dispatch immediately on change without waiting for search timers.
- Search input values are debounced independently (300ms) and merged with current active filters.
- Added regression unit tests in `useTicketList.test.ts` verifying that:
  1. Dropdown filter changes dispatch immediately.
  2. Search input typing is debounced and does not trigger requests per keystroke.
  3. Clearing search retains all selected dropdown filters.

---

### 1.2 Server-Side Filtering (FL-1 to FL-6)

#### Problem Analysis
`TicketQuery` accepted filter parameters (`status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `search`, and later `slaStatus`), but the repository/query handlers performed raw paging without appending `WHERE` clauses to the `IQueryable<Ticket>`.

#### Architecture & Implementation
- All filters were added directly to `apps/api/src/SupportDesk.Infrastructure/Queries/TicketQueries.cs` on the `IQueryable<Ticket>` pipeline before calling `.CountAsync()` and `.Skip().Take()`.
- Filter combinations:
  - Exact matches: `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`.
  - `unassignedOnly == true`: `t.AssignedAgentId == null`.
  - Search: trimmed, case-insensitive substring match against ticket reference (`t.Reference`), title (`t.Title`), or customer name (`t.Customer.Name`). Ignored if blank or whitespace.
  - AND combination: Each filter conditionally chains `.Where(...)` on the `IQueryable`.
- Verified that counting (`CountAsync`) executes after all filters are applied in SQL, ensuring `totalCount`, `totalPages`, and pagination indicators are exact without loading entities into C# memory.

---

## Task 2: Ticket Escalation & Assignment

### 2.1 Business Rules & Domain Architecture

#### Rule Placement (OOP & SOLID)
We separated concerns strictly across Domain, Application, and Presentation layers:
1. **Domain (`SupportDesk.Domain`):**
   - `Ticket`: Encapsulates aggregate state changes.
     - `Raise(...)`: Sets title, description, customer, category, optional agent, and due date.
     - `Escalate(...)`: Enforces the invariant that `Critical` tickets and `Resolved`/`Closed` tickets cannot be escalated (raising domain exception or invalid operation). Increments priority by exactly one step (`Low -> Medium -> High -> Critical`).
   - `TicketEscalation`: Immutable domain entity recording historical escalation snapshots: `TicketId`, `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `FromDueAtUtc`, `ToDueAtUtc`, `Reason`, `EscalatedBy`, `EscalatedAtUtc`.
   - `SlaEvaluator`: Pure, static domain service evaluating SLA status given `dueAtUtc`, `resolvedAtUtc`, `createdAtUtc`, and `nowUtc` (`NotApplicable`, `Met`, `Breached`, `AtRisk` at <= 25% of window remaining, `WithinSla`).

2. **Application (`SupportDesk.Application`):**
   - `ISlaPolicy` & `SlaPolicy`: Computes priority base SLA windows (Critical 4h, High 8h, Medium 24h, Low 72h), applies premium multiplier (0.5x) with a 1-hour floor, and evaluates SLA status using injected clock. Configurable via `SlaOptions`.
   - `ITicketTriageService` & `TicketTriageService`: Encapsulates triage logic shared between creation and escalation:
     - Priority: Requested priority (default Medium) or forced Critical if category has `ForcesCriticalPriority` (Security, Outage).
     - SLA window: Computes due date from creation or escalation time using `ISlaPolicy`.
     - Agent Assignment: Filters active agents with open ticket counts strictly below `MaxOpenTickets`. Requires specialist if category has `RequiresSpecialist`. Chooses agent with fewest open tickets with a deterministic tie-break (`agent.Id`). If no agent qualifies, tickets are created unassigned with clear human-readable explanation.
   - `RaiseTicketCommandHandler`: Invokes triage service and persists ticket with assigned agent, due date, and triage explanations.
   - `EscalateTicketCommandHandler`:
     - Checks ticket state; throws 409 Conflict if Critical, Resolved, or Closed.
     - Calculates new priority (+1 step).
     - Computes new due date starting from current escalation time.
     - Re-evaluates agent: retains current agent if still eligible under new priority/capacity, otherwise triages new agent.
     - Writes immutable `TicketEscalation` row.
     - Saves changes in single transaction and returns `EscalateTicketResponse`.

3. **Infrastructure (`SupportDesk.Infrastructure`):**
   - Added `TicketEscalations` table mapping via `TicketEscalationConfiguration`.
   - Created composite index `IX_TicketEscalations_TicketId_EscalatedAtUtc` (`TicketId`, `EscalatedAtUtc DESC`) for optimal query performance on history fetches.
   - Implemented server-side SLA status filtering in `TicketQueries.cs`. Configured provider-aware date difference calculation: SQL Server uses `DateDiffMinute` for SQL translation, while test environments (SQLite in-memory) use standard DateTime arithmetic.

4. **Frontend (`apps/web`):**
   - **`SlaIndicator`**: Reusable component rendering SLA status with distinct tone, label, and accessible symbol (`✓` Met, `✕` Breached, `▲` At risk, `●` Within SLA, `—` No SLA), ensuring information is not conveyed by color alone.
   - **`TicketFilters`**: Added SLA status dropdown filter, directly querying SQL backend.
   - **`CreateTicketPage`**: Displays clear automated triage feedback upon ticket creation (assigned agent, due date, priority, and rationale).
   - **`EscalationPanel`**: Detail page panel with client-side validation (5–500 chars for reason, required actor), pending state, server error / 409 conflict display, and disabled controls when ticket cannot be escalated.
   - **`EscalationHistory`**: Displays historical escalations newest first with before/after priority, agent, due date, reason, actor, and timestamp.
   - State updates occur reactively in place without full page reloads.

---
---

---

## Task 3: Login & Authentication

### 3.1 Problem Analysis & Requirements
The goal was to close the Northwind Support Desk to unauthenticated users and establish an end-to-end authentication flow for support agents across backend and frontend:
1. **Credentials & Persistence (AU-1, AU-2, AU-3):** Store credentials securely with vetted hashing and provide development seed fixtures.
2. **API Endpoints (AU-4, AU-5):** Issue JWTs via `POST /api/auth/login` with anti-enumeration protection and fetch current user profile via `GET /api/auth/me`.
3. **Authorization & Security (AU-6, AU-7, AU-8):** Central authorization policy requiring tokens for all endpoints by default, secure configuration from environment variables, 8-hour shift lifetime, and extraction of ticket escalation actor from token claims.
4. **Client Experience (AU-9 to AU-13):** Accessible login form, bearer token attachment in HTTP client, route guarding with return navigation, user session greeting with logout, and global 401 session expiration handling.

---

### 3.2 Architecture & Design Decisions

#### 1. Credentials Storage: Column on `Agent` vs. Separate `Users` Table (AU-1)
- **Decision:** Added `PasswordHash` (NVARCHAR(500)) directly to the `Agent` aggregate root in `SupportDesk.Domain`.
- **Rationale:**
  - In the Northwind Support Desk domain model, support agents are the only interactive users who authenticate.
  - Storing credentials on `Agent` avoids unnecessary table joins and redundant 1:1 foreign keys on every query.
  - Eliminates the need to synchronize user creation, email updates, and deactivation status across two separate aggregates.
  - If external customer self-service logins or multi-tenant SSO roles are introduced in the future, a polymorphic Identity/Users aggregate can be extracted then without overengineering the current domain model.

#### 2. Vetted Password Hashing (AU-2)
- **Decision:** Implemented `IPasswordHasher` in `SupportDesk.Infrastructure.Security` using standard .NET PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) with HMAC-SHA256, 100,000 iterations, and a 128-bit (16-byte) cryptographically secure salt.
- **Storage Format:** Standard format string `pbkdf2:sha256:100000:<salt-base64>:<hash-base64>`.
- **Side-Channel Mitigation:** Hash verification employs `CryptographicOperations.FixedTimeEquals` to ensure constant-time comparison, preventing timing-based attack vectors.

#### 3. Seed Development Credentials (AU-3)
- **Decision:** In `SupportDbSeeder.cs`, passwords are dynamically hashed at database seed time using `PasswordHasher`.
- **Fixture Password:** **`Password123!`** — an obvious, standard local development fixture documented in `.env.example` and `README.md`.
- **Seeded Accounts:** All seeded active agents (`alex.turner@northwind-support.example`, `ben.osei@northwind-support.example`, `priya.nair@northwind-support.example`, `sara.lindqvist@northwind-support.example`, `marcus.doyle@northwind-support.example`) can immediately authenticate using this password. Inactive agent `yuki.tanaka@northwind-support.example` is also seeded to verify inactive account rejection.

#### 4. Clean Architecture Boundaries
- **Domain (`SupportDesk.Domain`):** `Agent` aggregate encapsulates `PasswordHash` and provides `SetPasswordHash(...)`. It holds zero dependencies on ASP.NET Core or third-party auth packages.
- **Application (`SupportDesk.Application`):** Declares `IPasswordHasher` and `ITokenService` abstractions. Implements `LoginCommandHandler` and `GetCurrentUserQueryHandler`. Throws domain-appropriate `UnauthorizedException`.
- **Infrastructure (`SupportDesk.Infrastructure`):** Implements `IPasswordHasher` using pure BCL cryptography without web dependencies.
- **Presentation (`SupportDesk.Presentation`):** Implements `ITokenService` (`JwtTokenService`) with `System.IdentityModel.Tokens.Jwt` and `Microsoft.AspNetCore.Authentication.JwtBearer`. Maps `UnauthorizedException` to HTTP 401 ProblemDetails.

#### 5. Central Authorization Enforcement (AU-6)
- **Decision:** Configured ASP.NET Core central `FallbackPolicy`:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```
- **Rationale:** Ensures a secure-by-default posture. Every current and future controller action requires a valid JWT unless explicitly decorated with `[AllowAnonymous]`. Only `AuthController.Login` and Swagger endpoints bypass the policy.

#### 6. JWT Configuration & Token Lifetime (AU-7)
- **Configuration:** Bound via `JwtOptions` from `appsettings.json`, `appsettings.Development.json`, and `.env.example`.
- **Lifetime:** **480 minutes (8 hours)** — chosen to match a standard full support agent work shift, preventing mid-day session expiration while avoiding indefinitely valid tokens.

#### 7. Actor Resolution for Escalation (AU-8)
- `POST /api/tickets/{id}/escalate` extracts the authenticated actor from `User.FindFirst(ClaimTypes.Name)?.Value`, ignoring any client-provided actor string in the request body to maintain audit integrity.

#### 8. Client Token Storage & Trade-Offs (AU-10)
- **Storage Location:** Browser `localStorage` under `northwind_support_token`.
- **Trade-off:**
  - *Benefits:* Simple, stateless, works cleanly across API origins, and avoids CSRF vulnerabilities since browsers do not automatically send `localStorage` tokens with cross-site requests.
  - *Risk:* Vulnerable to token exfiltration if the application suffers from Cross-Site Scripting (XSS).
  - *Alternative Considered:* `HttpOnly` cookies. Cookies prevent script-based XSS access, but require SameSite cookie configurations, anti-forgery CSRF tokens, and complex CORS credentials setups. In an internal SPA without untrusted user-submitted HTML scripts, `localStorage` provides a robust, idiomatic balance.

#### 9. Frontend Route Guarding & Central 401 Handling (AU-11, AU-13)
- `<ProtectedRoute>` intercepts unauthenticated access, saving the target route in `state: { from: location }`. Upon login, `LoginPage` redirects back to `from` or falls back to `/tickets`.
- `apps/web/src/api/client.ts` intercepts HTTP 401 responses, immediately calls `clearStoredToken()`, and notifies registered unauthorized listeners to update `AuthContext` state and redirect to `/login`.

---

### 3.3 Testing Strategy
1. **Backend Tests (87 tests passing):**
   - `PasswordHasherTests`: Correct hash generation, deterministic verification, wrong password rejection, and malformed hash resilience.
   - `LoginCommandHandlerTests`: Valid credentials issuance, identical 401 rejection on wrong email and wrong password (anti-enumeration), and inactive agent rejection.
   - `GetCurrentUserQueryHandlerTests`: Successfully retrieves active agent profile; throws 401 on unknown or deactivated agent.
   - `ControllerAuthTests`: Verifies central policy rejects unauthenticated calls with 401 and validates actor claim extraction.
2. **Frontend Tests (17 tests passing):**
   - `LoginPage.test.tsx`: Validates form inputs before submission, displays clear error message on 401 credentials rejection, and redirects to intended route on success.
   - `ProtectedRoute.test.tsx`: Prevents access and redirects to `/login` when unauthenticated; renders nested content when authenticated.
   - `client.test.ts`: Verifies Bearer header attachment, automatic token clearance on 401, and storage lifecycle management.
