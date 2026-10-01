# Further Improvements (Post-Refactor Review)

**✅ All findings below are closed.** See [`FURTHER_IMPROVEMENTS_PLAN.md`](FURTHER_IMPROVEMENTS_PLAN.md) for the 5 commits that closed them — done, pushed to `refactor/further-improvements`, pending merge to `main`. This file is kept as the original findings for reference; each "What to do" below describes what the commit actually did, past tense in spirit if not always in wording.

A fresh pass against the original reviewer feedback, done *after* all 6 phases of [`REMEDIATION_PLAN.md`](REMEDIATION_PLAN.md) were merged to `main`. Only actionable findings are listed — points already fully closed by the 6-phase plan, with nothing further to do, aren't repeated here (see [`RULES_COMPLIANCE_AUDIT.md`](RULES_COMPLIANCE_AUDIT.md) for that record). **UI/design is out of scope for this file.**

## The reviewer's feedback

Overall decent work, but:

- Give Claude explicit rules when using it to build a project — e.g. a `CLAUDE.md` or some other rules file.
- Clearly separate areas of responsibility — the folder structure isn't entirely clear right now.
- Factor validation classes out separately — they currently live in two places: services and models.
- In many places, more data is returned from the database than is actually needed — return ViewModels wherever possible.
- Given current expectations for Full Stack Developers, pay closer attention to UI/design — it's becoming a baseline requirement for every Full Stack Developer.
- General code cleanup — comments in different languages here and there, missing `{}` in places, and so on.

## 1. Folder/responsibility clarity

Phase 6 closed the original finding (the "Domain" naming collision, `ViewComponents` not grouped by feature). Two residual, low-priority items, left as-is but worth recording:

- `Controllers/` is the one folder of five not grouped by feature — zero functional effect (routing doesn't care), purely cosmetic if ever done.
  - **Done (Commit 3):** moved into `Controllers/Properties/`, `Controllers/Units/`, `Controllers/Applications/`, `Controllers/Review/`, `Controllers/Account/` (`HomeController.cs` and `ModalFormControllerBase.cs` stayed at the root — neither belongs to one feature). Turned out not to be a pure file move: `UnitsController` referenced `PropertiesController` by name (`nameof(PropertiesController.List)`) for a redirect URL, a same-namespace dependency invisible before the move — needed one added `using`.
- `ApplicationWizardService` (`Services/Applications/ApplicationWizardService.cs`, 156 lines, 10 public methods) bundles three distinct responsibilities: listing the applicant's applications, the wizard step state machine, and the application lifecycle mutations. Defensible as one cohesive feature service today; the list-query method would be the natural first thing to split out if the class keeps growing.
  - **What to do (only if/when the class keeps growing — not urgent today):** extract `GetMyApplicationsAsync` into a new `IApplicationListService`/`ApplicationListService`, register it in `Program.cs`, and update `ApplicationsController.Index` to inject it instead of `IApplicationWizardService` for that one call. Leave `DetermineStep`/`GoBack`/`GetReviewCommentAsync`/the four mutation methods in `ApplicationWizardService` — those are the actual wizard state machine and belong together.

## 2. Validation and conditional logic in services

The original finding (`ApplicationWizardViewModel` had no `DataAnnotations`; the service hand-rolled the same checks) was closed in Phase 3, re-verified clean across every ViewModel and service in the codebase.

Looking specifically for the "scattered if/if/else" pattern in services, one real instance:

- **`ApplicationReviewService.ReviewAsync`** (lines 67–93) branches on `ReviewOutcome` with `if (outcome == ReviewOutcome.Approve) {...} else if (outcome == ReviewOutcome.Return) {...} else {...}`. `ReviewOutcome` is a 3-value enum — a `switch` on `outcome` reads more clearly, and a `default` case makes it impossible for a 4th outcome to silently fall through without updating `application.Status`.
  - **Done (Commit 1):** replaced with a `switch` on `outcome`, with an added `default: throw new ArgumentOutOfRangeException(...)` so a future 4th `ReviewOutcome` value can't silently fall through without updating `application.Status`. Also folds in the §4 lease-filtering fix below (via the new `LeaseRules.IsActiveOn`), since both live in the same block.
- No other service has this shape — `ApplicationBrowseService.GetAvailableUnitsAsync`'s four sequential `if (x.HasValue) query = query.Where(...)` lines and `UnitService.ValidateAsync`'s three independent guard checks are standard "build up optional filters / accumulate errors" idioms, not entangled branching, and don't need the same treatment.

## 3. Returning more data than needed

Phases 4–5 (Rule 9) fixed the systemic list/summary cases. One further instance, found by checking every remaining `GetByIdAsync`-style single-entity read against "does this feed a mutation, or is it read-only":

- **`ApplicationReviewController.Details(int id)`** used to call `_review.GetByIdAsync(id)`, which loaded the full `RentalApplication` row, to read only `.Id` and `.Status` — no mutation follows in that action (`Review`/`ReviewConfirm` load their own copy separately via `GetReviewableAsync`).
  - **Done (Commit 2):**
    1. Added `Task<ApplicationDetailsViewModel?> GetDetailsAsync(int id)` to `IApplicationReviewService`, a single `.Select()` projection: `_db.RentalApplications.Where(a => a.Id == id).Select(a => new ApplicationDetailsViewModel { Id = a.Id, Status = a.Status, CanReview = a.Status == ApplicationStatus.Submitted }).FirstOrDefaultAsync()` (`GetHistoryAsync`'s result still folded in afterward, same as before).
    2. `ApplicationReviewController.Details` now calls `GetDetailsAsync` instead of `GetByIdAsync`; the manual `ApplicationDetailsViewModel` construction is gone.
    3. `GetByIdAsync` is deleted — zero remaining callers (confirmed by the build itself failing if any existed) — and two new tests cover `GetDetailsAsync` (found-application and not-found cases).
- Checked the same pattern on `PropertiesController`/`UnitsController`'s `Edit`/`DeleteConfirm` actions (also load-full-entity-for-read-only-display): no action needed — `Property` and `Unit` are 3–6 scalar columns each, so there's negligible data actually being over-fetched compared to `RentalApplication`'s ~9 columns where only 2 are used.

## 4. Resource usage

The same shape of issue — load leases with `.ToListAsync()`, then filter with `LeaseRules.CoversDate` in C# instead of in SQL — showed up in **four** places:

- **`ApplicationReviewService.ReviewAsync`**, **`ApplicationWizardService.SubmitAsync`**, and **`ApplicationBrowseService.StartApplicationAsync`** each loaded one unit's leases (`.Where(l => l.UnitId == ...)`) before filtering in memory.
- **`ApplicationBrowseService.GetAvailableUnitsAsync`** did the same at a larger scale — it loaded *every* not-yet-expired lease in the system, not just one unit's, before filtering.
  - **Done (Commits 1 and 4):** `ReviewAsync` in Commit 1, which also introduced the shared `LeaseRules.IsActiveOn` expression — the single EF-translatable source of truth for the rule — and the other three in Commit 4. `IsActiveOn` means the condition lives in exactly one place instead of being copied by hand into four call sites, and `LeaseRulesTests` asserts it never drifts from `CoversDate`. `GetAvailableUnitsAsync`'s rewrite went further than a drop-in replacement — it collapsed from two queries (load every lease, build a `HashSet`, then query `Units`) into one correlated subquery.
- No sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) anywhere in the codebase — checked explicitly, zero hits, nothing to do.
- No N+1 query patterns beyond what Phases 4–5 already fixed — nothing to do.

## 5. Comments, braces, naming, formatting

- **Mixed-language comments:** re-scanned the entire working tree for any Cyrillic-range character — zero matches, nothing to do.
- **Braces:** re-checked specifically for the risky pattern (an `if` body missing braces where a second statement at the same indent was meant to be inside it) via an automated scan of every `.cs` file, and found zero instances; the codebase's brace-less single-statement guard clauses (`if (x == null) return NotFound();`) were applied consistently and hadn't caused a bug. That was a correctness finding, not a reason to drop the reviewer's actual request — the reviewer's literal words were "missing `{}` in places."
  - **Done (Commit 5):** `.editorconfig` (`csharp_prefer_braces = true:warning`, `dotnet_diagnostic.IDE0011.severity = warning`) + `dotnet format style --diagnostics IDE0011` applied braces to every remaining single-statement body across 14 files, mechanically — `dotnet build`/`dotnet test` confirmed zero behavior change. The `.editorconfig` is what makes this stick: new code that omits braces now shows as a build warning, not just a style a reviewer might notice.
- **Naming, line length, indentation:** checked for unclear short variable names (none outside idiomatic LINQ lambda parameters like `a`, `u`, `p`), lines over 150 characters (none), and mixed tabs/spaces (none) — all clean, nothing to do.
