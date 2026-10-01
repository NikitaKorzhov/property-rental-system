# Further Improvements (Post-Refactor Review)

**✅ All findings closed.** Fresh pass against the original reviewer feedback, done after the 6-phase plan merged. See [`FURTHER_IMPROVEMENTS_PLAN.md`](FURTHER_IMPROVEMENTS_PLAN.md) for the 5 commits that closed them (done, on `refactor/further-improvements`, pending merge). **UI/design out of scope.**

## The reviewer's feedback

- Give Claude explicit rules (`CLAUDE.md` or similar).
- Clearly separate areas of responsibility — folder structure unclear.
- Factor validation out separately — currently in both services and models.
- Too much data returned from the DB in places — prefer ViewModels.
- Pay closer attention to UI/design.
- General cleanup — mixed-language comments, missing `{}`, etc.

## Findings and fixes

1. **Folder clarity** — `Controllers/` was the one folder not grouped by feature. **Fixed (Commit 3):** grouped like `Services`/`ViewModels`/`Views`; `HomeController`/`ModalFormControllerBase` stay at the root. (`ApplicationWizardService`'s 3-responsibility bundle is defensible as-is today — split out `GetMyApplicationsAsync` only if the class keeps growing.)
2. **Scattered if/if/else** — `ApplicationReviewService.ReviewAsync` branched on `ReviewOutcome` with `if`/`else if`/`else`. **Fixed (Commit 1):** `switch` with a `default: throw ArgumentOutOfRangeException(...)`, so a future 4th outcome can't silently fall through.
3. **Over-fetching** — `ApplicationReviewController.Details` loaded the full `RentalApplication` entity to read 2 fields. **Fixed (Commit 2):** `GetDetailsAsync`, a single `.Select()` projection; `GetByIdAsync` deleted (zero callers). (Checked `Properties`/`Units` `Edit`/`DeleteConfirm` too — negligible over-fetch there, nothing to do.)
4. **In-memory lease filtering instead of SQL** — 4 occurrences (`ReviewAsync`, `SubmitAsync`, `StartApplicationAsync`, `GetAvailableUnitsAsync`) loaded leases then filtered with `LeaseRules.CoversDate` in C#. **Fixed (Commits 1 and 4):** `LeaseRules.IsActiveOn`, an `Expression<Func<Lease,bool>>` EF Core translates into SQL directly — one rule, reused everywhere, asserted against `CoversDate` by `LeaseRulesTests`. `GetAvailableUnitsAsync` went further, collapsing two queries + a `HashSet` into one correlated subquery.
5. **Missing braces** — the reviewer's literal point; pre-existing brace-less guard clauses were a deliberate, bug-free style choice, but still what was asked. **Fixed (Commit 5):** `.editorconfig` (`csharp_prefer_braces = true:warning`) + `dotnet format style --diagnostics IDE0011`, so new code can't reintroduce it either.

Checked and clean, nothing to do: no sync-over-async anywhere; no N+1 beyond what Phases 4–5 fixed; no unclear naming/line-length/indentation issues.
