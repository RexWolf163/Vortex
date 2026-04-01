# LoaderSystem (Unity)

**Namespace:** `Vortex.Unity.Components.LoaderSystem`
**Assembly:** `ui.vortex.unity.components`

---

## Purpose

Unity components for triggering and visualizing the application loading process.

Capabilities:

- Automatic `Loader.Run()` trigger on `AppStates.Starting`
- Progress visualization: module name, step number, completion percentage
- Visual state switching (Waiting → Loading → Completed)

Out of scope:

- Loading logic (implemented in `Vortex.Core.LoaderSystem`)
- Module registration

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LoaderSystem` | `Loader` — loading orchestration |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `AppStates` |
| `Vortex.Core.LocalizationSystem` | `TryTranslate()` — module name localization |
| `Vortex.Unity.UI` | `UIComponent`, `UIStateSwitcher` |

---

## Components

### LoaderStarter

MonoBehaviour — trigger for `Loader.Run()`.

- `OnEnable`: if `AppStates >= Starting` — runs immediately, otherwise subscribes to `App.OnStarting`
- `OnDisable`: unsubscribes from `App.OnStarting`

Placed on the loading scene. One instance per scene.

### LoaderView

MonoBehaviour — loading progress visualization.

Three visual states via `UIStateSwitcher`:

| State | Condition |
|-------|-----------|
| `Waiting` | `AppStates` < `Starting` |
| `Loading` | `AppStates == Starting` |
| `Completed` | `AppStates == Running` |

Text updates every 0.3 seconds (Coroutine). Format controlled by pattern:

```
{0} ({1}) → {2}: {3}%
 ↓    ↓      ↓     ↓
step total  name percent
```

Module name is passed through `TryTranslate()` for localization.

#### Inspector setup

| Field | Type | Description |
|-------|------|-------------|
| `switcher` | `UIStateSwitcher` | Visual state switcher |
| `uiComponent` | `UIComponent` | Text component for progress display |
| `loadingTextPattern` | `string` | Format pattern (default `{0} ({1}) → {2}: {3}%`) |

---

## Usage

1. Create a loading scene
2. Add `LoaderStarter` to any GameObject
3. (Optional) Add `LoaderView` with configured `UIStateSwitcher` and `UIComponent`
4. Modules register via `Loader.Register()` in drivers before `AppStates.Starting`

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| `LoaderStarter` enabled after `AppStates.Starting` | `Loader.Run()` called immediately |
| `LoaderView` without `switcher` | null-check, switching skipped |
| `GetCurrentLoadingData()` returns `null` | Text not updated |
| `ProcessData.Size == 0` | Percentage = 0 (division protected) |
| `App.OnStateChanged` → `Running` | LoaderView unsubscribes, Coroutine stopped |
