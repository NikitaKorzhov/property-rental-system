# Technical Solution

This document explains *how* the requirements in [`tech_task.md`](tech_task.md) were implemented, and the reasoning behind the key design decisions. It is the write-up the demo video's "discuss your Technical Solution" section is based on. For day-to-day development commands and architecture rules, see [`CLAUDE.md`](CLAUDE.md); for setup/installation, see [`README.md`](README.md).

## 1. Stack and project shape

- **ASP.NET Core MVC + Razor on .NET 10**, server-rendered throughout — no SPA framework, per the spec's constraint. Interactivity (modals, partial refreshes) is handled by a single small fetch-based helper, `wwwroot/js/modal-forms.js`, rather than a client-side framework.
- **EF Core (code-first) + SQL Server**, with migrations applied automatically on startup.
- **ASP.NET Identity** for authentication and the two roles (`Applicant`, `PropertyManager`, defined in `Models/Domain/Roles.cs`), using cookie auth.
- **Bootstrap 5** for layout/styling and its modal component.
- **xUnit + EF Core InMemory** for the test project (99 tests), **Bogus** for seed data.

Two projects in one solution: `PropertyRentalSystem.Web` (the app) and `PropertyRentalSystem.Tests` (which references the web project directly, so services/rules are tested in-process against InMemory rather than through HTTP).

## 2. Layered architecture

The codebase is intentionally split into three layers so that business logic is easy to find, easy to unit test, and never leaks into the web framework:

```
Controller  →  Service (ServiceResult)  →  Domain/Rules (pure, static)
   │                   │                            │
HTTP / ModelState   DB access & orchestration   business rules, no EF Core
```

- **Controllers** (`Controllers/`) only translate HTTP concerns: read the model/route values, call a service, map the result to a view/partial/redirect. No EF Core query or business rule lives here.
- **Services** (`Services/<Feature>/`, one interface + implementation per folder: `Properties`, `Units`, `Applications`, `Review`) do all database access via `ApplicationDbContext` and call into the rules layer. Every service method returns `ServiceResult`/`ServiceResult<T>` — a success flag plus a list of `ServiceError(Field, Message)` — instead of throwing for business failures. A controller turns a failed `ServiceResult` into `ModelState.AddModelError(error.Field, error.Message)`, which keeps the service layer decoupled from MVC entirely (see `Services/ServiceResult.cs`).
- **Domain/Rules** (`Domain/Rules/LeaseRules.cs`, `UnitTypeRules.cs`, `RentalApplicationRules.cs`) are pure, static, DB-free functions — the only layer the "business logic" unit tests the spec asks for target directly.

This split is also what makes `dotnet test` fast and deterministic: `Domain/Rules` tests need no database at all, and `Services` tests run against EF Core InMemory rather than a real SQL Server.

## 3. Data model

Core entities (`Models/Domain/`): `Property` → `Unit` (→ `UnitType` lookup) ← `RentalApplication` (→ `ResidenceHistory`, `ApplicationStatusHistory`, optional `Lease`) ← `ApplicationUser` (Identity user).

Key modeling decisions, encoded as EF Core constraints in `Data/ApplicationDbContext.OnModelCreating`:

- **One lease per application** is enforced by a one-to-one FK (`Lease.RentalApplicationId` unique) — not just application logic — so it can't be violated even by a bug or a concurrent write.
- **Unit numbers unique per property** (`[PropertyId, UnitNumber]` unique index), since two different buildings can legitimately both have a "101".
- **Delete policy is `Restrict` everywhere except an application's own detail rows.** Deleting a `Property`, `UnitType`, `Unit`, or user that's referenced elsewhere is rejected at the DB level rather than silently cascading and losing audit history; `ResidenceHistory`, `ApplicationStatusHistory`, and `Lease` cascade only with their owning `RentalApplication`, since they're meaningless without it.
- **`RentalApplication.IsApplicantInfoComplete` / `IsResidenceHistoryComplete`** are explicit booleans rather than inferring completion from data presence — an empty `ResidenceHistories` collection can't otherwise be distinguished from "this section was never visited." `Submit` is gated on both being `true` (`RentalApplicationRules.CanSubmit`).

## 4. The two core business rules

### 4.1 No double-booked unit (`Domain/Rules/LeaseRules.cs`)

> "A unit whose lease term covers today is not available."

`LeaseRules.CoversDate(start, end, date)` is the single predicate used everywhere a unit's availability matters, and `LeaseRules.ComputeEndDate(start)` computes the 12-month term as `start.AddMonths(12).AddDays(-1)` (so Jan 1 → Dec 31, not Jan 1 next year — a twelve-month term, not twelve months plus a day).

It's checked at two separate points, both required by the spec and both covered by service tests:

- **At submit** (`ApplicationWizardService.SubmitAsync`): rejected if any lease on the unit currently covers today.
- **At approval** (`ApplicationReviewService.ReviewAsync`, `ReviewOutcome.Approve`): rejected the same way — this is the check that actually prevents a second lease, since two applications for the same unit can both reach `Submitted` before either is reviewed. Rejecting one at approval time leaves every other open application for that unit untouched, per the spec ("other open applications for that unit are left as they are").

### 4.2 Application status / editability state machine (`Domain/Rules/RentalApplicationRules.cs`)

Statuses: `Draft, Submitted, Returned, Approved, Denied, Withdrawn` (`Models/Domain/ApplicationStatus.cs`), with `Approved`/`Denied`/`Withdrawn` terminal.

- `IsEditable(status)` — true only for `Draft`/`Returned` — is the single server-side source of truth for whether a wizard section renders editable. It's checked both when *rendering* (to decide read-only vs. editable partials) and when *handling a POST* (`ApplicationsController.Wizard`, `AddResidence`, etc. all re-check it before writing), so a stale client (back button, second tab, resubmitted form) can never bypass it — the controller just redirects back to the wizard, which recomputes the real step from the database.
- `IsOpen(status)` — true for `Draft`/`Submitted`/`Returned` — gates `Withdraw`.
- Every transition appends an `ApplicationStatusHistory` row (`Status`, `ChangedByUserId`, `ChangedAt`, optional `Comment`), which is what drives the status/review history the application page shows to property managers.

## 5. The rental application wizard

Implements the spec's single-page, one-view-model, one-form/one-action wizard (`Controllers/ApplicationsController.Wizard`, `ViewModels/Applications/ApplicationWizardViewModel`, `Services/Applications/ApplicationWizardService`):

- **One `[HttpPost] Wizard(ApplicationWizardViewModel model, string action)`** action dispatches on the `action` field submitted by whichever button was clicked (`Continue`, `Back`, `Submit`) combined with `model.Step` — not three separate actions — per the spec's "one form posts to one action."
- **`Continue`** calls the matching service method (`SaveApplicantInfoAsync`, `CompleteResidenceHistoryAsync`), which validates first and only flips the section's `IsComplete` flag / persists on success; on failure it re-renders the *same* step with `ModelState` errors attached via the `ServiceResult` → `ModelState` mapping described in §2. This is "Continue validates, persists only when valid" from the spec, verbatim.
- **`Back`** calls `ApplicationWizardService.GoBack(step)` (a pure step→step mapping) and re-renders without touching the database — explicitly `ModelState.Clear()`'d first so the previous step's Razor helpers (`asp-for`) read the fresh view model instead of redisplaying the just-submitted form's values under the same field names.
- **`Submit`** is only reachable from `WizardStep.Summary` and only succeeds once `RentalApplicationRules.CanSubmit` is true (both sections complete) — enforced server-side in `ApplicationWizardService.SubmitAsync`, independent of whatever the UI shows.
- **Editable vs. read-only** is decided once, server-side, per §4.2, and threaded through `ApplicationWizardViewModel.IsEditable` — the same partial renders either an input or plain text depending on that flag, rather than maintaining two separate templates.
- **Residence history** (`ResidenceHistoryService`, `_ResidenceForm.cshtml` partial) is managed entirely through modals — add/edit/delete — each a GET action that returns a partial and a POST action that either returns the same partial with validation errors or signals success (see §6), exactly matching the spec's modal pattern.

## 6. Modal / partial-view pattern (`wwwroot/js/modal-forms.js`)

A single generic script implements the spec's required UX for *every* modal in the app (properties, units, residences, withdraw/delete confirmations, review):

1. An element with `data-modal-url="<GET action>"` fetches a partial view and injects it into a shared `#crudModal`.
2. On submit, the form POSTs via `fetch`; the controller action either:
   - returns the **same partial view** with `ModelState` errors (validation failed) — the script swaps it back into the modal in place, or
   - signals success via an `X-Form-Success: true` response header (`ModalFormControllerBase.FormSuccess()`) — the script closes the modal and re-fetches `data-refresh-url` into `data-refresh-target` (or falls back to a full page reload if a form doesn't declare a specific target, e.g. Withdraw, which can be triggered from two different pages).

This one script/convention covers every CRUD and review modal in the app, instead of bespoke JS per feature — it's the concrete implementation of "populate modals from partial views returned by controller actions... re-render in place on failure, close and refresh on success."

## 7. Properties, units, and the unit-type lookup

- `PropertiesController`/`UnitsController` (both `[Authorize(Roles = Roles.PropertyManager)]`) run add/edit/remove through the same modal pattern as §6.
- **`UnitType` is Active/Inactive**, and `Domain/Rules/UnitTypeRules.CanAssign(candidateIsActive, candidateUnitTypeId, currentUnitTypeId)` is the single server-side gate for the spec's rule: an inactive type stays valid for the unit that already has it (`candidateUnitTypeId == currentUnitTypeId`) but can't be *newly* selected for any other unit. It's checked in the unit service on both create and edit — never trusted from the client dropdown.
- **Unit availability** for applicants browsing (`ApplicationBrowseService`) excludes any unit with a lease currently covering today, using the same `LeaseRules.CoversDate` predicate as §4.1 — one rule, two call sites (browse-time filtering and submit/approval-time rejection), so they can't drift apart.

## 8. Review flow

`ApplicationReviewController` (`[Authorize(Roles = Roles.PropertyManager)]`) + `ApplicationReviewService.ReviewAsync`:

- Only a `Submitted` application is reviewable (`GetReviewableAsync` returns `null` otherwise, which the controller turns into `NotFound`) — a manager can't review a `Draft` they happened to guess the ID of, or re-review something already decided.
- The review modal's outcome is `Approve | Return | Deny` (`Models/Domain/ReviewOutcome.cs`); the comment-required-for-Return/Deny rule is enforced in the view model's validation (`ViewModels/Review/`), tested directly in `PropertyRentalSystem.Tests/ViewModels/`.
- `Approve` re-checks "unit already has an active lease" (§4.1) before creating the `Lease` — this is the check that actually matters, since it's the first point two competing `Submitted` applications for the same unit are resolved.
- Every outcome appends an `ApplicationStatusHistory` row; `ApplicationReviewController`'s application detail view and `ViewComponents/ApplicationSummaryViewComponent` render the full history (who/when/comment) to the manager.

## 9. Filtering and authorization

- **Application lists are filtered in the database**, not in memory: `ApplicationWizardService.GetMyApplicationsAsync` and `ApplicationReviewService.GetFilteredAsync` both build an `IQueryable<RentalApplication>` and append `.Where(...)` clauses conditionally based on which filters (`status`, `propertyId`) were supplied, so EF Core translates the whole thing to one SQL query — never `ToListAsync()` followed by LINQ-to-objects filtering.
- **Ownership is enforced server-side, not just hidden in the UI.** Applicants only ever see their own applications (`GetMyApplicationsAsync` always scopes by `applicantId`; wizard/residence actions use `GetOwnedAsync`/`GetOwnedEditableApplicationAsync`, which return `null` — turned into `404`, not `403`, to avoid confirming another user's application ID exists — if the row isn't owned by the caller). Property managers see all applications, with no ownership filter.
- **Role separation is enforced at the controller level** via `[Authorize(Roles = Roles.Applicant)]` / `[Authorize(Roles = Roles.PropertyManager)]` on `ApplicationsController`, `PropertiesController`, `UnitsController`, `ApplicationReviewController` respectively, backed by a global `AuthorizeFilter` in `Program.cs` that requires authentication on every action by default (new controllers must opt out with `[AllowAnonymous]`, never opt in) — satisfying the spec's "ensure the permissions in the controllers and the UI reflect this."

## 10. Startup: migrations and idempotent seeding

On every app start (`Program.cs`, before `app.Run()`):

1. `DbInitializer.SeedAsync` calls `context.Database.MigrateAsync()` — creates the database and applies any pending migrations.
2. Roles are created if missing (`RoleManager.RoleExistsAsync`).
3. Seed users are created only if they don't already exist (`EnsureUserAsync` looks up by email first) — all seeded with password `Password123!`.
4. Lookups/properties/units/applications are each seeded only `if (!context.<Set>.Any())` — so re-running the app against an already-seeded database is a no-op, not a duplicate-data bug. This is the "seeded idempotently" requirement.
5. `Bogus` (`Randomizer.Seed = new Random(42)`) generates realistic names/addresses/phone numbers deterministically, so the same data appears on every clean run — useful for reproducible demos/recordings.
6. The seed deliberately includes **one unit with the inactive `2-Bedroom` type already assigned** (so §7's rule has something to demonstrate) and **an application in every status**, including a second `Submitted` application competing for the same unit an `Approved` application already leased (so §4.1's rejection path has real data to trigger), per the spec's "applications in every status" requirement.

## 11. Testing strategy

99 xUnit tests across three folders mirroring the architecture in §2:

- **`Domain/Rules`** — direct, no-dependency tests of `LeaseRules`, `UnitTypeRules`, `RentalApplicationRules` (e.g. lease date-range edge cases, the inactive-type-stays-valid-for-its-own-unit case, the Draft/Returned editability boundary). This is the "unit tests for business logic" the spec asks for.
- **`Services`** — one test class per service, run against `Microsoft.EntityFrameworkCore.InMemory` via a shared `TestDb` factory: ownership/editability checks, the lease-creation-on-approval path (including the active-lease rejection), wizard step transitions, and status-history recording. These exercise the orchestration layer without needing a real SQL Server.
- **`ViewModels`** — cross-field validation that lives in data annotations / `IValidatableObject` rather than a service (residence move-in/move-out ordering, review comment required for Return/Deny).

## 12. Organizing principles

These are the conventions the codebase is built around — most are already implied by §2–§9 above, but are collected here as explicit rules to follow when extending the app, since they're what keeps the three-layer split in §2 from eroding over time.

1. **A layer only talks to the layer directly below it.** Controller → Service → Domain/Rules. A controller never opens an `ApplicationDbContext` or evaluates a business condition itself; a service never inspects `ModelState` or HTTP concerns; a rule class never touches EF Core. Violating this (e.g. a query inlined in a controller) is the main thing to avoid when adding a feature.
2. **Business failures are values, not exceptions.** Every service method that can fail for a business reason (not a 404/400 HTTP concern) returns `ServiceResult`/`ServiceResult<T>` — a success flag plus field-scoped `ServiceError`s — instead of throwing. A controller maps a failed result onto `ModelState` with one line (`ModelState.AddModelError(error.Field, error.Message)`). Exceptions are reserved for genuinely unexpected failures (e.g. seeding errors in `DbInitializer`), not for expected business rejections like "unit already has an active lease."
3. **One interface + one implementation per feature folder.** `Services/Properties`, `Services/Units`, `Services/Applications`, `Services/Review` each group their interface(s) and implementation(s) together, mirroring the matching `Controllers`/`ViewModels`/`Views` feature split — so a change to one feature touches one area of the tree, and a new feature gets its own folder rather than being folded into an existing service.
4. **A business predicate is defined once and reused at every call site**, never re-derived inline. `LeaseRules.CoversDate` backs unit availability in Browse, the submit check, and the approval check; `RentalApplicationRules.IsEditable` backs both whether a section renders editable and whether a POST to it is accepted. This is what guarantees the three places a rule matters can't silently drift apart.
5. **Authorization defaults to deny, not allow.** The global `AuthorizeFilter` in `Program.cs` requires an authenticated user on every action; a new action must opt *out* with `[AllowAnonymous]` rather than opt in with `[Authorize]`. Role access is declared once per controller (`[Authorize(Roles = Roles.Applicant)]` / `Roles.PropertyManager`) rather than repeated per action, so a controller belongs entirely to one role.
6. **Ownership is re-checked server-side on every request that needs it, never assumed from a prior page load.** `GetOwnedAsync`/`GetOwnedEditableApplicationAsync`/`GetOwnedEditableResidenceAsync` all return `null` for a row that exists but isn't the caller's, and the controller turns that into `404` (not `403`) so the response doesn't confirm another user's resource exists.
7. **State transitions are re-validated on the way in, not trusted from what the UI last rendered.** `ApplicationsController.Wizard` and friends re-check `RentalApplicationRules.IsEditable`/`IsOpen` against the current database row before acting on *any* POST, independent of what step or status the submitted form claims — so a stale tab, browser back/forward, or a resubmitted old page can't force an illegal transition.
8. **Important invariants are enforced at the database level, not only in application code.** "At most one lease per application" and "unit numbers unique within a property" are unique indexes/FK constraints in `OnModelCreating`, not just checks in a service — a bug in the service layer still can't corrupt that data.
9. **Delete behavior defaults to `Restrict`; `Cascade` is opt-in and only for rows that are meaningless without their parent.** Every FK into a shared/audit record (`Property`, `UnitType`, `Unit`, `ApplicationUser`) is `Restrict`. Only `RentalApplication`'s own detail rows (`ResidenceHistory`, `ApplicationStatusHistory`, `Lease`) cascade with it, since they're owned by and meaningless without it.
10. **List queries are composed as `IQueryable` and filtered with `.Where(...)` before the only `ToListAsync()`**, so every filter combination still produces one SQL query — never `ToListAsync()` followed by LINQ-to-objects filtering.
11. **Whether something is editable/visible is a server-side decision passed down through the view model, not inferred in Razor or JavaScript.** `ApplicationWizardViewModel.IsEditable` is computed once in the controller/service and simply branched on in the view — the view never re-derives it from status itself.
12. **Every modal follows the same contract** (`data-modal-url` → GET a partial; POST re-renders the same partial with `ModelState` errors on failure, or signals success via the `X-Form-Success` header and a `data-refresh-url`/`data-refresh-target` pair) instead of bespoke JS per feature — see §6. A new CRUD-via-modal feature should reuse this contract rather than add a new interaction pattern.
13. **Seeding is additive and idempotent**, guarded per entity set (`if (!context.<Set>.Any())`) and per user (`EnsureUserAsync` looks up by email first) — re-running startup against an already-seeded database changes nothing, so it's safe to run on every app start rather than only once.
14. **Tests sit at the boundary matching their layer**: `Domain/Rules` tests take no dependencies at all; `Services` tests run against EF Core InMemory (no real SQL Server needed); `ViewModels` tests exercise validation attributes directly. A new business rule gets a `Domain/Rules` test, not a round-trip service/controller test, whenever it can be expressed as a pure function.

## 13. What was deliberately left out

All "bonus" items in [`tech_task.md`](tech_task.md) are optional per the spec and were not implemented; `README.md`'s "Roadmap / Not Yet Implemented" section explains each one and why, including the explicit note that the spec itself says *"no real-time synchronisation is expected"* for the multi-applicant bonus — so SignalR was not needed to meet the core requirements.
