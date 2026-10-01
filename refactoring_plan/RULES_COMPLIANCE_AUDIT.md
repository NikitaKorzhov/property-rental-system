# Rules Compliance Audit

A rule-by-rule audit of the current codebase against [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md), based on reading every controller, service, view component, and Razor view in `PropertyRentalSystem.Web`. Each section says whether the rule is followed, and lists concrete file/line evidence for every gap found — no rule is marked violated without a specific example.

## Summary

| Rule | Status |
|---|---|
| 1. Skinny Controllers | ✅ Compliant |
| 2. Business Logic in Services | ✅ Fixed (Phase 2) |
| 3. Separate Data Access | ✅ Fixed (Phase 4) |
| 4. Clean Razor Views | ✅ Fixed (Phase 2) |
| 5. DTOs / Type Safety | ✅ Compliant |
| 6. Strangler-Fig Refactoring | ➖ Not applicable to a static snapshot |
| 7. Folder / Module Boundaries | ✅ Fixed (Phase 6) |
| 8. Centralized Validation | ✅ Fixed (Phase 3) |
| 9. Project to ViewModels at the Boundary | ✅ Fixed (Phase 5) |
| 10. Services Own `DbContext` | ✅ Fixed (Phase 4) |
| 11. Codebase Hygiene & Consistency | ✅ Fixed (Phase 1) |
| 12. Short, Purpose-Focused Comments | ✅ Compliant |

---

## 1. Single Responsibility & Skinny Controllers — ✅ Compliant

No controller contains a direct `DbContext` call, LINQ query, or `SaveChangesAsync`: a repo-wide search for `_db.`, `context.`, `.Where(`, `.Include(`, `.FirstOrDefault`, `.ToListAsync`, `.SaveChangesAsync` inside `Controllers/` returns zero matches. Every action follows the accept-request → call-service → return-response shape. `ApplicationsController.BuildViewModel` and the `BuildDeleteConfirmModel` helpers in `PropertiesController`/`UnitsController` only assemble a ViewModel from data already returned by a service — that's response-shaping, not business logic, so it stays within the rule, though it does feed directly into the Rule 9 finding below (that assembly work could move into the service instead).

## 2. Extract Business Logic into Services — ✅ Fixed (Phase 2)

Originally found: the application-status "open"/"editable" business rule (`Domain/Rules/RentalApplicationRules.IsOpen` / `IsEditable`) was re-implemented inline, a second time, directly in Razor markup instead of being read from a precomputed ViewModel flag, in `Views/Applications/Wizard.cshtml:66` and `Views/Applications/Index.cshtml:65,67`. `ApplicationListItemViewModel` and `ApplicationWizardViewModel` now carry `IsEditable`/`CanWithdraw` flags computed once in `ApplicationsController` via `RentalApplicationRules`, and both views read those flags instead of re-deriving the status list themselves — verified both in the test suite (99/99 passing) and manually against a running instance across all 6 statuses (see the Phase 2 verification notes in `REMEDIATION_STATUS.md`).

**Related finding, now also fixed:** `ApplicationWizardService.SaveApplicantInfoAsync` used to hand-roll required-field and email-format checks instead of expressing them as `DataAnnotations` on `ApplicationWizardViewModel` the way every other form in the app does — Phase 2 didn't touch this, but Phase 3 did; see Rule 8 below.

## 3. Separate Data Access — ✅ Fixed (Phase 4)

See Rule 10 below (same underlying finding, from the data-access-layering angle, now fixed) — `UnitListViewComponent` and `ApplicationSummaryViewComponent` used to query `ApplicationDbContext` directly rather than through a service. Controllers were already clean (see Rule 1); view components now are too.

## 4. Keep Razor Views Clean — ✅ Fixed (Phase 2)

No view injects `DbContext` or a service directly — a repo-wide search for `@inject` across `Views/` returns zero matches, so the "query the database directly from a view" failure mode doesn't occur. The three inline status-membership checks that used to live in `Wizard.cshtml:66`, `Index.cshtml:65`, and `Index.cshtml:67` (the same "complex logical operation" flagged under Rule 2) are gone — both views now read the precomputed `IsEditable`/`CanWithdraw` flags instead. `Index.cshtml`'s now-unused `@using PropertyRentalSystem.Web.Models.Domain` was removed as part of the same fix. `Views/Shared/_StatusBadge.cshtml`'s `switch` from `ApplicationStatus` to a Bootstrap CSS class, and `Wizard.cshtml:18-20`'s `Denied ? "alert-danger" : "alert-warning"` wording choice, remain — both are purely presentational mappings (label/color, not a business decision), so they were correctly left alone.

## 5. Type Safety & DTOs — ✅ Compliant

Every `return View(...)` / `return PartialView(...)` across all controllers passes a dedicated ViewModel (`ApplicationListViewModel`, `BrowseUnitsViewModel`, `ApplicationWizardViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, `ConfirmDeleteViewModel`, etc.) — confirmed by inspecting every `View(`/`PartialView(` call site in `Controllers/`. No action renders a raw `Property`, `Unit`, or `RentalApplication` entity. Every POST action binds to a form ViewModel rather than an entity, so over-posting isn't possible.

## 6. Safe Refactoring Strategy — ➖ Not applicable to a static snapshot

This rule describes a *process* for making future changes, not a property the current code either satisfies or violates on its own. The one relevant fact: the existing 99 tests in `PropertyRentalSystem.Tests` (`Domain/Rules`, `Services`, `ViewModels`) already give the "write/verify tests first" step of this rule something real to build on — keep that coverage current as the fixes below are applied, including adding tests for any `IsEditable`/`CanWithdraw` ViewModel flags introduced to resolve Rule 2/4.

## 7. Folder / Module Boundaries — ✅ Fixed (Phase 6)

(Documented in `ARCHITECTURE_RULES.md` §7; restated here for completeness.)

Originally found: two unrelated, non-nested folders were both named "Domain" (`Domain/Rules`, `Models/Domain`), and `ViewComponents/` was flat unlike `Services/`/`ViewModels/`/`Views/`.

- **"Domain" removed from the tree entirely**, not just the collision resolved: `Models/Domain/*.cs` → `Models/*.cs` (namespace `...Models.Domain` → `...Models`, mirrored in `PropertyRentalSystem.Tests`), `Domain/Rules/*.cs` → `BusinessRules/*.cs` (namespace `...Domain.Rules` → `...BusinessRules`). `Models/` now pairs symmetrically with `ViewModels/`, the standard ASP.NET MVC convention — the old `Models/Domain/` nesting was a redundant qualifier with nothing else under `Models/` to distinguish it from. `BusinessRules` has no collision risk against "Domain" or ASP.NET's own "Policy" terminology (authorization policies). EF Core's migration snapshot/designer files embed each entity's full CLR namespace as string literals for model comparison — these were updated too, and `dotnet ef migrations has-pending-model-changes` confirms the model stayed fully in sync, so no phantom migration was generated.
- **Bonus finding, outside the original scope:** `Models/ErrorViewModel.cs` was sitting loose directly in `Models/` despite being a ViewModel by every convention in this codebase — moved to `ViewModels/Shared/ErrorViewModel.cs`, next to `ConfirmDeleteViewModel`.
- **`ViewComponents/` grouped by feature**: `ViewComponents/Units/UnitListViewComponent.cs`, `ViewComponents/Applications/ApplicationSummaryViewComponent.cs` — matching `Services/`/`ViewModels/`/`Views/`. Verified live that both still resolve correctly by their conventional string name (`Component.InvokeAsync("UnitList", ...)` / `"ApplicationSummary"`) despite the namespace/folder change — ASP.NET Core's view-component discovery is convention-based on class name, not namespace, which a build alone wouldn't have caught (it's a runtime string lookup).

(Note: `Controllers/` itself is flat, not grouped by feature — an earlier version of this audit incorrectly implied otherwise. This wasn't in Phase 6's scope since the Rule 7 finding was specifically the `ViewComponents` vs. `Services`/`ViewModels`/`Views` inconsistency.)

## 8. Centralized Validation — ✅ Fixed (Phase 3)

(Documented in `ARCHITECTURE_RULES.md` §8; restated here for completeness.)

Originally found: `RegisterViewModel`, `LoginViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, and `ResidenceHistoryFormViewModel` all validated presence/format via `DataAnnotations`/`IValidatableObject`, but `ApplicationWizardViewModel` had none — `ApplicationWizardService.SaveApplicantInfoAsync` manually checked `FullName`, `Phone`, `Email` (re-running `new EmailAddressAttribute().IsValid(email)` by hand), and `CurrentAddress` instead, reporting failures as `ServiceResult` field errors. `ApplicationWizardViewModel` now carries `[Required]`/`[EmailAddress]` on those four fields — the same `DataAnnotations` mechanism every other form uses — and `ApplicationsController.Wizard` checks `ModelState.IsValid` before calling the service, which no longer re-validates field shape at all.

**Why this fix landed in 2 commits instead of 1** (unlike every other single-commit phase so far): relocating validation across layers has a window where splitting it wrong leaves data completely unvalidated — e.g. removing the service's checks *before* the ViewModel/controller side existed would let invalid data save silently, since nothing would be checking it. The safe split is additive-first: commit 1 added the `DataAnnotations` + the controller's `ModelState.IsValid` guard, with the old service checks left in place (now redundant — the controller short-circuits before reaching them — but harmless); commit 2 removed the now-provably-dead service code once the new path was verified both by the test suite and manually against a running instance. Each commit left the app in a fully working state.

## 9. Project to ViewModels/DTOs at the Data-Access Boundary — ✅ Fixed (Phase 5)

`ARCHITECTURE_RULES.md` §9 originally named two methods as examples; a full pass found the pattern was systemic across every service interface. All five in-scope read paths now project straight to their ViewModel in the query:

| Service method | Now returns | Consumer |
|---|---|---|
| `IPropertyService.GetAllAsync()` | `List<PropertyListItemViewModel>` (Id, Name, **Address**) | `PropertiesController.LoadListAsync` (needs Address, not just Id+Name — the original "minimal Id+Name" idea in this table would have broken it), plus 3 Id/Name-only dropdowns |
| `IApplicationBrowseService.GetAvailableUnitsAsync(...)` | `List<BrowseUnitViewModel>` | `ApplicationsController.Browse` — `ExistingApplicationId` still composed by the controller afterward |
| `IApplicationWizardService.GetMyApplicationsAsync(...)` | `List<ApplicationListItemViewModel>` | `ApplicationsController.Index` — `IsEditable`/`CanWithdraw` deliberately filled in *after* `ToListAsync()`, not inside `.Select()` (see note below) |
| `IApplicationReviewService.GetFilteredAsync(...)` | `List<PmApplicationListItemViewModel>` | `ApplicationReviewController.Index` |
| `IApplicationReviewService.GetHistoryAsync(...)` | `List<StatusHistoryItemViewModel>` | `ApplicationReviewController.Details` |

**A real pitfall caught during execution:** `RentalApplicationRules.IsEditable`/`IsOpen` are plain C# static methods — EF Core's SQL Server provider cannot translate an arbitrary method call inside `.Select()`, and would throw at runtime. The `dotnet test` suite alone would **not** have caught this, since EF Core's InMemory provider evaluates LINQ very differently and tolerates method calls the real provider can't. `GetMyApplicationsAsync` was verified specifically against the real SQL Server container (not just InMemory-backed tests) before being trusted: the DB-translatable fields are projected in `.Select()`, and `IsEditable`/`CanWithdraw` are filled in with a loop over the materialized list afterward — single query, correct values, no translation risk, and Rule 2's single source of truth for the status list stays intact (no re-derived `Draft`/`Submitted`/`Returned` checks inline).

**Not a violation:** `GetByIdAsync`/`GetOwnedAsync`/`GetReviewableAsync`-style single-entity reads (`PropertiesController.Edit`, `UnitsController.Edit`, `ApplicationsController.Wizard`, `ApplicationReviewController.Review`, etc.) are legitimately entities — they're loaded immediately before an `Update`/`Delete`/status-transition call, and EF Core needs the tracked instance for that to work. Only the list/history reads above were in scope for this rule.

## 10. Services Own `DbContext`, Not ViewComponents — ✅ Fixed (Phase 4)

(Documented in `ARCHITECTURE_RULES.md` §10; restated here for completeness.)

Originally found: `UnitListViewComponent` and `ApplicationSummaryViewComponent` both had `ApplicationDbContext` injected directly into the constructor and queried it themselves inside `InvokeAsync`, bypassing the Services layer entirely — the one place in the codebase where the Rule 3 data-access boundary wasn't held (controllers were already clean, see Rule 1).

- `UnitListViewComponent` now calls `IUnitService.GetUnitsForPropertyAsync(propertyId)` — the existing single-query projection moved into `UnitService` verbatim (same SQL: one `INNER JOIN` to `UnitTypes`, confirmed via EF Core query logs — no N+1).
- `ApplicationSummaryViewComponent` now calls a new `IApplicationSummaryService.GetSummaryAsync(applicationId)`. This one needed a home that isn't role-specific, since the component is used by both the applicant's wizard Summary step *and* the manager's review details page — `IApplicationWizardService` wasn't a fit (Applicant-flow-only), so a new interface was added rather than overloading an existing one with a mismatched responsibility. The query was also upgraded from the old `.Include(...)` + in-memory mapping to a genuine single `.Select()` projection straight to `ApplicationSummaryViewModel` (applying Rule 9 properly here, not just relocating the weaker pattern) — confirmed via EF Core query logs to still be one SQL statement (a `LEFT JOIN` to `ResidenceHistories`), not N+1.

Both moves added service-level tests that didn't exist before (the logic was previously untestable outside the ASP.NET pipeline) — a net increase in coverage, not just a relocation.

## 11. Codebase Hygiene & Consistency — ✅ Fixed (Phase 1)

(Documented in `ARCHITECTURE_RULES.md` §11; restated here for completeness.)

Originally found: `Controllers/AccountController.cs` had 7 comments written in Ukrainian while the rest of the codebase comments in English (lines 21, 28, 40, 57, 61, 81, 88). **Correction to this audit's original method:** the scan that found those was scoped to `.cs`/`.cshtml` only — too narrow. Re-running it across the *entire* repository (still excluding `.git`/`bin`/`obj`/`.idea`) during Phase 1 turned up one more offender outside that scope: `PropertyRentalSystem.Web/Dockerfile` had 4 Ukrainian comments too. Both files are now fully English; a repo-wide Cyrillic scan finds nothing left in actual project files (only this plan's own documentation, which quotes the old text as historical evidence). Formatting/bracing style (brace-less single-statement guard clauses like `if (x == null) return NotFound();`) is applied uniformly across the codebase and is a deliberate, consistent house style rather than a defect — not flagged here.

## 12. Short, Purpose-Focused Comments — ✅ Compliant

A scan for `/* */` block comments across `PropertyRentalSystem.Web` returns zero matches, and a scan for runs of 4+ consecutive `//` lines in the same tree returns none anymore either — the one that used to exist, `ViewModels/Applications/ApplicationWizardViewModel.cs:6-9` (explaining why validation was done manually "rather than via DataAnnotations"), was rewritten in Phase 3 once that claim stopped being true.

---

## Suggested fix order

This list reflects `REMEDIATION_PLAN.md`'s phase order, which refined this order slightly (Phase 2 before Phase 3) based on a finer-grained complexity analysis — see that file for the authoritative sequencing and dependency notes.

1. ✅ **Rule 11** (translate the Ukrainian comments in `AccountController.cs`, and `Dockerfile`, to English) — done in Phase 1, merged to `main`.
2. ✅ **Rule 2 / 4** (add `IsEditable`/`CanWithdraw` to `ApplicationListItemViewModel`/`ApplicationWizardViewModel`, update the three view call sites) — done in Phase 2, merged to `main`.
3. ✅ **Rule 8** (move `ApplicationWizardViewModel`'s validation onto `DataAnnotations`, delete the manual checks from `ApplicationWizardService`) — done in Phase 3, merged to `main`.
4. ✅ **Rule 10** (move `UnitListViewComponent`'s and `ApplicationSummaryViewComponent`'s queries into `IUnitService`/a new `IApplicationSummaryService`) — done in Phase 4, merged to `main`. Also closed Rule 3's only open finding.
5. ✅ **Rule 9** (project the five listed read paths straight to their ViewModels in the service query) — done in Phase 5, merged to `main`.
6. ✅ **Rule 7** (resolved the "Domain" naming collision, grouped `ViewComponents` by feature) — done in Phase 6, on `refactor/phase-6-folder-namespace-cleanup` (committed, not yet pushed). The widest-reaching rename of all 6 phases (72 files touched by the namespace change alone), done last as planned to land on a settled, already-tested codebase.

All 6 phases of `REMEDIATION_PLAN.md` are now complete.
