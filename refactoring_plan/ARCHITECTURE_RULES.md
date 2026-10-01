# Architecture Rules

These are the binding rules this codebase is organized around and refactored toward. They apply to all new code and to any refactoring of existing code — see [`TECHNICAL_SOLUTION.md`](../TECHNICAL_SOLUTION.md) §12 for how they already show up in the current implementation, and [`CLAUDE.md`](../CLAUDE.md) for the day-to-day architecture summary.

## 1. Single Responsibility & "Skinny Controllers" (Fat Controllers → Skinny Controllers)

**Rule:** Controllers in MVC must not contain business logic, direct database calls (`DbContext`), or complex calculations.

**Goal:** A controller does exactly three things:
1. Accepts the HTTP request (`ViewModel` / DTO).
2. Passes the data to the service layer (`Application` / `Services`).
3. Returns the response (`View`, `Redirect`, or JSON).

## 2. Extract Business Logic into Services / Use Cases

**Rule:** All decision-making logic, business-rule validation, and data manipulation must be extracted out of controllers and Razor templates into independent services.

**Goal:** Pieces of code must be easily coverable by unit tests, without needing to stand up a web server or a database.

## 3. Separate Data Access (Data Access / Repository / EF Core)

**Rule:** Direct `DbContext` calls, LINQ queries against tables, and data persistence must not be scattered across controllers or Views.

**Goal:** Persistence work must be isolated behind a Repository, Unit of Work, or data-access-service pattern, so it can easily be moved into an Infrastructure layer in the future.

## 4. Keep Razor Views Clean (Presentation Layer)

**Rule:** `.cshtml` files must not perform complex logical operations or query services/the database directly (e.g. via `@inject Context`).

**Goal:** A View must be a "dumb component" — it only renders the model it was given and produces HTML.

## 5. Type Safety & DTOs (Data Transfer Objects)

**Rule:** Domain models or DB entities must never be passed directly into edit forms or controllers (to avoid over-posting / excessive binding). Use dedicated ViewModels or DTOs instead.

## 6. Safe Refactoring Strategy (Strangler Fig)

**Rule:** Never rewrite everything at once. Refactoring proceeds iteratively:
1. Write/verify tests for the existing piece of code (using the companion test project).
2. Extract part of the logic into a service.
3. Confirm the tests are green.
4. Move on to the next component.

---

The rules below were added after a code-review pass against the current implementation; each flags a gap the review found between how the codebase is organized today and rules 1–6 above.

## 7. Clear, Unambiguous Folder / Module Boundaries

**Rule:** Every top-level folder must have one clearly named responsibility, and no two folders may claim overlapping or confusingly similar names. A contributor should be able to tell where a new file belongs from the folder names alone, without reading code first.

**Current gap:** The project has two differently-scoped folders both named "Domain" — `Domain/Rules` (pure business-rule classes) and `Models/Domain` (EF Core entities) — which do not nest under each other and are easy to confuse. In addition, `ViewComponents` (unlike `Controllers`, `Services`, `ViewModels`, and `Views`, which are all grouped by feature — `Properties`, `Units`, `Applications`, `Review`) is a flat folder with no feature subgrouping, so it doesn't follow the same organizing convention as the rest of the tree.

**Goal:** Rename or restructure so "Domain" is used once, for one concept (e.g. keep `Models/Domain` for entities and rename the rules folder to something like `BusinessRules/` or `Domain/Policies`), and group `ViewComponents` by feature the same way `Services`/`ViewModels` already are.

## 8. Centralize Validation in One Place per Concern

**Rule:** For a given form/input, validation must live in exactly one place — not be re-implemented in both the ViewModel and the service that consumes it. As a default split: format/presence rules that don't need the database (required, string length, email shape, cross-field comparisons) belong on the ViewModel via `DataAnnotations`/`IValidatableObject`; rules that genuinely need database state (uniqueness, "does this unit exist," "is this lease active") belong in the service, expressed through `Domain/Rules` plus `ServiceResult` errors.

**Current gap:** Most forms (`RegisterViewModel`, `LoginViewModel`, `PropertyFormViewModel`, `UnitFormViewModel`, `ReviewFormViewModel`, `ResidenceHistoryFormViewModel`) validate via `DataAnnotations`/`IValidatableObject` on the ViewModel, as `ModelState` naturally expects. `ApplicationWizardViewModel`, however, has no `DataAnnotations` at all — `ApplicationWizardService.SaveApplicantInfoAsync` instead hand-rolls `string.IsNullOrWhiteSpace` and `EmailAddressAttribute` checks itself and reports them as `ServiceResult` field errors. That's a second validation mechanism for the same category of rule (field presence/format), living in a different layer than everywhere else. Pick one pattern for presence/format validation and apply it consistently — including for the wizard.

## 9. Project to ViewModels/DTOs at the Data-Access Boundary — Don't Return More Than the Caller Needs

**Rule:** A query that only backs a list/summary view should select directly into the ViewModel/DTO shape it will render (`.Select(x => new SomeViewModel {...})`), not materialize full entities (with their navigation properties) and map them to a ViewModel afterward in the controller.

**Current gap:** `UnitListViewComponent` already does this correctly — it projects straight to `UnitListItemViewModel` inside the EF Core query. Several other read paths don't: `ApplicationWizardService.GetMyApplicationsAsync` and `ApplicationReviewService.GetFilteredAsync` both `.Include(...)` and return full `List<RentalApplication>` entities (every column, plus `Unit`/`Property` navigation graphs) even though their only caller immediately maps 3–4 fields onto a list-item ViewModel in the controller. Prefer the `UnitListViewComponent` pattern everywhere a list/summary is the end goal — project in the query, return the ViewModel from the service, and let the controller just pass it to the view.

## 10. One Data-Access Boundary — Services Own `DbContext`, Not ViewComponents

**Rule:** This extends rule 3: `ApplicationDbContext` must only be injected into the Services layer. Controllers and ViewComponents call a service; they never hold a `DbContext` reference themselves.

**Current gap:** `UnitListViewComponent` and `ApplicationSummaryViewComponent` both inject `ApplicationDbContext` directly and query it themselves, bypassing the Services layer entirely — the same boundary rule 3 draws for controllers isn't currently applied to view components. Move their queries into the matching feature service (`IUnitService`, an application summary method on `IApplicationBrowseService`/`IApplicationWizardService`) so there is exactly one place per entity that talks to the database.

## 11. Codebase Hygiene & Consistency

**Rule:** All names (classes, methods, variables, files, folders), all comments, and all user-facing text (UI labels, validation/error messages, seed data) are in English only, across the **entire solution** — `PropertyRentalSystem.Web` and `PropertyRentalSystem.Tests` alike — and across every config/infrastructure file (`Dockerfile`, `docker-compose.yml`, `db-init/`, `.env.example`, etc.), not just application code. No exceptions, no mixing languages per file or per author. Formatting/bracing style is also applied consistently file-to-file.

**Status (as of Phase 1, `REMEDIATION_PLAN.md`):** Fixed. `Controllers/AccountController.cs` had several Ukrainian-language comments and `PropertyRentalSystem.Web/Dockerfile` had Ukrainian-language comments too — the latter wasn't in the original audit's scope (which only scanned `.cs`/`.cshtml`), found during Phase 1 execution by re-running the scan across the whole repo instead. Both are now English. A Cyrillic-range scan across the full solution — `PropertyRentalSystem.Web`, `PropertyRentalSystem.Tests`, every root-level config/infra file, and this plan's own documentation (`.git`/`bin`/`obj`/`.idea` excluded) — now finds none left anywhere.

## 12. Short, Purpose-Focused Comments

**Rule:** A comment is one or two lines, and explains *why* the code does something non-obvious — not *what* it does (the code already says that) or a running narration of the logic. If an explanation needs more than a couple of lines, that's usually a sign the code itself needs a clearer name, a smaller method, or a short pointer — not a longer comment.

**Current state:** Already followed almost everywhere — there are no `/* */` block comments anywhere in `PropertyRentalSystem.Web`, and only one comment in the whole codebase runs longer than two lines: `ViewModels/Applications/ApplicationWizardViewModel.cs:6-9`. That comment is also about to go stale — Phase 3 of `REMEDIATION_PLAN.md` moves validation onto `DataAnnotations`, which is the exact thing the comment currently says isn't done — so trimming/rewriting it is folded into that phase rather than tracked separately.
