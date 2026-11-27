# ClaudeCodeSkills — skills for designing and assessing Vortex packages

A set of skills for [Claude Code](https://docs.claude.com/claude-code), Anthropic's CLI agent. The skills automate routine work around Vortex: picking the right architectural layer for a new system, designing new packages by canon, authoring a TZ, building code layer by layer from a TZ, and assessing code along two axes — **form** (metrics/patterns) and **behavior** (soundness of logical threads).

This folder holds the **source** of the skills. To make Claude Code use them, copy each skill into one of the standard skill directories — see «Installation».

## Contents

| Skill | Purpose | Triggers |
|-------|---------|----------|
| [vortex-layer-detect](vortex-layer-detect/SKILL.md) | Pick the architectural layer (1/2/3) for a new Vortex system before any code is written | "let's add system X", "new subsystem Y", "I want a package for Z" in a Vortex context |
| [vortex-initial-architecture](vortex-initial-architecture/SKILL.md) | Design the initial architecture of a Vortex package via the canonical algorithm (Stage 0 + 7 steps) | "design the architecture", "lay out the package structure", "sketch system X" |
| [code-quality](code-quality/SKILL.md) | Strict assessment of Unity code **form** by formula with an architectural multiplier. 4 branches by size (≤3000 LOC / 3000–12000 / partial / subjective) | "assess code quality", "run the criteria", "check the package quality" |
| [applied-quality](applied-quality/SKILL.md) | Assessment of **applied** quality (behavior) via the "Scenario Soundness" canon: tracing logical threads entry→exit, entanglement, staging, recursion (catastrophe cap). 3 branches by size (mono ≤2000 / sharding with graph reconstruction 2000–10000 / partial) | "assess correctness of the package", "check the system for logic/adequacy/rightness", "where does it break in practice" |
| [tz-design](tz-design/SKILL.md) | Authoring a technical specification (TZ) for a new feature/subsystem, or a self-audit of an existing TZ. Two-tier document structure, calibrated to target maturity. For systems with external input or a guarding role — a threat model and a hardening boundary (TZ section 6a) | "write it up as a TZ", "make a TZ", "draft a technical spec", "review this TZ", "assess the TZ for completeness" |
| [tz-build](tz-build/SKILL.md) | Layer-by-layer build of a package's code from a closed TZ: 9 iterations from primitives to core logic; every confirmation point is a stop, deviations from the canon only with the user's consent. After each iteration — compile and assess with one agent (form or applied-quality). Keeps a build log | "build the code from the TZ", "build the package from the TZ", "implement the TZ", "layer-by-layer build" |

## Workflow

```
New Vortex system
  └─► vortex-layer-detect           (decide layer)
        └─► vortex-initial-architecture  (design package)
              └─► tz-design             (author the TZ, close open questions)
                    └─► tz-build              (build code from the TZ in iterations; per-iteration assessments inside)
                          ├─► code-quality      (assess FORM: metrics, patterns, coupling — code "at rest")
                          └─► applied-quality   (assess BEHAVIOR: threads entry→exit, entanglement, recursion — code "in motion")
```

- **vortex-layer-detect** — first step when adding any new system. MUST run before code generation.
- **vortex-initial-architecture** — after the layer is fixed. Produces the structure (bus, config, controller, model, presets, interface, views).
- **code-quality** — after implementation (or for auditing). Assesses the **form** of the code: metrics, structure, patterns, coupling. A 0–10 score with an architectural-defect breakdown.
- **applied-quality** — after implementation (or for auditing). Assesses the **behavior** of the code: soundness of logical threads entry→exit, staging, thread entanglement, and recursion (catastrophe cap). Evidence is **failure scenarios** (what breaks on re-entry/error/race), not abstract counts.
- **tz-design** — authoring a TZ for a feature/subsystem or auditing an existing one. Can also be used outside the code flow; to build code, the TZ must be closed (no open questions). For systems that take external input or act as a guard (allow-list, authorization, filter) the TZ fixes a threat model: input sources, cost of an error, safe failure direction, supported input classes and "out of model", platform assumptions, and a **hardening boundary** — the "good enough" criterion for assessments after implementation.
- **tz-build** — building code from a closed TZ. Does not start if the TZ has open questions (→ tz-design). If the layer is undefined: a Vortex project → vortex-layer-detect, otherwise the skill proposes a layer split for approval. Runs in 9 iterations: abstractions (classes/structs, then interfaces) → models → presets → model-changing logic → editor sugar → buses and thin controllers → core logic → polishing. Before each iteration — a search for existing samples in READMEs/XML docs (by the iteration's types and by the TZ's behavioral requirements for them), a list of deviations from the canon and a type table for confirmation; hybrid types (data + logic) are placed in the latest of their iterations once their hybrid status is confirmed. After it — compile and assess (form for iterations without dynamics, applied-quality for those with dynamics; both skills over the whole package in polishing); findings are grouped by root decision, and recurring ones mean revising the decision rather than patching. If the TZ has a threat model (section 6a), input findings beyond its boundary are downgraded to Minor and do not block; a finding that shows the boundary is wrong leads to revising the model in the TZ. Strict mode: the user's answer is never predicted, silence and "ok" are not consent. Answers to canon gaps accumulate in the principles memo (`<build folder>\BuildDocs\architecture-principles.md`); state, approved deviations and assessment results live in the build log (`<build folder>\BuildDocs\<Package>_build.md`), which lets a build resume in a new session. The build folder is asked once per project and kept in the project memory.

### code-quality ↔ applied-quality — twins, different axes

They measure **different** things and do not overlap:

| | code-quality | applied-quality |
|---|---|---|
| Axis | **form** (code at rest) | **behavior** (code in motion) |
| Method | deterministic (metrics/formulas) | semantic (tracing threads entry→exit) |
| Catches | Fat Interface, DIT, allocations, dupes | dangling refs, races, re-entry leaks, over-fetch, staging, recursion |

The twins' triggers are **similar**, so both skills confirm direction at the start (section 0 of each): the word "**quality / architecture / metrics**" → code-quality; "**correctness / logic / adequacy / rightness / entanglement / what breaks**" → applied-quality. On an ambiguous phrasing the skill asks one clarifying question before analyzing. A full picture of a package = run both.

## Installation

Claude Code loads skills from two locations:

| Scope | Path | When to use |
|-------|------|-------------|
| User | `~/.claude/skills/<skill-name>/SKILL.md` | All projects for current user |
| Project | `<project-root>/.claude/skills/<skill-name>/SKILL.md` | Only this project (committed to repo) |

**Manual install:**

```bash
# User-level (all projects)
cp -r Vortex/AITools/ClaudeCodeSkills/vortex-layer-detect ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/vortex-initial-architecture ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/code-quality ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/applied-quality ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/tz-design ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/tz-build ~/.claude/skills/

# Project-level (current project only)
mkdir -p .claude/skills
cp -r Vortex/AITools/ClaudeCodeSkills/* .claude/skills/
```

After copying, Claude Code picks up the skills on next launch. List active skills via `/skills` in the CLI.

## Platform

The skills are written for **Claude Code** and use its tools (`Agent`, `Bash`, `Grep`, `Read`, `Edit`, `Write`, `Glob`). Delegation conventions use `subagent_type=Explore`/`general-purpose` with a model hint (`haiku`/`sonnet`/`opus`).

For other LLM agent platforms (Cursor, Continue, OpenAI Assistants, Cline, Aider) you'll need to adapt:
- Replace tool calls with platform equivalents.
- Replace delegation (subagent with model X) with the available mechanism.
- The content (formulas, checklists, Vortex canon) ports unchanged.

## Skill lifecycle

`Vortex/AITools/ClaudeCodeSkills/` is the **source of truth**. To change a skill:

1. Edit the file here.
2. Re-sync into `~/.claude/skills/<name>/` (or `.claude/skills/<name>/`).

Symlinks via `mklink` (Windows) or `ln -s` (Unix) work too, but complicate IDE editing and git history — a plain copy is recommended.

## Related project files

- `Prompt code quality analysis.md` — original LLM prompt for code quality analysis (earlier manual version). The `code-quality` skill extends it with branch A+ and a formal protocol.
- `Критерии_оценки_кода_Unity.md` — canonical formulas for code quality. Not part of the repository; the `code-quality` skill inlines them.
- `vortex-context.md` and `architecture_context.md` — architectural foundations behind `vortex-layer-detect` and `vortex-initial-architecture`. Not part of the repository (the author's local documents); their canon is carried into the skill texts.

## Versioning

Skills are versioned with the Vortex package (via git history of files in this folder). The `description` frontmatter field is a trigger signal for Claude Code — change it carefully, as it affects how the model recognizes the skill.
