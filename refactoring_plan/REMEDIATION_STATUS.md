# Remediation Status

Mirrors [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md) phase-by-phase, step-by-step, so progress can be tracked as fixes land. Update this file (check boxes, flip the Status column, fill in Notes) as each step is actually completed in code — it should always reflect what's really in the repo, not what's planned. Verified against the codebase as of **2026-10-01**: Phases 1–5 are done and merged to `main`; Phase 6 is done on `refactor/phase-6-folder-namespace-cleanup` (committed, not yet pushed) — **all 6 phases of the plan are now complete**.

## Summary

| Phase | Rule(s) | Status | Progress |
|---|---|---|---|
| 1 — Translate non-English comments | 11 | ✅ Done (merged to main) | 2/2 |
| 2 — Remove duplicated status checks from views | 2, 4 | ✅ Done (merged to main) | 5/5 |
| 3 — Centralize wizard validation | 8 | ✅ Done (merged to main) | 6/6 |
| 4 — Move ViewComponent queries into services | 3, 10 | ✅ Done (merged to main) | 3/3 |
| 5 — Project to ViewModels at the boundary | 9 | ✅ Done (merged to main) | 4/4 |
| 6 — Resolve folder/namespace boundaries | 7 | ✅ Done (committed, not pushed) | 3/3 |

Status legend: ⬜ Not started · 🟡 In progress · ✅ Done

Rule 12 (short, purpose-focused comments) needs no phase of its own — already compliant; its one note (the comment at `ApplicationWizardViewModel.cs:6-9`) was rewritten as part of Phase 3 below.

---

## Phase 1 — Translate non-English comments (Rule 11)

**Status:** ✅ Done — changes made in the working tree on `refactor/phase-1-translate-comments`, not yet committed.

- [x] Translate the 7 Ukrainian comments in `Controllers/AccountController.cs` (lines 21, 28, 40, 57, 61, 81, 88) to English.
- [x] Translate the 4 Ukrainian comments in `PropertyRentalSystem.Web/Dockerfile` to English — found by widening the scan to the whole repo, not just `.cs`/`.cshtml`.
- [x] Re-run the non-ASCII scan across the entire repository to confirm no other file has the same issue.

**Verified current state:** `AccountController.cs` and `Dockerfile` no longer contain any Ukrainian text; a repo-wide non-ASCII scan (`.git`/`bin`/`obj`/`.idea` excluded) returns zero matches anywhere in actual project files. `dotnet build` succeeds with 0 warnings/0 errors.

**Notes:** Uncommitted by request — stage and commit when ready. Rule 11 in `ARCHITECTURE_RULES.md` was also broadened while doing this phase, to explicitly cover names and user-facing text, not just comments.

---

## Phase 2 — Remove duplicated status-rule checks from views (Rules 2 & 4)

**Status:** ✅ Done — changes made in the working tree on `refactor/phase-2-dedupe-status-checks`, not yet committed.

- [x] Add `IsEditable` and `CanWithdraw` (`bool`) to `ViewModels/Applications/ApplicationListItemViewModel.cs`.
- [x] Add `CanWithdraw` (`bool`) to `ViewModels/Applications/ApplicationWizardViewModel.cs`.
- [x] `ApplicationsController.Index` — set both fields when projecting `ApplicationListItemViewModel`.
- [x] `ApplicationsController.BuildViewModel` — set `CanWithdraw`.
- [x] Update `Views/Applications/Index.cshtml` and `Views/Applications/Wizard.cshtml` to read the new properties instead of the inline `ApplicationStatus is ...` conditions.

**Verified current state:** `ApplicationListItemViewModel` now has `IsEditable`/`CanWithdraw`; `ApplicationWizardViewModel` now has `CanWithdraw` alongside `IsEditable`. Both views read the new properties — no inline `ApplicationStatus is ...` status-membership checks remain anywhere in `Views/Applications/`. `Index.cshtml`'s now-unused `@using PropertyRentalSystem.Web.Models.Domain` was also removed; `Wizard.cshtml` keeps its `@using` since it still uses `ApplicationStatus.Denied` for a purely presentational label/color choice (not a business-rule duplication, per the audit). `dotnet build`: 0 warnings/0 errors. `dotnet test`: 99/99 passing.

**Notes:** Uncommitted by request — stage and commit when ready. No new tests were added for the `IsEditable`/`CanWithdraw` mapping (the plan calls this optional); consider adding them before or alongside the commit.

**Notes:** —

---

## Phase 3 — Centralize wizard validation (Rule 8)

**Status:** ✅ Done — split across 2 commits on `refactor/phase-3-centralize-validation` (not yet pushed): commit 1 introduced the new validation path alongside the old one (safe, redundant); commit 2 removed the old path once proven out. See `RULES_COMPLIANCE_AUDIT.md` §8 for why this phase used 2 commits instead of 1.

- [x] Add `[Required]`/`[EmailAddress]` to `FullName`, `Phone`, `Email`, `CurrentAddress` on `ApplicationWizardViewModel` (no `[StringLength]` was implied by the old checks, so none added).
- [x] Remove the manual `IsNullOrWhiteSpace`/`EmailAddressAttribute` checks from `ApplicationWizardService.SaveApplicantInfoAsync`, keeping the `IsEditable` business-state check and the trim/save logic. Also removed the now-unused `System.ComponentModel.DataAnnotations` `using`.
- [x] Add a `ModelState.IsValid` check in `ApplicationsController.Wizard`'s `case "Continue" when model.Step == WizardStep.ApplicantInfo`, re-rendering on failure.
- [x] Update `ApplicationWizardServiceTests` — removed `WithAllFieldsMissing_ReturnsAllFourFieldErrors` and `WithInvalidEmail_FailsOnEmailField`; kept `WhenNotEditable_Fails` and `TrimsWhitespaceAndMarksSectionComplete`.
- [x] Add `ApplicationWizardViewModelTests` (6 tests: valid case, each of the 4 required fields missing, invalid email format) covering the moved required/email-format cases.
- [x] Rewrote the comment at `ApplicationWizardViewModel.cs:6-9` in commit 1 (not commit 2) — it became stale as soon as the DataAnnotations + controller check landed, not when the service cleanup happened.

**Verified current state:** `dotnet build`: 0 warnings/0 errors. `dotnet test`: 103/103 passing (99 − 2 removed + 6 new = 103). Manually re-tested against a running instance (rebuilt Docker image) after *each* commit: blank fields → all 4 required errors, invalid email → its specific error, valid data → advances to Residence History — identical behavior before and after the service cleanup.

**Notes:** Uncommitted-to-`main` (pushed not requested yet) — both commits are local on this branch.

---

## Phase 4 — Move ViewComponent queries into services (Rules 3 & 10)

**Status:** ✅ Done — split across 2 commits on `refactor/phase-4-viewcomponent-services` (not yet pushed), one per component, since the two are fully independent.

- [x] Add `GetUnitsForPropertyAsync(int propertyId)` to `IUnitService`/`UnitService`, moving `UnitListViewComponent`'s query verbatim (same single-JOIN SQL, confirmed via EF Core query logs); update the component to call the service and drop its `ApplicationDbContext` dependency.
- [x] Decide the home for the application-summary query — added a new `IApplicationSummaryService`/`ApplicationSummaryService` rather than bolting it onto `IApplicationWizardService` (Applicant-only) or `IApplicationBrowseService` (mismatched responsibility), since it's used by both `Applications/Wizard.cshtml` and `ApplicationReview/Details.cshtml`. Also upgraded the query itself from `.Include()` + in-memory mapping to a genuine single `.Select()` projection (Rule 9) — confirmed via EF Core query logs to still be one SQL statement (`LEFT JOIN` to `ResidenceHistories`), not N+1. Registered in `Program.cs`. Updated the component to call it and drop its `ApplicationDbContext` dependency.
- [x] Add service-level tests for both new methods against EF Core InMemory (2 for `GetUnitsForPropertyAsync`, 3 for `GetSummaryAsync`).

**Verified current state:** `dotnet build`: 0 warnings/0 errors. `dotnet test`: 108/108 passing (105 + 3 new; the 2 `UnitService` tests landed in commit 1, pushing it to 105 first). Manually checked against a running instance after *each* commit: Properties page (unit list), the applicant's Wizard Summary step, and the manager's Review Details page all render identical data to before. EF Core query logs inspected after each commit to confirm no N+1 was introduced — both new methods still produce exactly one SQL statement per call.

**Notes:** Neither `ViewComponent` was grouped by feature yet at the time (still flat in `ViewComponents/`) — that part of Rule 7 was closed later, in Phase 6.

---

## Phase 5 — Project to ViewModels at the boundary (Rule 9)

**Status:** ✅ Done — split across 4 commits on `refactor/phase-5-project-viewmodels` (not yet pushed), one per method/service, since all 4 are mutually independent (unlike Phase 3, a signature change either compiles fully or doesn't at all — no unsafe partial state is possible, so the split is purely for reviewability).

- [x] `IPropertyService.GetAllAsync()` → project to the **existing** `PropertyListItemViewModel` (Id, Name, Address) — not a new minimal Id+Name shape as originally sketched here, which would have broken `PropertiesController.LoadListAsync` (needs Address for the main property list, not just the 3 dropdown call sites). `LoadListAsync` is now a direct pass-through; the 3 dropdowns (`ApplicationsController.Index`/`Browse`, `ApplicationReviewController.Index`) needed zero code changes.
- [x] `IApplicationBrowseService.GetAvailableUnitsAsync(...)` → project to `BrowseUnitViewModel`; `ExistingApplicationId` composed by the controller afterward via a loop (same pattern used for `IsEditable`/`CanWithdraw` below), not a `.Select()`.
- [x] `IApplicationWizardService.GetMyApplicationsAsync(...)` → project the DB-translatable fields to `ApplicationListItemViewModel` in `.Select()`; `IsEditable`/`CanWithdraw` filled in via `RentalApplicationRules` in a loop *after* `ToListAsync()` — calling `RentalApplicationRules` directly inside `.Select()` is not SQL-translatable and would throw against the real SQL Server provider, a failure `dotnet test`'s InMemory provider would not have caught. Verified specifically against the real SQL Server container, not just the test suite.
- [x] `IApplicationReviewService.GetFilteredAsync(...)` → project to `PmApplicationListItemViewModel`.
- [x] `IApplicationReviewService.GetHistoryAsync(...)` → project to `StatusHistoryItemViewModel`.
- [x] Checked every affected `Services` test: all 7 existing assertions (`PropertyServiceTests`, 3× `ApplicationBrowseServiceTests`, 2× `ApplicationReviewServiceTests` `GetFilteredAsync` tests) needed **zero code changes** — the entity and ViewModel property names/types happen to match exactly, so the same assertions compile and pass against the new return types unchanged. `GetMyApplicationsAsync`/`GetHistoryAsync` had no prior direct tests either way.

**Verified current state:** `dotnet build`: 0 warnings/0 errors after each of the 4 commits. `dotnet test`: 108/108 passing throughout (no test file needed editing). Manually checked against a running instance (real SQL Server, not InMemory) after each commit: Properties page (name+address), Browse (filters, Apply/Continue buttons, lease-exclusion), My Applications (all 5 live statuses → correct label/Withdraw), Review Index (applicant emails) and Details (full correct status history) all render identical data to before. EF Core query logs inspected after every commit — each of the 4 migrated methods still produces exactly one SQL statement with only the needed columns/JOINs, no N+1, and in every case strictly less data pulled than before.

**Notes:** —

---

## Phase 6 — Resolve folder/namespace boundaries (Rule 7)

**Status:** ✅ Done — split across 3 commits on `refactor/phase-6-folder-namespace-cleanup` (not yet pushed): (1) the Domain-collision resolution, by far the widest-reaching (72 referencing files), (2) a small bonus fix, (3) the ViewComponents grouping. Went beyond the plan's "pick one side of the collision" framing per an explicit requirement that the structure be maximally intuitive, not just non-colliding.

- [x] **Both** `Domain/Rules` and `Models/Domain` renamed, not just one: `Models/Domain/*.cs` → `Models/*.cs` (namespace `...Models.Domain` → `...Models`, mirrored in `PropertyRentalSystem.Tests`), `Domain/Rules/*.cs` → `BusinessRules/*.cs` (namespace `...Domain.Rules` → `...BusinessRules`). `Models/` now pairs symmetrically with `ViewModels/` — no redundant qualifier, standard ASP.NET MVC convention. Every `using`/`@using` across `Controllers/`, `Services/`, `Data/`, `ViewModels/`, `Views/`, `Migrations/` (see below), and both test projects updated — 72 files touched in total.
- [x] **Bonus, outside the original scope:** `Models/ErrorViewModel.cs` was loose in `Models/` despite being a ViewModel by every convention here — moved to `ViewModels/Shared/ErrorViewModel.cs`.
- [x] Moved `ViewComponents/UnitListViewComponent.cs` → `ViewComponents/Units/`, `ApplicationSummaryViewComponent.cs` → `ViewComponents/Applications/`, matching `Services/`/`ViewModels/`/`Views/`. (`Controllers/` itself turned out to still be flat on inspection — not part of this phase's scope, which was specifically the `ViewComponents` vs. `Services`/`ViewModels`/`Views` inconsistency.)
- [x] Full solution build + full test suite green after each of the 3 commits; `git diff --stat` reviewed — purely paths/namespaces, no logic changes.

**A risk caught during execution, outside the original plan's checklist:** EF Core's migration snapshot/designer files (`Migrations/ApplicationDbContextModelSnapshot.cs`, `*.Designer.cs`) embed every entity's full CLR namespace as **string literals** used for model-vs-snapshot comparison. Renaming the C# namespace without updating these would desync the snapshot from the code. Updated them as part of the same mechanical replace, then confirmed with `dotnet ef migrations has-pending-model-changes` (reports no changes) that the model is still fully in sync — no phantom migration will be generated.

**Verified current state:** `dotnet build`: 0 warnings/0 errors after each commit. `dotnet test`: 108/108 passing throughout — no test file needed logic changes, only the namespace rename. `dotnet ef migrations has-pending-model-changes`: in sync. Manually checked against a running instance after each commit: My Applications/Browse/Units-Create/Review-list (commit 1, exercising `BusinessRules` + renamed entities), `/Home/Error` + the role-based Home redirect (commit 2), and both ViewComponents resolving correctly by their conventional string name despite the namespace/folder change — verified specifically because that's a runtime lookup a build can't catch (commit 3).

**Notes:** This completes all 6 phases of `REMEDIATION_PLAN.md`.
