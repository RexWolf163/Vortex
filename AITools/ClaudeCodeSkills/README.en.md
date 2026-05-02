---
title: ClaudeCodeSkills
platform: Claude Code (CLI, Anthropic)
language: en
---

# ClaudeCodeSkills — skills for designing and assessing Vortex packages

A set of skills for [Claude Code](https://docs.claude.com/claude-code), Anthropic's CLI agent. The skills automate routine work around Vortex: designing new packages by canon, picking the right architectural layer for a new system, and running a strict canon-based code quality assessment.

This folder holds the **source** of the skills. To make Claude Code use them, copy each skill into one of the standard skill directories — see «Installation».

## Contents

| Skill | Purpose | Triggers |
|-------|---------|----------|
| [vortex-layer-detect](vortex-layer-detect/SKILL.md) | Pick the architectural layer (1/2/3) for a new Vortex system before any code is written | "let's add system X", "new subsystem Y", "I want a package for Z" in a Vortex context |
| [vortex-initial-architecture](vortex-initial-architecture/SKILL.md) | Design the initial architecture of a Vortex package via the canonical algorithm (Stage 0 + 7 steps) | "design the architecture", "lay out the package structure", "sketch system X" |
| [code-quality](code-quality/SKILL.md) | Strict Unity code quality assessment by formula with an architectural multiplier. 4 branches by size (≤3000 LOC / 3000–12000 / partial / subjective) | "assess code quality", "run the criteria", "check the package quality" |

## Workflow

```
New Vortex system
  └─► vortex-layer-detect           (decide layer)
        └─► vortex-initial-architecture  (design package)
              └─► (write code)
                    └─► code-quality   (assess finished package)
```

- **vortex-layer-detect** — first step when adding any new system. MUST run before code generation.
- **vortex-initial-architecture** — after the layer is fixed. Produces the structure (bus, config, controller, model, presets, interface, views).
- **code-quality** — after implementation (or for auditing existing packages). Returns a 0–10 score with an architectural-defect breakdown.

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

- [`F:\Claude\Vortex\AITools\Prompt code quality analysis.md`](../Prompt%20code%20quality%20analysis.md) — original LLM prompt for code quality analysis (earlier manual version). The `code-quality` skill extends it with branch A+ and a formal protocol.
- [`F:\Claude\Критерии_оценки_кода_Unity.md`](../../../Критерии_оценки_кода_Unity.md) — canonical formulas for code quality. The `code-quality` skill inlines them.
- [`F:\Claude\vortex-context.md`](../../../vortex-context.md) and [`F:\Claude\architecture_context.md`](../../../architecture_context.md) — architectural foundations behind `vortex-layer-detect` and `vortex-initial-architecture`.

## Versioning

Skills are versioned with the Vortex package (via git history of files in this folder). The `description` frontmatter field is a trigger signal for Claude Code — change it carefully, as it affects how the model recognizes the skill.
