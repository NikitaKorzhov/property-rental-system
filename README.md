# Property Rental Management System

A full-stack web application built with ASP.NET Core MVC for property management companies to handle properties, units, rental applications, and lease agreements.

---

## Table of Contents
- [Tech Stack](#tech-stack)
- [Core Features & Architecture](#core-features--architecture)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started & Installation](#getting-started--installation)
- [Running Tests](#running-tests)
- [Deliverables](#deliverables)

---

## Tech Stack

- **Backend:** .NET 10, ASP.NET Core MVC, Entity Framework Core (Code-First)
- **Database:** SQL Server / SQL Server Express
- **Authentication & Authorization:** ASP.NET Identity (Cookie-based auth with two roles: *Applicant* and *Property Manager*)
- **Frontend:** Server-rendered Razor Views, Partial Views, View Components, Bootstrap, Modals with AJAX validation[cite: 1]
- **Testing:** xUnit, Moq, FluentAssertions[cite: 1]
- **Seed Data:** Bogus for .NET[cite: 1]

---

## Core Features & Architecture

1. **User Management & Roles:**
    - Sign up, log in, and log out functionality using ASP.NET Identity[cite: 1].
    - Self-selection of role (*Applicant* or *Property Manager*) during registration for testing convenience[cite: 1].

2. **Properties and Units Management:**
    - Property managers can perform full CRUD operations (add, edit, remove) on properties and their units through interactive modals[cite: 1].
    - Unit Type lookup with *Active* and *Inactive* values (inactive types remain displayed on existing units but cannot be selected for new ones)[cite: 1].
    - Automatic lease creation (12-month term) upon application approval, with strict availability checks ensuring a unit with an active lease covering today cannot be rented[cite: 1].

3. **Rental Application Wizard:**
    - Built as a single-page multi-step wizard driven by a single view model:
        - **Applicant Information:** Name, phone, email, current address[cite: 1].
        - **Residence History:** List of prior residences managed via modals[cite: 1].
        - **Summary:** Read-only review view with final submission[cite: 1].
    - Section navigation handled via *Continue*, *Back*, and *Submit* buttons with server-side validation[cite: 1].
    - Strict status enforcement: Applications can only be edited by applicants when in *Draft* or *Returned* statuses[cite: 1].

4. **Review & Lifecycle:**
    - Application statuses: *Draft, Submitted, Returned, Approved, Denied, Withdrawn* (Approved, Denied, and Withdrawn are terminal)[cite: 1].
    - Property managers review submitted applications via a review modal, issuing an outcome (*Approve, Return, Deny*) with a mandatory comment for *Return* and *Deny*[cite: 1].
    - Detailed history tracking of status changes and reviews[cite: 1].

5. **Filtering & Database Logic:**
    - Application lists filtered by status and property directly at the database level[cite: 1].
    - Applicants view their own applications; property managers view all[cite: 1].

---

## Project Structure

```text
PropertyRentalSystem/
│
├── PropertyRentalSystem.Web/          # ASP.NET Core MVC Web Application
│   ├── Controllers/
│   ├── Data/                          # DbContext, Migrations, and Bogus Seeder
│   ├── Models/                        # Domain Entities (Properties, Units, Leases, etc.)
│   ├── ViewModels/
│   └── Views/                         # Razor Views, Partials, and View Components
│
├── PropertyRentalSystem.Tests/        # xUnit Test Project
│   └── BusinessLogicTests.cs          # Unit tests for core business rules
│
└── PropertyRentalSystem.sln           # Solution File
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
   - `web` — the ASP.NET Core MVC app, which waits for `db` to pass its health check before starting.

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