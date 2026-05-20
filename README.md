# MvvmMapper Prompt Package — How to Use

Three files, three jobs:

| File | Where it goes | Purpose |
|---|---|---|
| `01-master-prompt.md` | Paste as first chat message | The master instruction — kicks off the build |
| `CLAUDE.md` | Place at **repo root** | Always-on rules Claude Code reads every session |
| `SKILL.md` | Place at `.claude/skills/mvvm-scanning/SKILL.md` | Loaded when scanning logic is being written |

## Step-by-step

### 1. Create an empty repo

```bash
mkdir MvvmMapper && cd MvvmMapper
git init
```

### 2. Drop in the two persistent files

```bash
# Copy the always-on rules to repo root
cp /path/to/CLAUDE.md ./CLAUDE.md

# Copy the skill into the Claude skills folder
mkdir -p .claude/skills/mvvm-scanning
cp /path/to/SKILL.md ./.claude/skills/mvvm-scanning/SKILL.md

git add . && git commit -m "chore: add Claude Code guidance files"
```

### 3. Start Claude Code in that folder

```bash
claude
```

### 4. Paste `01-master-prompt.md` as your first message

That's the whole kickoff. The prompt tells Claude to read `CLAUDE.md` and the skill first, then begin Phase 1.

## How the three files cooperate

- **Master prompt** sets the *goal and phases*. Used once, at the start.
- **CLAUDE.md** sets the *invariants* (architecture rules, conventions, what not to do). Re-read by Claude Code every session — anyone joining the repo gets the same rules automatically.
- **SKILL.md** is the *domain knowledge* (how WPF MVVM apps actually look in the wild). Loaded on-demand when Claude is writing resolver logic.

If you split the build across multiple sessions or hand off to a teammate, only the master prompt needs to be re-pasted (and only to resume from a specific phase). The other two files persist with the repo.

## Working the phases

The master prompt defines 7 phases. After each phase, Claude should:
1. Summarize what changed
2. Run `dotnet build` and `dotnet test`
3. Wait for your confirmation before continuing

If Claude tries to barrel through multiple phases without pausing, tell it: *"Stop after Phase N. Run tests. Wait."*

## Customizing for your stack

The master prompt assumes generic WPF MVVM. If your real codebase uses something specific (Prism, Caliburn.Micro, ReactiveUI, Stylet), add a section to `CLAUDE.md` before starting:

```markdown
## Project-specific patterns

Our scanned projects use Prism. Key implications:
- Region navigation: `IRegionManager.RequestNavigate("MainRegion", "LoginView")`
- ViewModelLocationProvider auto-wires Views to VMs by convention
- Add a `PrismRegionResolver` for navigation tracking
```

Claude will pick this up and adjust the resolver list accordingly.

## What to expect on timing

Rough estimate with Claude Code on a clean repo:

| Phase | Time |
|---|---|
| 1 — Skeleton & Discovery | 30–60 min |
| 2 — Parsing | 1–2 hr |
| 3 — View↔VM resolvers (5 of them) | 2–3 hr |
| 4 — Commands | 1 hr |
| 5 — Endpoint resolvers (3 of them) | 2 hr |
| 6 — Rendering | 2–3 hr |
| 7 — Analysis & polish | 1–2 hr |

Total: roughly a long day of pair programming. You'll spend most of it reviewing PRs and saying "good, continue" or "no, the Locator resolver missed this edge case."

## Sanity checks at the end

The master prompt's deliverables checklist is your acceptance test. Run it literally — don't accept "looks done" without:

- `dotnet build` zero warnings
- `dotnet test` all green, ≥80% coverage on Core
- `mvvm-map scan samples/shared-vm` produces the expected output (1 shared VM, 3 Views, fan-in flag)
- HTML report opens with no internet connection, search works, Mermaid renders

If any of those fail, the build isn't done.
