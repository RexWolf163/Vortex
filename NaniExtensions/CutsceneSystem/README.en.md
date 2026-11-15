# NaniExtensions — CutsceneSystem

> ⚠️ **Experimental package** — a companion of [CutsceneSystem](../../Unity/CutsceneSystem/README.en.md), which has not been battle-tested in production. The set and parameters of the script commands may change along with it without backwards compatibility.

**Namespace:** `Vortex.NaniExtensions.CutsceneSystem`
**Assembly:** `ru.vortex.nani.cutscenes` (defineConstraints: `USING_NANINOVELL`)

---

## Purpose

Naninovel script commands for [`CutsceneSystem`](../../Unity/CutsceneSystem/README.en.md). The commands only translate script lines into `CutsceneBus` calls: content loading, phases and completion are handled by the cutscene package.

The cutscene bus executes commands at the end of the frame, not at the moment of the call. So script commands whose result the next script line depends on wait for that result.

Out of scope:

- Closing a cutscene when the script stops is not done by itself: a cutscene is finished by its chain or by `@cutscene_Stop` / `@cutscene_Skip`. The safety net for that case is `CutsceneAutostopHandler` in the scene (see below).
- The render order of a cutscene relative to Naninovel backgrounds, characters and text.

---

## Commands

| Command | What it does | What the script waits for |
|---|---|---|
| `@cutscene_Play key` | opens a cutscene; the key is the `LogicChain` record id | the show starting, or a refusal |
| `@cutscene_Next` | "next": go to the next step | nothing |
| `@cutscene_Stop` | stops the cutscene: the show is interrupted, the content unloaded | the cutscene closing |
| `@cutscene_Skip` | the same as `@cutscene_Stop` | the cutscene closing |

### `@cutscene_Play`

- A running cutscene is closed first, and the script waits for it to close.
- Then the script waits until the content is loaded and the show starts — or until the open is refused (the cutscene package logs the reason). The command listens to bus events with its own key only.
- The command does not wait for the cutscene to end: the script continues while the cutscene is shown.

### `@cutscene_Next`

Works on any phase of a running cutscene. Ignored: before the first phase has started (the content is still loading), during a fly-through, and earlier than the debounce delay (an orchestrator setting). The phase changes on the orchestrator's next pass; sprite content shows it subject to its own switch delay.

### `@cutscene_Stop` and `@cutscene_Skip`

The bus has a single way to interrupt — a fly-through of the chain to the final step — so the two commands do the same thing. Two names are for script readability: `Stop` — the script itself stops the cutscene, `Skip` — it is being skipped.

The script continues once the cutscene is closed.

---

## CutsceneAutostopHandler

An emergency stop of the cutscene when the script stops. The component goes in the scene — on an object that stays enabled for as long as cutscenes may run in the scene (usually the orchestrator object). It has no settings.

The handler keeps a "cutscene is open" latch:

- the bus event `OnShowOpening` (the orchestrator accepted an open and started loading) opens the latch;
- the `OnShowFinished` event closes it.

If the script stops (`NaniWrapper.OnNaniStop`) while the latch is open, the cutscene is interrupted, as with `@cutscene_Stop`: a running one flies through to the end, a loading one is cancelled. If the latch is closed, the handler does nothing.

The handler's contract: **a cutscene plays while the script plays.** There is one latch and it does not compare cutscene keys — the handler has no per-cutscene setting. If the latch is open, whichever cutscene is open right now gets stopped.

- Needed where the script can end before the cutscene: leaving to the menu, loading a save, a script error. If the script always closes the cutscene itself, the component is not needed.
- It acts on any cutscene of the scene, including one started outside a script, and on a script's natural end too. Do not put the component in a scene where cutscenes must outlive the script.
- It does not see a cutscene opened while the handler was disabled: the latch closes on disable.
- It is built for normal use of the cutscene package — a single driving source. With competing opens from show event handlers the latch may diverge from reality (see "Limitations" in the cutscene package README).
- On a content prefab the component does not work: by the time the content is created, the `OnShowOpening` event has already passed.

`OnNaniStop` arrives when the script player has stopped and is not waiting for a player choice — one frame after the stop. Switching scripts and jumps inside a script do not count as a stop: if a new script is started within that frame, the event does not arrive and the cutscene is not interrupted.

---

## Example

```
@cutscene_Play 3f6c1c0e-8d2a-4b7e-9a51-0c2f6d1e7b90
The first phase plays under this line.
@cutscene_Next
The second phase.
@cutscene_Stop
```

---

## Edge cases

| Situation | Behaviour |
|---|---|
| `@cutscene_Play` with no key | `Debug.LogError`, the command is skipped |
| No orchestrator, key not in the index, the chain failed validation | an error logged by the cutscene package, the script continues |
| The script is stopped while a command waits | the wait is released; a load already started and a command already issued are not cancelled |
| The script is stopped while `@cutscene_Play` waits for the previous cutscene to close | the new cutscene is not opened |
| `@cutscene_Next` / `@cutscene_Stop` / `@cutscene_Skip` with no open cutscene | nothing |
| `@cutscene_Play` of the cutscene that is already running | the previous show is closed, then the cutscene opens again |
| The script stops while a cutscene is running and the scene has `CutsceneAutostopHandler` | the cutscene is interrupted: the stop event arrives one frame later, the fly-through runs on the orchestrator's next pass |
| The script stops while the cutscene content is loading and the scene has `CutsceneAutostopHandler` | the load is cancelled, the cutscene does not appear |
| `CutsceneAutostopHandler` is on a content prefab | never fires |
