# Remediation Plan

**✅ Complete.** All 6 phases executed and merged to `main`. See [`REMEDIATION_STATUS.md`](REMEDIATION_STATUS.md) for what actually happened and [`RULES_COMPLIANCE_AUDIT.md`](RULES_COMPLIANCE_AUDIT.md) for current compliance. Kept for historical reference — describes the codebase *before* execution.

Phases ordered simplest → most complex, each independently executable/testable/mergeable (Strangler-Fig, Rule 6). Rules 1, 5, 6, 12 needed no dedicated phase (already compliant).

| Phase | Rule(s) | What | Depends on |
|---|---|---|---|
| 1 | 11 | Translate non-English comments | None |
| 2 | 2, 4 | Remove duplicated status checks from Razor views | None |
| 3 | 8 | Centralize wizard validation into the ViewModel | None |
| 4 | 3, 10 | Move ViewComponent queries into services | None |
| 5 | 9 | Project read-only queries directly to ViewModels | Phase 2 (one method needs its new fields) |
| 6 | 7 | Resolve folder/namespace boundaries | Done last — touches the most files |
