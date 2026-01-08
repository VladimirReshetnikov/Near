# Console File Manager + Git Repo Manager (Terminal.Gui v2)

## Main Design Document (Guiding Implementation Plan)

## 1. Vision

We are building a cross-platform console-mode .NET application (codenamed "Near") that combines:

1. **A high-performance, keyboard-first file manager** inspired by Far Manager
2. **A Git repository manager** inspired by the Git tooling experience in JetBrains IDEs (Rider, IntelliJ family)
3. **An integrated PowerShell terminal experience (pwsh)** that remains available even when tool panels are open, and can become effectively “full-screen terminal” when panels are hidden.

The app runs entirely in a terminal window and uses **Terminal.Gui (v2)** for a responsive multi-panel UI that supports keyboard + mouse and automatically reflows on console resize.

---

## 2. High-level Goals

### 2.1 User outcomes

* Navigate directories quickly (two-panel workflow).
* Perform file operations safely and efficiently (copy/move/delete/rename/create, batch operations).
* Manage Git workflows without leaving the console UI:

  * View status, diff, stage/unstage, commit, push/pull/fetch
  * Browse history/log, branches, tags, stashes
  * Resolve merge conflicts with good visibility and tooling
* Keep a **real pwsh session** available:

  * When UI panels are shown: terminal occupies a reserved area at the bottom
  * When UI panels are hidden: terminal becomes effectively full-screen

### 2.2 Non-functional goals

* **Fast**: responsive even in large folders and large repos.
* **Reliable**: robust error handling; no silent failures; safe file ops.
* **Discoverable**: command palette, key hints, contextual actions.
* **Customizable**: configurable layout, panels, key bindings, and key chords.
* **Cross-platform**: Windows + Linux + macOS (with platform-specific terminal backends where needed).

---

## 3. Guiding Principles

1. **Keyboard-first, mouse-friendly**
   Everything must be usable via keyboard, but mouse should be first-class (click to focus, select, open context menus, scroll, resize splits).

2. **Far-like ergonomics, JetBrains-like Git visibility**

   * Far: fast navigation, two-panel file ops, predictable key-based actions
   * JetBrains Git: rich status/log/diff/commit flows, clear staging model, history browsing

3. **Panel-based UI, composable and dockable**
   Panels are the fundamental building block. Layout is a composition of panels.

4. **Commands are the API**
   All user actions are commands (with IDs), enabling:

   * key bindings
   * chord sequences
   * command palette
   * macro/automation
   * future plugin contributions

5. **Async everywhere (UI never blocks)**
   All expensive tasks (filesystem enumeration, git queries, diff generation, search) run off the UI thread with progress + cancellation.

---

## 4. Product Scope

### 4.1 Core UI capabilities (foundation)

* Dockable/multi-panel layout
* Focus management (active panel vs passive panel)
* Input routing (global keys, per-panel keys, terminal keys)
* Mouse support (selection, scrolling, resizing)
* Resize handling (auto layout + terminal resize)
* Notifications and background task progress view

### 4.2 File manager features (Far-inspired)

**Must-have**

* Two file panels (left/right), active/passive behavior
* Directory navigation (enter, back, parent, history)
* Sorting and filtering
* Multi-select / mark/unmark
* Copy/move/delete/rename/create folder
* Quick search within panel (type-to-select)
* File viewer (quick view) and basic text viewer
* File info panel (size, timestamps, attributes, permissions)
* Bookmarks/favorites

**Should-have**

* Batch rename
* Search across directory tree (find files / grep-like text search)
* Archive handling (at least via external tools initially)
* Compare/sync directories (later)

### 4.3 Git features (JetBrains-inspired)

**Must-have**

* Detect repo(s) from current directories
* Status view (unstaged/staged/untracked/ignored)
* Diff viewer (file diff, hunk view; stage/unstage per file; hunk staging as stretch goal)
* Commit view (message, amend, sign-off, stage selection)
* Branch list + checkout/create/delete (with safety checks)
* Log view (commit history, details, file changes)
* Fetch/pull/push (delegating auth to git/ssh/credential manager)
* Stash (list/apply/pop/drop)

**Should-have**

* Cherry-pick
* Rebase (guided / interactive later)
* Conflict resolution assistance (detect conflicts, open conflict diff)
* Blame/annotate (later)
* Worktrees (later)

### 4.4 Integrated terminal (pwsh) behavior

* A running shell session displayed inside the UI (pwsh by default)
* When panels are visible: terminal occupies a configurable reserved height at bottom
* When panels are hidden: terminal expands to fill available space (“panels off”)
* Option to synchronize terminal working directory with active file panel (off by default)
* Command to send paths/selections to terminal (paste path, `cd`, etc.)

---

## 5. UX Model

### 5.1 Layout concept

The screen is divided into:

* **Top bar**: global status (current path, repo, branch, operation status, time, etc.)
* **Main workspace**: dockable panels (file panels, git panels, preview panels)
* **Bottom terminal area**: pwsh session view (height configurable)
* **Footer** (optional): key hints / mode indicators / notifications line

### 5.2 Panel modes

We support two key layout modes:

#### Mode A: Panels on (default)

```
┌─────────────────────────────────────────────┐
│ Top bar: path | repo | branch | status      │
├───────────────────────────────┬─────────────┤
│ Left File Panel               │ Right Panel │
│                               │ (Git/Tree/  │
│                               │  Preview)   │
├─────────────────────────────────────────────┤
│ Terminal (pwsh) - reserved height            │
└─────────────────────────────────────────────┘
```

#### Mode B: Panels off (“terminal focus”)

```
┌─────────────────────────────────────────────┐
│ Terminal (pwsh) full height                  │
│                                               │
│ (Optional minimal overlay or hidden entirely) │
└─────────────────────────────────────────────┘
```

> This mirrors the “toggle panels” behavior users expect from Far-like workflows, while staying within a single Terminal.Gui rendering model.

### 5.3 Focus and navigation

* One panel is **Active** (receives navigation keys, acts as source for operations).
* A second file panel (if visible) is typically **Passive** (destination for copy/move by default).
* `Tab` cycles focus between major regions: left panel → right panel → terminal → other tool panels.
* Mouse click focuses a panel.
* A focus indicator must be unmistakable (border highlight, title bar change).

### 5.4 Selection model

* File panels support:

  * single selection (cursor)
  * multi-select (marks)
  * range selection
* Git status panel supports selection of changed files (and later hunks).

### 5.5 Mouse interactions (minimum set)

* Click: focus + selection
* Double click / Enter: open dir / open file / open diff
* Wheel: scroll lists, log, diff
* Drag on splitters: resize docked regions
* Context menu on right-click (optional early, required later)

---

## 6. Command System and Key Bindings

### 6.1 Why command-first

Every user action is expressed as a `CommandId` with a single execution path. This unlocks:

* consistent behavior across UI, palette, macros
* testability (commands are unit-testable)
* custom keymaps without wiring keys into UI code

### 6.2 Command definition (conceptual)

Each command has:

* `Id` (stable string)
* `Title`, `Description`
* `DefaultBindings` (one or more)
* `Context` (when it applies: file panel, git panel, terminal, global)
* `ExecuteAsync(CommandContext ctx, CancellationToken ct)`

### 6.3 Key binding resolution

Input handling checks bindings in this order:

1. **Global bindings** (e.g., toggle panels, open palette, exit)
2. **Focused panel bindings**
3. **Terminal passthrough** (if terminal focused or no binding matches)

Bindings may be:

* single keystrokes (e.g., `F5`)
* chords (e.g., `Ctrl+K` then `Ctrl+C`)
* sequences (e.g., `g` then `s` for Git Status)

### 6.4 Chord engine

Implement chords as a trie:

* Key sequences accumulate until:

  * an exact match triggers a command
  * no match exists → flush keys to focused view (or show “unknown chord”)
  * timeout expires → cancel chord and flush or discard based on policy

**Timeout behavior**:

* Default: ~800–1200ms (configurable)
* Visual feedback: show “Chord: Ctrl+K …” in footer or status overlay.

### 6.5 Example default bindings (draft)

Far-like:

* `F3` View, `F4` Edit, `F5` Copy, `F6` Move, `F7` MkDir, `F8` Delete, `F10` Exit
* `Tab` switch active panel
* `Ctrl+O` toggle panels (panels on/off)
* `Alt+F1` / `Alt+F2` change drive/root (platform-adapted)

Git-like:

* `g s` Git status panel
* `g l` Git log panel
* `g c` Commit panel
* `g b` Branches panel
* `g p` Push/Pull menu (or `g p` then `p`/`l`)

Terminal:

* `Ctrl+`… reserved global chords must remain available even in terminal focus (e.g., `Ctrl+O`, palette).

---

## 7. Architecture Overview

### 7.1 Layering

We use a clean architecture style:

1. **Core Domain** (no UI, no Terminal.Gui dependency)

   * models: filesystem items, repo state, diffs, commands
   * policies: selection logic, operation planning, conflict resolution rules

2. **Services / Application Layer**

   * orchestrates commands, background tasks
   * manages app state store and event flow

3. **Infrastructure**

   * filesystem access, git backend, terminal backend, config IO, logging

4. **UI Layer**

   * Terminal.Gui views/panels
   * input routing → command dispatcher
   * rendering and layout

### 7.2 Project structure (suggested)

* `App/` (entry point, host, composition root)
* `Core/`

  * `Models/`, `Commands/`, `State/`, `Utilities/`
* `Services/`

  * `FileOperations/`, `Git/`, `Search/`, `Tasks/`
* `Infrastructure/`

  * `FileSystem/`, `GitCli/`, `Terminal/`, `Config/`, `Logging/`
* `UI/`

  * `Panels/`, `Layout/`, `Input/`, `Views/`, `Theming/`
* `Plugins/` (optional later)

### 7.3 Dependency injection and hosting

Use .NET Generic Host:

* `Microsoft.Extensions.Hosting`
* `Microsoft.Extensions.DependencyInjection`
* `Microsoft.Extensions.Configuration`
* `Microsoft.Extensions.Logging`

This provides consistent configuration, logging, and testability.

---

## 8. State Management and Eventing

### 8.1 App state store

Adopt a single source of truth for UI-relevant state:

* active panel id
* left/right panel directory + selection
* open panels and their layout
* active repo context
* git status summary
* running tasks, progress, notifications

Use immutable records where practical and update via reducer-like methods:

* `AppState` (record)
* `IStateStore<AppState>` with:

  * `AppState Current { get; }`
  * `IDisposable Subscribe(Action<AppState> observer)`
  * `void Dispatch(IAction action)` or `Task DispatchAsync(IAction action)`

This makes UI updates predictable and reduces “hidden coupling”.

### 8.2 Event bus

Use an internal event bus for:

* file watcher events
* git refresh triggers
* terminal output chunks
* background task progress updates

Implementation can use `System.Threading.Channels`:

* producers write events
* a single consumer processes and updates state or sends UI notifications

### 8.3 Background task runner

All long operations run through a task runner supporting:

* progress reporting (percent + message)
* cancellation
* concurrency limits (e.g., avoid 20 concurrent scans)
* consistent error handling

Define:

* `IBackgroundTask` with `RunAsync(IProgress<ProgressInfo>, CancellationToken)`
* `ITaskRunner.Enqueue(task)` returns handle with status

---

## 9. Terminal Integration Design (pwsh)

### 9.1 Key decision: embed shell via PTY + terminal emulator

To keep a consistent UI model (panels on/off are just layout changes), the terminal is implemented as:

* **a child process** (pwsh)
* connected via a **pseudo-terminal (PTY)**
* rendered in a **terminal emulator view** inside Terminal.Gui

This avoids “handing back” the real console (which breaks input interception and panel toggling).

### 9.2 Cross-platform PTY backends

Create `ITerminalBackend` abstraction:

* Windows: ConPTY
* Linux/macOS: POSIX pty

Interface sketch:

* `Task StartAsync(ShellStartInfo info, CancellationToken ct)`
* `void SendInput(ReadOnlySpan<byte> data)`
* `IAsyncEnumerable<ReadOnlyMemory<byte>> OutputChunks`
* `void Resize(int cols, int rows)`
* `Task StopAsync()`

### 9.3 Terminal emulator view

Create a dedicated `TerminalEmulatorView : View`:

* Maintains screen buffer of cells (char + attributes)
* Parses ANSI/VT escape sequences
* Tracks cursor position, scrollback
* On output chunks:

  * parse → update buffer
  * invalidate region → redraw

**Minimum supported sequences** for pwsh usability:

* cursor movement
* clear line/screen
* color attributes
* scroll regions

### 9.4 Input mapping

Map Terminal.Gui key events into terminal input bytes:

* printable chars → UTF-8
* Enter/Backspace/Tab/Esc
* arrows, Home/End, PgUp/PgDn
* Ctrl combinations
* function keys (if desired)

Important: global shortcuts (like `Ctrl+O`) must be intercepted before passing to terminal.

### 9.5 Resize handling

On console resize:

* Terminal.Gui layout recalculates sizes
* terminal view gets new rows/cols
* backend receives `Resize(cols, rows)` so pwsh adapts

### 9.6 Terminal + file panel integration

Optional commands:

* “Send selection path to terminal”
* “cd terminal to active panel directory”
* “sync terminal cwd with active panel” (toggle)

Because this can be disruptive, default to *explicit* commands first.

---

## 10. Filesystem Design

### 10.1 File listing engine

File panels need fast directory enumeration:

* asynchronous enumeration
* cancellation if user navigates away
* metadata retrieval strategy:

  * minimal for initial list
  * lazy-load heavy metadata (e.g., git status overlays, file hashes)
* sorting and filtering performed on cached list

Represent items:

* `FileItem { FullPath, Name, Type, Size?, Modified?, Attributes, IsSymlink, Target? }`

### 10.2 Panel model

Each file panel has:

* `CurrentDirectory`
* `Items` (virtualized list)
* `SortMode`
* `Filter`
* `SelectionState` (cursor + marked items)
* `History` (back/forward stack)

### 10.3 File operations pipeline

Operations are tasks:

* Copy, move, delete, rename, mkdir
* Designed as a plan + execution:

  1. **Plan**: compute what will happen, detect conflicts
  2. **Confirm**: UI prompt if needed (overwrite? merge? skip?)
  3. **Execute**: run with progress + cancellation
  4. **Report**: results, errors, summary

Conflict resolution policies:

* overwrite
* skip
* rename
* merge directories
* apply-to-all

Deletion strategy:

* platform-specific “trash/recycle bin” if possible
* fallback to permanent delete with explicit confirmation

### 10.4 File watching and refresh

Use file system watchers for active directories (and optionally for repo roots):

* debounce frequent changes
* update panel lists incrementally where possible
* always allow manual refresh command

---

## 11. Git Integration Design

### 11.1 Git backend strategy

Define `IGitClient` abstraction with a primary implementation using **git CLI**:

**Why CLI first**

* maximum compatibility with user’s git config, credential managers, ssh agents
* supports advanced features earlier without re-implementing core git behavior
* “porcelain” outputs provide stable parsing

Later we can add optional lib-based optimization, but keep CLI as baseline.

### 11.2 Repo discovery

Repo context is derived from:

* active file panel directory
* optionally both panels (show combined workspace)
* a “Repo list” panel that tracks detected repos

forcing rules:

* If active panel is inside a repo → that repo is active
* If neither panel in a repo → git panels show “no repo detected” or prompt to pick repo

### 11.3 Git status model

Use `git status --porcelain` style outputs for parsing into:

* `ChangedFile { Path, StatusKind, StagedStatus, UnstagedStatus, RenameFrom?, IsSubmodule? }`
* group into:

  * staged
  * unstaged
  * untracked
  * conflicts

### 11.4 Diff model

Diff viewer needs structured hunks:

* file header
* hunks with line ranges
* lines with types: context/add/remove
* optional intra-line highlighting later

Generate via `git diff`:

* unstaged diff: `git diff`
* staged diff: `git diff --cached`

Later:

* hunk staging via `git apply --cached` or `git add -p` integration (interactive) or custom patch application.

### 11.5 Commit workflow

Commit panel design:

* list changed files (staged/unstaged)
* commit message editor (multi-line)
* options: amend, sign-off, no-verify
* action: Commit
* post-commit: refresh status, show notification

### 11.6 Branch and log

* Branch panel:

  * current branch highlighted
  * checkout/create/delete/rename
  * remote branches (read-only initially)
* Log panel:

  * commit list (hash, author, date, subject)
  * commit details view (files changed, diff)
  * actions: checkout commit (detached), create branch, cherry-pick (later)

### 11.7 Network operations

Fetch/pull/push:

* run in background task
* stream git output to a task log panel (and/or terminal-style output window)
* rely on user’s configured credential/auth workflows

### 11.8 Conflicts

Conflict detection:

* status identifies unmerged paths
* conflict resolution UX:

  * “Conflicts” group in status
  * open diff viewer for conflict file
  * offer commands:

    * “take ours/theirs” (implemented via git checkout commands)
    * “mark resolved” (git add)

---

## 12. Panel System

### 12.1 Panel interface

Define `IPanel`:

* `string Id`
* `string Title`
* `View CreateView()` (Terminal.Gui view)
* `PanelDockPreference DefaultDock`
* `void OnActivated() / OnDeactivated()`
* `bool CanClose`
* `Task HandleCommandAsync(CommandContext ctx, ...)` (optional)

Panels are lightweight wrappers around a view + state binding.

### 12.2 Core panels (MVP)

* **FilePanel** (Left)
* **FilePanel** (Right)
* **TerminalPanel** (always available, resizable)
* **GitStatusPanel**
* **GitDiffPanel**
* **GitCommitPanel**
* **GitLogPanel**
* **Tasks/ProgressPanel** (background jobs)
* **NotificationsPanel** or toast system

### 12.3 Layout manager

Create a custom layout manager built on Terminal.Gui container views:

* supports docking: left/right/top/bottom/fill
* supports split panes (vertical/horizontal)
* supports collapse/expand
* persists layout config

The layout manager owns:

* panel instances
* split ratios
* visibility states
* terminal reserved height

---

## 13. Configuration and Persistence

### 13.1 Configuration layers

Support layered configuration:

1. Built-in defaults
2. User config (global)
3. Workspace config (per directory/repo)
4. Session state (last open layout, last dirs) optionally persisted

Recommended formats:

* JSON for structured config (bindings, layout, theme)
* simple TOML/YAML is fine if preferred, but JSON integrates well with .NET config stack

### 13.2 Keymap configuration

Keymap config structure:

* `bindings`: array of `{ commandId, chord, context, when }`
* chord expressed as sequences: `["Ctrl+K", "Ctrl+C"]` or `"g s"`
* allow platform variants (Windows vs Unix function key quirks)

### 13.3 Layout configuration

Persist:

* which panels are open
* dock positions and split ratios
* terminal height
* last focused panel

### 13.4 Theme configuration

Expose:

* color schemes
* highlight styles (active panel, selection, git statuses)
* minimal set for MVP; expand later

---

## 14. Extensibility (Planned)

### 14.1 Plugin architecture (later)

Provide extension points without compromising stability:

* new panels
* new commands
* file viewers
* git providers (future: other VCS)
* integration with external tools

Possible plugin mechanisms:

* .NET assembly loading with a strict API surface
* optional scripting integration (PowerShell scripts) for user commands/macros

### 14.2 Macro/chord scripting

Because chords exist, macros naturally follow:

* define a command sequence
* bind to a chord
* optionally parameterize (prompt user)

---

## 15. Error Handling and Diagnostics

### 15.1 Error policy

* Every background task reports:

  * success/failure
  * summary
  * detailed log (expandable)
* UI never crashes from a single failed operation:

  * show notification
  * write to log
  * allow retry

### 15.2 Logging

* structured logs via `ILogger`
* log file location in user profile
* include correlation IDs for tasks

### 15.3 Crash recovery

* on crash, preserve last session state if possible
* on next start, offer “restore last session layout” (optional)

---

## 16. Performance Strategy

### 16.1 Key performance risks

* huge directories (100k+ entries)
* huge git repos (status/log/diff expensive)
* terminal output bursts

### 16.2 Mitigations

* list virtualization (only render visible rows)
* incremental loading and debounced refresh
* cancellation on navigation changes
* caching:

  * directory listing cache with invalidation
  * git status cache keyed by HEAD + index timestamp if feasible
* terminal emulator optimized redraw (dirty regions)

---

## 17. Security Considerations

* Treat all external command execution as untrusted input:

  * escape paths correctly
  * never build shell commands via string concatenation when avoidable
* Do not store credentials:

  * rely on git credential helpers / ssh agent
* Plugins/scripts:

  * clearly mark as trusted user code
  * provide warnings and allow disable

---

## 18. Testing Strategy

### 18.1 Unit tests

* selection logic, sorting/filtering
* chord matching engine
* command dispatch/routing
* parsing git outputs (porcelain, log formats)

### 18.2 Integration tests

* run git operations in temp repos (init, commit, branch, merge conflict)
* file operations in temp directories (copy/move conflicts, trash behavior)

### 18.3 UI tests (limited but valuable)

* panel layout persistence
* command palette search behavior
* “toggle panels” state transitions

---

## 19. Milestones / Delivery Plan

### Phase 0: Skeleton

* Terminal.Gui app scaffolding, DI host, config, logging
* Basic layout manager + terminal placeholder panel
* Command system + key binding + chord engine

### Phase 1: MVP File Manager + Terminal

* Two file panels: list, navigate, sort, select
* File ops: copy/move/delete/rename/mkdir with progress
* Integrated pwsh terminal view working with PTY backend
* Toggle panels on/off, terminal resize

### Phase 2: MVP Git

* Repo detection
* Git status panel
* Diff viewer
* Commit panel
* Branches + log panels
* Fetch/pull/push tasks with logs

### Phase 3: Polish and power-user features

* Search, preview improvements
* Better diff rendering, hunk-level staging
* Conflict assistance
* Layout presets, theming
* Plugin/macro foundations

---

## Appendix A: Concrete “building blocks” to implement first

### A1. Core abstractions (minimal set)

* `IStateStore<AppState>`
* `ICommandDispatcher`
* `IKeyBindingService` + `ChordMatcher`
* `ITaskRunner`
* `IFilePanelService` (enumeration, navigation, selection)
* `IFileOperationService`
* `IGitClient`
* `ITerminalBackend` + `TerminalEmulator`

### A2. “Golden path” user flow for early validation

1. Start app → shows left/right panels + terminal bottom
2. Navigate directories with keyboard
3. Copy file from left → right with `F5`
4. Toggle panels off with `Ctrl+O` → full terminal
5. Toggle panels on again → layout restored
6. Enter a repo → Git status panel shows changes
7. View diff → commit → push (or simulate push)

---

## Appendix B: Example configuration sketch (illustrative)

```json
{
  "terminal": {
    "shell": "pwsh",
    "reserveRows": 8,
    "syncCwdWithActivePanel": false
  },
  "layout": {
    "startup": "dualPane",
    "panels": [
      { "id": "file.left", "dock": "left", "size": "50%" },
      { "id": "file.right", "dock": "fill" },
      { "id": "terminal", "dock": "bottom", "sizeRows": 8 }
    ]
  },
  "keybindings": [
    { "command": "ui.togglePanels", "chord": ["Ctrl+O"], "context": "global" },
    { "command": "file.copy", "chord": ["F5"], "context": "filePanel" },
    { "command": "git.status.show", "chord": ["g", "s"], "context": "global" },
    { "command": "ui.commandPalette", "chord": ["Ctrl+Shift+P"], "context": "global" }
  ],
  "chords": {
    "timeoutMs": 1000
  }
}
```

---

## Open Decisions (captured for implementation planning)

These are not blockers for writing code, but should be decided early because they affect architecture:

1. **Terminal emulator library**: build minimal ANSI parser ourselves vs embed an existing VT parser component
2. **Git CLI parsing formats**: pick exact commands/flags once and lock test fixtures (status/log/diff)
3. **Virtualization approach for lists**: custom list view vs adapting Terminal.Gui list components
4. **Session persistence**: how much state to restore on startup (layout only vs also directories/repos)

---

# File and Directory Operations Design Document

## Console File Manager (Terminal.Gui v2) — UI + Execution Engine

## 1. Purpose

This document specifies the **user experience**, **command surface**, **UI dialogs**, **data model**, and **execution pipeline** for file/directory operations in our console-mode file manager (Far-inspired). It is intended to guide implementation of:

* Copy / Move / Delete / Rename
* Create directory / file
* Selection and targeting (active/passive panels)
* Conflict detection and resolution
* Progress, cancellation, background execution, and reporting
* Cross-platform behavior (Windows/Linux/macOS) and edge cases

This document does **not** cover Git operations, plugin architecture, or the terminal emulator beyond how file ops interact with them.

---

## 2. Design Goals and Constraints

### 2.1 Goals

* **Fast keyboard workflow** (Far-like): predictable keys, minimal prompts, quick defaults.
* **Safe by default**: confirmations for destructive actions, clear conflict resolution options.
* **Non-blocking UI**: operations run in background with progress and cancellation.
* **Works with multi-select and directories**: operations apply to marked items or cursor item.
* **Cross-platform correctness**: support differences in permissions, symlinks, case sensitivity, path rules.
* **Recoverability**: strong reporting; optional “trash/recycle” mode; future undo hooks.

### 2.2 Constraints

* UI built with **Terminal.Gui v2**
* App is console-mode; must handle resizing, mouse, and keyboard.
* Must be robust under:

  * huge directories
  * long-running copies
  * permission errors
  * partial failures

---

## 3. Terminology

* **Active panel**: the file panel with focus (source by default).
* **Passive panel**: the “other” file panel (destination by default).
* **Cursor item**: item under the selection cursor.
* **Marked items**: multi-selected items (checkbox/mark model).
* **Operation**: a user-triggered file action (copy/move/delete/etc.).
* **Plan**: computed set of steps and decisions (conflict detection, destination mapping).
* **Step**: a single file-system action (copy file X → Y, create directory Z, etc.).
* **Policy**: user choices affecting plan/execution (overwrite/skip/rename/merge, follow symlinks, etc.).

---

## 4. User Experience Model

### 4.1 Selection rules (what the operation applies to)

When an operation command is invoked in a file panel:

1. If there are **marked items**, the operation applies to **all marked items**.
2. Else, it applies to the **cursor item** (single selection).
3. If cursor is on `..` (parent) or empty area: operation is disabled (except Create).

UI must show a **selection summary** in dialogs:

* `3 items (2 files, 1 folder)`
* Optional total size if already known (see progress section for lazy sizing)

### 4.2 Source and destination defaults (Far-style)

* **Copy/Move** default destination: **passive panel current directory**.
* If passive panel is not visible, default destination: **active panel current directory** (or last-used destination).
* For single-file copy: destination path defaults to `<destDir>\<sourceName>` and is editable.
* For multi selection: destination defaults to `<destDir>\` (directory only).

### 4.3 Operations are asynchronous

* When user confirms, the dialog closes and operation appears in:

  * a **Tasks/Operations panel**, and/or
  * a **bottom status line** with a compact progress indicator,
  * and a **notification** on completion/failure.

User may continue navigating while operations run.

---

## 5. Command Surface and Default Keys

The following command IDs are examples; IDs must be stable.

### 5.1 Primary operations (Far-like defaults)

* `file.copy` — **F5**
* `file.move` — **F6**
* `file.rename` — **Shift+F6** (or `F2` if desired)
* `file.mkdir` — **F7**
* `file.delete` — **F8**
* `file.refresh` — **Ctrl+R**
* `file.properties` — **Alt+Enter**

### 5.2 Utility operations

* `file.newFile` — **Shift+F4** (optional)
* `file.duplicate` — (optional)
* `file.copyPathToClipboard` — (optional)
* `file.paste` — (optional)
* `file.toggleMark` — **Insert**
* `file.unmarkAll` — **Ctrl+U**
* `file.invertMarks` — **Num * / Ctrl+I** (optional)

All are bound via the command/keybinding system; chords are supported.

---

## 6. UI Components

### 6.1 Operation dialogs (common patterns)

All operation dialogs share:

* **Header**: operation name + selection summary
* **Source preview** (read-only): truncated paths; optionally “Show list…”
* **Destination field** when relevant
* **Options** section (checkboxes/radio groups)
* **Buttons**: `OK` (default), `Cancel`, sometimes `Queue/Background` (if we differentiate)

Terminal.Gui widgets likely used:

* `Dialog`, `Label`, `TextField`, `CheckBox`, `RadioGroup`, `Button`, `ListView`, `FrameView`

#### List of sources

For multi-selection, we do not dump all paths into the dialog by default.

* Show: `Selected: 123 items` + `Show items…` button
* Clicking opens a scrollable list (read-only) with a quick filter.

### 6.2 Progress UI

We need two layers:

1. **Compact**: status line indicator

   * `Copying 3/120 • 1.2 GB / 10.4 GB • 52 MB/s • ETA 02:31 (Esc to cancel)`
2. **Detailed**: Operations/Tasks panel (or a modal “progress window” when user chooses)

   * per-operation log
   * current file path
   * bytes progress bar
   * file count progress bar
   * buttons: `Cancel`, `Pause` (optional), `Details`

We should support multiple concurrent operations (limited concurrency in runner), so the tasks panel is the best long-term UI.

### 6.3 Conflict resolution UI (modal)

When a conflict is encountered:

* if policy is already “apply to all”, execution continues silently
* otherwise show a modal conflict dialog that blocks only the operation’s progress (not the whole UI thread—UI stays responsive, but that operation awaits user decision)

Conflict dialog shows:

* Source: path, size, modified time
* Destination: path, size, modified time (if exists)
* Options:

  * Overwrite
  * Skip
  * Rename (auto-suggest)
  * Compare (optional: open viewer/diff for text)
  * Apply to all (checkbox)
* Buttons: `OK`, `Cancel Operation`

For directory conflicts:

* Merge directories (default)
* Replace directory (dangerous; requires extra confirmation)
* Skip

### 6.4 Error UI and result summary

On completion:

* success: toast/notification `Copied 120 items (10.4 GB) in 2m31s`
* partial failure: notification `Completed with errors (3 failed). View log…`
* failure: `Operation cancelled` or `Operation failed` with a “View details” action

Detailed result view contains:

* list of failed items with error messages
* last N operations log lines
* “Retry failed items” (later)

---

## 7. Operation Engine Architecture

### 7.1 Data model

#### Operation request (from UI)

```csharp
public sealed record FileOperationRequest(
    FileOperationKind Kind,                 // Copy, Move, Delete, Rename, MkDir, NewFile
    IReadOnlyList<string> SourcePaths,       // absolute paths
    string? DestinationPath,                // for copy/move/rename/newfile
    FileOperationOptions Options,            // overwrite policy, symlink policy, etc.
    Guid CorrelationId,
    DateTimeOffset RequestedAt
);
```

#### Options (policy)

Key policies:

* `ConflictPolicy`:

  * `Ask` (default)
  * `Overwrite`
  * `Skip`
  * `Rename`
  * `OverwriteIfNewer` (optional)
* `DirectoryConflictPolicy`:

  * `Merge` (default)
  * `Replace` (dangerous)
  * `Skip`
* `SymlinkPolicy`:

  * `CopyAsLink` (default)
  * `FollowAndCopyTarget` (optional)
* `DeletePolicy`:

  * `TrashIfPossible` (default)
  * `Permanent`
* `PreserveTimestamps` (default true for copy)
* `PreservePermissions` (best effort; platform-specific)
* `VerifyAfterCopy` (optional; expensive)
* `Parallelism` (default 1–2; configurable)

#### Plan and steps

```csharp
public sealed record FileOperationPlan(
    FileOperationRequest Request,
    IReadOnlyList<FileOperationStep> Steps,
    EstimatedTotals Totals,
    IReadOnlyList<PlanWarning> Warnings
);

public abstract record FileOperationStep
{
    public required string DisplayName { get; init; }
}
public sealed record CreateDirectoryStep(string Path) : FileOperationStep;
public sealed record CopyFileStep(string Source, string Dest) : FileOperationStep;
public sealed record MoveFileStep(string Source, string Dest) : FileOperationStep;
public sealed record DeletePathStep(string Path, DeletePolicy Policy) : FileOperationStep;
public sealed record RenamePathStep(string Source, string Dest) : FileOperationStep;
// … plus maybe: SetAttributesStep, SetPermissionsStep
```

#### Progress events

Use a strongly-typed progress channel:

```csharp
public abstract record FileOpProgressEvent(Guid OperationId);
public sealed record FileStarted(Guid OperationId, string Path, long? Size) : FileOpProgressEvent(OperationId);
public sealed record BytesTransferred(Guid OperationId, long DeltaBytes, long? TotalBytesForFile) : FileOpProgressEvent(OperationId);
public sealed record FileCompleted(Guid OperationId, string Path) : FileOpProgressEvent(OperationId);
public sealed record StepFailed(Guid OperationId, string Path, Exception Error) : FileOpProgressEvent(OperationId);
public sealed record OperationCompleted(Guid OperationId, FileOpResult Result) : FileOpProgressEvent(OperationId);
```

### 7.2 Pipeline

**Phase A — Gather** (UI)

* Determine source paths from selection model
* Determine destination default
* Show dialog; on confirm create `FileOperationRequest`

**Phase B — Plan** (background, cancellable)

* Validate paths exist and are accessible
* Map source(s) → destination(s)
* Expand directories to steps:

  * create dest directories
  * enumerate contents (lazy or upfront—see 7.5)
* Detect conflicts (dest exists, type mismatch, etc.)
* Compute initial totals (files count, bytes estimate if possible)
* Return `FileOperationPlan` to the operation runner

**Phase C — Execute** (background, cancellable, interactive on conflicts)

* Execute steps sequentially or with limited parallelism
* On conflict/error:

  * ask user (if policy == Ask) via conflict dialog
  * continue based on decision
* Report progress events and logs
* On completion: refresh relevant panels + show summary

### 7.3 Separation of concerns

* `FileOperationPlanner`: pure logic + filesystem inspection
* `FileOperationExecutor`: step execution, retry policy, progress reporting
* `IFileSystem`: abstraction around filesystem calls for testability
* `IUserDecisionService`: UI bridge for conflict prompts (returns decisions asynchronously)
* `ITaskRunner`: schedules operations, maintains status list

### 7.4 Cancellation and pausing

* Every operation has a `CancellationToken`
* Cancel behavior:

  * copy: stop after current buffer write (best effort)
  * move/delete: stop between steps
* (Optional later) pause:

  * executor checks `PauseToken` between chunks

---

## 7.5 Planning strategy for large directories

Computing “total bytes” for huge directory trees can be slow.

We support two planning modes:

1. **Fast plan (default)**

   * Enumerate directory structure to generate steps incrementally
   * Do not precompute exact total bytes; show “estimating…” until known
   * Progress shows file count and bytes transferred so far

2. **Full pre-scan (optional)**

   * Compute totals (bytes, file count) before executing
   * Better ETA but slower start

UI option in copy/move dialog:

* `[ ] Pre-scan to compute total size (slower start)`

---

## 8. Detailed Operation Specifications

## 8.1 Copy

### 8.1.1 Supported inputs

* single file → file or directory destination
* multiple selection → directory destination
* directory → directory destination (recursive)

### 8.1.2 Copy dialog (F5)

Fields:

* `From:` (read-only summary + “Show items…”)
* `To:` TextField (default passive panel path)
* Options:

  * Conflict: `Ask / Overwrite / Skip / Rename` (RadioGroup)
  * Directory conflict: `Merge / Replace / Skip` (RadioGroup; Replace requires confirmation)
  * `[x] Preserve timestamps` (default on)
  * `[ ] Preserve permissions (best effort)` (default on for Unix, optional on Windows)
  * `[ ] Copy symlinks as symlinks` (default on)
  * `[ ] Pre-scan to compute total size` (default off)
  * `[ ] Verify after copy (checksum)` (default off)
    Buttons:
* `Copy` (default)
* `Cancel`

### 8.1.3 Execution rules

* Copy file implemented with stream copy for progress:

  * open source stream
  * create dest temp file (optional) then rename for atomicity
  * copy in chunks, report bytes
  * preserve timestamps/attrs after write
* If destination exists and policy is Ask:

  * raise conflict prompt with file metadata
* Directories:

  * ensure destination directory exists
  * recurse; apply directory conflict policy

### 8.1.4 Atomicity and partial results

* When copying a file:

  * (Preferred) write to `dest.tmp` and rename to dest after completion

    * avoids partial overwritten files if cancelled mid-copy
  * if rename fails, fall back to direct write with warning
* For directory copy, partial results may exist if cancelled; summary must communicate.

---

## 8.2 Move

### 8.2.1 Move dialog (F6)

Similar to copy, but with differences:

* `Preserve timestamps` is implicit (move keeps metadata)
* Include option: `[x] Try atomic rename when possible` (default on)

### 8.2.2 Execution rules

* Attempt filesystem rename when:

  * same volume/device and compatible filesystem
* If rename fails with cross-device error:

  * fall back to **copy + delete** (with same conflict policies)
* Deletion after copy must follow delete policy:

  * if delete fails, mark operation partial failure and show details

---

## 8.3 Delete

### 8.3.1 Delete dialog (F8)

Shows:

* `Delete:` selection summary
* Mode:

  * `Move to Trash/Recycle Bin` (default when available)
  * `Permanent delete`
* `[ ] Confirm each file` (default off; can be enabled for safety)
* `[ ] Skip read-only/permission protected` (default off; better to ask)
  Buttons:
* `Delete`
* `Cancel`

### 8.3.2 Execution rules

* Default: `TrashIfPossible`

  * Windows: Recycle Bin
  * macOS/Linux: move to Trash location (implementation behind `ITrashService`)
* If trash fails for an item:

  * fallback prompt: “Trash failed; permanently delete?” (apply-to-all supported)
  * or mark as failure based on user choice

### 8.3.3 Safety prompts

* If selection includes a non-empty directory:

  * show explicit warning `This will delete N items inside folders.`
* Permanent delete requires extra confirmation:

  * type-to-confirm (optional) or “Hold Shift + Enter” pattern (optional)
  * at minimum: second confirmation screen if `Permanent`

### 8.3.4 Read-only / permission issues

If delete fails due to attributes/permissions:

* prompt:

  * “Remove read-only and delete?” (Windows)
  * “Try chmod to allow delete?” (Unix; best effort and only if configured)
* Apply-to-all supported

---

## 8.4 Rename

### 8.4.1 Rename UI

Two modes:

1. **Inline rename** (fast): pressing Shift+F6 starts editing the filename in place

   * commit with Enter, cancel with Esc
   * best for single file

2. **Rename dialog** (fallback for long names/paths):

   * `From:` full path
   * `To:` editable name (or full path)
   * Options:

     * `[ ] Keep extension` (optional)
     * `[ ] Case-only rename safe mode` (see 10.4)
   * `Rename` / `Cancel`

### 8.4.2 Execution rules

* If rename is within same directory: use `File.Move` / `Directory.Move`
* If target exists:

  * conflict policy applies (Ask/Overwrite/Skip/Rename)
* For case-only renames on case-insensitive FS:

  * use two-step rename via temp name (see 10.4)

---

## 8.5 Create directory (MkDir)

### 8.5.1 MkDir dialog (F7)

* `Create folder in:` active panel path
* `Name:` TextField (supports relative paths like `a\b\c` if enabled)
* Buttons: `Create`, `Cancel`
* Option: `[ ] Create intermediate folders` (default on if relative path contains separators)

### 8.5.2 Execution rules

* Create directory
* Refresh panel and select new folder
* On conflict (exists):

  * show `Folder already exists` with option to focus it

---

## 8.6 Create file (New File) (optional MVP+)

### 8.6.1 New file dialog

* `Create file in:` active panel path
* `Name:` TextField
* Buttons: `Create`, `Cancel`
* Option: `[ ] Open in editor after creating` (default on)

### 8.6.2 Execution rules

* Create empty file (safe create: fail if exists unless overwrite chosen)
* If open editor: open internal viewer/editor or send to external command (separate feature)

---

## 9. Conflict Handling Specification

### 9.1 Conflict types

* Destination path exists:

  * file vs file
  * dir vs dir
  * file vs dir (type mismatch)
* Destination path is read-only / not writable
* Name collisions due to case-insensitivity
* Symlink collisions
* Invalid destination path characters for OS

### 9.2 Conflict resolution decisions

For file overwrite conflicts:

* Overwrite
* Skip
* Rename (generate unique: `name (2).ext` or `name~2.ext`)
* Compare (optional later; opens diff/viewer)
* Apply to all

For directory conflicts:

* Merge (default)
* Replace (dangerous):

  * requires confirmation “This will delete/overwrite destination directory contents”
* Skip

### 9.3 Policy persistence

When a conflict dialog is shown, user can apply decision:

* only to this conflict
* to all remaining conflicts in this operation

Store in operation runtime context:

```csharp
public sealed class ConflictResolutionContext {
    public ConflictPolicy? FilePolicyForRest { get; set; }
    public DirectoryConflictPolicy? DirPolicyForRest { get; set; }
}
```

### 9.4 Rename suggestion rules

* If destination exists:

  * append ` (n)` before extension
  * preserve extension
* If path too long or invalid:

  * fallback to truncation + hash suffix (best effort) and warn

---

## 10. Edge Cases and Platform Considerations

### 10.1 Symlinks and junctions

Default: **copy symlink as symlink** (don’t dereference).
Options allow “follow link and copy target”.

When traversing directories:

* Detect symlink loops if follow mode is enabled:

  * track visited inode/path identity where possible
  * or maintain max recursion depth

### 10.2 Permissions and ownership

* Preserve timestamps: generally supported cross-platform
* Preserve permissions: best effort

  * Unix: `chmod` after copy (copy mode bits from source)
  * Windows: consider ACL copy later; for MVP, keep default created ACLs unless option is explicitly enabled and we have a safe implementation

### 10.3 Long paths and normalization

* Use absolute paths internally
* Normalize separators
* Ensure robust behavior with:

  * Windows long paths (prefix handling if needed)
  * trailing dots/spaces (Windows restrictions)
  * reserved device names (CON, NUL…)

Planner should validate destination names early and show a clear UI error.

### 10.4 Case-only rename on case-insensitive FS

Example: `readme.md` → `README.md` on Windows/macOS default.

Implementation:

1. rename to temp: `readme.md` → `.__tmp__rename__<guid>`
2. rename temp → `README.md`

If temp name exists, pick another.

### 10.5 Cross-volume moves

* Attempt rename; if throws cross-device:

  * fallback to copy + delete
  * preserve timestamps if configured
  * if delete fails: partial failure

### 10.6 Handling locked files / in-use

* On copy: reading may fail; treat as failure or retry.
* On move/delete: may fail; prompt with:

  * Retry
  * Skip
  * Cancel operation
  * Apply to all

A retry loop with small backoff (configurable) can handle transient locks.

---

## 11. Refresh and Panel Updates

### 11.1 Refresh policy

After operation step completion (or at end), update file panels:

* If active/passive panel directory is affected, refresh listing.
* For large operations, avoid refreshing on every file; use throttled refresh:

  * refresh at end, plus maybe periodic “dirty” indicators

### 11.2 Selection preservation

After refresh:

* attempt to keep cursor on:

  * newly created/copied/renamed item, if applicable
  * or keep same filename if still present
* if item removed, move cursor to nearest index

### 11.3 Interaction with file watchers

If watchers are enabled:

* during large operations, watcher events may flood
* apply debounce and coalesce
* optionally disable watchers for directories involved in active operations and do a final refresh

---

## 12. Implementation Details: Execution Algorithms

### 12.1 Copy file with progress (sketch)

* `OpenRead(source)`
* `Create(destTemp)`
* loop:

  * read buffer
  * write buffer
  * report bytes
  * check cancellation
* flush and close
* set timestamps/attrs
* rename destTemp → dest (overwrite per policy if allowed)

### 12.2 Directory traversal

Planner generates steps:

* create directory
* enumerate children
* for each child:

  * if dir: recurse
  * if file: copy step
  * if symlink: copy link or follow based on policy

### 12.3 Limited parallelism

Optional optimization:

* Allow copying multiple files concurrently (e.g., parallelism 2–4)
* Maintain ordered progress reporting per file
* Be conservative to avoid disk thrash

Default: 1 (safe, predictable). Provide config knob.

---

## 13. UI Flow Examples

### 13.1 Copy (F5) — multi selection

1. User marks files/folders
2. Press F5
3. Copy dialog opens:

   * To: `<passiveDir>`
   * Options default
4. Press Enter → dialog closes
5. Task appears in Tasks panel and status line updates
6. On conflict: conflict dialog appears; user chooses Overwrite + Apply to all
7. Completion notification + passive panel refresh

### 13.2 Delete (F8) — permanent

1. Press F8
2. Delete dialog shows default “Trash”
3. User switches to “Permanent”
4. App shows second confirmation step
5. Execute delete with progress
6. Report any failures (locked files) with Retry/Skip choices

### 13.3 Rename (Shift+F6) — inline

1. Press Shift+F6
2. Filename becomes editable in list row
3. User types new name, Enter
4. If conflict: prompt
5. Refresh row in place, maintain cursor

---

## 14. Testing Strategy (File Ops)

### 14.1 Unit tests

* selection → source list generation rules
* destination mapping logic
* conflict detection logic
* rename suggestion generator
* case-only rename two-step planner
* planner step generation for directory trees (with symlink policies)

### 14.2 Integration tests (temp filesystem)

* copy large file + cancel mid-way (ensure no corrupted dest if temp strategy enabled)
* move across volumes (simulate cross-device) → copy+delete fallback
* delete to trash (mock `ITrashService`)
* conflict flows: overwrite/skip/rename
* permission errors: read-only file delete handling

### 14.3 UI behavior tests (where feasible)

* dialogs open with correct defaults
* key bindings invoke commands correctly
* progress panel updates without blocking UI

---

## 15. MVP Checklist (for file operations)

Minimum features to ship file ops MVP:

* Selection model with marked items
* Copy dialog + copy execution with progress + cancellation
* Move dialog + rename/fallback cross-device behavior
* Delete dialog with trash/permanent modes
* Rename (dialog-based at least)
* MkDir
* Conflict dialogs for file and directory collisions
* Task list panel or at least a single-operation progress UI
* Refresh panels after operations

---

## 16. Future Enhancements (Post-MVP)

* Batch rename with patterns/regex and preview
* Compare/sync directories
* Undo stack (limited) for non-destructive ops (rename/move within same FS, trash restore)
* Smart conflict policies: “overwrite if source newer”
* Hunk-level copy? (not applicable) / file patching (not file ops)
* Integration: “Send selected paths to terminal” and “Copy path”

---

# Read‑only Git Operations Design Document

## Browse, View, Analyze Git Repositories (Terminal.Gui v2 UI)

## 1. Purpose and Scope

This document defines the **read‑only Git experience** inside our console app: how users **browse**, **view**, and **analyze** repository state without making changes (no staging, committing, checkout, rebase, push/pull, etc.). It specifies:

* UI panels, layouts, and interactions (keyboard + mouse)
* Command surface (IDs + default key chords)
* Data models used by UI and services
* Git query backend architecture and parsing strategy
* Performance, caching, incremental loading
* Error handling and testing plan

### 1.1 In scope (read‑only)

* Repository discovery + selection
* Status browsing (changed/untracked/ignored/conflicts summary)
* Diff viewing (working tree vs HEAD, staged vs HEAD if we show it read‑only, commit diffs)
* Commit log browsing + commit details (files changed, stats)
* Branch/tag/remote browsing (read‑only lists)
* Stash browsing (list + show patch)
* File history browsing (log for a path, follow renames)
* Blame/annotate (optional but designed here)
* Search/grep (e.g., `git grep`, history search)
* “Repo insights” (counts, ahead/behind, recent activity summaries)

### 1.2 Explicitly out of scope (mutating)

* Stage/unstage, reset, commit, amend
* Checkout/switch branches, create/delete branches
* Merge/rebase/cherry‑pick
* Push/pull/fetch (even though fetch is “mostly read‑only”, it changes remote tracking refs locally)
* Stash apply/pop/drop
* Conflict resolution edits

> UX note: it’s OK if we show those actions as disabled/hidden with “Coming later”, but this document does not design their behavior.

---

## 2. UX Principles (Read‑only Git)

1. **Always safe**
   No command in this scope changes the repository. No accidental writes.

2. **Fast navigation between “Status → Diff → Log → Details”**
   The UI should feel like a graph of views rather than a linear wizard.

3. **Deep information, shallow interaction**
   You can open and inspect anything quickly, but actions are limited to viewing and filtering.

4. **Lazy loading**
   Do not compute diffs/logs until requested. Avoid expensive repo scans unless the user asks.

5. **Integrated with file panels**
   Git UI follows the active directory context by default and can be pinned to a selected repo.

---

## 3. Repository Context and Discovery

### 3.1 Active repo context

We define **ActiveRepo** as the repo most relevant to the user right now.

Rules (in priority order):

1. If the Git UI is **pinned** to a repo → use that pinned repo.
2. Else if the **active file panel** path is inside a repo → use that repo.
3. Else if the passive file panel is inside a repo → use that repo.
4. Else → “No repo detected” state; offer repo picker.

### 3.2 Discovery strategy

We discover repos from:

* The active/passive panel paths (walk up parents until `.git` is found)
* A **Recent Repos** list persisted in config
* Optional: user‑configured “workspace roots” (scan at startup *only* if configured)

### 3.3 Repo identity and special cases

Store:

* `RepoRoot` (working tree root)
* `GitDir` from `git rev-parse --git-dir` (important for worktrees where `.git` is a file)
* `IsBare` (we can display but browsing working tree‑based status is limited)

---

## 4. UI Overview: Panels and Layout

### 4.1 Default Git panel set

Read‑only Git features are presented through panels:

* **Repo Overview (Header / Summary)**: repo path, branch, ahead/behind, dirty state
* **Status Panel**: groups of changed files (unstaged/staged/untracked/conflicts/ignored summary)
* **Diff Panel**: unified or side‑by‑side diff view
* **Log Panel**: commit list with filtering + pagination
* **Commit Details Panel**: metadata + files changed + stats + open diff
* **Refs Panel**: branches/tags/remotes lists (read‑only)
* **Stash Panel**: stash list + show diff
* **Blame Panel** (optional): annotate file lines
* **Search Panel**: `git grep` results + open file at match
* **Insights Panel** (optional): activity summaries and simple metrics

### 4.2 Layout recommendations

Because the app is multi‑panel, we define two common layouts:

#### Layout A: “Status + Diff”

```
┌────────────────────────────────────────────────────────────┐
│ Repo: <name>  Branch: main  Ahead/Behind: +2/-0  Dirty: yes │
├───────────────────────────────┬────────────────────────────┤
│ Git Status (files)            │ Diff (selected file)        │
│  - Unstaged (12)              │  ┌───────────────────────┐  │
│  - Untracked (3)              │  │ unified/side-by-side   │  │
│  - Conflicts (0)              │  │ scroll, search, nav    │  │
│                               │  └───────────────────────┘  │
├────────────────────────────────────────────────────────────┤
│ Terminal (pwsh) reserved height                              │
└────────────────────────────────────────────────────────────┘
```

#### Layout B: “Log + Details”

```
┌────────────────────────────────────────────────────────────┐
│ Repo summary bar                                             │
├───────────────────────────────┬────────────────────────────┤
│ Log (commit list)             │ Commit details + diff/files │
│  hash  msg  author  date      │ metadata, changed files     │
│  ...                           │ open file diff             │
├────────────────────────────────────────────────────────────┤
│ Terminal                                                     │
└────────────────────────────────────────────────────────────┘
```

### 4.3 Responsive behavior

* When width is narrow:

  * Diff defaults to **unified** mode
  * Commit details collapses into tabbed view (Metadata / Files / Diff)
* When height is low:

  * Status groups become collapsible (only headers show counts)
  * Log uses compact rows (hash+subject)

---

## 5. Repo Overview (Header / Summary UI)

The top Git summary line is always visible whenever any Git panel is open.

Fields (read‑only):

* Repo name (folder name) + root path (truncated middle)
* HEAD state:

  * branch name, or “detached @ <shortHash>”
* Upstream and ahead/behind (if upstream exists)
* Dirty state:

  * counts: `M:12 A:3 D:1 ??:4` (or grouped)
* Special states:

  * merge/rebase/cherry‑pick in progress (detected via `.git` state)

Interactions:

* Click repo name → open Repo Picker
* Click branch name → open Refs panel focused on current branch
* Click dirty counts → open Status panel (or focus it)

---

## 6. Git Status Panel (Read‑only)

### 6.1 Purpose

Provide a fast, grouped view of working tree state and a launchpad to diffs.

### 6.2 Data shown

Groups (expandable/collapsible):

* **Conflicts** (unmerged paths)
* **Unstaged changes**
* **Staged changes** (read‑only visibility only; no staging operations yet)
* **Untracked**
* **Ignored** (optional hidden by default; show count and toggle)

Each item row:

* Status code badge (e.g., `M`, `A`, `D`, `R`, `C`, `U`, `??`)
* Path (relative to repo root)
* Optional secondary path for renames `old → new`
* Optional file size/time (lazy)

### 6.3 Keyboard interactions

* `↑/↓` move selection
* `Enter` open diff for selected item in Diff panel
* `Space` preview diff in-place (if Diff panel is not visible, open a popup)
* `/` filter/search within list (type to filter by path)
* `Tab` move focus to Diff panel / next panel
* `R` refresh status (or global `Ctrl+R`)

### 6.4 Mouse interactions

* Click selects
* Double‑click opens diff
* Scroll wheel scrolls list
* Click group header toggles collapse
* Right‑click (later): context menu with read‑only actions (Open diff, Open file, Copy path)

### 6.5 Filters and toggles

Status panel has a small toolbar row:

* `[ ] Show ignored`
* `[ ] Show submodules`
* Diff basis dropdown (read‑only):

  * “Working tree vs HEAD”
  * “Index vs HEAD” (if we show staged)
  * (Later) “Working tree vs index”

---

## 7. Diff Viewer Panel (Read‑only)

### 7.1 Modes

* **Unified diff** (default, always available)
* **Side‑by‑side** (enabled when width ≥ configurable threshold; e.g., 120 cols)
* **File list + diff** (optional: left list of changed files, right diff)

### 7.2 What diffs can be shown (read‑only)

* Working tree vs HEAD for a path
* Index vs HEAD for a path (staged diff display)
* Commit diff:

  * commit vs parent (selectable parent for merges)
  * compare two arbitrary refs (optional later UI)
* Stash diff (from stash panel)

### 7.3 Diff rendering model

We build a structured diff model for navigation and search:

* File header sections
* Hunks with line ranges
* Lines of types: context/add/remove/meta
* Optional intra-line highlighting later (not required for MVP)

### 7.4 Navigation & search

Keyboard:

* `PgUp/PgDn` scroll page
* `Home/End` jump to top/bottom
* `n` / `p` next/prev hunk
* `f` next/prev file section (if diff contains multiple files)
* `/` search within diff; `Enter` next match; `Shift+Enter` previous
* `w` toggle “ignore whitespace” (re-runs diff command)
* `u` toggle unified/side‑by‑side
* `g` “go to line” (within hunk/view) (optional)

Mouse:

* scroll wheel
* click scrollbar area (if present)
* click file header to collapse section (optional)

### 7.5 Header controls

Diff header shows:

* `<path>` and mode (WT vs HEAD, Index vs HEAD, Commit)
* Stats (adds/deletes) if available
* Toggle buttons: `[Unified] [Side-by-side] [Ignore WS] [Word wrap]`

### 7.6 Large diff handling

* Diff content is loaded **on demand**.
* For very large diffs:

  * show “Loading…” with cancellation
  * optionally cap diff size by default (configurable), with “Load more / Load full diff”
* Render virtualization:

  * maintain a line cache and only draw visible lines
  * parse output stream incrementally

---

## 8. Log Panel (Commit History Browser)

### 8.1 Purpose

Provide fast browsing of commit history with filtering and deep inspection.

### 8.2 Commit list row fields

* Short hash
* Subject (single line)
* Author (short)
* Date (relative or absolute; toggle)
* Decorations (branch/tag labels) (compact)

### 8.3 Filters

Top filter row:

* Text filter (subject/body)
* Author filter
* Ref selector (branch/tag/HEAD)
* Path filter (show commits touching selected file/path)
* Date range (optional later)

Implementation note: use pagination rather than loading entire history.

### 8.4 Pagination

* Load initial N commits (e.g., 200)
* `PageDown` near bottom triggers “Load more”
* Provide explicit command: `Load next page`, `Load previous page`

### 8.5 Interactions

Keyboard:

* `Enter` open Commit Details panel for selected commit
* `Space` quick preview details (popup)
* `d` open commit diff (details or directly diff panel)
* `/` search/filter within loaded commits
* `Ctrl+F` open filter bar (if hidden)
* `R` refresh (re-load from top)

Mouse:

* click selects
* double-click opens details
* scroll loads more near bottom

### 8.6 Merge commits

In details view/diff operations:

* If commit has multiple parents, allow selecting parent:

  * `Parent 1`, `Parent 2`, or “Combined” (later)
* Default: parent 1

---

## 9. Commit Details Panel (Read‑only)

### 9.1 Data shown

* Commit hash (full), parents, decorations
* Author + committer + dates
* Commit message (wrapped)
* Stats summary (files changed, insertions, deletions)
* Changed files list:

  * status (M/A/D/R)
  * path(s)
  * clicking a file opens diff for that file in Diff panel

### 9.2 Interactions

Keyboard:

* `Tab` cycles metadata → files → diff (if embedded)
* `Enter` on file opens file diff
* `c` copy commit hash to clipboard (optional)
* `o` open commit in external browser (if remote URL known) (optional later)
* `Esc` return focus to log

Mouse:

* click file row opens diff
* scroll message area

### 9.3 Optional: embedded diff

Details panel can optionally include a diff preview below files list, but for simplicity:

* MVP: details shows metadata + changed files; diff is shown in Diff panel.
* Later: a split view details+diff.

---

## 10. Refs Panel (Branches / Tags / Remotes) — Read‑only

### 10.1 Purpose

Browse refs and their metadata; jump log view to a ref.

Tabs:

* **Local branches**
* **Remote branches**
* **Tags**
* **Remotes** (names + URLs)

### 10.2 Row fields

Branches:

* name
* last commit short hash + subject
* last commit date
* upstream + ahead/behind (if available)

Tags:

* tag name
* target commit
* tag date (annotated tags)
* message (optional)

### 10.3 Interactions

Keyboard:

* `Enter` → open Log panel filtered to selected ref (e.g., `git log <ref>`)
* `/` filter by name
* `Tab` switch tabs
  Mouse:
* click row selects
* double-click opens log view for ref

---

## 11. Stash Panel — Read‑only

### 11.1 Data shown

List of stashes:

* `stash@{n}`
* date
* message
* branch (if shown by git)
* optional file count summary (lazy)

### 11.2 Interactions

* `Enter` open stash details:

  * show list of paths changed in stash
  * open stash patch in Diff panel (`git stash show -p stash@{n}`)
* `/` filter by message

---

## 12. File History Panel (Read‑only, path‑focused)

### 12.1 Purpose

Given a path (usually from file panel selection), show commits affecting it.

* Uses `--follow` for renames (configurable due to performance)
* Presents a commit list similar to Log panel
* `Enter` shows commit details/diff for that file

Integration:

* From File Panel: command “Git: File history” opens File History panel for selected file.

---

## 13. Blame Panel (Optional, Read‑only)

### 13.1 UI concept

A blame view is essentially a text viewer with a left gutter:

```
┌─────────────────────────────────────────────────────────────┐
│ <path>  (Blame @ HEAD)                                       │
├───────────────┬─────────────────────────────────────────────┤
│ a1b2c3d (Bob) │  10  public void Foo() {                     │
│ a1b2c3d (Bob) │  11      ...                                 │
│ e4f5a6b (Ana) │  12      return x;                            │
└───────────────┴─────────────────────────────────────────────┘
```

### 13.2 Interactions

* Arrow keys scroll
* `Enter` on a line opens commit details for that line’s commit
* `/` search within file
* Toggle:

  * `B` blame at selected ref (HEAD / branch) (read‑only)
  * `W` ignore whitespace (if supported by blame flags)

### 13.3 Performance concerns

Blame can be expensive; implement:

* explicit command to open blame (never auto-load)
* show loading indicator + cancellation
* optionally cap to visible window first (advanced; later)

---

## 14. Search Panel (Read‑only)

### 14.1 Search types

* **Content search**: `git grep`
* **Commit search**: search log messages/author (implemented as log filter)
* **Path search**: filter status/log lists

### 14.2 `git grep` UI

Fields:

* query text
* options:

  * `[ ] Case sensitive`
  * `[ ] Regex`
  * `[ ] Whole word`
  * Path scope (repo root, current folder, selected folder)
    Results list:
* `path:line: snippet`
  Interactions:
* `Enter` opens file viewer at line (internal viewer or an external viewer later)
* `Space` previews snippet context (optional)

---

## 15. “Insights” Panel (Read‑only Analysis)

This is the “analyze” portion: high‑value summaries that are cheap enough to compute.

### 15.1 Suggested insights (MVP‑friendly)

* Repo summary:

  * current branch, upstream, ahead/behind
  * dirty counts (by category)
  * stash count
  * last commit (hash, author, time)
* Activity:

  * commits in last 7/30 days (count)
  * top authors by commit count (last 30 days) (optional)
* Hot paths (optional later):

  * files changed most in last N days (expensive; later)

### 15.2 UX

* Small cards or grouped lines; click a card to jump:

  * click “last commit” → details
  * click “dirty files” → status
  * click “ahead/behind” → show upstream comparison summary

---

## 16. Command Surface (Read‑only)

All actions are commands (for bindings, palette, chords). Below are suggested IDs and defaults.

### 16.1 Global Git commands

* `git.panel.status` — `g s`
* `git.panel.diff` — `g d`
* `git.panel.log` — `g l`
* `git.panel.refs` — `g r`
* `git.panel.stash` — `g t`
* `git.panel.search` — `g /`
* `git.panel.insights` — `g i`
* `git.repo.pick` — `g g`
* `git.refresh` — `g R` (or `Ctrl+R` when in git panels)

### 16.2 Contextual commands

From File Panel:

* `git.file.history` — `g h`
* `git.file.diff` — `g d` (diff selected file vs HEAD)
* `git.file.blame` — `g b`

Within Status Panel:

* `git.status.openDiff` — `Enter`
* `git.status.toggleIgnored` — `I`
* `git.status.filter` — `/`

Within Diff Panel:

* `git.diff.nextHunk` — `n`
* `git.diff.prevHunk` — `p`
* `git.diff.search` — `/`
* `git.diff.toggleWhitespace` — `w`
* `git.diff.toggleMode` — `u`

Within Log Panel:

* `git.log.openDetails` — `Enter`
* `git.log.filterBar` — `Ctrl+F`
* `git.log.loadMore` — `PageDown` at end

> All keys are defaults; users can remap and create chords.

---

## 17. Data Models (Read‑only)

### 17.1 Repo context

```csharp
public sealed record RepoContext(
    string RepoRoot,
    string GitDir,
    bool IsBare,
    HeadInfo Head,
    UpstreamInfo? Upstream,
    DirtySummary DirtySummary,
    DateTimeOffset LastRefreshedAt
);

public sealed record HeadInfo(
    string HeadRefName,       // "main" or "(detached)"
    string HeadCommitHash,
    bool IsDetached
);

public sealed record UpstreamInfo(
    string UpstreamRefName,
    int Ahead,
    int Behind
);

public sealed record DirtySummary(
    int Conflicts,
    int Unstaged,
    int Staged,
    int Untracked,
    int Ignored
);
```

### 17.2 Status entries

```csharp
public enum StatusGroup { Conflicts, Unstaged, Staged, Untracked, Ignored }

public sealed record StatusEntry(
    StatusGroup Group,
    string Path,
    string? SecondaryPath,    // rename/copy source
    string Code,              // e.g. "M", "R100", "UU", "??"
    bool IsSubmodule
);
```

### 17.3 Log entries

```csharp
public sealed record CommitEntry(
    string Hash,
    IReadOnlyList<string> Parents,
    string AuthorName,
    DateTimeOffset AuthorDate,
    string Subject,
    IReadOnlyList<string> Decorations // branches/tags labels
);
```

### 17.4 Diff model

```csharp
public sealed record DiffDocument(
    IReadOnlyList<DiffFile> Files
);

public sealed record DiffFile(
    string OldPath,
    string NewPath,
    IReadOnlyList<DiffHunk> Hunks,
    DiffStats? Stats
);

public sealed record DiffHunk(
    int OldStart, int OldCount,
    int NewStart, int NewCount,
    IReadOnlyList<DiffLine> Lines
);

public enum DiffLineKind { Context, Add, Remove, Meta }

public sealed record DiffLine(DiffLineKind Kind, string Text);
```

### 17.5 Refs and stash

```csharp
public sealed record RefEntry(
    string Name,
    string FullName,          // refs/heads/...
    string TargetHash,
    DateTimeOffset? Date,
    string? Upstream,
    int? Ahead,
    int? Behind
);

public sealed record StashEntry(
    string Name,              // stash@{0}
    DateTimeOffset Date,
    string Message
);
```

---

## 18. Backend Architecture (Read‑only Git Queries)

### 18.1 Services

* `IGitRepoLocator`

  * find repo root + git dir for a path
* `IGitQueryService` (read‑only façade)

  * `GetRepoContextAsync(...)`
  * `GetStatusAsync(...)`
  * `GetDiffAsync(DiffRequest ...)`
  * `GetLogPageAsync(LogRequest ...)`
  * `GetCommitDetailsAsync(hash)`
  * `GetRefsAsync(...)`
  * `GetStashesAsync(...)`
  * `GrepAsync(...)`
  * `BlameAsync(...)` (optional)
* `IGitProcessRunner`

  * runs git commands, streams stdout/stderr, supports cancellation

### 18.2 Process execution guidelines

For reliability and speed:

* Prefer structured output and safe parsing:

  * `-z` (NUL separators) where available (status, name lists)
  * custom record separators for log formats
* Run git in `RepoRoot` as working directory
* Use `--no-optional-locks` where supported to reduce lock contention for read-only queries
* Capture stderr for diagnostics; show friendly UI error messages

### 18.3 Caching and invalidation

We cache per repo:

* Status results
* Repo context (HEAD, upstream, dirty summary)
* Recently viewed diffs (by request key)
* Log pages (by filter key + page offset)

Invalidate cache when any of these changes:

* `.git/HEAD` changes
* `.git/index` changes
* refs change (`.git/refs/*` or packed-refs changes)
* working tree changes (optional watcher or periodic refresh)

Strategy:

* **Fast refresh triggers** from file watchers (debounced)
* **Manual refresh** always available
* **Throttling**: do not refresh status more than once per X ms (e.g., 300–800ms) during heavy churn

### 18.4 Watchers

Watch the git directory:

* Use `git rev-parse --git-dir` and watch that location
* Worktrees: `.git` file points to actual dir; watch actual git dir
* Debounce events into a single “repo changed” signal

### 18.5 Incremental loading

* Status: fast enough to load fully, but do it asynchronously
* Diff: stream output and build a lazy line buffer
* Log: page-based queries; load more on demand

---

## 19. Git CLI Commands and Parsing Strategy (Recommended)

> These are recommendations for stable parsing; exact flags can be tuned during implementation, but keep fixtures/tests aligned.

### 19.1 Repo root and git dir

* `git rev-parse --show-toplevel`
* `git rev-parse --git-dir`
* `git rev-parse --is-bare-repository`
* `git rev-parse --short HEAD` (for display) and full hash for keys

### 19.2 Status (structured)

Use porcelain v2 for machine parsing:

* `git status --porcelain=v2 --branch -z`

Parse:

* branch info lines (`# branch.head`, `# branch.upstream`, `# branch.ab +A -B`)
* entry records for file states, untracked, ignored

### 19.3 Refs (branches/tags/remotes)

Prefer `for-each-ref` formatting:

* `git for-each-ref refs/heads refs/remotes --format=<fields>`
* `git for-each-ref refs/tags --format=<fields>`

Fields to include:

* `%(refname:short)`, `%(refname)`, `%(objectname)`, `%(committerdate:iso-strict)`, `%(upstream:short)` etc.

### 19.4 Log (page-based, parse-safe)

Use a format with separators unlikely to appear:

* Record separator `\x1e`, field separator `\x1f`
* `git log <ref> --date=iso-strict --pretty=format:%H%x1f%P%x1f%an%x1f%ad%x1f%s%x1f%D%x1e --max-count=N --skip=K [-- <path>]`

Parse each record split by `\x1e`, fields by `\x1f`.

### 19.5 Diff (no color)

* Working tree vs HEAD:

  * `git diff --no-color --patch -- <path>`
* Index vs HEAD (staged diff display):

  * `git diff --cached --no-color --patch -- <path>`
* Commit show:

  * `git show --no-color --patch <hash> -- <path?>`
* Whitespace ignore toggles:

  * add `-w` or `--ignore-space-change` depending on user preference

### 19.6 Stash (list and show)

* `git stash list --date=iso-strict`
* `git stash show -p --no-color stash@{n}`

### 19.7 Grep

* `git grep -n --full-name <pattern> [-- <path>]`
* For regex: add `-E` or `-P` based on config (prefer `-E` portability)

### 19.8 Blame

* `git blame --line-porcelain <ref> -- <path>`
  Parse commit header blocks + line content.

---

## 20. Integration with File Panels (Read‑only)

### 20.1 Git decorations in file list

When a file panel directory is inside a repo:

* show a lightweight git status badge per visible row:

  * e.g., `M`, `A`, `D`, `?`, `U`
* badges come from cached status; do not run `git status` per file

### 20.2 Context actions from file panel

* Open diff for selected file (`git.file.diff`)
* Open file history (`git.file.history`)
* Open blame (`git.file.blame`)

These commands open the relevant Git panels pre‑scoped to that file path.

---

## 21. Error Handling and Edge Cases

### 21.1 Git not installed or not in PATH

* Git panels show a clear message:

  * “Git executable not found. Configure path in settings.”
* Provide “Open settings” or show help text.

### 21.2 Not a repository

* Git panels show “No repository detected for current directory.”
* Offer buttons:

  * “Pick repo…”
  * “Show recent repos…”

### 21.3 Very large repos

* Status: still usually OK; provide:

  * “Status took Xs. Consider disabling ignored files display.”
* Diff: cap by size; load on demand
* Log: page-based; avoid `--all` by default

### 21.4 Locked index / concurrent git operations

Read-only queries may fail if repo is busy; handle gracefully:

* Show “Repository busy (index locked). Retry?”
* Auto retry a small number of times with backoff (configurable), then stop

### 21.5 Submodules

Status may include submodule summaries; optionally:

* show submodule entries with a distinct marker
* clicking opens a submodule repo context (read‑only)

---

## 22. Testing Strategy (Read‑only Git)

### 22.1 Unit tests

* Parsing fixtures for:

  * porcelain v2 status
  * log record format parsing
  * for-each-ref parsing
  * diff parsing (basic hunk structure)
  * stash list parsing
  * blame parsing (if implemented)

### 22.2 Integration tests (temp repos)

Create ephemeral repos with scripted setups:

* untracked, modified, deleted, renamed
* merge commits, multiple parents
* tags (annotated and lightweight)
* stashes
* submodules (optional)

Assert:

* UI model outputs correct grouping and counts
* diff/log queries return expected data

### 22.3 Performance tests (optional but valuable)

* log pagination performance
* diff streaming + render latency
* status refresh debounce under file churn

---

## 23. MVP Checklist (Read‑only Git)

Minimum “ship-worthy” read‑only Git experience:

* Repo discovery (from active panel path)
* Repo header summary (branch + dirty counts)
* Status panel with groups + open diff
* Diff panel (unified) with hunk navigation and search
* Log panel with pagination + open commit details
* Commit details panel (metadata + changed files)
* Refs panel (branches + tags) that can open log view
* Stash list + stash diff view
* `git grep` search panel (optional for MVP, but high value)

---

## 24. Post‑MVP Enhancements (Still Read‑only)

* Side‑by‑side diff rendering with better alignment
* Diff of two arbitrary refs (compare picker)
* Graph view (ASCII graph or custom render)
* Rich blame (per-line heat map / age coloring)
* Insights: churn by path, commit frequency charts (text-based)
* “Open in external” integrations:

  * open remote URL for commit/branch if origin is recognized (GitHub/GitLab/etc.)


