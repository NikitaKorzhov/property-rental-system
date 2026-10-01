# Further Improvements — Execution Plan

**✅ Complete.** All 5 commits executed, verified (build/test after each; a real-SQL-Server check via Docker for the lease-filtering commits), pushed to `refactor/further-improvements` — pending merge. Implements the findings in [`FURTHER_IMPROVEMENTS.md`](FURTHER_IMPROVEMENTS.md). Kept for historical reference; exact code is in the repo, not duplicated here.

## Commit 1 — `ReviewAsync`: switch instead of if/else, lease check via SQL

Added `LeaseRules.IsActiveOn(date)` — an `Expression<Func<Lease,bool>>` twin of `CoversDate`, since EF Core can translate an expression tree into SQL but not a call to an arbitrary method. `ReviewAsync`'s `if`/`else if`/`else` became a `switch` with a `default: throw ArgumentOutOfRangeException(...)`; its lease check now uses `AnyAsync(LeaseRules.IsActiveOn(today))` instead of loading leases into memory. New tests: a lease-ends-exactly-today boundary case, and a `LeaseRulesTests` theory asserting `IsActiveOn`/`CoversDate` never drift apart. Verified live against real SQL Server (not just the InMemory test provider) since this exact category of change — a `BusinessRules` predicate used inside a query — is what Phase 5 found the InMemory provider can mask a translation failure for.

## Commit 2 — `GetDetailsAsync` replaces `GetByIdAsync`

`ApplicationReviewController.Details` loaded a full `RentalApplication` to read 2 fields. Added `GetDetailsAsync`, a `.Select()` projection straight to `ApplicationDetailsViewModel`; deleted `GetByIdAsync` (zero other callers, zero existing tests). Two new tests cover found/not-found.

## Commit 3 — Group `Controllers/` by feature

Moved `Properties`/`Units`/`Applications`/`Review`/`Account` controllers into matching subfolders and namespaces (`HomeController`/`ModalFormControllerBase` stay at the root). The 4 controllers inheriting `ModalFormControllerBase` each needed `using PropertyRentalSystem.Web.Controllers;` added. Caught during execution: `UnitsController` referenced `PropertiesController` by name (`nameof(...)`) for a redirect URL — a same-namespace dependency invisible before the move, needing its own added `using`. Verified by routing through every moved controller's actions against the running app.

## Commit 4 — Close the remaining 3 lease-filter occurrences

Same `LeaseRules.IsActiveOn`, applied to `ApplicationWizardService.SubmitAsync` and `ApplicationBrowseService.StartApplicationAsync`/`GetAvailableUnitsAsync`. The last one is system-wide (not one unit), so it became a correlated subquery — `_db.Units.Where(u => !_db.Leases.Where(l => l.UnitId == u.Id).Any(IsActiveOn(today)))` — collapsing two queries + an in-memory `HashSet` into one `NOT EXISTS` SQL statement. Verified live: Browse excludes exactly the leased units, and a direct POST to start an application on one is still rejected.

## Commit 5 — Enforce braces via `.editorconfig`

Added a repo-root `.editorconfig` (`csharp_prefer_braces = true:warning`, `dotnet_diagnostic.IDE0011.severity = warning`), then ran `dotnet format style --diagnostics IDE0011` to mechanically brace every remaining single-statement body across 14 files — zero logic change, confirmed by inspection and an unchanged test count.

## What this plan closed

| Reviewer point | Closed by |
|---|---|
| Scattered if/if/else | Commit 1 |
| DB over-fetching | Commit 2 |
| Folder structure unclear | Commit 3 |
| In-memory filtering instead of SQL | Commits 1 + 4 |
| Missing `{}` | Commit 5 |
