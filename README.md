# Property Rental Management System

A full-stack web application built with ASP.NET Core MVC for property management companies to handle properties, units, rental applications, and lease agreements.

---

## Table of Contents
- [Tech Stack](#tech-stack)
- [Core Features & Architecture](#core-features--architecture)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started & Installation](#getting-started--installation)
- [Test Accounts](#test-accounts)
- [Running Tests](#running-tests)

---

## Tech Stack

- **Backend:** .NET 10, ASP.NET Core MVC, Entity Framework Core (Code-First)
- **Database:** SQL Server / SQL Server Express
- **Authentication & Authorization:** ASP.NET Identity (cookie-based auth with two roles: *Applicant* and *Property Manager*)
- **Frontend:** Server-rendered Razor views, partial views, view components, Bootstrap 5, modals driven by a small fetch-based JS helper (`modal-forms.js`)
- **Testing:** xUnit
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

---

## Project Structure

```text
PropertyRentalSystem/
│
├── PropertyRentalSystem.Web/
│   ├── Controllers/                   # Properties, Units, Applications, ApplicationReview, Account, Home
│   ├── Domain/Rules/                  # Pure, unit-tested business rules (lease availability,
│   │                                  #   unit-type assignment, application status rules)
│   ├── Data/                          # ApplicationDbContext, EF Core migrations, Bogus-based DbInitializer
│   ├── Models/Domain/                 # Domain entities (Property, Unit, RentalApplication, Lease, etc.)
│   ├── ViewComponents/                # UnitList, ApplicationSummary
│   ├── ViewModels/                    # Per-feature view models (Account, Properties, Units, Applications, Review)
│   └── Views/                         # Razor views and partials, incl. Views/Shared/Components for view components
│
├── PropertyRentalSystem.Tests/        # xUnit test project
│   ├── Domain/Rules/                  # Tests for the business rules above
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

The test project covers the business rules that live outside the controllers in `PropertyRentalSystem.Web/Domain/Rules/` (lease-availability dates, the inactive-unit-type assignment rule, application status/editability rules) plus the cross-field validation on the residence-history and review-decision forms.
