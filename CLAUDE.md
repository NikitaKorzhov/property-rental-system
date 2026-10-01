# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Business context

This is a tech-assignment solution: a full-stack web application for a property management company's internal staff and prospective tenants. Two roles share the same app:

- **Applicant** — a prospective tenant who browses available units, fills in a multi-step rental application (personal info + residence history), submits it, and can correct/resubmit if a manager sends it back.
- **Property Manager** — manages the company's property/unit inventory and reviews submitted applications (Approve / Return / Deny), with an approval automatically creating a 12-month lease.

The core domain rule the whole system protects: **a unit with an active lease (one whose 12-month term covers today) cannot be assigned to, or approved for, another application.** Most of the interesting business logic (`BusinessRules/`) exists to enforce this plus the application's status/editability state machine (`Draft → Submitted → Returned/Approved/Denied/Withdrawn`).

[`tech_task.md`](tech_task.md) (a Markdown transcription of `tech_task.pdf`) is the original specification this app was built against — consult it for the authoritative requirements (including the optional "bonus" scope) before assuming a feature is missing or out of scope. [`TECHNICAL_SOLUTION.md`](TECHNICAL_SOLUTION.md) explains how each requirement was implemented and why (architecture, data model, the two core business rules, the wizard/modal patterns) — read it before making architectural changes, so new work stays consistent with the existing design rationale. `README.md` has a maintained "Roadmap / Not Yet Implemented" section listing which bonus items were deliberately left out and why (e.g. real-time sync via SignalR is explicitly *not* required by the spec for the multi-applicant scenario).

## Commands

All commands run from the repo root unless noted.

```bash
# Run the full app via Docker (SQL Server + web, migrations/seed applied automatically)
cp .env.example .env            # first time only; edit DB_PASSWORD etc.
docker compose up --build       # add -d to detach
docker compose down             # stop; add -v to also wipe the DB volume

# Run locally without Docker (requires a reachable SQL Server and .NET 10 SDK)
cd PropertyRentalSystem.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=PropertyRentalDb;User Id=rental_admin;Password=<pwd>;TrustServerCertificate=True;"
dotnet run --project PropertyRentalSystem.Web

# Build
dotnet build

# Run all tests (108 tests)
dotnet test

# Run a single test / fixture, e.g.:
dotnet test --filter "FullyQualifiedName~LeaseRulesTests"
dotnet test --filter "FullyQualifiedName~LeaseRulesTests.Method_Scenario"

# EF Core migrations (run inside PropertyRentalSystem.Web)
dotnet ef migrations add <Name> --project PropertyRentalSystem.Web
dotnet ef database update --project PropertyRentalSystem.Web
```

Seeded test accounts (password `Password123!` for all) are documented in `README.md` under "Test Accounts" — use them when manually exercising a flow instead of registering fresh users, since several encode specific application states (Draft, two open Submitted, Returned, Approved-with-lease, Denied).

## Architecture

Single ASP.NET Core MVC project (`PropertyRentalSystem.Web`, net10.0) with server-rendered Razor views (Bootstrap 5, no SPA framework — CRUD and the review flow go through Bootstrap modals driven by `wwwroot/js/modal-forms.js`, a small fetch-based helper that posts a form and swaps in the returned partial), plus a companion xUnit test project (`PropertyRentalSystem.Tests`) that references it directly and tests two of its three layers.

**The three-layer split is the architecture to preserve when adding or changing features:**

1. **Controllers** (`Controllers/`) are HTTP/`ModelState` glue only — they call a service, then map the `ServiceResult` back to a view/redirect/partial. No EF Core query or business rule should ever appear in a controller.
2. **Services** (`Services/<Feature>/`, one interface + one implementation per folder — `Properties`, `Units`, `Applications`, `Review`) own all database access and orchestration: loading entities, invoking business rules, persisting changes. They never touch `ModelState`; instead every service method returns `ServiceResult` / `ServiceResult<T>` (success flag + field-scoped errors), which is how business failures (e.g. "unit already has an active lease") travel back to a controller without coupling the service layer to MVC. Read paths that back a list/summary view project straight to their ViewModel inside the EF Core query (`.Select(x => new SomeViewModel {...})`) rather than returning full entities for the controller to map — the one exception is a business-rule-derived field that EF Core can't translate to SQL (e.g. `ApplicationListItemViewModel.IsEditable`/`CanWithdraw`, from `RentalApplicationRules`), which gets filled in with a loop over the materialized rows *after* `ToListAsync()`, not inside `.Select()`.
3. **BusinessRules** (`BusinessRules/`: `LeaseRules`, `UnitTypeRules`, `RentalApplicationRules`) are pure, static, DB-free rule classes with no EF Core dependency. This is the layer the business-logic unit tests target directly (`PropertyRentalSystem.Tests/BusinessRules/`); keep new business rules here, not inline in a service, so they stay independently testable.

Services are registered in `Program.cs` (`AddScoped<I...Service, ...Service>()`) — register any new service there. `Program.cs` also wires: Identity with two roles (`Roles.cs`) and cookie auth (`/Account/Login`, `/Account/AccessDenied`), a global `AuthorizeFilter` so every action requires authentication by default (opt out per-action with `[AllowAnonymous]`, don't flip the default), EF Core with `EnableRetryOnFailure()` to ride out the SQL container's startup window, and automatic migration + Bogus-based seeding (`Data/DbInitializer.cs`) on app startup inside a `try/catch` that logs and rethrows.

Other structural notes:
- `Models/` holds the EF Core entities (`Property`, `Unit`, `UnitType`, `RentalApplication`, `ApplicationStatus`, `ApplicationStatusHistory`, `Lease`, `ResidenceHistory`, `ReviewOutcome`, `WizardStep`, `ApplicationUser`), flat and mirroring `ViewModels/` — and `Data/ApplicationDbContext` + `Migrations/`.
- The rental application wizard (`Services/Applications/ApplicationWizardService`, `ViewModels/Applications/`) is driven by one view model across three sections (Applicant Information, Residence History, Summary); whether a section renders editable is a server-side decision based on application status (editable only in `Draft`/`Returned`), not client-side state. A section is only persisted once its own validation passes (`Continue` validates-then-persists) — the "save invalid data and list blockers on Summary" bonus variant described in the README is not implemented, so don't assume that behavior exists.
- Filtering of application lists (`ApplicationBrowseService`) is done in the database via `IQueryable.Where`, not in memory — keep new list/filter features consistent with that pattern. Applicants only ever see their own applications; property managers see all.
- `ViewComponents/` is grouped by feature like `Services`/`ViewModels`/`Views` (`ViewComponents/Units/UnitListViewComponent`, `ViewComponents/Applications/ApplicationSummaryViewComponent`); both call a service (`IUnitService`, `IApplicationSummaryService`) rather than holding `ApplicationDbContext` themselves. `Views/Shared/Components/` holds their templates, keyed by component name, independent of the component's namespace/folder.

**Refactoring history:** [`refactoring_plan/`](refactoring_plan/) documents a completed 6-phase refactor that brought this codebase into compliance with `ARCHITECTURE_RULES.md` (`RULES_COMPLIANCE_AUDIT.md` is the current, fully-passing audit; `REMEDIATION_PLAN.md`/`REMEDIATION_STATUS.md` record what was planned vs. what actually happened in each phase, including a few deviations worth reading before assuming a similar change will go the same way). The rules in `ARCHITECTURE_RULES.md` remain binding for all new code.

## Infrastructure

- **Local/dev runtime:** `docker-compose.yml` defines two services — `db` (`mcr.microsoft.com/mssql/server:2022-latest`, custom entrypoint `db-init/entrypoint.sh` + `db-init/init.sql` that provisions the `DB_NAME` database and the `DB_USER` SQL login since the base image only creates `sa`) and `web` (built from `PropertyRentalSystem.Web/Dockerfile`). `web` waits on `db`'s healthcheck, which deliberately probes with `DB_USER`/`DB_NAME` rather than `sa` to avoid a startup race against `db-init`. Config is environment-driven via `.env` (git-ignored; see `.env.example` for `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `WEB_PORT`). DB data persists in the `mssqldata` named volume.
- **No CI/CD, cloud, or container-registry config exists in this repo** — it's a self-contained local/demo deployment only (`docker compose up`). Don't assume a pipeline, hosting target, or secrets manager beyond the `.env` file.
- **Auth:** ASP.NET Identity, cookie-based, two roles (`Applicant`, `Property Manager`) assigned at registration.
