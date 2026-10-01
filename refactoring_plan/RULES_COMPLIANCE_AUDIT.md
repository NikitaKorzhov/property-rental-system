# Rules Compliance Audit

A rule-by-rule audit of the current codebase against [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md), based on reading every controller, service, view component, and Razor view in `PropertyRentalSystem.Web`. Each section says whether the rule is followed, and lists concrete file/line evidence for every gap found — no rule is marked violated without a specific example.

## Summary

| Rule | Status |
|---|---|
| 1. Skinny Controllers | ✅ Compliant |
| 2. Business Logic in Services | ⚠️ Partial violations |
| 3. Separate Data Access | ⚠️ Partial violations |
| 4. Clean Razor Views | ⚠️ Partial violations |
| 5. DTOs / Type Safety | ✅ Compliant |
| 6. Strangler-Fig Refactoring | ➖ Not applicable to a static snapshot |
| 7. Folder / Module Boundaries | ❌ Violations found |
| 8. Centralized Validation | ❌ Violations found |
| 9. Project to ViewModels at the Boundary | ❌ Systemic violations found |
| 10. Services Own `DbContext` | ❌ Violations found |
| 11. Codebase Hygiene & Consistency | ❌ Violations found |
| 12. Short, Purpose-Focused Comments | ✅ Compliant |

---

## 1. Single Responsibility & Skinny Controllers — ✅ Compliant

No controller contains a direct `DbContext` call, LINQ query, or `SaveChangesAsync`: a repo-wide search for `_db.`, `context.`, `.Where(`, `.Include(`, `.FirstOrDefault`, `.ToListAsync`, `.SaveChangesAsync` inside `Controllers/` returns zero matches. Every action follows the accept-request → call-service → return-response shape. `ApplicationsController.BuildViewModel` and the `BuildDeleteConfirmModel` helpers in `PropertiesController`/`UnitsController` only assemble a ViewModel from data already returned by a service — that's response-shaping, not business logic, so it stays within the rule, though it does feed directly into the Rule 9 finding below (that assembly work could move into the service instead).

## 2. Extract Business Logic into Services — ⚠️ Partial violations

The application-status "open"/"editable" business rule (`Domain/Rules/RentalApplicationRules.IsOpen` / `IsEditable`) is re-implemented inline, a second time, directly in Razor markup instead of being read from a precomputed ViewModel flag:

- `Views/Applications/Wizard.cshtml:66` — `@if (Model.Status is ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.Returned)` duplicates `RentalApplicationRules.IsOpen`.
- `Views/Applications/Index.cshtml:65` — `@(app.Status is ApplicationStatus.Draft or ApplicationStatus.Returned ? "Continue" : "View")` duplicates `RentalApplicationRules.IsEditable`.
- `Views/Applications/Index.cshtml:67` — the same `Draft or Submitted or Returned` condition as the Wizard case, duplicating `IsOpen` again.

Because this is the exact status list already centralized in `RentalApplicationRules`, a future status change (e.g. adding a new open status) requires updating the rule class *and* finding and updating these three view-level copies — exactly what Rule 2 exists to prevent. `ApplicationWizardViewModel.IsEditable` already shows the right pattern (a boolean computed once in the controller/service and simply branched on in the view); `ApplicationListItemViewModel` and `PmApplicationListItemViewModel` don't yet have the equivalent `IsEditable`/`CanWithdraw` flags, which is why the views fell back to re-deriving it themselves.

This also overlaps with the Rule 8 finding below: `ApplicationWizardService.SaveApplicantInfoAsync` (`Services/Applications/ApplicationWizardService.cs:71-99`) hand-rolls required-field and email-format checks with `string.IsNullOrWhiteSpace`/`EmailAddressAttribute` inside the service, rather than expressing them as `DataAnnotations` on `ApplicationWizardViewModel` the way every other form in the app does — see Rule 8 for detail.

## 3. Separate Data Access — ⚠️ Partial violations

See Rule 10 below (same underlying finding, from the data-access-layering angle): `UnitListViewComponent` and `ApplicationSummaryViewComponent` query `ApplicationDbContext` directly rather than through a service, which is a direct-DbContext-access path outside the layer Rule 3 reserves for it. Controllers themselves are clean (see Rule 1).

## 4. Keep Razor Views Clean — ⚠️ Partial violations

No view injects `DbContext` or a service directly — a repo-wide search for `@inject` across `Views/` returns zero matches, so the "query the database directly from a view" failure mode doesn't occur. However, the three inline status-membership checks listed under Rule 2 (`Wizard.cshtml:66`, `Index.cshtml:65`, `Index.cshtml:67`) are exactly the kind of "complex logical operation" Rule 4 asks to keep out of `.cshtml` files — a multi-value business condition, not a simple display branch. By contrast, `Views/Shared/_StatusBadge.cshtml`'s `switch` from `ApplicationStatus` to a Bootstrap CSS class, and `Wizard.cshtml:18-20`'s `Denied ? "alert-danger" : "alert-warning"` wording choice, are purely presentational mappings (label/color, not a business decision) and are fine as-is.

**Fix for both Rule 2 and Rule 4:** add `IsEditable`/`CanWithdraw` (or similar) booleans to `ApplicationListItemViewModel` and `PmApplicationListItemViewModel`, computed once via `RentalApplicationRules` where the ViewModel is built, and have the three view call sites read those flags instead of re-deriving them.

## 5. Type Safety & DTOs — ✅ Compliant

Every `return View(...)` / `return PartialView(...)` across all controllers passes a dedicated ViewModel (`ApplicationListViewModel`, `BrowseUnitsViewModel`, `ApplicationWizardViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, `ConfirmDeleteViewModel`, etc.) — confirmed by inspecting every `View(`/`PartialView(` call site in `Controllers/`. No action renders a raw `Property`, `Unit`, or `RentalApplication` entity. Every POST action binds to a form ViewModel rather than an entity, so over-posting isn't possible.

## 6. Safe Refactoring Strategy — ➖ Not applicable to a static snapshot

This rule describes a *process* for making future changes, not a property the current code either satisfies or violates on its own. The one relevant fact: the existing 99 tests in `PropertyRentalSystem.Tests` (`Domain/Rules`, `Services`, `ViewModels`) already give the "write/verify tests first" step of this rule something real to build on — keep that coverage current as the fixes below are applied, including adding tests for any `IsEditable`/`CanWithdraw` ViewModel flags introduced to resolve Rule 2/4.

## 7. Folder / Module Boundaries — ❌ Violations found

(Documented in `ARCHITECTURE_RULES.md` §7; restated here for completeness.)

- Two unrelated, non-nested folders are both named "Domain": `Domain/Rules` (business-rule classes) and `Models/Domain` (EF Core entities).
- `ViewComponents/` is a flat folder (`UnitListViewComponent.cs`, `ApplicationSummaryViewComponent.cs`), unlike `Controllers/`, `Services/`, `ViewModels/`, and `Views/`, which are all grouped by feature (`Properties`, `Units`, `Applications`, `Review`).

## 8. Centralized Validation — ❌ Violations found

(Documented in `ARCHITECTURE_RULES.md` §8; restated here for completeness.)

`RegisterViewModel`, `LoginViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, and `ResidenceHistoryFormViewModel` all validate presence/format via `DataAnnotations`/`IValidatableObject`. `ApplicationWizardViewModel` has none; instead `ApplicationWizardService.SaveApplicantInfoAsync` (`Services/Applications/ApplicationWizardService.cs:77-90`) manually checks `FullName`, `Phone`, `Email` (including re-running `new EmailAddressAttribute().IsValid(email)` by hand), and `CurrentAddress`, reporting failures as `ServiceResult` field errors instead. Same category of rule (field presence/format), two different mechanisms, in two different layers.

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

## 11. Codebase Hygiene & Consistency — ❌ Violations found

(Documented in `ARCHITECTURE_RULES.md` §11; restated here for completeness.)

`Controllers/AccountController.cs` has several comments written in Ukrainian while the rest of the codebase comments in English:
- Line 21: `// ---------- Реєстрація ----------`
- Line 28: `// Сервер не довіряє тому, що прийшло з форми: пропускаємо тільки дві ролі`
- Line 40: `// Помилки Identity: "пароль закороткий", "email зайнятий" і т.д.`
- Line 57: `// одразу залогінити`
- Line 61: `// ---------- Вхід ----------`
- Line 81: `// Повертаємо тільки на свій сайт, щоб ніхто не підсунув посилання на чужий`
- Line 88: `// ---------- Вихід ----------`

A repo-wide scan for non-ASCII (Cyrillic-range) characters across every `.cs`/`.cshtml` file in both projects found no other occurrences — this file is the only offender. Formatting/bracing style (brace-less single-statement guard clauses like `if (x == null) return NotFound();`) is applied uniformly across the codebase and is a deliberate, consistent house style rather than a defect — not flagged here.

## 12. Short, Purpose-Focused Comments — ✅ Compliant

A scan for `/* */` block comments across `PropertyRentalSystem.Web` returns zero matches, and a scan for runs of 4+ consecutive `//` lines in the same tree returns exactly one: `ViewModels/Applications/ApplicationWizardViewModel.cs:6-9`. That comment will need rewriting once Phase 3 of `REMEDIATION_PLAN.md` lands — it currently explains why validation is done manually "rather than via DataAnnotations," a claim Phase 3 makes false — so it's tracked there, not as a separate violation here.

---

## Suggested fix order

1. **Rule 11** (translate the seven comments in `AccountController.cs` to English) — mechanical, zero risk, do first.
2. **Rule 8** (move `ApplicationWizardViewModel`'s validation onto `DataAnnotations`, delete the manual checks from `ApplicationWizardService`) — contained to one view model + one service method, has existing test coverage to refactor against (per Rule 6).
3. **Rule 2 / 4** (add `IsEditable`/`CanWithdraw` to the two list ViewModels, update the three view call sites) — small, testable, removes the duplicated business rule from Razor.
4. **Rule 10** (move `UnitListViewComponent`'s and `ApplicationSummaryViewComponent`'s queries into `IUnitService`/`IApplicationWizardService`) — closes the one data-access-boundary gap.
5. **Rule 9** (project the five listed read paths straight to their ViewModels in the service query, following `UnitListViewComponent`'s existing pattern) — do this after Rule 10, since two of the queries in scope are moving out of view components as part of that fix anyway.
6. **Rule 7** (resolve the "Domain" naming collision, group `ViewComponents` by feature) — a rename/move, best done last since it touches the most file paths and should land on a clean diff.
