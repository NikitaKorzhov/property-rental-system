# Remediation Status

Mirrors [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md) phase-by-phase, step-by-step, so progress can be tracked as fixes land. Update this file (check boxes, flip the Status column, fill in Notes) as each step is actually completed in code — it should always reflect what's really in the repo, not what's planned. Verified against the codebase as of **2026-10-01**: Phases 1 and 2 are done (Phase 1 merged to `main`; Phase 2 done on `refactor/phase-2-dedupe-status-checks`, uncommitted).

## Summary

| Phase | Rule(s) | Status | Progress |
|---|---|---|---|
| 1 — Translate non-English comments | 11 | ✅ Done (merged to main) | 2/2 |
| 2 — Remove duplicated status checks from views | 2, 4 | ✅ Done (uncommitted) | 5/5 |
| 3 — Centralize wizard validation | 8 | ⬜ Not started | 0/5 |
| 4 — Move ViewComponent queries into services | 3, 10 | ⬜ Not started | 0/3 |
| 5 — Project to ViewModels at the boundary | 9 | ⬜ Not started | 0/5 |
| 6 — Resolve folder/namespace boundaries | 7 | ⬜ Not started | 0/3 |

Status legend: ⬜ Not started · 🟡 In progress · ✅ Done

Rule 12 (short, purpose-focused comments) needs no phase of its own — already compliant; its one note (the comment at `ApplicationWizardViewModel.cs:6-9`) is folded into Phase 3's checklist below.

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

**Status:** ⬜ Not started

- [ ] Add `[Required]`/`[EmailAddress]` (and any needed `[StringLength]`) to `FullName`, `Phone`, `Email`, `CurrentAddress` on `ApplicationWizardViewModel`.
- [ ] Remove the manual `IsNullOrWhiteSpace`/`EmailAddressAttribute` checks from `ApplicationWizardService.SaveApplicantInfoAsync`, keeping the `IsEditable` business-state check and the trim/save logic.
- [ ] Add a `ModelState.IsValid` check in `ApplicationsController.Wizard`'s `case "Continue" when model.Step == WizardStep.ApplicantInfo`, re-rendering on failure.
- [ ] Update `ApplicationWizardServiceTests` — remove/replace `WithAllFieldsMissing_ReturnsAllFourFieldErrors` and `WithInvalidEmail_FailsOnEmailField`; keep `WhenNotEditable_Fails` and `TrimsWhitespaceAndMarksSectionComplete`.
- [ ] Add `ApplicationWizardViewModelTests` covering the moved required/email-format cases.
- [ ] Rewrite the comment at `ApplicationWizardViewModel.cs:6-9` (now-stale once validation moves; keep it short per Rule 12).

**Verified current state:** `ApplicationWizardViewModel` has no `DataAnnotations` on any field. `ApplicationWizardService.SaveApplicantInfoAsync` still hand-rolls the required/email checks. `ApplicationWizardServiceTests` still has all 4 original tests, including the two that test field-validation error messages. The comment at lines 6-9 is still present and still accurate (not yet stale).

**Notes:** —

---

## Phase 4 — Move ViewComponent queries into services (Rules 3 & 10)

**Status:** ⬜ Not started

- [ ] Add `GetUnitsForPropertyAsync(int propertyId)` to `IUnitService`/`UnitService`, moving `UnitListViewComponent`'s query; update the component to call the service and drop its `ApplicationDbContext` dependency.
- [ ] Decide the home for the application-summary query (it's used by both `Applications/Wizard.cshtml` and `ApplicationReview/Details.cshtml`, so it needs a neutral service, not `IApplicationWizardService` alone) and move `ApplicationSummaryViewComponent`'s query there; update the component to call it and drop its `ApplicationDbContext` dependency.
- [ ] Add service-level tests for both new methods against EF Core InMemory.

**Verified current state:** both `UnitListViewComponent.cs` and `ApplicationSummaryViewComponent.cs` still inject `ApplicationDbContext` directly and query it in `InvokeAsync` — unchanged since the audit.

**Notes:** —

---

## Phase 5 — Project to ViewModels at the boundary (Rule 9)

**Status:** ⬜ Not started — **blocked on Phase 2** for the `GetMyApplicationsAsync` item (needs `IsEditable`/`CanWithdraw` to already exist on `ApplicationListItemViewModel`).

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
