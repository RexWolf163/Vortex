# FormulaEvaluator

String-based mathematical formula evaluation with Inspector-driven parameter binding to class members.

## Purpose

- Declare formulas as string fields with the `[Formula]` attribute
- Bind parameters `{0}`, `{1}`, ... to fields, properties, methods, and constants via dropdown
- Preview evaluation results using test values in Inspector
- Dependency-free recursive descent math parser

Out of scope: runtime formula evaluation binding (parser is available, binding is project responsibility).

## Dependencies

| Package | Purpose |
|---------|---------|
| `ru.vortex.unity.editortools` | MultiDrawer, PropertyData, DrawingUtility, SearchablePopup |

## Formula Format

```
"sqrt(3 + {0}^2) * ({1} / {2})"
```

- `{N}` — parameter slot, N is the index (from 0)
- Operators: `+`, `-`, `*`, `/`, `^` (exponentiation, right-associative)
- Parentheses: `(`, `)`
- Constants: `pi`, `e`
- Functions (1 arg): `sqrt`, `abs`, `sin`, `cos`, `tan`, `log`, `floor`, `ceil`, `round`
- Functions (2 args): `min`, `max`, `pow`
- Functions (3 args): `clamp(value, min, max)`

## Components

```
FormulaEvaluatorSystem/
├── Attributes/
│   └── FormulaAttribute.cs          → [Formula("slotsFieldName")] attribute
├── Model/
│   └── FormulaSlot.cs               → serializable binding: memberName + testValue
├── FormulaParser.cs                  → recursive descent expression parser
├── FormulaReflectionResolver.cs      → #if UNITY_EDITOR: numeric member discovery
└── FormulaDrawer.cs                  → #if UNITY_EDITOR: MultiDrawer rendering
```

## Usage

```csharp
using Vortex.Unity.FormulaEvaluatorSystem.Attributes;
using Vortex.Unity.FormulaEvaluatorSystem.Model;

public class DamageCalculator : MonoBehaviour
{
    [SerializeField] private float baseAttack;
    [SerializeField] private int level;
    protected float defenseModifier = 0.5f;

    [Formula(nameof(damageSlots))]
    [SerializeField] private string damageFormula = "sqrt({0}) * {1} + {2}";
    [SerializeField, HideInInspector] private FormulaSlot[] damageSlots;
}
```

### Inspector Layout (top to bottom)

1. **Info bubble** — evaluation result from test values (or error message)
2. **Label** — field name (bold, full width)
3. **Formula field** — string input
4. **Slot list** — one row per `{N}`:
   - Left: `{N}` label + class member dropdown (SearchablePopup)
   - Right: test value input (float, default 0)

### Dropdown Grouping

Class members are grouped by category and origin:

```
Fields — Own/
  health : int
  mana : float
Fields — Inherited/
  maxHealth : float
Properties — Own/
  DamageMultiplier : float
Methods — Own/
  GetBaseAttack : float
Constants/
  MAX_LEVEL : int
```

### Eligible Members

- **Fields**: any access modifier, instance and static
- **Properties**: with getter, any access modifier
- **Methods**: parameterless with numeric return type
- **Constants**: `const` and `static readonly` of numeric types
- Numeric types: `int`, `float`, `double`, `long`, `decimal`, `byte`, `short`, `uint`, `ulong`, `ushort`, `sbyte`
- Inherited `private` members are excluded (inaccessible)

## Parser API

| Method | Description |
|--------|-------------|
| `FormulaParser.Evaluate(formula, parameters)` | Evaluate. Throws on error |
| `FormulaParser.TryEvaluate(formula, parameters, out result, out error)` | Safe evaluation |
| `FormulaParser.GetMaxSlotIndex(formula)` | Highest `{N}` index in formula (-1 if none) |

### Runtime Evaluation Example

```csharp
var parameters = new double[] { 10.0, 5.0, 2.0 };
if (FormulaParser.TryEvaluate("sqrt({0}) * {1} + {2}", parameters, out var result, out var error))
    Debug.Log($"Result: {result}");
else
    Debug.LogError(error);
```

## Edge Cases

- Empty formula — no topper rendered, no slots
- Non-contiguous indices (`{0} + {5}`) — 6 slots created, intermediate ones unbound
- Formula change — slots array auto-resizes, existing bindings preserved
- Division by zero — returns `Infinity` (IEEE 754 behavior)
- Unknown function or constant — parse error shown in topper
- Unset test value — treated as 0
- Negative substituted values are wrapped in parentheses for correct parsing
