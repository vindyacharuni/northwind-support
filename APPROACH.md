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
