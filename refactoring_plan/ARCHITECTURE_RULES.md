# Architecture Rules

Binding rules this codebase is organized around, for all new code. See [`RULES_COMPLIANCE_AUDIT.md`](RULES_COMPLIANCE_AUDIT.md) for current compliance status.

## 1. Skinny Controllers
Controllers only: accept the request, call a service, return a view/redirect/JSON. No `DbContext`, no business logic.

## 2. Business Logic in Services
Decision-making and data manipulation live in services, not controllers or Razor — so it's unit-testable without a web server or database.

## 3. Separate Data Access
`DbContext`/LINQ/persistence stay behind the Services layer, never in controllers or Views.

## 4. Keep Razor Views Clean
Views render a model and produce HTML — no complex logic, no `@inject Context`.

## 5. Type Safety & DTOs
Entities never bind directly to forms/controllers (over-posting risk) — always a dedicated ViewModel/DTO.

## 6. Safe Refactoring (Strangler Fig)
Change iteratively: verify tests → extract → confirm green → move on. Never a big-bang rewrite.

---

The rules below were added after a code-review pass; each closed a gap between the codebase and rules 1–6.

## 7. Clear Folder / Module Boundaries
One name, one responsibility, per top-level folder.
**Status: ✅ Fixed (Phase 6; Controllers grouped later in `FURTHER_IMPROVEMENTS_PLAN.md`).** "Domain" removed from the tree (`Models/Domain`→`Models`, `Domain/Rules`→`BusinessRules`); `ViewComponents/` and `Controllers/` now grouped by feature like `Services`/`ViewModels`/`Views`.

## 8. Centralize Validation in One Place per Concern
Format/presence → `DataAnnotations`/`IValidatableObject` on the ViewModel; DB-dependent rules → the service, via `BusinessRules`.
**Status: ✅ Fixed (Phase 3).** `ApplicationWizardViewModel` now validates like every other form; the service only re-checks DB-dependent state.

## 9. Project to ViewModels at the Data-Access Boundary
A list/summary/single-entity read-only query should select straight into its ViewModel, not materialize a full entity.
**Status: ✅ Fixed (Phases 5–6 + a later finding).** All list/summary reads project directly; `ApplicationReviewController.Details` was the one single-entity exception, closed via `GetDetailsAsync`. One caveat: a field derived from a `BusinessRules` method isn't SQL-translatable — compiles and passes against EF Core's InMemory provider, throws against real SQL Server — so such fields are filled in after `ToListAsync()`, and any reusable query *predicate* (not a projected field) is expressed as an `Expression<Func<T,bool>>` instead (`LeaseRules.IsActiveOn`).

## 10. One Data-Access Boundary — Services Own `DbContext`
ViewComponents call a service, never hold a `DbContext` themselves.
**Status: ✅ Fixed (Phase 4).** `UnitListViewComponent`/`ApplicationSummaryViewComponent` now call `IUnitService`/a new `IApplicationSummaryService`.

## 11. Codebase Hygiene & Consistency
English only — names, comments, user-facing text — across the whole solution and every config file. Formatting applied consistently.
**Status: ✅ Fixed (Phase 1 for language; `FURTHER_IMPROVEMENTS_PLAN.md` Commit 5 for bracing).** A repo-root `.editorconfig` (`csharp_prefer_braces = true:warning`) now enforces braces mechanically, not just by convention.

## 12. Short, Purpose-Focused Comments
One or two lines, explaining *why*, not *what*.
**Status: ✅ Compliant.**
