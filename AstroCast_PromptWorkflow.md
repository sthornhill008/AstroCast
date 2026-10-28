# AstroCast: Claude Code Prompt Development Workflow

> **2026-10-14: historical.** Superseded by CLAUDE.md's versioning and tagging
> sections and the release-bump skill (`.claude/skills/release-bump/`); the
> paths and process below are as of the document's date, 2026-03-07.

**Date:** 2026-03-07
**Applies to:** All AstroCast feature development using Claude Code
**Location:** Keep this document alongside CLAUDE.md in the project root, or in D:\OneDrive\Code\Archive_AstroCast\Documentation\

---

## Purpose

This document defines the standard workflow for developing Claude Code prompts that implement new AstroCast features. Every feature — from initial design through implementation, testing, commit, and summary — follows this process to ensure consistency, traceability, and quality.

---

## Workflow Overview

```
┌──────────────────────────────────────────────────────────┐
│  Phase 1: Design Session (Claude.ai)                     │
│  ─ Explore the concept and architectural fit              │
│  ─ Produce reference design files (models, interfaces)    │
│  ─ Identify interop additions, DI changes, test strategy  │
└──────────────────────────┬───────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────┐
│  Phase 2: Staged Implementation Prompt (.md)             │
│  ─ Write a self-contained .md prompt for Claude Code      │
│  ─ 3–5 sequential stages, each with build/test/commit     │
│  ─ Full code listings for new files                       │
│  ─ Precise modification instructions for existing files   │
│  ─ Test specifications per stage                          │
└──────────────────────────┬───────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────┐
│  Phase 3: Execution (Claude Code)                        │
│  ─ Feed the prompt to Claude Code                         │
│  ─ Claude Code executes stages sequentially               │
│  ─ Each stage: implement → build → test → commit → tag    │
│  ─ Summary report written on completion                   │
└──────────────────────────┬───────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────┐
│  Phase 4: Review & Archive                               │
│  ─ Verify all tests pass (zero regressions)               │
│  ─ Review the summary report                              │
│  ─ Archive prompt + summary in Documentation folder       │
└──────────────────────────────────────────────────────────┘
```

---

## Phase 1: Design Session (Claude.ai)

The design session happens in the Claude.ai conversation interface — **not** in Claude Code. Its purpose is to explore the feature concept, make architectural decisions, and produce reference artifacts.

### What to produce

| Artifact | Purpose |
|----------|---------|
| Reference design files | C# source files for new models, enums, interfaces, services, formatters — written against the actual AstroCast codebase patterns |
| Architectural decisions | Where the feature fits (new service vs. extending existing), DI registration approach, whether it integrates into `ChartEngine`/`ChartResult` or stands alone |
| Interop additions | Any new Swiss Ephemeris P/Invoke bindings or constants needed |
| Test strategy | Which tests are unit vs. integration, fixture requirements, edge cases |

### How to run it

1. Upload the current AstroCast source package (`Package-AstroCast-SourceOnly-*.tar.gz`) so Claude.ai can read the actual codebase.
2. Discuss the feature concept — what it is, why it matters, architectural options.
3. Make decisions on key design questions (Claude.ai can present options for your input).
4. Claude.ai produces the reference design files as downloadable outputs.
5. These files become the **Reference** cited in the implementation prompt.

### Key principle

The design session is exploratory and interactive. The implementation prompt is prescriptive and autonomous. Keep them separate.

---

## Phase 2: Staged Implementation Prompt

The implementation prompt is a single `.md` file that Claude Code can execute autonomously from start to finish. It must be self-contained — Claude Code should not need to ask clarifying questions.

### Required sections

Every implementation prompt MUST include these sections in this order:

#### Header block

```markdown
# AstroCast: [Feature Name] Implementation — Staged Prompt for Claude Code

**Date:** YYYY-MM-DD
**Project:** AstroCast (D:\OneDrive\Code\AstroCast)
**Reference:** [Design files or spec document name]
**Summary Report:** You MUST create a summary report file at D:\OneDrive\Code\Archive_AstroCast\Documentation\[Feature]_Implementation_Summary.md
```

#### Context section

- What AstroCast is (one sentence)
- What the feature does (2–3 sentences)
- Key architectural facts relevant to this feature
- Current version number and test count

#### Implementation Plan — N Stages

3 to 5 stages, executed sequentially. Each stage follows this template:

```markdown
### Stage N: [Goal Statement]

**Goal:** One sentence describing what this stage achieves.

#### NA. [Component] — [Filename]

[Full code listing or precise modification instructions]

#### NB. [Component] — [Filename]

[Full code listing or precise modification instructions]

#### NC. Tests for Stage N

[Test specifications: test names, what they verify, assertions, categories]

**Build, test, commit:**
```
feat: [conventional commit message]
```

Git tag with incremented version.
```

#### Stage design principles

| Principle | Rule |
|-----------|------|
| **Incremental compilation** | Every stage must leave the project in a buildable, testable state |
| **Test with each stage** | Tests for the stage's code are part of the same stage, not deferred |
| **Commit after each stage** | Each stage ends with a conventional commit + version tag |
| **Bottom-up ordering** | Stage 1 = foundations (models, enums, config). Stage 2 = core logic (service). Stage 3 = integration (DI, formatters, ViewModel). Stage 4 = end-to-end validation |
| **Full code listings for new files** | Include the complete source code in the prompt — don't reference external files that Claude Code might not have |
| **Precise edits for existing files** | Name the exact file, the exact location (after which line/method/region), and the exact code to add |
| **One logical change per prompt** | Each prompt addresses a single feature or concern — do not combine unrelated changes in one prompt |

#### Cross-Cutting Concerns section

Thread safety, performance, edge cases, architectural decisions that apply across all stages.

#### Summary Report section

```markdown
## Summary Report

You MUST create a summary report at:
```
D:\OneDrive\Code\Archive_AstroCast\Documentation\[Feature]_Implementation_Summary.md
```

Include:
- Implementation date
- Stages completed
- Files created/modified (with full paths)
- Test count before and after
- Any issues encountered and resolutions
- [Feature-specific items]
- Final version number
```

#### Git Rule section

```markdown
## Git Rule

Every stage MUST include a git commit with version tag and build number increment. This is the standard for all AstroCast code updates.
```

### Naming convention

Prompt files: `AstroCast_[Feature]_Implementation_Prompt.md`
Summary reports: `[Feature]_Implementation_Summary.md`

---

## Phase 3: Execution

Feed the implementation prompt to Claude Code. It executes autonomously.

### What Claude Code does at each stage

1. Create/modify the files specified in the stage
2. Run `dotnet build` — must succeed with zero errors
3. Run `dotnet test` — ALL tests must pass (existing + new)
4. Commit with the specified conventional commit message
5. Increment the version in all four `.csproj` files if needed:
   - `src/AstroCast.Core/AstroCast.Core.csproj`
   - `src/AstroCast/AstroCast.csproj`
   - `src/AstroCast.Cli/AstroCast.Cli.csproj`
   - `tests/AstroCast.Tests/AstroCast.Tests.csproj`
6. Create an annotated git tag: `git tag -a "v{version}" -m "Release v{version}"`
7. Proceed to the next stage

### What Claude Code does on completion

Write the summary report to `D:\OneDrive\Code\Archive_AstroCast\Documentation\[Feature]_Implementation_Summary.md`.

### Failure handling

If a stage fails (build error, test failure), Claude Code should:
1. Diagnose the error
2. Fix it
3. Re-run build/test
4. Only commit when everything passes

---

## Phase 4: Review & Archive

After Claude Code completes:

1. **Verify** — open the project in Visual Studio 2026, build, run all tests
2. **Review** — read the summary report, check test counts, review any issues noted
3. **Archive** — the prompt `.md` file goes to `D:\OneDrive\Code\Archive_AstroCast\Documentation\` alongside the summary report

### Archive folder structure

```
D:\OneDrive\Code\Archive_AstroCast\Documentation\
├── FixedStars_Implementation_Summary.md
├── AstroCast_FixedStars_Implementation_Prompt.md
├── Paran_Implementation_Summary.md
├── AstroCast_Paran_Implementation_Prompt.md
├── [Future features...]
└── AstroCast_PromptWorkflow.md   ← this document
```

> **Note:** Files created before this workflow was established (2026-03-07) use older naming conventions such as `Prompt-Stage{N}-{Name}.md` and `{Feature}-Summary.md`. The naming convention above applies to all new prompts going forward.

---

## Checklist for Prompt Authors

Use this checklist when writing a new implementation prompt:

- [ ] Header block has correct date, project path, reference, and summary report path
- [ ] Context section names the current version and test count
- [ ] Each stage has a clear Goal statement
- [ ] New files include complete source code with full XML documentation
- [ ] Existing file modifications specify exact file path and insertion point
- [ ] Each stage includes test specifications with category traits
- [ ] Each stage ends with build/test/commit instruction and conventional commit message
- [ ] Cross-Cutting Concerns covers thread safety, performance, and edge cases
- [ ] Summary Report section specifies the exact output file path
- [ ] Git Rule section is present
- [ ] All code follows AstroCast conventions: file-scoped namespaces, XML docs on all public members, `ArgumentNullException.ThrowIfNull()` guards, Serilog logging

---

## Quick Reference: AstroCast Conventions

| Convention | Rule |
|------------|------|
| Language | C# 14, .NET 10 |
| Testing | xUnit + FluentAssertions |
| Logging | Serilog `ILogger` |
| DI | Manual wiring in `App.xaml.cs` (no container) |
| Thread safety | `SemaphoreSlim` for Swiss Ephemeris access |
| Test fixture | `EphemerisFixture` + `[Collection("Ephemeris")]` for integration tests |
| Test categories | `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]` |
| Commits | Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`) |
| Versioning | Auto-increment patch in all 4 `.csproj` files, annotated git tag `v{Major}.{Minor}.{Patch}` |
| Summary reports | `D:\OneDrive\Code\Archive_AstroCast\Documentation\` — always outside the project tree |
| Code docs | XML documentation on ALL public types and members |
| Namespaces | File-scoped |
| Null handling | Nullable reference types enabled, explicit `ArgumentNullException.ThrowIfNull()` |
| C# 14 features | Primary constructors, field-backed properties (`field` keyword), extension blocks |
| Configuration | `IOptions<T>` pattern for configuration binding |
| Using order | System namespaces → third-party → project namespaces |
| Verification | PowerShell verification scripts for significant changes |
