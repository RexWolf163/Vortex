# ZCodeSkills — Vortex skills for ZCode

An adaptation of the skills from [ClaudeCodeSkills](../ClaudeCodeSkills/) for [ZCode](https://zcode.z.ai), the AI-agent development environment by Z.AI. The substance (canons, formulas, protocols, checklists) is the same; only the packaging is changed for skill loading in ZCode.

## Purpose

Three skills for code evaluation and TZ (technical specification) design in Vortex projects:

| Skill | Purpose | Triggers |
|-------|---------|----------|
| [code-quality](code-quality/SKILL.md) | Evaluation of the **form** of Unity code using canonical formulas with an architectural multiplier. Branches by size: detailed (≤3000 LOC), by subgroups (3000–12000), partial, subjective review (>12000) | "evaluate code quality", "run the evaluation criteria", "evaluate the package architecture" |
| [applied-quality](applied-quality/SKILL.md) | Evaluation of code **behavior** using the "Scenario robustness" canon: input→output threads, re-entry, tangling, recursion. Branches: mono (≤2000 LOC), sharding with graph reconstruction (2000–10000), partial | "evaluate package correctness", "check for logic / adequacy", "what breaks at runtime" |
| [tz-design](tz-design/SKILL.md) | Writing a TZ for a feature or subsystem and self-audit of a finished TZ | "make a TZ", "format as a TZ", "analyze the TZ", "check the TZ for completeness" |

Out of scope: `vortex-layer-detect` and `vortex-initial-architecture` are not adapted for ZCode — they exist only in ClaudeCodeSkills.

## Dependencies

- **ZCode** with skill support (a folder containing `SKILL.md`).
- **Source** — [ClaudeCodeSkills](../ClaudeCodeSkills/): the canon is shared between the sets; this set is its adaptation (see "Lifecycle").
- For code evaluation: `Критерии_оценки_кода_Unity.md` is embedded in `code-quality`; no separate setup is needed.

## Usage

### Installation

ZCode loads skills from folders containing `SKILL.md`:

| Level | Path | When to use |
|-------|------|-------------|
| User | `~/.zcode/skills/<skill-name>/` | For all of the user's projects |
| Project | `<project-root>/.zcode/skills/<skill-name>/` | This project only (committed to the repository) |

Copy the **whole skill folder**, including `references/`:

```bash
# User-level
cp -r Vortex/AITools/ZCodeSkills/code-quality ~/.zcode/skills/
cp -r Vortex/AITools/ZCodeSkills/applied-quality ~/.zcode/skills/
cp -r Vortex/AITools/ZCodeSkills/tz-design ~/.zcode/skills/

# Project-level
mkdir -p .zcode/skills
cp -r Vortex/AITools/ZCodeSkills/*/ .zcode/skills/
```

If your ZCode version looks for skills in a different folder, the path is shown in ZCode's skill settings.

### Which skill when

```
Feature concept
  └─► tz-design            (TZ and its self-audit)
        └─► (write code)
              ├─► code-quality      (FORM: metrics, patterns, coupling — code "at rest")
              └─► applied-quality   (BEHAVIOR: threads, re-entry, tangling — code "in motion")
```

`code-quality` and `applied-quality` measure different things and do not duplicate each other; the full picture of a package takes both runs. Their triggers are similar, so each one confirms the direction at start: "quality / architecture / metrics" → `code-quality`, "correctness / logic / adequacy / what breaks" → `applied-quality`. On an ambiguous request the skill asks one clarifying question.

## Structure

```
ZCodeSkills/
├── code-quality/
│   ├── SKILL.md                         # Trigger, LOC measurement, branch selection, final formula
│   └── references/
│       ├── detailed-canon.md            # Branch A: reading order, category 1–4 formulas, markers, greps
│       └── sharding-and-partial.md      # Branches A+ (subgroups), B (partial), C (review)
├── applied-quality/
│   ├── SKILL.md                         # Trigger, LOC measurement, thread model, formula, output format
│   └── references/
│       ├── axes.md                      # Axes 1/2/3/5, stepping, tangling, catastrophe tier
│       └── sharding.md                  # Branches A+ (sharding with graph reconstruction) and B
└── tz-design/
    ├── SKILL.md                         # Phases, document structure, maturity calibration
    └── references/
        ├── phase2-decomposition.md      # Data decomposition: atomicity, registries, invariants
        └── self-audit.md                # Audit protocol for a finished TZ
```

- **Main file and references.** `SKILL.md` holds the protocol and branching; detailed canons are moved to `references/`. The skill reads the required reference **in full** at the specified step — the skill text forbids working from memory.
- **Frontmatter.** Besides `name` and `description`, there is a `when_to_use` field with triggers. `description` states what the skill does; `when_to_use` lists the launch phrases and the boundary with its twin.
- **Delegation.** Sub-agent instructions (`Agent`, `subagent_type`, haiku/sonnet models) are kept from the source skills. If ZCode has no equivalent with model selection, the main agent performs those steps — the substance of the analysis does not change.

## Differences from ClaudeCodeSkills

| | ClaudeCodeSkills | ZCodeSkills |
|---|---|---|
| Platform | Claude Code | ZCode |
| Contents | 5 skills | 3 skills (no `vortex-layer-detect`, `vortex-initial-architecture`) |
| Packaging | one `SKILL.md` per skill | `SKILL.md` + `references/` |
| Triggers | in `description` | separate `when_to_use` field |
| Installation | `~/.claude/skills/`, `.claude/skills/` | `~/.zcode/skills/`, `.zcode/skills/` |
| Canon | original | the same |

## Lifecycle

The `Vortex/AITools/ZCodeSkills/` folder is the source of truth for the ZCode version. When changing a skill:

1. Apply the canon change to **both** sets — ClaudeCodeSkills and ZCodeSkills (for ZCode, in `SKILL.md` or the corresponding `references/` file).
2. Re-sync the installed copy in `~/.zcode/skills/<name>/` or `.zcode/skills/<name>/`.

Skills are versioned together with Vortex through the folder's git history.

## Edge cases

- **Only `SKILL.md` was copied.** Without `references/` the skill cannot find the canon at the required step. Copy the whole folder.
- **The sets diverge.** A change made only in ClaudeCodeSkills does not reach the ZCode version, and evaluations on the two platforms start to differ.
- **Editing `description` / `when_to_use`.** These fields determine when ZCode launches the skill; changing the wording changes recognition.
- **`vortex-layer-detect` / `vortex-initial-architecture` are needed.** They are not adapted for ZCode — use the ClaudeCodeSkills versions in Claude Code, or adapt them following this set.
