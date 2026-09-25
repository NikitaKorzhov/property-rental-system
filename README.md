# Property Rental Management System

A full-stack web application built with ASP.NET Core MVC for property management companies to handle properties, units, rental applications, and lease agreements.

**Demo video:** https://drive.google.com/file/d/17QuOJyUgS5mTu-RMuc09fBAAZJlvtEIL/view?usp=sharing

---

## Table of Contents
- [Tech Stack](#tech-stack)
- [Core Features & Architecture](#core-features--architecture)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started & Installation](#getting-started--installation)
- [Test Accounts](#test-accounts)
- [Running Tests](#running-tests)
- [Roadmap / Not Yet Implemented](#roadmap--not-yet-implemented)

---

## Tech Stack

- **Backend:** .NET 10, ASP.NET Core MVC, Entity Framework Core (Code-First)
- **Database:** SQL Server / SQL Server Express
- **Authentication & Authorization:** ASP.NET Identity (cookie-based auth with two roles: *Applicant* and *Property Manager*)
- **Frontend:** Server-rendered Razor views, partial views, view components, Bootstrap 5, modals driven by a small fetch-based JS helper (`modal-forms.js`)
- **Testing:** xUnit, EF Core InMemory provider for service-layer tests
- **Seed Data:** Bogus for .NET

---

## Core Features & Architecture

1. **User Management & Roles:**
    - Sign up, log in, and log out using ASP.NET Identity.
    - Self-selection of role (*Applicant* or *Property Manager*) during registration.

2. **Properties and Units Management:**
    - Property managers can add, edit, and remove properties and their units through modals.
    - Unit Type lookup with *Active* and *Inactive* values — an inactive type still displays on a unit that already uses it, but can't be selected for any other unit (enforced server-side).
    - Approval of a rental application creates a 12-month lease; a unit whose lease term covers today is not available for new applications.

3. **Rental Application Wizard:**
    - Single-page, multi-step wizard driven by one view model:
        - **Applicant Information:** name, phone, email, current address.
        - **Residence History:** list of prior residences, added/edited/removed through a modal.
        - **Summary:** read-only view of both sections, with Submit.
    - Section navigation via *Continue*, *Back*, and *Submit* buttons on a single form/action, with server-side, per-section validation.
    - A section renders editable or read-only based on a server-side decision: applicants can edit while the application is *Draft* or *Returned*; every other status is read-only.
    - Submitting (or approving) is rejected with an error if the unit already has an active lease.

4. **Review & Lifecycle:**
    - Application statuses: *Draft, Submitted, Returned, Approved, Denied, Withdrawn* (the last three are terminal).
    - Property managers review a submitted application through a review modal, choosing an outcome (*Approve, Return, Deny*) with a comment required for *Return* and *Deny*.
    - The application page shows a full history of status changes and review outcomes (who, when, comment).

5. **Filtering & Database Logic:**
    - Application lists are filtered by status and property with the filtering done in the database (`IQueryable.Where`), not in memory.
    - Applicants see only their own applications; property managers see all of them.

6. **Layered Architecture:**
    - Controllers stay thin: they translate HTTP/`ModelState` concerns into calls on a service and translate the result back into a view or a redirect. No EF Core or business logic lives in a controller.
    - The **Services** layer (`Services/Properties`, `Services/Units`, `Services/Applications`, `Services/Review`) owns orchestration that touches the database — loading entities, invoking domain rules, and persisting changes — and reports outcomes through a shared `ServiceResult` / `ServiceResult<T>` type (a success flag plus field-scoped errors), kept deliberately decoupled from `ModelState` and MVC.
    - The **Domain/Rules** layer (`Domain/Rules`) holds pure, DB-free static rule classes (`LeaseRules`, `UnitTypeRules`, `RentalApplicationRules`) that services call into — these are what the business-logic unit tests target directly.

---

## Project Structure

```text
PropertyRentalSystem/
│
├── PropertyRentalSystem.Web/
│   ├── Controllers/                   # Properties, Units, Applications, ApplicationReview, Account, Home
│   │                                  #   — HTTP/ViewModel glue only, no EF Core or business logic
│   ├── Domain/Rules/                  # Pure, DB-free, unit-tested business rules (lease availability,
│   │                                  #   unit-type assignment, application status/editability rules)
│   ├── Services/                      # DB-backed orchestration, grouped by feature, one interface + one
│   │   ├── Properties/                #   implementation per folder; all return ServiceResult/ServiceResult<T>
│   │   ├── Units/
│   │   ├── Applications/              #   ApplicationBrowseService, ApplicationWizardService, ResidenceHistoryService
│   │   ├── Review/                    #   ApplicationReviewService
│   │   └── ServiceResult.cs           #   shared success/field-scoped-error result type
│   ├── Data/                          # ApplicationDbContext, EF Core migrations, Bogus-based DbInitializer
│   ├── Models/Domain/                 # Domain entities (Property, Unit, RentalApplication, Lease, etc.)
│   ├── ViewComponents/                # UnitList, ApplicationSummary
│   ├── ViewModels/                    # Per-feature view models (Account, Properties, Units, Applications, Review)
│   └── Views/                         # Razor views and partials, incl. Views/Shared/Components for view components
│
├── PropertyRentalSystem.Tests/        # xUnit test project (99 tests)
│   ├── Domain/Rules/                  # Tests for the pure business rules above
│   ├── Services/                      # Boundary tests for each service, against EF Core InMemory
│   │   ├── Properties/
│   │   ├── Units/
│   │   ├── Applications/
│   │   ├── Review/
│   │   └── TestDb.cs                  #   shared InMemory ApplicationDbContext factory
│   └── ViewModels/                    # Tests for view-model validation (residence dates, review comment rules)
│
└── PropertyRentalSystem.sln
```

---

## Prerequisites

- [Docker](https://www.docker.com/get-started/) and Docker Compose (bundled with Docker Desktop)
- (Optional, for local development without Docker) [.NET 10 SDK](https://dotnet.microsoft.com/download)

---

## Getting Started & Installation

### Run with Docker (recommended)

1. **Clone the repository** and move into the project folder.

2. **Create your `.env` file** from the provided example and adjust the values if needed:
   ```bash
   cp .env.example .env
   ```
   The `.env` file controls the database and web app configuration:

   | Variable      | Description                                   | Default            |
   |---------------|------------------------------------------------|---------------------|
   | `DB_PORT`     | Host port mapped to the SQL Server container    | `1433`              |
   | `DB_NAME`     | Database name                                   | `PropertyRentalDb`  |
   | `DB_USER`     | SQL login created for the app                   | `rental_admin`      |
   | `DB_PASSWORD` | Password for `DB_USER` and the `sa` account      | —                   |
   | `WEB_PORT`    | Host port mapped to the web application          | `8080`              |

   > `.env` is git-ignored on purpose (it holds a real password) — every machine that runs the project needs its own copy.

3. **Build and start the containers:**
   ```bash
   docker compose up --build
   ```
   This starts two services:
   - `db` — SQL Server 2022. On first start it creates the `PropertyRentalDb` database and the `DB_USER` SQL login (the base image only provisions `sa` by default).
   - `web` — the ASP.NET Core MVC app, which waits for `db` to pass its health check before starting, then applies EF Core migrations and seeds the database automatically.

   To run in the background, add `-d`:
   ```bash
   docker compose up --build -d
   ```

4. **Open the app** at `http://localhost:<WEB_PORT>` (default: [http://localhost:8080](http://localhost:8080)).

5. **View logs** (useful while the database is initializing on first run):
   ```bash
   docker compose logs -f db
   docker compose logs -f web
   ```

6. **Stop the containers:**
   ```bash
   docker compose down
   ```
   Database data persists in the `mssqldata` volume between runs. To wipe it and start completely fresh (e.g. after changing `DB_PASSWORD` in `.env`):
   ```bash
   docker compose down -v
   ```

> **Note (Apple Silicon / ARM machines):** the SQL Server image is `amd64`-only and runs under emulation on ARM hosts. It still works, just expect a slower first start.

### Run locally without Docker

1. Start a local or containerized SQL Server instance yourself, matching the credentials you intend to use.
2. Set the connection string via [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) instead of committing it to `appsettings.json`:
   ```bash
   cd PropertyRentalSystem.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=PropertyRentalDb;User Id=rental_admin;Password=<your-password>;TrustServerCertificate=True;"
   ```
3. Run the app:
   ```bash
   dotnet run --project PropertyRentalSystem.Web
   ```

---

## Test Accounts

The database is seeded automatically on first start with lookups, properties, units, and rental applications in every status. All seeded accounts use the password **`Password123!`**.

| Role             | Email                    | Notes                                   |
|------------------|--------------------------|------------------------------------------|
| Property Manager | `manager@radency.com`    | |
| Property Manager | `manager2@radency.com`   | |
| Applicant        | `applicant@radency.com`  | Has a Draft and a Withdrawn application |
| Applicant        | `applicant2@radency.com` | Has two open Submitted applications — good for trying the review flow |
| Applicant        | `applicant3@radency.com` | Has a Returned application — good for the "correct and resubmit" flow |
| Applicant        | `applicant4@radency.com` | Has an Approved application (with a lease) |
| Applicant        | `applicant5@radency.com` | Has a Denied application |

You can also register a new account from the sign-up page and pick either role.

---

## Running Tests

```bash
dotnet test
```

99 tests across three layers:
- **`Domain/Rules`** — the pure business rules that live outside the controllers/services (lease-availability dates, the inactive-unit-type assignment rule, application status/editability rules).
- **`Services`** — boundary tests for every service (`Properties`, `Units`, `Applications`, `Review`) against the EF Core InMemory provider: filtering, ownership/editability checks, the lease-creation-on-approval flow (including the "unit already has an active lease" rejection), wizard step transitions, and status-history recording.
- **`ViewModels`** — cross-field validation on the residence-history and review-decision forms.

---

## Roadmap / Not Yet Implemented

Everything in the tech spec's core requirements is implemented; the items below are the optional "bonus" enhancements it explicitly does not require, plus a couple of nuances worth being upfront about.

- **Paging/sorting + JSON grid endpoint.** The application list is filtered in the database as required, but isn't paged or sorted, and isn't exposed as a documented JSON/OpenAPI endpoint behind a reusable grid view component.
- **Review queue (claim/release).** A property manager can open and review any submitted application directly; there's no "Under Review" claim step to prevent two managers from working the same application at once.
- **Property-manager-only private notes.** Not implemented — there's currently no field on an application that's writable by a manager and hidden from the applicant.
- **Save-with-errors + Summary blocker list.** A section currently has to pass validation before it's persisted (per the core spec's "*Continue* validates, persists only when valid" rule). The bonus variant — save invalid data anyway, surface every blocking error on the Summary — isn't built.
- **Multiple applicants per application.** An application has exactly one owning applicant today; the bonus's shared-ownership model, plus its optimistic-concurrency rule ("the second save to the same section is rejected as stale"), isn't implemented.

### Real-time updates (SignalR)

Not implemented, but worth addressing directly since it's easy to conflate with the multi-applicant bonus above: the tech spec explicitly says **"no real-time synchronisation is expected"** for that scenario, so skipping it there is by design, not an oversight.

That said, SignalR would be a good fit *elsewhere* in this app, and adding it wouldn't conflict with the "no SPA framework" constraint — a SignalR hub is a transport layer bolted onto the existing server-rendered Razor pages, not a client-side app framework; the rendering model stays exactly as it is today. Two places it would genuinely help:

- **The property-manager application list / review queue** — push a lightweight "a new application was submitted" or "application #123 was just claimed" event to connected managers, instead of requiring a manual refresh. This pairs naturally with the review-queue bonus above.
- **An applicant's own application page** — notify the applicant in near real time when a manager finishes a review (Approved/Returned/Denied), rather than only surfacing the new status on next page load.

Both are additive: a hub that broadcasts on the same events the relevant service (`ApplicationReviewService`, `ApplicationWizardService`) already raises when it changes status, with the Razor views subscribing via a small script to refresh just the affected fragment — the same "refresh the affected part of the page" pattern the modal-forms already use, just pushed instead of pulled. It would not be a good fit for masking the lack of real synchronization in the multi-applicant bonus, since SignalR delivers notifications, not conflict resolution — that still needs the optimistic-concurrency check the spec calls for.
