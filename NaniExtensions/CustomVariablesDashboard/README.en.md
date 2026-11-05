# CustomVariablesDashboard

**Assemblies:** `ru.vortex.nani.variables.dashboard` (runtime, contract), `ru.vortex.nani.variables.dashboard.editor` (editor, window)
**Namespace:** `Vortex.NaniExtensions.CustomVariablesDashboard`

## Activation

Both assemblies — `defineConstraints: ["USING_NANINOVELL"]` (the `naninovellExt` toggle in `SdkSettings`). The Vortex quest source is included only with `USING_VORTEX_QUESTS`; without it the panel works on `.nani` scripts only.

## Purpose

Naninovel variables panel: `Tools → Vortex → Debug → Custom Variables Dashboard`.

- **Values** — engine variables in Play Mode with current values and editing.
- **Links** — for each variable: where it is written, read, compared and awaited — in `.nani` scripts and in Vortex quests separately; a click on a location opens the script at the line in the external editor or selects the quest preset, the **Find** button on every row selects the script or preset and shows it in the Inspector. If the asset was moved or deleted after the build — a warning in the console. Works without running the game.
- **Lost** — declared but unused; used but undeclared; never written; never read; case twins; exclusions without a variable.
- **Problems** — what could not be read during the build.

Out of scope:
- Variable usages outside scripts and quests (prefabs with `NaniVariableSwitcher`, the character catalog, code calling `SetVariableValue`/`GetVariableValue`) — not scanned; false losses from them are closed with exclusions.
- The "Nani Variables" window of the `NarrativeImportTool` plugin — a separate tool, untouched.

## Dependencies

- Naninovel — `Engine`, `ICustomVariableManager`, `CustomVariablesConfiguration`
- Unity Editor — `AssetDatabase`, `SerializedObject`, `ScriptableSingleton`
- Vortex quests — only via the preset type name (`t:QuestPreset`) and its serialized field names; no reference to the quests assembly

## Usage

### Building the link map

The "Build" button on any tab except "Values". The build reads the variables config, every `.nani` script in `Assets` and every quest preset in the project, with a cancellable progress bar. The map lives in memory only: it is reset on recompilation and on every Play Mode entry (domain reload is enabled in the project). Changes to scripts and quests after the build are not tracked — the build time in the window shows how fresh the data is.

### Hooking up a new quest condition or logic

A quest object that works with a Naninovel variable implements `INaniVariableReference` — otherwise the panel does not see it, and the variable may end up among the "lost" ones:

```csharp
[Serializable]
public class NaniVariableRangeCondition : QuestConditionLogic, INaniVariableReference
{
    [SerializeField] private string variableName;
    [SerializeField] private string range;

    string INaniVariableReference.VariableName => variableName;
    NaniVariableOperation INaniVariableReference.Operation => NaniVariableOperation.Compare;
    string INaniVariableReference.Operand => range;
    // ...
}
```

The contract does not change in-game behavior. It is currently implemented by `NaniVariableCondition` (compare), `NaniVariableValueComparation` (compare), `NaniVariableValueWait` (wait).

An object that works with a formula of several variables implements `INaniExpressionReference` — a single `Expression` property. The panel parses the formula with the same condition parsing as `if:` and `@if` in scripts: every variable is its own link with a compare or read, all located at this quest object. `NaniExpressionComparation` is implemented this way.

```csharp
string INaniExpressionReference.Expression => expression;   // "Day>=3 || (Flag && Money>100)"
```

An object may implement both contracts — they are processed independently.

### Exclusions

Project settings: `ProjectSettings/CustomVariablesDashboard.asset` (version-controlled, shared by the team). An entry is a variable name (case-sensitive), loss kinds and a reason. An exclusion hides the variable only from the sections of the listed kinds; in the "Links" list and the card it is always visible. Added with the "Exclude" button in a loss section (extends the kinds of an existing entry for that name) or in the "Exclusions" section. Case twins cannot be excluded.

## Architecture

```
CustomVariablesDashboard/
├── INaniVariableReference.cs          ← contract + NaniVariableOperation (runtime)
└── Editor/
    ├── CustomVariablesDashboardWindow*.cs  ← window: Values / Links / Losses / Problems
    ├── Model/
    │   ├── VariableModels.cs          ← declaration, link, locations (.nani / quest), problem, LossKinds
    │   ├── VariableIndex.cs           ← VariableEntry, VariableIndex (Current — static, memory)
    │   ├── LossAnalysis.cs            ← loss kinds, twins, exclusions without a variable
    │   └── DashboardSettings.cs       ← exclusions (ScriptableSingleton in ProjectSettings)
    └── Sources/
        ├── VariableIndexBuilder.cs    ← build and Rebuild: config → .nani → quests → variables
        ├── NaniScriptSource.cs        ← .nani parsing
        └── QuestSource.cs             ← quest preset traversal (USING_VORTEX_QUESTS)
```

Dependencies are one-directional: window → builder and sources → model. The model knows neither the builder nor the sources. A link location (`LinkLocation`) is polymorphic: `Describe`, `Open` (navigate), `Find` (select the asset in the Inspector).

### Operation vocabulary

| Kind | Meaning | Example |
|------|---------|---------|
| write | assignment | `@set Day=1`, `@set Day++`, `@set Day+=2` |
| compare | condition with an operator and operand | `@if Day>=3`, `if:Flag` (`== true`), `if:!Flag` (`== false`), quest condition `>=3` |
| wait | quest logic waits for the comparison to hold | `NaniVariableValueWait` |
| read | part of an expression without a comparison | `b` in `@set a=b+1`, a function argument, arithmetic in a condition |
| other | not recognized, the source text is shown | — |

For the "never written" / "never read" losses: compare, wait and read count as reads; compound assignment is a write; other counts as neither. The initial value from the config is not a write.

### `.nani` parsing (heuristic, not the Naninovel compiler)

- Parsed: `@set`, the `if:` parameter of any command, `@if`, `@elseif`; commands inside text lines (`[set …]`) — the same way. Comments (`;`) and labels (`#`) are skipped.
- A variable in an expression is an identifier outside a string literal, not followed by `(`, and not `true`/`false`. In `@set CardsUnlocked++ if:isUnlocked("VArtCop00")` there is one variable — `CardsUnlocked`.
- The comparison `3 < Day` is shown as `Day > 3`. A single `=` in a condition is equality (`if:Day=1`).
- An expression in curly braces (`if:{a=5 && b>=70}`) is one parameter; spaces inside do not split it.
- An unquoted expression with spaces is parsed by its first word — the same way Naninovel sees it.
- Text substitutions (`{Var}` in dialogue lines) are not parsed: a variable that is only substituted into text lands in "never read".

### Quest traversal

Every `QuestPreset` asset in the project, via `SerializedObject`: the `startConditions` (start), `interruptConditions` (interrupt) and `logic` (logics) fields with nested gates at any depth; rewards are not traversed. The location is the preset and a path: "Старт › группа 2 › условие 1 › вложенное 3", "Логики › 4". Links come from objects implementing `INaniVariableReference` (one link) and `INaniExpressionReference` (a link per formula variable). A broken type reference, an empty element, an empty variable name, an empty formula go to "Problems".

## Contract

### Input
- `CustomVariablesConfiguration.PredefinedVariables`, `.nani` assets in `Assets`, `QuestPreset` assets
- Quest objects implementing `INaniVariableReference` or `INaniExpressionReference`

### Output
- The window; writes — only exclusions in `ProjectSettings` and a variable value edit in Play Mode

### Guarantees
- A build replaces the map entirely or leaves it unchanged (cancellation, an exception in the build — the previous map)
- Every declaration produces a variable in the map, even without links; every link lies in exactly one variable with the same name
- Names are compared case-sensitively, as in the Naninovel variable manager
- Loss kinds are computed on display from the current map and exclusions, never stored
- An error in one asset does not abort the build

### Limitations
- Expression parsing is heuristic: exotic expressions may land in "other"
- The map does not track asset changes; rebuilding is manual
- The current value in the "Links" card updates live only while the window is subscribed to the variable manager — after the "Values" tab has been opened; otherwise on window repaint
- `{Var}` substitutions in text lines are not parsed (see "`.nani` parsing")
- The quest preset field names (`startConditions`, `interruptConditions`, `logic`) and the type name (`t:QuestPreset`) are hard-coded in the quest source. A broken seam is loud: a missing field in a preset or no presets found is a build problem, links are not lost silently

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Map not built | A hint and the "Build" button |
| Play Mode entry, recompilation | The map is reset, build again |
| Empty / duplicate name in the config | Build problem; duplicate — the first one counts |
| Empty element in a condition group or logics | Build problem |
| Exception while traversing one preset | Build problem, other presets are traversed |
| A preset lacks a section field (`startConditions` / `interruptConditions` / `logic`) | Build problem: the preset structure changed |
| No quest presets found while quests are enabled | Build problem: the preset type was renamed |
| Unclosed quote in a `.nani` command | Build problem with the line; the rest of the line is parsed as one parameter |
| Click or Find on an asset moved after the build | Warning in the console |
| Broken type reference in a preset | Build problem with the type name |
| Variable reference with an empty name | A problem, not a link |
| Several exclusions for one name | Kinds are merged |
| Exclusion for a name absent from the map | "Exclusions without a variable" section |
| Exclusion with an empty name | Has no effect, warning |
| `USING_VORTEX_QUESTS` off | Scripts only; the card shows "Квесты Vortex отключены" |
| Variable absent at runtime (Play Mode) | The card shows "переменной нет в рантайме" |
