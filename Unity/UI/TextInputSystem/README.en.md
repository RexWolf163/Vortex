# TextInputSystem

**Namespace:** `Vortex.Unity.UI.TextInputSystem`
**Assembly:** `ru.vortex.unity.ui.textinput`

## Purpose

Keyboard text input over a displayed value: a button switches the value into input, a field with its own background appears over it, Enter saves, Esc rolls back. Input is the built-in `TMP_InputField` (caret, selection, clipboard, IME, multiline), the mask is a list of filters.

Capabilities:
- Knows no data: the source text comes into `Begin`, the result leaves via the `OnSave` event — the value owner decides what to do with it
- Mask via `[SerializeReference]`: checked on every typed character and on save
- Input goes into one field at a time
- Finishing by a click outside the field — through public `Save` / `Rollback` linked from the layout (a backdrop)

Out of scope:
- Muting game keys during input. The field reads the keyboard itself, bypassing input maps, and knows nothing about them. Muting is done by layout handlers in the «input» state: `InputMapMuteHandler` (input bus), `NaniInputMuteHandler` (Naninovel)
- Parsing the result into the value type (number etc.) — the value owner's job

## Dependencies

| Dependency | Purpose |
|------------|---------|
| TextMeshPro (`com.unity.ugui`) | `TMP_InputField` |
| `Vortex.Unity.UI.Misc` | `UIStateSwitcher`, `[StateSwitcher]` |
| `Vortex.Core.Extensions` | `SwitcherState` |
| `Vortex.Core.ExtensibleEnumSystem` | Required by `UIStateSwitcher.Set` overloads and the `[StateSwitcher]` attribute |
| Odin Inspector | Inspector attributes, `[SerializeReference]` drawing |

---

## Usage

### Layout

A working example — `ParameterItem` in `CheatConsole.prefab`:

```
ParameterItem                          ← UITextInput, value owner (HeroParameterTextInput)
├── [WriteModeSwitcher]                ← UIStateSwitcher: Off — empty, On — enable Container and [InputHandler]
│   └── [InputHandler] (off)           ← input mutes
│       ├── InputMapMuteHandler ×N
│       ├── InputActionHandler ×N      ← (optional) intercept single actions excluded from a mute
│       └── NaniInputMuteHandler       ← if input can happen over a Nani scene
├── Value (text)
├── InputBtn                           ← AdvancedButton → value owner → UITextInput.Begin(text)
└── Container (off)                    ← overlay over the value; Button.onClick → UITextInput.Save or Rollback
    ├── cover                          ← backdrop: the click bubbles up to the Button on Container
    ├── Background                     ← background over the value
    └── Input                          ← TMP_InputField
```

- **`UITextInput` — on an object the switcher never disables**: disabling the component during input is a rollback. The field's object may be disabled.
- **Switcher:** `Off` — view, `On` — input. Must start at `Off` (`stateOnEnable`). A `GameObjectsSwitch` in the `On` state disables its objects itself when switching to `Off` — `Off` needs no items.
- **Input button** — `AdvancedButton`, not `Button`: input must be opened by the value owner via `Begin`, not by switching the switcher directly (otherwise there is no text and no focus).
- **Backdrop** — a button on the overlay root. A click on the field itself is taken by `TMP_InputField` and does not reach the backdrop.
- **`TMP_InputField`:** Line Type sets the Enter behaviour (see below). Content Type / Character Validation are not needed — the mask is set by filters, and the component takes the field's `onValidateInput` itself. «Select all on focus» (on by default) — the first character replaces the value.

### Mutes during input

Enabled by the `On` state, i.e. after the subscribers they override (see `InputBusSystem` → «Map mute»).

| Map | What to mute | Exceptions |
|---|---|---|
| `VirtualCursor` | Keyboard navigation: arrows, Submit (Enter), Cancel (Esc) | **Cursor position, `Click`, `AltClick`, gamepad cursor movement.** The cursor and clicks go through the bus (`MouseInputDriver`, `ActionInputDriver`): muted, the cursor freezes and the backdrop and field cannot be clicked |
| `UI` | Everything — otherwise Esc fires the screen's `UICancel` (e.g. closes the console) | — |
| Naninovel | Not by map: Naninovel subscribes to actions bypassing the bus, `InputMapMuteHandler` on its map mutes nothing. Use `NaniInputMuteHandler` | — |

An action from the exceptions can be intercepted separately — an `InputActionHandler` with empty events on the same layer: it subscribes later and goes on top.

### Value owner code

```csharp
[SerializeField] private UITextInput input;

private void OnEnable() => input.OnSave += OnSave;
private void OnDisable() => input.OnSave -= OnSave;

private void OnClick() => input.Begin(_data.Value.ToString(CultureInfo.InvariantCulture));
private void OnSave(string text) => _data.SetValue(int.Parse(text, CultureInfo.InvariantCulture));
```

Project example — `HeroParameterTextInput` (hero parameters in the cheat console).

### Mask

A list of `ITextInputFilter`. A string is valid only if **all** filters accept it.

| Filter | While typing | Result |
|---|---|---|
| `IntegerRangeTextFilter` (min, max) | Digits and a leading minus (if `min < 0`). Only what insertion can no longer fix is cut: a number past the bound in the direction of a growing magnitude. «5» with `min = 10` is allowed — more digits may follow | An integer in range |
| `RegexTextFilter` (two patterns) | The «while typing» pattern — must accept unfinished strings too (`^-?\d{0,3}$`) | The «result» pattern (`^-?\d{1,3}$`) |

Patterns check the whole string, the author sets the `^…$` anchors. An empty pattern accepts any string.

A custom filter — a `[Serializable]` class implementing `ITextInputFilter`:

```csharp
bool IsValidPartial(string candidate);   // the string after inserting the character; false — the character is not typed
bool IsValidFinal(string text);          // false — saving fails, input continues
```

---

## Behaviour

| Action | Result |
|---|---|
| `Begin(text)` | Rollback of input in another field, switcher `On`, text into the field, focus. During input — replace the text |
| Character | `IsValidPartial` of all filters for the string after insertion; failed — the character is not typed. Clipboard paste is checked per character |
| Enter | `Save` (single-line field or multiline with submit on Enter) |
| Esc | `Rollback` |
| `Save()` | `IsValidFinal` of all filters: failed — input continues; passed — exit input, then `OnSave(text)` |
| `Rollback()` | Exit input, then `OnRollback` |
| Click outside the field | The field loses focus, input does not finish — the backdrop decides |
| Component disabled during input | Exit input without events |

Exiting input: drop the field focus and the `EventSystem` selection, switcher `Off`. The event fires **after** the exit: the overlay with the mutes is already disabled when the value owner changes the value.

Enter by the field's `Line Type`:
- `Single Line`, `Multi Line Submit` — Enter saves;
- `Multi Line Newline` — Enter inserts a line break; saving only via `Save` from the layout.

---

## Edge Cases

| Situation | Behaviour |
|---|---|
| `Save` / `Rollback` outside input | Nothing (the backdrop can be linked unconditionally) |
| `Begin` in another field during input | The current input is rolled back (`OnRollback`) |
| The result fails the mask on Enter | The field loses focus and regains it on the next frame; Esc after that restores the already typed text, but input is still rolled back |
| Deleting characters | Not checked: an intermediate string may fail `IsValidPartial`, the result is checked on save |
| No `EventSystem` | Focus is not set — no keyboard input |
| No filters | Any string is accepted |
