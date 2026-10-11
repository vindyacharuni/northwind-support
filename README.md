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
| 3 | **Login and authentication** | *Next* | Planned. |
| 4 | **Dockerise the API** | *Pending* | Planned. |

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

## 4. Design Decisions: Chosen, Rejected, and Why

| Decision | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| **Triage & SLA Architecture** | Domain aggregate methods (`Ticket.Raise`, `Ticket.Escalate`) + Application services (`SlaPolicy`, `TicketTriageService`) | Everything in controller or EF DbContext hooks | Keeps business invariants inside domain entities while delegating multi-aggregate orchestration (agents, categories, customers) to application services. |
| **SLA Status Calculation** | Pure in-memory evaluation for entities; SQL expression evaluation in `TicketQueries` for list queries | Storing SLA status as a database column | SLA status is time-dependent. Storing it leads to stale data without expensive background cron jobs updating every record. |
| **Provider-Aware Date Math** | `DateDiffMinute` for SQL Server, `DateTime` arithmetic for SQLite test runner | Forcing all tests to run against Docker SQL Server | Allows fast, database-free in-memory unit tests while leveraging high-performance native SQL Server functions in production. |
| **Configuration Binding** | Decoupled `SlaOptions` POCO | Referencing `Microsoft.Extensions.Options` in domain | Keeps domain and application core free of third-party framework dependencies. |

---

## 5. Testing

### How to Run
```bash
npm test                      # runs both backend (dotnet test) and web (vitest) tests
npm run typecheck             # verifies full TypeScript typing
```

### What Was Tested
- **Backend (71 tests across Unit and Architecture suites):**
  - Architecture tests: Layer dependency constraints.
  - SlaPolicy: Base windows per priority, premium customer half-window, 1-hour floor.
  - TicketTriageService: Category forcing Critical, fewest open tickets, specialist requirements, capacity limits, inactive agent exclusion, deterministic tie-breaking.
  - Escalation handler: 1-step bump, due date from now, assignee retention/reassignment, 409 rejections on Critical/Resolved/Closed tickets.
  - Ticket queries: SQL-level filtering for Breached, Met, search, and newest-first history sorting.
  - SlaEvaluator: Exact boundary tests (including $\le 25\%$ at-risk threshold).
  - PersistenceModelTests: Model snapshot matches EF migrations.
- **Frontend (9 tests across 4 test suites):**
  - Dropdown filter immediacy and debounced search behavior (DEFECT-117).
  - SLA Indicator rendering all 5 states distinguishably with distinct symbols and labels.
  - Escalation panel: short reason (<5 chars) blocks submission and never calls API.
  - Escalation panel: 409 conflict error message is rendered and ticket remains unchanged.
  - Escalation panel: non-escalatable tickets disable submission.

---

## 6. AI Usage Disclosure
- **Tools & Purpose:** Pair programming assistant for scaffolding boilerplate DTOs, EF Core entity configurations, and generating comprehensive unit test edge cases.
- **Workflow:** Reviewed candidate assignment specifications, designed architecture collaboratively, implemented domain models, executed tests iteratively, and verified with strict TypeScript and .NET compiler checks.
- **Changes / Rejections:** AI initially attempted to use `DateDiffMinute` unconditionally, which fails in SQLite in-memory test doubles; branched by database provider name to support both SQLite and SQL Server.
- **Verification:** Every line of code was verified via `dotnet test` (71 passed), `vitest run` (9 passed), and `tsc -b --noEmit` (0 errors).

---

## 7. Known Limitations
- Background worker/scheduler for proactive SLA breach notifications is not implemented (out of scope).
- User authentication and actor resolution from JWT will be implemented in Task 3 (currently accepts actor name).
