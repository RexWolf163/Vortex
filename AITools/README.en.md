# AITools

**Namespace:** `Vortex.Unity.Extensions.Editor.AITools`
**Assembly:** `ru.vortex.aitools`

## Purpose

Toolkit for interacting with external code analysis systems (LLM). Aggregates source code and documentation from a specified directory into text artifacts suitable for passing to a language model. Includes ready-made prompts for common tasks.

Capabilities:
- Aggregation of all `.cs` and `.md` files from the selected folder into a single text file
- Recursive subdirectory traversal preserving relative paths
- Separate artifacts for code and documentation
- Results placed outside the project tree (excluded from VCS and build)
- Ready-made prompts for code quality evaluation and documentation generation

Out of scope:
- Interaction with language model APIs
- Code parsing or analysis
- Source file modification

## Dependencies

- **UniTask** — async processing (`UniTask.Yield()` every 100 files)
- **UnityEditor** — `MenuItem`, `Selection`, `AssetDatabase`

## Architecture

```
AITools/
├── AIContextCreator.cs                  # Editor-only aggregation utility
├── Prompt code quality analysis.md      # Prompt: code quality evaluation
└── Prompt create docs.md               # Prompt: documentation generation
```

### AIContextCreator

Static class behind `#if UNITY_EDITOR`. Invoked via context menu:
**Assets → Vortex → Debug → Create Context for AI**

On invocation:
1. Scans the selected folder recursively
2. Collects `.cs` files into `{FolderName}_{Timestamp}.txt`
3. Collects `.md` files into `{FolderName}_MD_{Timestamp}.txt`
4. Saves two levels above `Application.dataPath`

Format of each file in the artifact:
```
// ============================================================
// FILE: relative/path/from/Assets/MyFile.cs
// ============================================================
<file contents>
```

## Ready-Made Prompts

The package includes two prompt files for use with language models.

### Prompt code quality analysis.md

Methodology for quantitative code quality evaluation on a 10-point scale across four categories:

| Category | What it evaluates |
|----------|------------------|
| **Documentability** | Documentation-to-systems ratio, comments, contracts |
| **Scalability** | Neutral classes, atomicity, pattern purity, inheritance depth, cyclomatic complexity |
| **Maintainability** | Class size, constants, duplication, magic numbers |
| **Performance** | Hot path allocations, `GetComponent` caching, string operations in loops |

Contains formulas for each metric, evaluation scales, architectural problem markers, and the final quality formula.

**Applicability criteria:**

| Code volume | Applicability | Reason |
|------------|--------------|--------|
| 1 system / package (up to ~3000 LOC) | Full | Every file is read in its entirety |
| 2–5 systems (up to ~10,000 LOC) | Partial | Quantitative metrics work, problem markers are missed |
| Entire framework (10,000+ LOC) | Not applicable | LLM switches to cataloging instead of line-by-line analysis |

The prompt requires deep reading: checking every interface for ISP, every `as ConcreteType` for false extensibility, every `async void`, counting fields across the entire inheritance hierarchy. With large context, the LLM replaces this analysis with folder and namespace overviews — formally completing the task while missing all real problems.

**Rule:** if the code doesn't fit in context such that every file can be read in full — split into packages and analyze one at a time.

### Prompt create docs.md

Rules for README documentation formatting for Vortex Framework packages:
- Structure of 8 sections (purpose → dependencies → architecture → contract → usage → edge cases)
- Style: academic, factual, no marketing
- Rules for multi-component systems and debug sections
- Core/Unity documentation separation

## Contract

### Input
- One selected folder in the Project Window

### Output
- Two text files outside the project (`.cs` and `.md` artifacts)

### Constraints
- Editor mode only
- Exactly one folder in selection
- Files must be inside `Assets/`

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| 0 or >1 objects selected | Menu item unavailable (validate) |
| File selected instead of folder | Menu item unavailable |
| Folder contains no .cs/.md | `LogWarning`, artifact not created |
| File read error | `LogWarning` for file, remaining files processed |
| >100 files | Async yield every 100 files |
