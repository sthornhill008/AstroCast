# H-1 MainViewModel Decomposition — Pattern Reference

> **2026-10-14: historical.** Superseded by CLAUDE.md's versioning and tagging
> sections and the release-bump skill (`.claude/skills/release-bump/`); the
> paths and process below are as of the document's last edit, 2026-04-19.

This document is the working reference for the H-1 initiative: splitting
`MainViewModel` (currently ~6,500 lines) into a coordinator plus a set of
feature-focused child view models. Every H-1.N session follows the same
checklist, so the mechanical parts stop being load-bearing on memory.

The pattern was proven end-to-end in **H-1.1** by `ScaffoldingProbeViewModel`
— a minimal child VM that exercises each piece of the plumbing
(`ChildViewModelBase`, parent property, XAML binding, command-refresh
routing, unit tests). The probe will be removed in H-1.2 when
`ChartEntryViewModel` becomes the first real child.

---

## Goals

1. **Shrink the parent.** `MainViewModel` becomes a coordinator that owns
   services and composes children, not a 6.5 kloc god class.
2. **Make feature boundaries explicit.** Each child owns a single pane or
   workflow (chart entry, location search, synastry, transits, electional,
   returns, cycles, mundane, return/ingress timeline, relocation, ACG).
3. **Zero behaviour change per session.** Every H-1.N session must leave
   the tree green and UX-identical. Refactoring risk is bounded by making
   each step a tiny, reviewable slice.

## Non-goals

- No DI container. Manual-DI in `App.xaml.cs` remains the composition root.
- No `ReactiveUI`, no `CommunityToolkit.Mvvm` — `ChildViewModelBase`
  stays in-house so the pattern is exactly what the parent already uses.
- No new NuGet packages.
- No public-API break on `MainViewModel` across sessions. XAML bindings
  compile against the same surface; children are reached via new
  parent properties, not by replacing existing ones.

---

## Architecture

```
MainViewModel (coordinator)
├── services: IChartEngine, IChartRepository, …       (still owned here)
├── commands: CalculateAndSaveCommand, ExportPdfCommand, …  (still owned here)
├── RefreshExportCommandStates()                        (still private here)
└── children (public properties — bound from XAML):
    ├── ChartEntryViewModel            (H-1.2)
    ├── LocationSearchViewModel        (H-1.3)
    ├── SynastryViewModel              (H-1.4)
    ├── TransitsViewModel              (H-1.5)
    ├── ElectionalViewModel            (H-1.6)
    ├── ReturnsViewModel               (H-1.7)
    ├── CyclesViewModel                (H-1.8)
    ├── MundaneViewModel               (H-1.9)
    ├── ReturnIngressTimelineViewModel (H-1.10)
    ├── RelocationViewModel            (H-1.11)
    └── AstrocartographyViewModel      (H-1.12)
```

Children are constructed by `MainViewModel` internally (Option B): the
parent owns child lifetime, children take the parent as a constructor
argument, `App.xaml.cs` remains unaware of child VMs.

### `ChildViewModelBase`

Shared base for every child. Provides:

| Member                              | Purpose                                                           |
|-------------------------------------|-------------------------------------------------------------------|
| `Parent` (protected)                | Last-resort back-reference for cross-VM reads.                    |
| `PropertyChanged` (public)          | `INotifyPropertyChanged` — dispatcher-marshalled to the UI thread. |
| `CommandStateRefreshRequested`      | Event the parent subscribes to for command `CanExecute` refresh.  |
| `OnPropertyChanged` (protected)     | Raises `PropertyChanged`, mirrors H-2 dispatcher marshalling.      |
| `SetProperty<T>` (protected)        | Same shape as `MainViewModel`'s private helper — lift-and-keep.    |
| `RequestCommandStateRefresh`        | Raises `CommandStateRefreshRequested` without exposing internals.  |

### Cross-VM communication rules

1. **No sibling-to-sibling references.** A child VM must not hold a
   reference to another child VM. Any cross-VM signal goes through the
   parent.
2. **`Parent` is last-resort.** Prefer input properties on the child
   (set by the parent) over `Parent.Whatever` reads. When you do reach
   into the parent, leave a short code comment explaining why an input
   property could not express it.
3. **Command refresh routing.** Children never touch the parent's
   command table. They raise `CommandStateRefreshRequested`; the parent
   routes the signal in its constructor:
   ```csharp
   child.CommandStateRefreshRequested += (_, _) => RefreshExportCommandStates();
   ```
4. **Commands, services, and follow-on handlers that cross VM
   boundaries stay on the parent.** Services (e.g. `IChartEngine`,
   `ITransitService`, `IElectionalService`) are never fields on a child;
   the parent holds them and invokes them from command `Execute`
   bodies. A follow-on handler (one that subscribes to a child's
   `PropertyChanged` and does work in response) stays on the parent
   whenever its writes target **sibling-owned** state — the parent is
   the only legitimate mediator of cross-sibling writes.

   **Refinement (H-1.6 nested-child composition).** When a child owns
   another child as a nested sub-component and the follow-on handler's
   writes target fields inside the enclosing child's own surface
   (i.e. `this.Whatever = ...`), the writes are **intra-VM, not
   cross-VM**, and the handler moves with its owning VM. Concrete
   example from H-1.6: the electional location-search follow-on
   handler used to live on `MainViewModel` and write
   `ElectionalLatitudeText` etc. (then sibling fields on the parent);
   when those fields migrated into `ElectionalViewModel` alongside
   the nested `ElectionalViewModel.LocationSearch`, the handler moved
   to `ElectionalViewModel.OnLocationSearchPropertyChanged` because
   the targets are now `this.LatitudeText` etc. under the same
   umbrella. The one parent-owned write in that handler
   (`Parent.StatusText = ...` — a shared UI surface) still routes
   through `Parent` per Rule #2.

   **Test for applicability.** If the handler writes only to
   `this.*` fields of the owning VM (plus `Parent.*` reads/writes that
   Rule #2 already covers as last-resort), it belongs with the VM.
   If the handler writes to a genuinely sibling VM's state (any
   `parent.OtherChild.Whatever = ...`), it stays on the parent.

---

## Per-session checklist

Every H-1.N extraction follows the same steps. Copy this checklist into
the session prompt and tick items off as you go.

1. **Audit the slice.** Identify the fields, properties, commands,
   methods, and XAML bindings belonging to the feature. List every
   external read (parent fields the slice touches) and every external
   write (parent fields the slice mutates).
2. **Draft the child VM.** Create
   `src/AstroCast/ViewModels/<Feature>ViewModel.cs`. Derive from
   `ChildViewModelBase`. Move the audited state and behaviour verbatim
   — no drive-by refactoring.
3. **Expose the child.** Add
   `public <Feature>ViewModel <Feature> { get; }` to `MainViewModel`
   in the `H-1 Decomposition — Child View Models` region.
4. **Wire the child in the constructor.** Inside `MainViewModel`'s
   constructor, construct the child after services are assigned and
   subscribe to `CommandStateRefreshRequested`. If the child needs to
   observe parent-owned state, pass it through via an input property —
   not through the back-reference — and push updates from the parent
   setter.
5. **Rebind XAML.** Update every binding for moved properties from
   `{Binding OldName}` to `{Binding <Feature>.OldName}`. Keep the
   property names identical across the move to minimise churn.
6. **Route commands.** Commands stay on the parent (the parent owns
   services). Child state that affects `CanExecute` must raise
   `CommandStateRefreshRequested` inside its property setters so the
   parent's command table refreshes.
7. **Migrate tests.** Split tests that now target the child into a
   `<Feature>ViewModelTests.cs` file. Tests that exercise routing
   (parent ↔ child) stay in `MainViewModelTests.cs`.
8. **Verify green.** `dotnet build` (zero warnings) + `dotnet test`
   (zero failures, zero skips). Behaviour must be identical — if you
   needed to change any assertion that is not a renamed binding path,
   stop and audit: you have altered behaviour.
9. **Version bump.** Four csproj files. Patch increment per session.
10. **Commit + tag.** Conventional-commit prefix `refactor(vm):` and an
    annotated tag.
11. **Session summary.** Save to
    `D:\OneDrive\Code\Archive_AstroCast\Documentation\` and update
    the continuity document.

---

## Invariants

- `ChildViewModelBase.OnPropertyChanged` must preserve its dispatcher
  check. Losing it would regress H-2 and reintroduce cross-thread
  binding failures.
- Command ownership stays on the parent for the life of H-1. Moving
  commands into children is a separate initiative and is not an H-1
  objective.
- `MainViewModel` remains `sealed`. No inheritance-based extension
  points; composition only.
- `App.xaml.cs` never learns about child VMs. Adding a DI registration
  for a child is a signal that the pattern has been broken.

---

## Removing the scaffolding (H-1.2)

When H-1.2 lands, the following are deleted as a single atomic step:

- `src/AstroCast/ViewModels/ScaffoldingProbeViewModel.cs`
- `MainViewModel.ScaffoldingProbe` property + its constructor line +
  its `CommandStateRefreshRequested` subscription
- The `H-1 Decomposition — Child View Models` region header in
  `MainViewModel.cs` *unless* H-1.2 already re-uses it for
  `ChartEntryViewModel` (preferred — keep the region, just replace
  its content)
- `<TextBlock x:Name="ScaffoldingProbeBinding" … />` in `MainWindow.xaml`
- `tests/AstroCast.Tests/ViewModels/ScaffoldingProbeViewModelTests.cs`

Everything else added by H-1.1 (`ChildViewModelBase`, the pattern
documented here) stays — that is the permanent shape of the project.
