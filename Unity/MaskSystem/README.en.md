# MaskSystem (Unity)

**Namespace:** `Vortex.Unity.MaskSystem`
**Assembly:** `ru.vortex.unity.masksystem`
**Status:** experimental — the API and inspector behaviour may change without backward compatibility.

## Purpose

Unity layer for the core masks (`ru.vortex.core.masksystem`; syntax and guarantees are in the core README).

- `UnityWildcardMask`, `UnityRegexMask` — inspector-ready masks. Matching semantics are unchanged; in the editor (Play Mode included) editing parameters revalidates the mask on the fly, in a build a change after validation is a programmer error.
- `[MaskSelector]` — mask type selection via a dropdown (Odin has no filtered type picker for `[SerializeReference]`).

## Dependencies

- `ru.vortex.core.masksystem`
- Odin Inspector (drawer)

## Usage

```csharp
[SerializeReference, MaskSelector] private Mask urlMask;

private void Init()
{
    var validation = urlMask.Validate();
    if (!validation.IsValid)
        throw new InvalidOperationException(validation.Message);   // fail-fast during consumer initialization
}
```

## Inspector

A field with `[MaskSelector]` is drawn as:

1. Type dropdown: subclasses of the field type declared outside the core assembly (`UnityWildcardMask`, `UnityRegexMask`, project masks). Core implementations are not offered; if one is assigned from code, a warning is shown.
2. Mask parameters. Hovering the pattern field shows the syntax rules of the selected implementation.
3. Pattern error — a red box with the message and position (`▸` marks the spot).
4. "Test string" — editor-only, not saved. Shows match / no match and captures.

## Limitations

- The attribute works with a single field of type `Mask` (or a subclass). For lists, wrap the mask in an element class with a `[SerializeReference, MaskSelector]` field.
- A listed type must be non-abstract, `[Serializable]` and have a parameterless constructor.
- The test string lives in the drawer and resets on domain reload.
