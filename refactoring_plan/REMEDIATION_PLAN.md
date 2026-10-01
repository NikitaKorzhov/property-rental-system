# Remediation Plan

A concrete plan to close the gaps found in [`RULES_COMPLIANCE_AUDIT.md`](RULES_COMPLIANCE_AUDIT.md) against [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md). Phases are ordered **from simplest to most complex** — each phase is small enough to execute, test, and merge on its own, per the Strangler-Fig approach in Rule 6 (write/verify tests → extract → confirm green → move on). Dependencies between phases are called out explicitly where they exist; phases with no dependency note can be done in any relative order (including in parallel by different people).

Rules 1, 5, 6, and 12 need no dedicated phase (already compliant — see `RULES_COMPLIANCE_AUDIT.md` §12 for Rule 12's one minor note, folded into Phase 3 below) — they aren't phases of their own.

**Comment style throughout every phase (Rule 12):** keep any comment added or edited while executing this plan short — one or two lines on *why*, never a multi-line narration of *what* the code does. If a change needs more explanation than that, that's a sign to simplify the code, not to write a longer comment.

## Overview

| Phase | Audit rule(s) | Complexity | Depends on |
|---|---|---|---|
| 1 | 11 — Hygiene | Trivial | None |
| 2 | 2, 4 — Duplicated view logic | Simple | None (soft file overlap with Phase 3) |
| 3 | 8 — Centralized validation | Moderate | None (soft file overlap with Phase 2) |
| 4 | 3, 10 — Services own `DbContext` | Moderate–High | None (soft synergy with Phase 5) |
| 5 | 9 — Project to ViewModels at the boundary | High | Phase 2 (hard, for one method) |
| 6 | 7 — Folder/namespace boundaries | Highest blast radius | Do last (soft, avoids churn) |

---

## Phase 1 — Translate non-English comments (Rule 11) — ✅ Done

**Why it's first:** small, no behavior change, no test impact, zero risk.

**Scope:** `Controllers/AccountController.cs` — the 7 Ukrainian comments identified in the audit (lines 21, 28, 40, 57, 61, 81, 88) — plus `PropertyRentalSystem.Web/Dockerfile`, which had 4 more Ukrainian comments the original audit missed (it only scanned `.cs`/`.cshtml`; the whole repo needs scanning per Rule 11's broadened scope — see `ARCHITECTURE_RULES.md` §11).

**Steps:**
1. Translate each comment to English, preserving its meaning (e.g. the Ukrainian section-header comment above the `Register` action became `// ---------- Registration ----------`).
2. Re-run a non-ASCII scan across the **entire repository** (not just `.cs`/`.cshtml`) to confirm no other file — code, config, or infrastructure (`Dockerfile`, `.sh`, `.sql`, `.yml`, etc.) — has the same issue.

**Verification:** `dotnet build` (comments don't affect compilation, but confirms nothing else broke); no test changes needed. Both done — build succeeds with 0 warnings/0 errors, repo-wide scan is clean outside this plan's own documentation (which quotes the old text as historical evidence).

**Dependencies:** None. Can be done first, anytime, by anyone.

---

## Phase 2 — Remove duplicated status-rule checks from Razor views (Rules 2 & 4)

**Why it's next-simplest:** purely additive (new ViewModel properties) plus a like-for-like swap of an inline condition for a property read in three `@if`/`@(...)` spots. No business behavior changes — the rendered HTML is identical before and after.

**Scope:**
- `ViewModels/Applications/ApplicationListItemViewModel.cs` — add `IsEditable` and `CanWithdraw` (`bool`).
- `ViewModels/Applications/ApplicationWizardViewModel.cs` — add `CanWithdraw` (`bool`) alongside the existing `IsEditable`.
- `Controllers/ApplicationsController.cs`:
  - `Index` — when projecting `ApplicationListItemViewModel`, set `IsEditable = RentalApplicationRules.IsEditable(a.Status)` and `CanWithdraw = RentalApplicationRules.IsOpen(a.Status)`.
  - `BuildViewModel` — set `CanWithdraw = RentalApplicationRules.IsOpen(application.Status)`.
- `Views/Applications/Index.cshtml` — replace `app.Status is ApplicationStatus.Draft or ApplicationStatus.Returned ? "Continue" : "View"` with `app.IsEditable ? "Continue" : "View"`; replace the `Draft or Submitted or Returned` withdraw-button condition with `app.CanWithdraw`.
- `Views/Applications/Wizard.cshtml` — replace the same withdraw-button condition with `Model.CanWithdraw`.

**Verification:** No existing test asserts on these exact conditions (they're markup, not covered by the C# test suite) — verify manually: load "My Applications" and the wizard page for an application in each status and confirm the Continue/View label and Withdraw button visibility are unchanged. Optionally add a couple of `RentalApplicationRules`-level or ViewModel-construction tests asserting `IsEditable`/`CanWithdraw` map correctly per status, to lock in the behavior going forward.

**Dependencies:** None functionally. **Soft overlap:** both this phase and Phase 3 touch `ApplicationWizardViewModel.cs` and `ApplicationsController.cs`, but different members/regions of each — no merge conflict if done in either order, just don't run them as literally simultaneous edits to the same file without rebasing.

---

## Phase 3 — Centralize wizard validation into the ViewModel (Rule 8)

**Why it's next:** contained to one feature (the Applicant-Info wizard step), but requires a control-flow change in the controller, not just a data/markup change, plus matching test migration — more involved than Phase 2.

**Scope:**
- `ViewModels/Applications/ApplicationWizardViewModel.cs` — add `[Required]`/`[EmailAddress]` (and any `[StringLength]` the service's checks implied) to `FullName`, `Phone`, `Email`, `CurrentAddress`, matching the messages the service currently produces.
- `Services/Applications/ApplicationWizardService.cs` — delete the manual `IsNullOrWhiteSpace`/`EmailAddressAttribute` checks from `SaveApplicantInfoAsync`; keep the `IsEditable` business-state check (that one stays — it needs DB state, not just form shape) and the trim-and-save logic.
- `Controllers/ApplicationsController.cs`, `Wizard` POST action, `case "Continue" when model.Step == WizardStep.ApplicantInfo`: add a `ModelState.IsValid` check before calling `SaveApplicantInfoAsync`, re-rendering the `ApplicantInfo` step on failure — the same shape `AddResidence`/`EditResidence` already use for their modal forms.
- `PropertyRentalSystem.Tests/Services/Applications/ApplicationWizardServiceTests.cs` — remove/replace `SaveApplicantInfoAsync_WithAllFieldsMissing_ReturnsAllFourFieldErrors` and `SaveApplicantInfoAsync_WithInvalidEmail_FailsOnEmailField` (that coverage moves to ViewModel-level validation tests); keep `SaveApplicantInfoAsync_WhenNotEditable_Fails` and `SaveApplicantInfoAsync_TrimsWhitespaceAndMarksSectionComplete` as-is (still service concerns).
- Add a new `PropertyRentalSystem.Tests/ViewModels/ApplicationWizardViewModelTests.cs` (or extend an existing `ViewModels` test file) covering the moved required/email-format cases, mirroring how `ResidenceHistoryFormViewModel`'s date-ordering rule is already tested.
- `ViewModels/Applications/ApplicationWizardViewModel.cs:6-9` — rewrite the comment explaining the old manual-validation approach; it currently says validation is done "rather than via DataAnnotations," which this phase makes false. Keep the replacement short, per Rule 12.

**Verification:** `dotnet test` — the two moved test cases should reappear (renamed/relocated) under `ViewModels`, not disappear; `dotnet test --filter "FullyQualifiedName~ApplicationWizardServiceTests"` and `~ApplicationWizardViewModelTests` both green. Manually re-submit the Applicant Info step with a blank field and a malformed email to confirm the same inline error messages still appear in the same place.

**Dependencies:** None functionally. **Soft overlap** with Phase 2 as noted above.

---

## Phase 4 — Move ViewComponent queries into the Services layer (Rules 3 & 10)

**Why it's next:** two self-contained components, but each requires designing a new service method (not just moving code verbatim), updating an interface, registering nothing new (the services are already DI-registered), and reasoning about where a cross-feature read belongs.

**Scope:**
- `UnitListViewComponent` → `IUnitService`: add `Task<List<UnitListItemViewModel>> GetUnitsForPropertyAsync(int propertyId)`, moving the existing `.Where(...).Include(...).OrderBy(...).Select(...)` query (already correctly projected to `UnitListItemViewModel`) from the component into `UnitService`. The component's `InvokeAsync` becomes a one-line call to the service.
- `ApplicationSummaryViewComponent` → needs a slightly more careful home: it's invoked from **both** `Applications/Wizard.cshtml` (applicant-facing) **and** `ApplicationReview/Details.cshtml` (property-manager-facing) — it's a genuinely cross-feature read, not owned by one role's service. Add it as a new method on a neutral service rather than bolting it onto `IApplicationWizardService` (which is otherwise Applicant-flow-specific) — e.g. a method on `IApplicationBrowseService`, or a new small `IApplicationReadService` if neither existing interface feels like a natural fit. Move the existing `.Include(...).Select(...)` query (already correctly projected to `ApplicationSummaryViewModel`) there.
- Both ViewComponents: remove the `ApplicationDbContext _db` field/constructor parameter and the `Microsoft.EntityFrameworkCore`/`Data` `using`s; inject the chosen service instead.

**Verification:** `dotnet build` (confirms no leftover `ApplicationDbContext` references in `ViewComponents/`); `dotnet test` full suite green; add service-level tests for the two new methods (`GetUnitsForPropertyAsync`, the summary method) against `EF Core InMemory`, mirroring existing `Services` tests — there were none before since the logic lived outside the testable Services layer, so this phase is a net *increase* in test coverage. Manually load the Properties page (unit list expands correctly) and both the wizard Summary step and the manager's application details page (summary renders correctly).

**Dependencies:** None blocking. **Soft synergy with Phase 5:** write both new service methods so they return ViewModels directly from the query (as `UnitListViewComponent` already did) — i.e., apply Phase 5's "project at the boundary" rule to these two methods *now*, while they're being written, rather than creating them as entity-returning methods and having to revisit them in Phase 5.

---

## Phase 5 — Project read-only queries directly to ViewModels (Rule 9)

**Why it's near-last:** touches five existing service method signatures, which ripples into every controller that calls them and into the existing Services test suite that currently asserts against entity shapes — the widest *functional* change of any phase.

**Scope** (the five read paths identified in the audit, excluding `GetByIdAsync`/`GetOwnedAsync`/`GetReviewableAsync`-style entity reads that feed mutations — those stay as entities):

1. `IPropertyService.GetAllAsync()` → change to return `List<PropertyOptionViewModel>` (or reuse `PropertyListItemViewModel`/a new minimal `Id`+`Name` DTO) projected in the query. Update the three call sites (`PropertiesController.LoadListAsync`, `ApplicationsController.Index/Browse`, `ApplicationReviewController.Index`) to drop their `.Select(p => new SelectListItem(...))` mapping's dependency on entity fields — they already only use `Id`/`Name`, so this is a signature change, not a logic change, at each call site.
2. `IApplicationBrowseService.GetAvailableUnitsAsync(...)` → project directly to `BrowseUnitViewModel` (minus `ExistingApplicationId`, which is computed separately in the controller from `GetOpenApplicationUnitMapAsync` — keep that composition in the controller, just stop returning the raw `Unit` entity for the rest of the fields).
3. `IApplicationWizardService.GetMyApplicationsAsync(...)` → project directly to `ApplicationListItemViewModel`, including the `IsEditable`/`CanWithdraw` fields added in Phase 2 (see dependency note below).
4. `IApplicationReviewService.GetFilteredAsync(...)` → project directly to `PmApplicationListItemViewModel`.
5. `IApplicationReviewService.GetHistoryAsync(...)` → project directly to `StatusHistoryItemViewModel`.

**Design note to record alongside this change:** projecting to ViewModels inside the Services layer means `Services/*` now takes a compile-time dependency on `ViewModels/*` (a presentation-layer type) for these five methods. That's an accepted, deliberate trade-off here — the two namespaces already live in the same project/assembly, so it isn't a hard layering violation, but it's a one-way door worth a short comment at each changed method (similar to the existing "// filtering is done in the database" comments) so a future reader understands why a service returns a ViewModel instead of an entity.

**Verification:** `dotnet test` — every existing `Services` test touching these five methods needs its assertions updated from entity-property access (`result[0].Unit.Property.Name`) to ViewModel-property access (`result[0].PropertyName`); no test should need to change *what* it asserts, only *how* it reads the result. Run `dotnet test --filter "FullyQualifiedName~Services"` to confirm the full boundary is green before moving on. Manually re-check the four affected pages (Properties/Units dropdowns, Browse, My Applications, Review list, Review details history) for identical rendered output.

**Dependencies:**
- **Hard, for item 3 only:** do this phase *after* Phase 2, so the `IsEditable`/`CanWithdraw` fields already exist on `ApplicationListItemViewModel` and can be included directly in the new `.Select(...)` projection from the start. Doing it in the other order means writing the projection once without those fields, then revisiting it again when Phase 2 lands — wasted work, not a correctness problem.
- **Soft, for items 1–2 and 4–5:** no ordering requirement relative to any other phase; they could in principle be split out and done before Phase 2/3/4 if someone wanted to parallelize, but are grouped here as one phase since they share the same mechanical pattern and review focus.

---

## Phase 6 — Resolve folder/namespace boundaries (Rule 7)

**Why it's last:** the two changes here (rename the `Domain/Rules` ↔ `Models/Domain` naming collision; group `ViewComponents` by feature) are conceptually simple — a rename and a move — but touch the **largest number of files** of any phase, since `LeaseRules`/`UnitTypeRules`/`RentalApplicationRules` are referenced via `using PropertyRentalSystem.Web.Domain.Rules;` across most controllers, most services, and most of the `Domain/Rules` test folder — including files modified by every phase above. Doing it last means it lands on a settled, already-tested codebase instead of chasing moving targets.

**Scope:**
- Pick one of: rename `Domain/Rules` → something that doesn't collide with `Models/Domain` (e.g. `BusinessRules/`), or rename `Models/Domain` → `Models/Entities` and leave `Domain/Rules` as-is. Either resolves the collision; the audit doesn't mandate which.
- Update every `using PropertyRentalSystem.Web.Domain.Rules;` (or `Models.Domain`, whichever is renamed) across `Controllers/`, `Services/`, `Data/`, `Views/` (`@using` directives), and `PropertyRentalSystem.Tests/Domain/Rules/`.
- Move `ViewComponents/UnitListViewComponent.cs` and `ApplicationSummaryViewComponent.cs` into feature subfolders matching the rest of the tree (e.g. `ViewComponents/Units/`, `ViewComponents/Applications/`), updating their namespaces and any `using` referencing them.

**Verification:** `dotnet build` across both projects (a missed `using` fails the build immediately, so this is a safe mechanical check); `dotnet test` full suite green; `git diff --stat` reviewed to confirm the change is purely paths/namespaces with no logic diff hiding inside it.

**Dependencies:** **Soft, do last.** Not blocked by anything, but doing it earlier would mean every file touched by Phases 2–5 picks up a mid-flight rename, increasing the odds of merge conflicts or stale `using`s for no benefit. Also benefits from running after Phase 4, since Phase 4 is what moves `ApplicationSummaryViewComponent`'s logic out — easier to decide its *final* home (which service, which `ViewComponents` subfolder) once Phase 4 has already settled where its query logic lives.

---

## Executing this plan

Per Rule 6, treat each phase as its own Strangler-Fig cycle: confirm the relevant tests are green before starting, make the change, add/update tests to cover it, confirm green again, then move to the next phase. Phases 1–4 can be done in any order relative to each other (respecting the soft file-overlap note between 2 and 3); Phase 5 must follow Phase 2 for the one method noted; Phase 6 should be done last regardless of what else is in flight.
