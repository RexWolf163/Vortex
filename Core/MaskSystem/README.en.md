# MaskSystem (Core)

**Namespace:** `Vortex.Core.MaskSystem`
**Assembly:** `ru.vortex.core.masksystem`
**Status:** experimental — the API and pattern syntax may change without backward compatibility.

## Purpose

A mask is a string-matching rule. It is domain-neutral: it works with any strings (URLs, paths, keys); input normalization is the consumer's job.

- Check: string → `bool` (`IsMatch`)
- Execute: string → match flag + captures dictionary "name → value" (`Match`)
- Self-validation of the pattern: result, message, error position (`Validate`)
- A match always covers the **whole** string
- Two implementations: wildcard pattern (`WildcardMask`) and regular expression (`RegexMask`)

The Unity layer (`ru.vortex.unity.masksystem`) adds mask selection in the inspector, hints, error highlighting and a test string.

## Dependencies

None (only `System.Text.RegularExpressions`).

## Usage

```csharp
var mask = new WildcardMask { pattern = "https://{host}.partner.com/v{ver}/**" };
var validation = mask.Validate();            // by the consumer, during its initialization
if (!validation.IsValid)
    throw new InvalidOperationException(validation.Message);

mask.IsMatch("https://cdn.partner.com/v2/items/5");   // true
var match = mask.Match("https://cdn.partner.com/v2/items/5");
// match.IsMatch == true; match.Captures: host = cdn, ver = 2
```

## Model

```
Mask (abstract)
├── pattern : string            ← pattern, syntax depends on the implementation
├── caseSensitive : bool = true ← affects comparison only; captures keep the input's case
├── Validate() → MaskValidation { IsValid, Message, Position }
├── IsMatch(string) → bool
└── Match(string)  → MaskMatch { IsMatch, Captures : IReadOnlyDictionary<string,string> }

WildcardMask : Mask   + separator : char = '/'
RegexMask    : Mask
```

## `WildcardMask` syntax

| Element | Meaning | In result |
|---|---|---|
| literal | character by character | — |
| `*` | zero or more characters within a segment | no |
| `**` | zero or more of any characters, separator included | no |
| `{name}` | one or more characters within a segment | `name` |
| `{*name}` | zero or more of any characters; last element only | `name` |
| `{{` `}}` `\*` `\\` | literals `{` `}` `*` `\` | — |

- Literals and captures may share a segment: `v{ver}`, `{name}.png`.
- On ambiguity within a segment the left capture is greedy: `{a}.{b}` on `x.y.z` → `a = x.y`, `b = z`.
- Invalid: empty pattern, unclosed or stray brace, `{}`, duplicate name, `{*name}` not last, adjacent captures (`{a}{b}`), unknown escape, separator is a reserved character (`*` `{` `}` `\`) or unset.

## `RegexMask` syntax

.NET regular expression.

- Anchoring is implicit and covers the whole expression: `a|b` does not match `xa`.
- Named group → key by name; unnamed group → numeric key (`"1"`, `"2"`…).
- Group 0 and groups that did not participate in the match are excluded.
- Invalid: empty pattern; the expression does not compile (checked before anchoring is applied, so `a)|(b` is not "fixed" by the wrapper).

## Guarantees and limitations

- `IsMatch(s) == Match(s).IsMatch` for any string.
- No match → empty captures dictionary.
- An invalid mask is never used: `IsMatch`/`Match` throw `InvalidOperationException`. `null` input — `ArgumentNullException`. An empty string is a regular input.
- After the first build (`Validate`, `IsMatch` or `Match`) mask parameters are immutable: a change is detected on the next call and throws `InvalidOperationException`. A subclass may allow revalidation (`RebuildOnChange`) — the Unity layer does so in the editor.
- Main thread only.
- No `RegexOptions.Compiled`: works on AOT platforms (IL2CPP, WebGL).
- Mask lists (check order, "first match") are the list holder's responsibility.
