# Rules Compliance Audit

Current status of the codebase against [`ARCHITECTURE_RULES.md`](ARCHITECTURE_RULES.md).

| Rule | Status |
|---|---|
| 1. Skinny Controllers | ✅ Compliant |
| 2. Business Logic in Services | ✅ Fixed (Phase 2) |
| 3. Separate Data Access | ✅ Fixed (Phase 4) |
| 4. Clean Razor Views | ✅ Fixed (Phase 2) |
| 5. DTOs / Type Safety | ✅ Compliant |
| 6. Strangler-Fig Refactoring | ➖ N/A (a process, not a code property) |
| 7. Folder / Module Boundaries | ✅ Fixed (Phase 6 + later) |
| 8. Centralized Validation | ✅ Fixed (Phase 3) |
| 9. Project to ViewModels at the Boundary | ✅ Fixed (Phase 5 + later) |
| 10. Services Own `DbContext` | ✅ Fixed (Phase 4) |
| 11. Codebase Hygiene & Consistency | ✅ Fixed (Phase 1 + later) |
| 12. Short, Purpose-Focused Comments | ✅ Compliant |

## Notes worth keeping (non-obvious findings)

- **Rule 7:** "Domain" was used for two unrelated things (`Domain/Rules`, `Models/Domain`) — removed from the tree entirely rather than just de-collided. `Controllers/` stayed flat through Phase 6 (out of its scope) and was grouped by feature later, in `FURTHER_IMPROVEMENTS_PLAN.md`.
- **Rule 8:** moving `ApplicationWizardViewModel`'s validation safely took 2 commits — add the new path first (redundant but safe), remove the old path once proven, never a single commit that could leave a gap where nothing validates.
- **Rule 9:** a `BusinessRules` method called inside `.Select()`/`.Where()` compiles and passes against EF Core's InMemory test provider, then throws at runtime against real SQL Server — caught by testing against a real SQL Server container, not just `dotnet test`. The durable fix for a *predicate* (not a projected field) is an `Expression<Func<T,bool>>` twin (`LeaseRules.IsActiveOn` alongside `LeaseRules.CoversDate`), which EF Core can translate directly. `ApplicationReviewController.Details` (`GetByIdAsync` → `GetDetailsAsync`) was a single-entity read found later that genuinely needed this treatment — most `GetByIdAsync`-style reads don't, since they feed a tracked-entity mutation.
- **Rule 10:** `ApplicationSummaryViewComponent` is used by both the applicant wizard and the manager review page, so its query went into a new `IApplicationSummaryService` rather than an existing role-scoped interface.
- **Rule 7 (EF Core gotcha):** migration snapshot/designer files embed entity namespaces as string literals — a namespace rename has to update those too, or the model silently desyncs from the snapshot. `dotnet ef migrations has-pending-model-changes` is the check.
- **Rule 11:** bracing was a deliberate, consistent house style (not a bug) until the original reviewer flagged it explicitly — closed via a `.editorconfig` rule enforced from then on, not a one-time manual sweep.

A follow-up pass against the original reviewer feedback ([`FURTHER_IMPROVEMENTS.md`](FURTHER_IMPROVEMENTS.md)/[`FURTHER_IMPROVEMENTS_PLAN.md`](FURTHER_IMPROVEMENTS_PLAN.md)) closed everything these 6 phases hadn't covered. All 5 of its commits are done, on `refactor/further-improvements` (pushed, pending merge).
