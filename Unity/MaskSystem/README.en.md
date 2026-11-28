# MaskSystem (Unity)

**Namespace:** `Vortex.Unity.MaskSystem`
**Assembly:** `ru.vortex.unity.masksystem`

---

## Purpose

Inspector-configurable masks over the Core package `ru.vortex.core.masksystem` (matching rules, contract and guarantees are described in its README).

Capabilities:

- Three mask kinds selectable in a `[SerializeReference] Mask` field:
  - `StringEqualMask` — string equals a pattern (comparison mode, case, input trimming, NFC normalization)
  - `UrlMask` — URL parts: scheme, host, port, path
  - `RegexMask` — .NET regular expression with captures
- `UnityMask` — common base of the kinds: header label, rules text, pre-match check, inspector check panel
- Inspector: error highlight with position, rules tooltip on settings fields, test string with "match / no match" and captures

Out of scope:

- Matching logic — static controllers of the Core package
- Mask lists and "first match" — the list holder

Where masks should and should not be used (including: not for player input control) — Core package README, "Applicability" section.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.core.masksystem` | `Mask`, results, controllers |
| `ru.vortex.unity.editortools` | `[ClassLabel]` |
| Odin Inspector | inspector attributes |

---

## Usage

```csharp
public class PartnerRules : MonoBehaviour
{
    [SerializeReference] private Mask partnerUrl;

    private void Awake()
    {
        var validation = partnerUrl.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.Message);
    }

    public bool IsPartner(string url) => partnerUrl.IsMatch(url);
}
```

In the inspector pick the kind in the field's type dropdown and fill its settings. A mask is created in the inspector only; creating a configured mask from code is not provided.

### Inspector

- **Header** — kind name (`GetLabel`, type name by default).
- **Error** — red box above the test string: message and, where applicable, "(position N)".
- **Tooltip** — hovering a settings field shows the rules of the kind (`RulesHint`).
- **Test string** — editor field, does not affect matching. "Result" shows "match" / "no match", "Captures" — the captures dictionary when not empty.
- `RegexMask` checks the test string by the **"Check"** button, not on typing (protection from heavy expressions). Other kinds update on typing.

---

## Architecture

```
Mask (Core)
  └── UnityMask (abstract, [Serializable, ClassLabel("$GetLabel"), HideReferenceObjectPicker])
        ├── GetLabel()            — header label (virtual, type name by default)
        ├── RulesHint             — rules text of the kind (abstract)
        ├── TestOnDemand          — test string by button (virtual, false)
        ├── ResetEditorCache()    — reset of the check panel results (protected)
        ├── RequireValid(input)   — fast-fail before matching: null input, invalid settings (protected)
        ├── StringEqualMask       → StringEqualController
        ├── UrlMask               → UrlController
        └── RegexMask             → RegexController
```

Each kind:
- holds settings in `[SerializeField] private` fields with read-only getters;
- calls `RequireValid(input)` before matching;
- calls the controller of its kind; the input is prepared by the controller (`StringEqualController.TryPrepareInput`, `UrlController.TryParseInput`), the mask only passes its settings;
- caches validation result and prepared data (`[NonSerialized]`); in the editor a settings field change (Odin `[OnValueChanged]`) resets the caches and the check panel.

### Custom kind

```csharp
[Serializable]
public class PrefixMask : UnityMask
{
    [SerializeField, PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
    private string prefix = "";

    [NonSerialized] private ValidationResult _validation;

    public override string RulesHint => "The string starts with the prefix.";

    public override ValidationResult Validate() => _validation ??= /* controller of the kind */;
    public override bool IsMatch(string input)
    {
        RequireValid(input);
        return /* controller of the kind */;
    }

    public override MatchResult Match(string input) => IsMatch(input) ? MatchResult.Matched() : MatchResult.NoMatch();

#if UNITY_EDITOR
    private void OnSettingsChanged()
    {
        _validation = null;
        ResetEditorCache();
    }
#endif
}
```

`ValidationResult` is created only by Core controllers, so the validation of a custom kind is implemented by a controller.

---

## Contract

### Input

- Settings — via the inspector.
- `IsMatch` / `Match` input — any string; `null` is a programmer error.

### Output

- Result of the controller of the kind; the mask does not change the matching semantics.

### Guarantees

- In a build settings cannot be changed: fields are `private`, outward read-only.
- In the editor (Play Mode included) a settings change revalidates the mask on the fly.
- Matching on invalid settings — `Debug.LogError` + `InvalidOperationException`; `null` input — `Debug.LogError` + `ArgumentNullException`.
- Input data that cannot be matched (not a URL, invalid Unicode) — "no match".

### Constraints

| Constraint | Reason |
|---|---|
| A URL input must be `scheme://…` without `\`, control characters or edge spaces | `System.Uri` is more lenient than an HTTP stack; such input could be read differently |
| Hosts and paths in settings must be in the form `System.Uri` gives them | Otherwise a valid-looking setting would never match; validation suggests the form |
| The test string field is serialized in builds too | The serialization layout must be the same in the editor and in a build |

---

## Edge Cases

| Situation | Behavior |
|---|---|
| Settings changed by Undo, prefab revert or code | Caches are reset only by Odin `[OnValueChanged]`; the check panel may show an outdated result |
| `IsMatch` and `Match` of a kind disagree on the test string | `Debug.LogError` once per change |
| Test string is empty | Checked as an empty string (a regular input) |
| Validation or matching throws while drawing the check panel | The panel shows nothing until the next change of the test string or settings |
