# Rules Compliance Audit

A rule-by-rule audit of the current codebase against [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md), based on reading every controller, service, view component, and Razor view in `PropertyRentalSystem.Web`. Each section says whether the rule is followed, and lists concrete file/line evidence for every gap found — no rule is marked violated without a specific example.

## Summary

| Rule | Status |
|---|---|
| 1. Skinny Controllers | ✅ Compliant |
| 2. Business Logic in Services | ✅ Fixed (Phase 2) |
| 3. Separate Data Access | ⚠️ Partial violations |
| 4. Clean Razor Views | ✅ Fixed (Phase 2) |
| 5. DTOs / Type Safety | ✅ Compliant |
| 6. Strangler-Fig Refactoring | ➖ Not applicable to a static snapshot |
| 7. Folder / Module Boundaries | ❌ Violations found |
| 8. Centralized Validation | ✅ Fixed (Phase 3) |
| 9. Project to ViewModels at the Boundary | ❌ Systemic violations found |
| 10. Services Own `DbContext` | ❌ Violations found |
| 11. Codebase Hygiene & Consistency | ✅ Fixed (Phase 1) |
| 12. Short, Purpose-Focused Comments | ✅ Compliant |

---

## 1. Single Responsibility & Skinny Controllers — ✅ Compliant

No controller contains a direct `DbContext` call, LINQ query, or `SaveChangesAsync`: a repo-wide search for `_db.`, `context.`, `.Where(`, `.Include(`, `.FirstOrDefault`, `.ToListAsync`, `.SaveChangesAsync` inside `Controllers/` returns zero matches. Every action follows the accept-request → call-service → return-response shape. `ApplicationsController.BuildViewModel` and the `BuildDeleteConfirmModel` helpers in `PropertiesController`/`UnitsController` only assemble a ViewModel from data already returned by a service — that's response-shaping, not business logic, so it stays within the rule, though it does feed directly into the Rule 9 finding below (that assembly work could move into the service instead).

## 2. Extract Business Logic into Services — ✅ Fixed (Phase 2)

Originally found: the application-status "open"/"editable" business rule (`Domain/Rules/RentalApplicationRules.IsOpen` / `IsEditable`) was re-implemented inline, a second time, directly in Razor markup instead of being read from a precomputed ViewModel flag, in `Views/Applications/Wizard.cshtml:66` and `Views/Applications/Index.cshtml:65,67`. `ApplicationListItemViewModel` and `ApplicationWizardViewModel` now carry `IsEditable`/`CanWithdraw` flags computed once in `ApplicationsController` via `RentalApplicationRules`, and both views read those flags instead of re-deriving the status list themselves — verified both in the test suite (99/99 passing) and manually against a running instance across all 6 statuses (see the Phase 2 verification notes in `REMEDIATION_STATUS.md`).

**Related finding, now also fixed:** `ApplicationWizardService.SaveApplicantInfoAsync` used to hand-roll required-field and email-format checks instead of expressing them as `DataAnnotations` on `ApplicationWizardViewModel` the way every other form in the app does — Phase 2 didn't touch this, but Phase 3 did; see Rule 8 below.

## 3. Separate Data Access — ⚠️ Partial violations

See Rule 10 below (same underlying finding, from the data-access-layering angle): `UnitListViewComponent` and `ApplicationSummaryViewComponent` query `ApplicationDbContext` directly rather than through a service, which is a direct-DbContext-access path outside the layer Rule 3 reserves for it. Controllers themselves are clean (see Rule 1).

## 4. Keep Razor Views Clean — ✅ Fixed (Phase 2)

No view injects `DbContext` or a service directly — a repo-wide search for `@inject` across `Views/` returns zero matches, so the "query the database directly from a view" failure mode doesn't occur. The three inline status-membership checks that used to live in `Wizard.cshtml:66`, `Index.cshtml:65`, and `Index.cshtml:67` (the same "complex logical operation" flagged under Rule 2) are gone — both views now read the precomputed `IsEditable`/`CanWithdraw` flags instead. `Index.cshtml`'s now-unused `@using PropertyRentalSystem.Web.Models.Domain` was removed as part of the same fix. `Views/Shared/_StatusBadge.cshtml`'s `switch` from `ApplicationStatus` to a Bootstrap CSS class, and `Wizard.cshtml:18-20`'s `Denied ? "alert-danger" : "alert-warning"` wording choice, remain — both are purely presentational mappings (label/color, not a business decision), so they were correctly left alone.

## 5. Type Safety & DTOs — ✅ Compliant

Every `return View(...)` / `return PartialView(...)` across all controllers passes a dedicated ViewModel (`ApplicationListViewModel`, `BrowseUnitsViewModel`, `ApplicationWizardViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, `ConfirmDeleteViewModel`, etc.) — confirmed by inspecting every `View(`/`PartialView(` call site in `Controllers/`. No action renders a raw `Property`, `Unit`, or `RentalApplication` entity. Every POST action binds to a form ViewModel rather than an entity, so over-posting isn't possible.

## 6. Safe Refactoring Strategy — ➖ Not applicable to a static snapshot

This rule describes a *process* for making future changes, not a property the current code either satisfies or violates on its own. The one relevant fact: the existing 99 tests in `PropertyRentalSystem.Tests` (`Domain/Rules`, `Services`, `ViewModels`) already give the "write/verify tests first" step of this rule something real to build on — keep that coverage current as the fixes below are applied, including adding tests for any `IsEditable`/`CanWithdraw` ViewModel flags introduced to resolve Rule 2/4.

## 7. Folder / Module Boundaries — ❌ Violations found

(Documented in `ARCHITECTURE_RULES.md` §7; restated here for completeness.)

- Two unrelated, non-nested folders are both named "Domain": `Domain/Rules` (business-rule classes) and `Models/Domain` (EF Core entities).
- `ViewComponents/` is a flat folder (`UnitListViewComponent.cs`, `ApplicationSummaryViewComponent.cs`), unlike `Controllers/`, `Services/`, `ViewModels/`, and `Views/`, which are all grouped by feature (`Properties`, `Units`, `Applications`, `Review`).

## 8. Centralized Validation — ✅ Fixed (Phase 3)

(Documented in `ARCHITECTURE_RULES.md` §8; restated here for completeness.)

Originally found: `RegisterViewModel`, `LoginViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, and `ResidenceHistoryFormViewModel` all validated presence/format via `DataAnnotations`/`IValidatableObject`, but `ApplicationWizardViewModel` had none — `ApplicationWizardService.SaveApplicantInfoAsync` manually checked `FullName`, `Phone`, `Email` (re-running `new EmailAddressAttribute().IsValid(email)` by hand), and `CurrentAddress` instead, reporting failures as `ServiceResult` field errors. `ApplicationWizardViewModel` now carries `[Required]`/`[EmailAddress]` on those four fields — the same `DataAnnotations` mechanism every other form uses — and `ApplicationsController.Wizard` checks `ModelState.IsValid` before calling the service, which no longer re-validates field shape at all.

**Why this fix landed in 2 commits instead of 1** (unlike every other single-commit phase so far): relocating validation across layers has a window where splitting it wrong leaves data completely unvalidated — e.g. removing the service's checks *before* the ViewModel/controller side existed would let invalid data save silently, since nothing would be checking it. The safe split is additive-first: commit 1 added the `DataAnnotations` + the controller's `ModelState.IsValid` guard, with the old service checks left in place (now redundant — the controller short-circuits before reaching them — but harmless); commit 2 removed the now-provably-dead service code once the new path was verified both by the test suite and manually against a running instance. Each commit left the app in a fully working state.

## 9. Project to ViewModels/DTOs at the Data-Access Boundary — ❌ Systemic violations found

`ARCHITECTURE_RULES.md` §9 names two methods as examples; a full pass over every service interface shows the pattern is systemic — **every** service interface returns full domain entities, and the controller does the entity → ViewModel mapping afterward. The clear instance of doing it correctly is `UnitListViewComponent`, which projects straight into `UnitListItemViewModel` inside the EF Core query (`.Select(u => new UnitListItemViewModel {...})`) — that's the pattern to copy.

Read paths that exist purely to back a list/summary/history view, and are never used for further mutation, so projecting them to a ViewModel directly in the query would lose nothing:

| Service method | Returns | Only ever used for |
|---|---|---|
| `IPropertyService.GetAllAsync()` | `List<Property>` | An `Id`+`Name` list/dropdown in `PropertiesController.LoadListAsync`, `ApplicationsController.Index/Browse`, `ApplicationReviewController.Index` |
| `IApplicationBrowseService.GetAvailableUnitsAsync(...)` | `List<Unit>` (+ `Property`, `UnitType` navigation) | `ApplicationsController.Browse`, mapped to `BrowseUnitViewModel` |
| `IApplicationWizardService.GetMyApplicationsAsync(...)` | `List<RentalApplication>` (`.Include(a => a.Unit).ThenInclude(u => u.Property)`) | `ApplicationsController.Index`, mapped to 4 fields of `ApplicationListItemViewModel` |
| `IApplicationReviewService.GetFilteredAsync(...)` | `List<RentalApplication>` (+ `Applicant`, `Unit`, `Property`) | `ApplicationReviewController.Index`, mapped to `PmApplicationListItemViewModel` |
| `IApplicationReviewService.GetHistoryAsync(...)` | `List<ApplicationStatusHistory>` (+ `ChangedBy`) | `ApplicationReviewController.Details`, mapped to `StatusHistoryItemViewModel` |

**Not a violation:** `GetByIdAsync`/`GetOwnedAsync`/`GetReviewableAsync`-style single-entity reads (`PropertiesController.Edit`, `UnitsController.Edit`, `ApplicationsController.Wizard`, `ApplicationReviewController.Review`, etc.) are legitimately entities — they're loaded immediately before an `Update`/`Delete`/status-transition call, and EF Core needs the tracked instance for that to work. Only the list/history reads in the table above are in scope for this rule.

## 10. Services Own `DbContext`, Not ViewComponents — ❌ Violations found

(Documented in `ARCHITECTURE_RULES.md` §10; restated here for completeness.)

`UnitListViewComponent` (`ViewComponents/UnitListViewComponent.cs:9-16`) and `ApplicationSummaryViewComponent` (`ViewComponents/ApplicationSummaryViewComponent.cs:12-18`) both have `ApplicationDbContext` injected directly into the constructor and query it themselves inside `InvokeAsync`, bypassing the Services layer entirely. This is the one place in the codebase where the Rule 3 data-access boundary isn't actually held — controllers hold it correctly (see Rule 1), view components don't.

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
3. ✅ **Rule 8** (move `ApplicationWizardViewModel`'s validation onto `DataAnnotations`, delete the manual checks from `ApplicationWizardService`) — done in Phase 3, on `refactor/phase-3-centralize-validation` (committed, not yet pushed).
4. **Rule 10** (move `UnitListViewComponent`'s and `ApplicationSummaryViewComponent`'s queries into `IUnitService`/a neutral service) — closes the one data-access-boundary gap, which is also Rule 3's only open finding. Not started.
5. **Rule 9** (project the five listed read paths straight to their ViewModels in the service query, following `UnitListViewComponent`'s existing pattern) — do this after Rule 2 (done) and after Rule 10, since the `GetMyApplicationsAsync` projection needs the `IsEditable`/`CanWithdraw` fields that now exist, and two of the queries in scope are moving out of view components as part of the Rule 10 fix anyway. Not started.
6. **Rule 7** (resolve the "Domain" naming collision, group `ViewComponents` by feature) — a rename/move, best done last since it touches the most file paths and should land on a clean diff. Not started.
