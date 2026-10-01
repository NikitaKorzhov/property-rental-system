# Remediation Status

What actually happened in each phase of [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md), including deviations from the plan. All 6 phases merged to `main`. See [`FURTHER_IMPROVEMENTS.md`](FURTHER_IMPROVEMENTS.md)/[`FURTHER_IMPROVEMENTS_PLAN.md`](FURTHER_IMPROVEMENTS_PLAN.md) for the follow-up pass (also done, pending merge).

| Phase | Status | Notes |
|---|---|---|
| 1 — Translate comments | ✅ Done | Scope widened mid-phase: `Dockerfile` had 4 more Ukrainian comments the original audit's `.cs`/`.cshtml`-only scan missed. |
| 2 — Dedupe view status-checks | ✅ Done | Straightforward — new ViewModel bools, views read them instead of inline `ApplicationStatus is ...` checks. |
| 3 — Centralize wizard validation | ✅ Done (2 commits) | Split deliberately: add the new `DataAnnotations` path first (redundant but safe), remove the old service checks only once proven — one commit risks a window where nothing validates. |
| 4 — ViewComponents → services | ✅ Done (2 commits) | `ApplicationSummaryViewComponent` needed a new `IApplicationSummaryService` (used by both applicant and manager flows, not a fit for either existing role-scoped interface). Verified via EF Core query logs that neither move introduced N+1. |
| 5 — Project to ViewModels | ✅ Done (4 commits) | `IPropertyService.GetAllAsync` kept the existing `PropertyListItemViewModel` (not a minimal Id+Name shape) since one caller needed `Address`. `GetMyApplicationsAsync`'s `IsEditable`/`CanWithdraw` can't go inside `.Select()` (not SQL-translatable) — filled in after `ToListAsync()`, verified against real SQL Server since EF Core's InMemory provider would not have caught the translation failure. |
| 6 — Folder/namespace boundaries | ✅ Done (3 commits) | Widest-reaching phase (72 files) — both `Domain/Rules`→`BusinessRules` and `Models/Domain`→`Models` renamed, not just one side de-collided. EF Core's migration snapshot embeds namespaces as string literals; updated and confirmed in sync via `dotnet ef migrations has-pending-model-changes`. `Controllers/` was left flat (out of scope) — grouped later in `FURTHER_IMPROVEMENTS_PLAN.md`. |

Rule 12 needed no phase of its own — its one stale comment was rewritten as part of Phase 3.
