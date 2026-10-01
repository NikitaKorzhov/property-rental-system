# Further Improvements — Execution Plan

Exact, unambiguous instructions for the 5 commits that implement the concrete findings in [`FURTHER_IMPROVEMENTS.md`](FURTHER_IMPROVEMENTS.md) §1, §2, §3, §4, §5. One branch, 5 commits, in the order below.

**Revision history:**
- v1 deferred §4 (in-memory lease filter) and §5 (braces) entirely, then contradicted that deferral by adding brace-less new code in Commit 2 while leaving §4 untouched inside the exact block Commit 1 was already rewriting. Fixed: §4's `ReviewAsync` instance folded into Commit 1; every new line in the plan uses braces.
- v2 split "fix the lease filter" across one closed commit (`ReviewAsync`) and one merely-disclosed-but-excluded finding (`SubmitAsync`), and left two more occurrences (`ApplicationBrowseService`) undiscovered. Fixed: all 4 occurrences of the same pattern are now closed by this plan (Commit 1 + Commit 4), via a single reusable `LeaseRules.IsActiveOn` expression instead of copying the condition by hand into each call site. §5 (braces repo-wide) is no longer deferred either — it's Commit 5, enforced via `.editorconfig` rather than a one-off manual sweep, so the rule stays enforced for code written after this plan too.

**Before each commit:** `dotnet build` (0 warnings/errors) and `dotnet test` (all passing). **After each commit:** re-run both, plus the specific check listed under that commit's "Verify" step.

---

## Commit 1 — `ReviewAsync`: switch instead of if/else, lease check via `LeaseRules.IsActiveOn`

### 1a. Add `LeaseRules.IsActiveOn` — `PropertyRentalSystem.Web/BusinessRules/LeaseRules.cs`

This is the one place the new expression is defined; Commit 4 reuses it as-is.

**Replace the whole file with:**

```csharp
using System.Linq.Expressions;
using PropertyRentalSystem.Web.Models;

namespace PropertyRentalSystem.Web.BusinessRules;

public static class LeaseRules
{
    // "A unit whose lease term covers today is not available."
    public static bool CoversDate(DateTime startDate, DateTime endDate, DateTime date) =>
        startDate <= date && endDate >= date;

    // Same rule as CoversDate, as an expression tree EF Core can translate into SQL — a plain
    // method call to CoversDate inside a query can't be. LeaseRulesTests asserts they agree.
    public static Expression<Func<Lease, bool>> IsActiveOn(DateTime date) =>
        l => l.StartDate <= date && l.EndDate >= date;

    // A twelve-month lease runs through the day before its one-year anniversary (e.g.
    // Jan 1 – Dec 31), not through the anniversary date itself — otherwise the term would
    // run 12 months plus one extra day.
    public static DateTime ComputeEndDate(DateTime startDate) => startDate.AddMonths(12).AddDays(-1);
}
```

Only the two `using` lines and the new `IsActiveOn` method are additions; `CoversDate` and `ComputeEndDate` are unchanged.

### 1b. `ReviewAsync` — `PropertyRentalSystem.Web/Services/Review/ApplicationReviewService.cs`

**Replace this exact block** (currently lines 67–93):

```csharp
        if (outcome == ReviewOutcome.Approve)
        {
            var today = DateTime.UtcNow.Date;
            var unitLeases = await _db.Leases.Where(l => l.UnitId == application.UnitId).ToListAsync();
            if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            {
                // The approval check prevents a second lease. Other open applications for
                // this unit are left as they are — only this one is rejected.
                return ServiceResult.Fail("This unit already has an active lease. Approval is blocked to prevent a second lease.");
            }

            application.Status = ApplicationStatus.Approved;
            application.Lease = new Lease
            {
                UnitId = application.UnitId,
                StartDate = today,
                EndDate = LeaseRules.ComputeEndDate(today)
            };
        }
        else if (outcome == ReviewOutcome.Return)
        {
            application.Status = ApplicationStatus.Returned;
        }
        else
        {
            application.Status = ApplicationStatus.Denied;
        }
```

**With this exact block:**

```csharp
        switch (outcome)
        {
            case ReviewOutcome.Approve:
            {
                var today = DateTime.UtcNow.Date;
                var hasActiveLease = await _db.Leases
                    .Where(l => l.UnitId == application.UnitId)
                    .AnyAsync(LeaseRules.IsActiveOn(today));
                if (hasActiveLease)
                {
                    // The approval check prevents a second lease. Other open applications for
                    // this unit are left as they are — only this one is rejected.
                    return ServiceResult.Fail("This unit already has an active lease. Approval is blocked to prevent a second lease.");
                }

                application.Status = ApplicationStatus.Approved;
                application.Lease = new Lease
                {
                    UnitId = application.UnitId,
                    StartDate = today,
                    EndDate = LeaseRules.ComputeEndDate(today)
                };
                break;
            }

            case ReviewOutcome.Return:
            {
                application.Status = ApplicationStatus.Returned;
                break;
            }

            case ReviewOutcome.Deny:
            {
                application.Status = ApplicationStatus.Denied;
                break;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
        }
```

What changed and why, precisely:
- `if`/`else if`/`else` → `switch` on the 3-value `ReviewOutcome` enum (§2's finding).
- `default: throw new ArgumentOutOfRangeException(...)` added. Without it, a future 4th `ReviewOutcome` value would fall through the switch silently — `application.Status` stays unchanged, but the `ApplicationStatusHistories.Add(...)` call after the switch still runs, recording a history row for a status change that never happened.
- The lease check no longer loads every lease on the unit into memory with `.ToListAsync()` and filters with `.Any(l => LeaseRules.CoversDate(...))` in C#. It's `AnyAsync(LeaseRules.IsActiveOn(today))` — EF Core receives the expression tree directly and translates it into SQL; no row is materialized into memory for this check (§4's finding). No explanatory comment is needed at the call site — `IsActiveOn` is the self-documenting name; the one-time "why two methods" comment lives on `IsActiveOn`'s own definition instead (1a), not repeated at every call site.
- Every brace that was already there stays; every *new* line this edit introduces has braces around it, including the single-statement `case` bodies (`Return`, `Deny`, `default`) — not stylistically required by C#, but applied here deliberately so this commit doesn't add more brace-less code while a brace-related finding is open (closed by Commit 5).

Nothing else in the file changes — the method signature, the guard clause above this block (`if (application.Status != ApplicationStatus.Submitted) ...`, pre-existing code outside this block's scope, left untouched here and fixed for braces in Commit 5), and everything after the `switch` (the `ApplicationStatusHistories.Add(...)` call) stay exactly as they are.

### 1c. New test — append to `PropertyRentalSystem.Tests/BusinessRules/LeaseRulesTests.cs`

Add `using PropertyRentalSystem.Web.Models;` to the top of the file (needed for `Lease`). Then add this test, asserting `IsActiveOn` and `CoversDate` agree at every boundary already covered above for `CoversDate` alone — this is what actually backs the "LeaseRulesTests asserts they agree" comment in 1a:

```csharp
    [Theory]
    [InlineData(-10, 10, true)]    // within range
    [InlineData(1, 365, false)]    // starts in the future
    [InlineData(-400, -1, false)]  // already ended
    [InlineData(0, 365, true)]     // starts today (inclusive)
    [InlineData(-365, 0, true)]    // ends today (inclusive)
    public void IsActiveOn_AgreesWithCoversDate(int startOffsetDays, int endOffsetDays, bool expected)
    {
        var start = Today.AddDays(startOffsetDays);
        var end = Today.AddDays(endOffsetDays);
        var lease = new Lease { StartDate = start, EndDate = end };

        var viaCoversDate = LeaseRules.CoversDate(start, end, Today);
        var viaIsActiveOn = LeaseRules.IsActiveOn(Today).Compile()(lease);

        Assert.Equal(expected, viaCoversDate);
        Assert.Equal(viaCoversDate, viaIsActiveOn);
    }
```

### 1d. New test — append to `PropertyRentalSystem.Tests/Services/Review/ApplicationReviewServiceTests.cs`

The existing `ReviewAsync_ApproveWhenUnitAlreadyHasActiveLease_FailsAndPreventsASecondLease` test only covers a lease that's clearly active (`today.AddDays(-1)` to `today.AddDays(30)`). Add this one to lock in the *inclusive* boundary — a lease ending exactly today must still count as active, through the real `AnyAsync(LeaseRules.IsActiveOn(...))` call this time, not just `CoversDate` in isolation:

```csharp
    [Fact]
    public async Task ReviewAsync_ApproveWhenLeaseEndsExactlyToday_StillCountsAsActiveAndFails()
    {
        await using var db = TestDb.Create();
        var (_, unit, application) = await SeedAsync(db);
        var today = DateTime.UtcNow.Date;
        db.Leases.Add(new Lease { UnitId = unit.Id, StartDate = today.AddDays(-30), EndDate = today });
        await db.SaveChangesAsync();
        var service = new ApplicationReviewService(db);

        var result = await service.ReviewAsync(application, ReviewOutcome.Approve, null, "manager-1");

        Assert.False(result.Succeeded);
    }
```

**Verify:** `dotnet test --filter "FullyQualifiedName~ApplicationReviewServiceTests|FullyQualifiedName~LeaseRulesTests"` — all pre-existing tests plus the 2 new ones pass. Then, because this replaces a `LeaseRules`-driven in-memory check with `AnyAsync(Expression<Func<Lease,bool>>)` — the kind of change Phase 5 found EF Core's InMemory test provider can be more permissive about than real SQL Server — **also** rebuild the Docker image and manually approve a submitted application against a unit with an active lease through the running app, confirming the same rejection message appears. Do not rely on `dotnet test` alone to sign off this one.

**Commit message subject:** `ReviewAsync: switch with a default case, lease check via LeaseRules.IsActiveOn`

---

## Commit 2 — Add `GetDetailsAsync`, remove the now-unused `GetByIdAsync`

`IApplicationReviewService.GetByIdAsync` has exactly one caller in the entire codebase (`ApplicationReviewController.Details`) and zero direct tests — confirmed by a full-repo search before writing this plan. After this commit it has zero callers, so it is deleted rather than left as dead code.

### 2a. `PropertyRentalSystem.Web/Services/Review/IApplicationReviewService.cs`

Delete this line:
```csharp
    Task<RentalApplication?> GetByIdAsync(int id);
```

Add this line in its place (same position in the interface):
```csharp
    // Projected straight to the ViewModel (Rule 9) — History is filled in by the caller afterward.
    Task<ApplicationDetailsViewModel?> GetDetailsAsync(int id);
```

### 2b. `PropertyRentalSystem.Web/Services/Review/ApplicationReviewService.cs`

Delete this line:
```csharp
    public async Task<RentalApplication?> GetByIdAsync(int id) => await _db.RentalApplications.FindAsync(id);
```

Add this method in its place (same position in the class):
```csharp
    public async Task<ApplicationDetailsViewModel?> GetDetailsAsync(int id) =>
        await _db.RentalApplications
            .Where(a => a.Id == id)
            .Select(a => new ApplicationDetailsViewModel
            {
                Id = a.Id,
                Status = a.Status,
                CanReview = a.Status == ApplicationStatus.Submitted
            })
            .FirstOrDefaultAsync();
```

No `using` changes needed in either file — `ApplicationDetailsViewModel` is in `PropertyRentalSystem.Web.ViewModels.Review`, already imported in both files (it's the namespace `PmApplicationListItemViewModel`/`StatusHistoryItemViewModel` already come from).

### 2c. `PropertyRentalSystem.Web/Controllers/ApplicationReviewController.cs`

**Replace this exact method body:**
```csharp
    public async Task<IActionResult> Details(int id)
    {
        var application = await _review.GetByIdAsync(id);
        if (application == null) return NotFound();

        var history = await _review.GetHistoryAsync(id);

        return View(new ApplicationDetailsViewModel
        {
            Id = application.Id,
            Status = application.Status,
            CanReview = application.Status == ApplicationStatus.Submitted,
            History = history
        });
    }
```

**With this exact method body** — note the braced `if`, unlike the rest of this controller's existing one-line guard clauses, for the same reason as Commit 1 (closed repo-wide by Commit 5):
```csharp
    public async Task<IActionResult> Details(int id)
    {
        var model = await _review.GetDetailsAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        model.History = await _review.GetHistoryAsync(id);
        return View(model);
    }
```

### 2d. New tests — append to `PropertyRentalSystem.Tests/Services/Review/ApplicationReviewServiceTests.cs`

```csharp
    [Fact]
    public async Task GetDetailsAsync_ForExistingApplication_MapsFieldsAndSetsCanReview()
    {
        await using var db = TestDb.Create();
        var (_, _, application) = await SeedAsync(db, ApplicationStatus.Submitted);
        var service = new ApplicationReviewService(db);

        var result = await service.GetDetailsAsync(application.Id);

        Assert.NotNull(result);
        Assert.Equal(application.Id, result!.Id);
        Assert.Equal(ApplicationStatus.Submitted, result.Status);
        Assert.True(result.CanReview);
    }

    [Fact]
    public async Task GetDetailsAsync_ForUnknownApplication_ReturnsNull()
    {
        await using var db = TestDb.Create();
        var service = new ApplicationReviewService(db);

        var result = await service.GetDetailsAsync(999);

        Assert.Null(result);
    }
```

Place both methods anywhere inside the class body — exact position doesn't matter, xUnit discovers by attribute.

**Verify:** `dotnet build` (confirms `GetByIdAsync` is gone with zero leftover references — if any existed, this step would fail to compile); `dotnet test --filter "FullyQualifiedName~ApplicationReviewServiceTests"` — the 2 new tests pass, plus everything from Commit 1 still passes.

**Commit message subject:** `Project ApplicationReviewController.Details to a ViewModel instead of loading a full entity`

---

## Commit 3 — Group `Controllers/` by feature

**Critical detail, not optional:** `PropertiesController`, `UnitsController`, `ApplicationsController`, and `ApplicationReviewController` all inherit from `ModalFormControllerBase`, which stays at `Controllers/ModalFormControllerBase.cs` (it isn't one feature's controller). Once those 4 files move to a different namespace, each needs `using PropertyRentalSystem.Web.Controllers;` added so `ModalFormControllerBase` still resolves — **without this, the build fails.** `AccountController` and `HomeController` inherit directly from ASP.NET Core's `Controller` and need no such addition. Confirmed before writing this plan: nothing anywhere in the codebase references the `PropertyRentalSystem.Web.Controllers` namespace by `using` except the controller files' own declarations, so no other file needs touching.

Perform all 5 moves, then the namespace edits, then build once at the end — this is one mechanical commit, not 5 separate ones.

### 3a. Move files (`git mv`, preserves history)

| From | To |
|---|---|
| `Controllers/PropertiesController.cs` | `Controllers/Properties/PropertiesController.cs` |
| `Controllers/UnitsController.cs` | `Controllers/Units/UnitsController.cs` |
| `Controllers/ApplicationsController.cs` | `Controllers/Applications/ApplicationsController.cs` |
| `Controllers/ApplicationReviewController.cs` | `Controllers/Review/ApplicationReviewController.cs` |
| `Controllers/AccountController.cs` | `Controllers/Account/AccountController.cs` |

`Controllers/HomeController.cs` and `Controllers/ModalFormControllerBase.cs` **do not move** — neither belongs to one feature (mirrors `Services/ServiceResult.cs` staying at the `Services/` root).

### 3b. Namespace + using edits, one per moved file

| File | Change `namespace PropertyRentalSystem.Web.Controllers;` to | Add this `using` (only if not already present) |
|---|---|---|
| `Controllers/Properties/PropertiesController.cs` | `namespace PropertyRentalSystem.Web.Controllers.Properties;` | `using PropertyRentalSystem.Web.Controllers;` |
| `Controllers/Units/UnitsController.cs` | `namespace PropertyRentalSystem.Web.Controllers.Units;` | `using PropertyRentalSystem.Web.Controllers;` |
| `Controllers/Applications/ApplicationsController.cs` | `namespace PropertyRentalSystem.Web.Controllers.Applications;` | `using PropertyRentalSystem.Web.Controllers;` |
| `Controllers/Review/ApplicationReviewController.cs` | `namespace PropertyRentalSystem.Web.Controllers.Review;` | `using PropertyRentalSystem.Web.Controllers;` |
| `Controllers/Account/AccountController.cs` | `namespace PropertyRentalSystem.Web.Controllers.Account;` | *(none needed)* |

This mirrors the existing `Services/Properties` → `namespace ....Services.Properties` convention exactly — same pattern, new folder.

**Verify:** `dotnet build` (0 warnings/0 errors — a missing `using PropertyRentalSystem.Web.Controllers;` on any of the first 4 files fails to compile with a clear "ModalFormControllerBase could not be found" error, so the build step itself proves correctness); `dotnet test` (unchanged count from Commit 2 — no test references a controller by namespace); manually load the app (`docker compose up --build`) and click through one page per moved controller (Properties list, a Unit edit modal, Browse → Start an application, the Review list) to confirm routing still resolves every action.

**Commit message subject:** `Group Controllers/ by feature, matching Services/ViewModels/Views/ViewComponents`

---

## Commit 4 — Close the remaining 3 occurrences of the in-memory lease filter

Same finding as Commit 1, same fix, same `LeaseRules.IsActiveOn` (already defined in 1a — not redefined here). All 4 occurrences in the codebase of "load leases, filter with `LeaseRules.CoversDate` in memory" are closed after this commit: `ReviewAsync` (Commit 1), and these 3.

### 4a. `ApplicationWizardService.SubmitAsync` — `PropertyRentalSystem.Web/Services/Applications/ApplicationWizardService.cs`

**Replace this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;
        var unitLeases = await _db.Leases.Where(l => l.UnitId == application.UnitId).ToListAsync();
        if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            return ServiceResult.Fail("This unit currently has an active lease and can't accept new applications.");
```

**With this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;
        var hasActiveLease = await _db.Leases
            .Where(l => l.UnitId == application.UnitId)
            .AnyAsync(LeaseRules.IsActiveOn(today));
        if (hasActiveLease)
        {
            return ServiceResult.Fail("This unit currently has an active lease and can't accept new applications.");
        }
```

### 4b. `ApplicationBrowseService.StartApplicationAsync` — `PropertyRentalSystem.Web/Services/Applications/ApplicationBrowseService.cs` (currently lines 71–74)

**Replace this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;
        var unitLeases = await _db.Leases.Where(l => l.UnitId == unit.Id).ToListAsync();
        if (unitLeases.Any(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today)))
            return ServiceResult<RentalApplication>.Fail("This unit is no longer available.");
```

**With this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;
        var hasActiveLease = await _db.Leases
            .Where(l => l.UnitId == unit.Id)
            .AnyAsync(LeaseRules.IsActiveOn(today));
        if (hasActiveLease)
        {
            return ServiceResult<RentalApplication>.Fail("This unit is no longer available.");
        }
```

### 4c. `ApplicationBrowseService.GetAvailableUnitsAsync` — same file (currently lines 20–30)

This one is a system-wide scan, not a single unit, so the rewrite is a correlated subquery rather than a single `AnyAsync` — but it uses the exact same `IsActiveOn` expression, and it removes the two-query, load-every-lease-into-memory shape entirely, not just the in-memory filter:

**Replace this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;

        // Narrow to not-yet-expired leases in SQL, then apply the exact "covers today" rule
        // in memory so the same LeaseRules.CoversDate logic is what gets unit-tested.
        var unavailableUnitIds = (await _db.Leases.Where(l => l.EndDate >= today).ToListAsync())
            .Where(l => LeaseRules.CoversDate(l.StartDate, l.EndDate, today))
            .Select(l => l.UnitId)
            .ToHashSet();

        var query = _db.Units
            .Where(u => !unavailableUnitIds.Contains(u.Id));
```

**With this exact block:**
```csharp
        var today = DateTime.UtcNow.Date;

        var query = _db.Units
            .Where(u => !_db.Leases.Where(l => l.UnitId == u.Id).Any(LeaseRules.IsActiveOn(today)));
```

What changed and why, precisely: the old code ran one query to pull every not-yet-expired lease in the system into memory, filtered it in C#, and built a `HashSet<int>` just to test membership in a second query. The new code is a single query — SQL Server sees it as `WHERE NOT EXISTS (SELECT 1 FROM Leases WHERE Leases.UnitId = Units.Id AND ...)` — no leases are ever loaded into the application process for this check.

**Verify:** `dotnet build`; `dotnet test --filter "FullyQualifiedName~ApplicationWizardServiceTests|FullyQualifiedName~ApplicationBrowseServiceTests"` — all existing tests for both services still pass unchanged (the rewrites are behavior-preserving, confirmed by inspection: `IsActiveOn` is exactly `CoversDate`'s condition, and the correlated subquery in 4c is exactly equivalent to the old two-step filter). Because 4c is a correlated subquery — a shape EF Core translates differently from a flat `Where`/`AnyAsync` — **also** rebuild the Docker image and manually check the Browse page: a unit with an active lease must not appear in the available list, and must reappear the day after the lease ends. Do not rely on `dotnet test` alone to sign off 4c.

**Commit message subject:** `Close the remaining in-memory lease-filter occurrences with LeaseRules.IsActiveOn`

---

## Commit 5 — Enforce braces repo-wide via `.editorconfig`

The reviewer's literal point ("десь пропущені {}" — missing braces in places) is about **pre-existing** brace-less single-statement guard clauses across the codebase (`if (x == null) return NotFound();` and similar) — Commits 1, 2, and 4 stopped adding to the problem, but didn't touch the existing ones. This commit closes it for good: not a one-time manual sweep, but a `.editorconfig` rule the compiler enforces from now on, so new code can't reintroduce it either.

### 5a. New file — `.editorconfig` (repository root)

```ini
root = true

[*.cs]
csharp_prefer_braces = true:warning
dotnet_diagnostic.IDE0011.severity = warning
```

### 5b. Apply the fix repo-wide

```bash
dotnet format style --diagnostics IDE0011
```

This runs Roslyn's own code fixer for IDE0011 ("Add braces") across every `.cs` file in the solution and rewrites every matching brace-less single-statement body in place. Review the diff afterward — expected to be a large number of files, each with a small, mechanical, same-shape change (braces added around an existing single statement), zero logic changes.

### 5c. Verify

`dotnet build` (0 warnings/0 errors — any brace-less `if`/`else` now reported as IDE0011 would show here as a warning if `dotnet format` missed one); `dotnet test` (full suite, unchanged count and all passing — zero behavior change expected from adding braces).

**Commit message subject:** `Enforce braces on all control-flow bodies via .editorconfig (IDE0011)`

---

## Summary of what this plan closes

| Reviewer point | Closed by |
|---|---|
| Validation/conditional logic scattered, if/if/else | Commit 1 (`ReviewAsync` switch + `default`) |
| DB over-fetching, prefer ViewModels | Commit 2 (`GetDetailsAsync`) |
| Folder structure not entirely clear | Commit 3 (`Controllers/` by feature) |
| Resource usage — in-memory filtering instead of SQL | Commit 1 + Commit 4 (all 4 occurrences, via `LeaseRules.IsActiveOn`) |
| Missing `{}` in places | Commit 5 (`.editorconfig`, enforced going forward) |
