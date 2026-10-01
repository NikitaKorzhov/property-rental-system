# Architecture Rules

These are the binding rules this codebase is organized around. They apply to all new code and to any refactoring of existing code — see [`TECHNICAL_SOLUTION.md`](../TECHNICAL_SOLUTION.md) §12 for how they show up in the current implementation, and [`CLAUDE.md`](../CLAUDE.md) for the day-to-day architecture summary.

**Status: the codebase is fully compliant with all 12 rules below** — [`RULES_COMPLIANCE_AUDIT.md`](RULES_COMPLIANCE_AUDIT.md) is the up-to-date audit, and [`REMEDIATION_STATUS.md`](REMEDIATION_STATUS.md) records how the rules 7–10 gaps originally found here were closed. Rules stay in force for all new code regardless — this file is the ongoing reference, not a one-time checklist.

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

**Status: ✅ Fixed (Phase 6).** Originally: two differently-scoped folders were both named "Domain" (`Domain/Rules`, `Models/Domain`), and `ViewComponents` was flat unlike `Services`/`ViewModels`/`Views`. Resolved by removing "Domain" from the tree entirely rather than just un-colliding it: `Models/Domain/*.cs` → `Models/*.cs` (now pairs symmetrically with `ViewModels/`), `Domain/Rules/*.cs` → `BusinessRules/*.cs`. `ViewComponents/` is now grouped by feature (`ViewComponents/Units/`, `ViewComponents/Applications/`), matching `Services`/`ViewModels`/`Views`. `Models/ErrorViewModel.cs`, found loose in `Models/` during this work despite being a ViewModel by every convention here, was also moved to `ViewModels/Shared/`. See `RULES_COMPLIANCE_AUDIT.md` §7 for the full detail, including an EF Core migration-snapshot subtlety the rename had to account for.

## 8. Centralize Validation in One Place per Concern

**Rule:** For a given form/input, validation must live in exactly one place — not be re-implemented in both the ViewModel and the service that consumes it. As a default split: format/presence rules that don't need the database (required, string length, email shape, cross-field comparisons) belong on the ViewModel via `DataAnnotations`/`IValidatableObject`; rules that genuinely need database state (uniqueness, "does this unit exist," "is this lease active") belong in the service, expressed through `BusinessRules` plus `ServiceResult` errors.

**Status: ✅ Fixed (Phase 3).** Originally, every form but `ApplicationWizardViewModel` validated presence/format via `DataAnnotations`/`IValidatableObject`; `ApplicationWizardService.SaveApplicantInfoAsync` instead hand-rolled `string.IsNullOrWhiteSpace`/`EmailAddressAttribute` checks and reported them as `ServiceResult` field errors — a second mechanism for the same category of rule. `ApplicationWizardViewModel` now carries `[Required]`/`[EmailAddress]` on its four applicant-info fields, the same as every other form; `ApplicationsController.Wizard` checks `ModelState.IsValid` before calling the service, which no longer re-validates field shape at all — only the `IsEditable` business-state check (genuinely DB-dependent) remains there. See `RULES_COMPLIANCE_AUDIT.md` §8 for why this specific relocation needed 2 commits instead of 1 to stay safe.

## 9. Project to ViewModels/DTOs at the Data-Access Boundary — Don't Return More Than the Caller Needs

**Rule:** A query that only backs a list/summary view should select directly into the ViewModel/DTO shape it will render (`.Select(x => new SomeViewModel {...})`), not materialize full entities (with their navigation properties) and map them to a ViewModel afterward in the controller.

**Status: ✅ Fixed (Phase 5).** `UnitListViewComponent` was already doing this correctly (the pattern to copy); five other read paths weren't — `IPropertyService.GetAllAsync`, `IApplicationBrowseService.GetAvailableUnitsAsync`, `IApplicationWizardService.GetMyApplicationsAsync`, `IApplicationReviewService.GetFilteredAsync`, and `GetHistoryAsync` all returned full entity lists that the controller then mapped to a ViewModel itself. All five now project directly in the query. One required care rather than a blind copy: `RentalApplicationRules.IsEditable`/`IsOpen` (needed for `GetMyApplicationsAsync`'s `IsEditable`/`CanWithdraw`) aren't SQL-translatable, so those two fields are filled in with a loop over the materialized rows *after* `ToListAsync()`, not inside `.Select()` — see `RULES_COMPLIANCE_AUDIT.md` §9 for why, and for the EF-InMemory-vs-real-provider gap that made this worth catching before it shipped.

## 10. One Data-Access Boundary — Services Own `DbContext`, Not ViewComponents

**Rule:** This extends rule 3: `ApplicationDbContext` must only be injected into the Services layer. Controllers and ViewComponents call a service; they never hold a `DbContext` reference themselves.

**Status: ✅ Fixed (Phase 4).** `UnitListViewComponent` and `ApplicationSummaryViewComponent` both used to inject `ApplicationDbContext` directly, bypassing the Services layer entirely. `UnitListViewComponent` now calls `IUnitService.GetUnitsForPropertyAsync`. `ApplicationSummaryViewComponent` needed a home that isn't role-specific — it's used by both the applicant's wizard Summary step and the manager's review details page — so a new `IApplicationSummaryService` was added rather than bolting it onto an existing, role-scoped interface; see `RULES_COMPLIANCE_AUDIT.md` §10 for the reasoning.

## 11. Codebase Hygiene & Consistency

**Rule:** All names (classes, methods, variables, files, folders), all comments, and all user-facing text (UI labels, validation/error messages, seed data) are in English only, across the **entire solution** — `PropertyRentalSystem.Web` and `PropertyRentalSystem.Tests` alike — and across every config/infrastructure file (`Dockerfile`, `docker-compose.yml`, `db-init/`, `.env.example`, etc.), not just application code. No exceptions, no mixing languages per file or per author. Formatting/bracing style is also applied consistently file-to-file.

**Status (as of Phase 1, `REMEDIATION_PLAN.md`):** Fixed. `Controllers/AccountController.cs` had several Ukrainian-language comments and `PropertyRentalSystem.Web/Dockerfile` had Ukrainian-language comments too — the latter wasn't in the original audit's scope (which only scanned `.cs`/`.cshtml`), found during Phase 1 execution by re-running the scan across the whole repo instead. Both are now English. A Cyrillic-range scan across the full solution — `PropertyRentalSystem.Web`, `PropertyRentalSystem.Tests`, every root-level config/infra file, and this plan's own documentation (`.git`/`bin`/`obj`/`.idea` excluded) — now finds none left anywhere.

The bracing half of this rule (consistent formatting file-to-file) is, as of `refactoring_plan/FURTHER_IMPROVEMENTS_PLAN.md` Commit 5, mechanically enforced rather than just manually followed: a repo-root `.editorconfig` sets `csharp_prefer_braces = true:warning`/`dotnet_diagnostic.IDE0011.severity = warning`, and `dotnet format style --diagnostics IDE0011` was run once to bring every pre-existing brace-less single-statement `if`/`else` body into line. New code that omits braces now shows as a build warning instead of relying on a reviewer to catch it.

## 12. Short, Purpose-Focused Comments

**Rule:** A comment is one or two lines, and explains *why* the code does something non-obvious — not *what* it does (the code already says that) or a running narration of the logic. If an explanation needs more than a couple of lines, that's usually a sign the code itself needs a clearer name, a smaller method, or a short pointer — not a longer comment.

**Current state:** Already followed almost everywhere — there are no `/* */` block comments anywhere in `PropertyRentalSystem.Web`, and only one comment in the whole codebase runs longer than two lines: `ViewModels/Applications/ApplicationWizardViewModel.cs:6-9`. That comment is also about to go stale — Phase 3 of `REMEDIATION_PLAN.md` moves validation onto `DataAnnotations`, which is the exact thing the comment currently says isn't done — so trimming/rewriting it is folded into that phase rather than tracked separately.
