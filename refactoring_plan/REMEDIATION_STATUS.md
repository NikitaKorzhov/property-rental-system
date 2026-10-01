# Remediation Status

Mirrors [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md) phase-by-phase, step-by-step, so progress can be tracked as fixes land. Update this file (check boxes, flip the Status column, fill in Notes) as each step is actually completed in code — it should always reflect what's really in the repo, not what's planned. Verified against the codebase as of **2026-10-01**: Phases 1–3 are done and merged to `main`; Phase 4 is done on `refactor/phase-4-viewcomponent-services` (committed, not yet pushed).

## Summary

| Phase | Rule(s) | Status | Progress |
|---|---|---|---|
| 1 — Translate non-English comments | 11 | ✅ Done (merged to main) | 2/2 |
| 2 — Remove duplicated status checks from views | 2, 4 | ✅ Done (merged to main) | 5/5 |
| 3 — Centralize wizard validation | 8 | ✅ Done (merged to main) | 6/6 |
| 4 — Move ViewComponent queries into services | 3, 10 | ✅ Done (committed, not pushed) | 3/3 |
| 5 — Project to ViewModels at the boundary | 9 | ⬜ Not started | 0/5 |
| 6 — Resolve folder/namespace boundaries | 7 | ⬜ Not started | 0/3 |

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

**Notes:** Neither `ViewComponent` is grouped by feature yet (still flat in `ViewComponents/`) — that part of Rule 7 is still open, tracked in Phase 6.

---

## Phase 5 — Project to ViewModels at the boundary (Rule 9)

**Status:** ⬜ Not started — no longer blocked (Phases 2 and 4 are both done, so `IsEditable`/`CanWithdraw` already exist on `ApplicationListItemViewModel`, and `UnitListViewComponent`/`ApplicationSummaryViewComponent` are no longer part of this phase's scope — see the note in Phase 4).

- [ ] `IPropertyService.GetAllAsync()` → project to a minimal `Id`+`Name` shape; update the three call sites (`PropertiesController.LoadListAsync`, `ApplicationsController.Index`/`Browse`, `ApplicationReviewController.Index`).
- [ ] `IApplicationBrowseService.GetAvailableUnitsAsync(...)` → project to `BrowseUnitViewModel` (composing `ExistingApplicationId` in the controller as today).
- [ ] `IApplicationWizardService.GetMyApplicationsAsync(...)` → project to `ApplicationListItemViewModel`, including `IsEditable`/`CanWithdraw`. *(requires Phase 2 done first)*
- [ ] `IApplicationReviewService.GetFilteredAsync(...)` → project to `PmApplicationListItemViewModel`.
- [ ] `IApplicationReviewService.GetHistoryAsync(...)` → project to `StatusHistoryItemViewModel`.
- [ ] Update every affected `Services` test to assert on ViewModel properties instead of entity/navigation properties.

**Verified current state:** all five methods still return full entity lists (`List<Property>`, `List<Unit>`, `List<RentalApplication>`, `List<ApplicationStatusHistory>`) — e.g. `IPropertyService.GetAllAsync()` is still `Task<List<Property>> GetAllAsync();`. Controllers still do the entity → ViewModel mapping themselves.

**Notes:** —

---

## Phase 6 — Resolve folder/namespace boundaries (Rule 7)

**Status:** ⬜ Not started — recommended to start last, after Phases 1–5 are done or at least Phase 4 (so `ApplicationSummaryViewComponent`'s final home is already settled).

- [ ] Rename either `Domain/Rules` or `Models/Domain` so the two no longer share the "Domain" name; update every `using` across `Controllers/`, `Services/`, `Data/`, `Views/` (`@using`), and `PropertyRentalSystem.Tests/Domain/Rules/`.
- [ ] Move `ViewComponents/UnitListViewComponent.cs` and `ApplicationSummaryViewComponent.cs` into feature subfolders matching the rest of the tree.
- [ ] Full solution build + full test suite green; review `git diff --stat` to confirm the change is purely paths/namespaces.

**Verified current state:** `Domain/Rules` and `Models/Domain` both still exist as separate, same-named folders; `ViewComponents/` is still flat (no feature subfolders).

**Notes:** —
