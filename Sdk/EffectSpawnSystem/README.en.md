# EffectSpawnSystem

**Namespace:** `Vortex.Sdk.EffectSpawnSystem.*`
**Assembly:** `ru.vortex.sdk.effectspawn`

---

## Purpose

Pool of GameObjects for "fire-and-forget" visual effects (explosions, sparks, hits, dust). Spawn → play → auto-return to pool, no allocations on the hot path.

The visual side is driven by `TweenerHub` on the prefab (it triggers Animator/Spine/ParticleSystem). The pool itself doesn't know what plays the effect — it just calls `Forward`/`Back` on the `TweenerHub` on activation/deactivation.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Sdk.Core.GameCore` | `GameController.OnGameStateChanged`, `GameStates.Paused` — to broadcast pause to active effects |
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` — animation side of the effect |

---

## Architecture

### Two-layer storage (lazy auto-create)

```
[EffectPool]                          ← root, active, DontDestroyOnLoad
    └── Storage                       ← child, INACTIVE — all idle instances live here
            ├── ExplosionPrefab(Clone)    (parent inactive → child does not Update)
            ├── DustPrefab(Clone)
            └── ...
```

`EffectPool` is created **lazily** on first access from `EffectSpawn` (modeled after `MapLevelsController.VoidParent`). No scene objects placed manually — the pool appears when needed.

**Idle**: child of inactive `Storage` → `activeInHierarchy = false` → `Update`/`OnEnable` are not called. No manual `SetActive` anywhere.

**Active**: on `Spawn`, the instance is moved from `Storage` to the active target — Unity itself fires `OnEnable` on `EffectView`.

**Return**: on `Release`, the instance is moved back to `Storage` — Unity itself fires `OnDisable`.

### Resolving the parent for an active effect

`Spawn` requires a `target` — the consumer's `Transform`. Where the effect actually parents in the hierarchy:

```
1. owner.GetComponentInParent<EffectsLayer>()      ← walk up looking for the marker
2. layer != null:
   ├── layer.Target != null  → park into layer.Target
   └── layer.Target == null  → park into layer.transform
3. layer == null              → fallback: park into target itself
```

The effect is set as **last sibling** (`SetAsLastSibling`).

### Effect placement

| Parameter | Default | Source |
|---|---|---|
| World position | `target.position` | `Spawn` parameter `position?` if provided |
| World rotation | `Quaternion.identity` | `Spawn` parameter `rotation?` if provided |

```csharp
// Defaults: position = target.position, rotation = identity
EffectSpawn.Spawn(target, prefab);

// Custom position (e.g., hit point), default rotation
EffectSpawn.Spawn(target, prefab, position: hitPoint);

// Custom rotation (e.g., aligned to surface normal), default position
EffectSpawn.Spawn(target, prefab, rotation: Quaternion.LookRotation(normal));

// Both overridden
EffectSpawn.Spawn(target, prefab, hitPoint, Quaternion.LookRotation(normal));
```

`target` remains required: it's used to look up the `EffectsLayer` in the parent chain (where to park) and as the source of the default position when one is not passed explicitly.

### EffectsLayer — parking marker

An empty marker component placed in the scene (modeled after `MapsView`). Marks "park effects of any descendant here". Optional `Target` field — where to actually place; if not set — the marker's own transform.

Typical scenarios:
- Single `EffectsLayer` at the scene root — all scene effects go there.
- `EffectsLayer` on a sub-scene root (logically convenient) with `Target` pointing to a child `Visuals/EffectsParent` (visually convenient).
- `EffectsLayer` on an enemy → its own hits attach to the local node, not somewhere up the scene.

### EffectView — on the prefab

```
EffectView (RequireComponent TweenerHub)
  ├── duration: float                 — active cycle length, unscaled time
  ├── tweenerHub: TweenerHub
  │
  ├── OnEnable  → enabled=true, _spawnTime=Time.unscaledTime, tweenerHub.Forward()
  │              + checks EffectSpawn.IsPaused → starts in _paused=true if game already paused
  ├── OnDisable → tweenerHub.Back(skip:true)
  ├── Update    → counts duration in unscaled time, accumulates _pausedAccum during pause,
  │               on threshold calls Release()
  └── Release() → enabled=false (flag "already released this cycle")
                  + EffectSpawn.Release(this)
```

No `LifetimeStrategy`/`maxPoolSize`/`prewarmCount`/`useUnscaledTime` — only `duration` and the `TweenerHub` link. Activation always through `OnEnable` (the parent stack provides this automatically).

### Recommended effect prefab layout

```
Effect (prefab root)
├── EffectView + TweenerHub                      ← on the root
├── SkeletonGraphic / ParticleSystem / Image     ← visual layer
│   └── … (Forward/Back animation via TweenerHub)
└── [Sound] (GameObject)
    └── AudioHandler
        ├── Audio Source: None (no local AudioSource)
        ├── Audio Sample: <DbRecord(Sound) — sample GUID>
        ├── Channel: sfx
        └── Play On Enable: ✓
```

**Why this shape:**
- Visual and audio are separate child nodes. Audio is independent from animation — easy to mute/swap without touching the visual.
- `AudioHandler` without a local `AudioSource` relays the sound through `AudioController.PlaySound` → pool from `AudioPlayer`. Effects don't spawn extra `AudioSource`s and don't require manual source-lifecycle management.
- `Play On Enable: ✓` — sound fires exactly when the effect is activated via `EffectSpawn.Spawn(...)` (the child `[Sound]`'s `OnEnable` runs synchronously with the root activation).
- The node is named with square brackets — `[Sound]` — to visually separate the "system sound layer" from the visual hierarchy in the Hierarchy window.

One effect = one prefab with three layers (visual, sound, `EffectView`/`TweenerHub`). Effect cascades (e.g. hit-effect followed by a glow) are assembled as multiple `Spawn` calls of different prefabs from the same anchor.

### EffectsCatalog — index by key

```
EffectsCatalog : ScriptableObject
  ├── effects: GameObject[]
  ├── Keys → IReadOnlyList<string>     ← prefab.name of each
  ├── GetPrefab(key)
  └── ScanProject() / Validate()        — Editor-only
```

Registered explicitly: `EffectSpawn.RegisterCatalog(catalog)` from startup code.

The `[EffectKey]` attribute on a string field draws a popup of `EffectSpawn.AllKeys`. In Editor mode without registration, the drawer collects the union of keys from all `EffectsCatalog` assets in the project (via `AssetDatabase`).

### EffectSpawn — Bus

```
EffectSpawn (static)
  ├── RegisterCatalog(catalog) / UnregisterCatalog(catalog)
  ├── AllKeys → IReadOnlyList<string>
  ├── IsPaused → GameController.GetState() == Paused
  │
  ├── Spawn(target, prefab) → EffectView      ← target is required
  ├── Spawn(target, key)    → EffectView
  ├── Release(view)
  │
  └── (static-ctor)
      └── GameController.OnGameStateChanged += OnGameStateChanged
              ├── Paused  → Pool.PauseAll()
              └── else    → Pool.ResumeAll()
```

`Spawn`'s first parameter is `target` — without it the API is not callable (Logger.Error + null).

---

## Flow

```
GAME-CODE
    | EffectSpawn.Spawn(bullet.transform, "Explosion")
    v
[Bus] catalog.GetPrefab("Explosion") → prefab
    v
[Pool] Acquire:
    ├── stack.Pop() or Instantiate(prefab, Storage)
    ├── ResolveLayerTarget(target): GetComponentInParent<EffectsLayer> → Target | layer.transform | target
    ├── view.transform.SetParent(layer)             ← parent active → Unity fires OnEnable
    ├── SetAsLastSibling
    ├── SetPositionAndRotation(position ?? target.position, rotation ?? Quaternion.identity)
    ↓
[Unity] OnEnable fires automatically:
    ├── enabled = true (reset from previous cycle)
    ├── _spawnTime = Time.unscaledTime
    ├── _paused = EffectSpawn.IsPaused              ← when spawned during pause, freezes immediately
    ├── tweenerHub.Forward()
    ↓
[View.Update] every frame:
    ├── if (_paused) _pausedAccum += unscaledDeltaTime; return;
    ├── if (now - _spawnTime - _pausedAccum >= duration) Release();
    ↓
[View.Release] enabled = false → EffectSpawn.Release(this)
    ↓
[Pool] Return:
    ├── _activePrefab.Remove(view)
    ├── view.transform.SetParent(Storage)            ← parent inactive → Unity fires OnDisable
    ├── _idle[prefab].Push(view)
    ↓
[Unity] OnDisable fires automatically:
    └── tweenerHub.Back(skip: true)                   ← snap animation back to the start
```

On `GameController.OnGameStateChanged` → `Paused`:
```
[Bus] OnGameStateChanged() → state == Paused
    ↓
[Pool] PauseAll: foreach view in _activePrefab → view.OnPause() → _paused = true
    ↓
[View.Update] starts accumulating _pausedAccum, no Release
```

On unpause — `Pool.ResumeAll`, `_paused = false`, the duration counter resumes.

---

## Usage

### Simplest — bullet explodes

```csharp
public class Bullet : MonoBehaviour
{
    [SerializeField] private GameObject explosionPrefab;

    private void OnCollisionEnter(Collision c)
    {
        // position = bullet.position (default), rotation = identity (default)
        EffectSpawn.Spawn(transform, explosionPrefab);
        Destroy(gameObject);
    }
}
```

### By key from the catalog with custom rotation

```csharp
public class Weapon : MonoBehaviour
{
    [EffectKey] [SerializeField] private string muzzleFlash;
    [EffectKey] [SerializeField] private string impactSparks;

    // Default position (muzzle.position), default rotation (identity)
    private void Fire(Transform muzzle) => EffectSpawn.Spawn(muzzle, muzzleFlash);

    // Custom position (hit point) and rotation (aligned to surface normal)
    private void OnImpact(Transform owner, Vector3 hitPoint, Vector3 normal)
        => EffectSpawn.Spawn(owner, impactSparks, hitPoint, Quaternion.LookRotation(normal));
}
```

### Catalog registration at startup

```csharp
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private EffectsCatalog effectsCatalog;

    private void Awake()
    {
        EffectSpawn.RegisterCatalog(effectsCatalog);
    }
}
```

### EffectsLayer in the scene

```
Scene Root
├── World
│   ├── Player
│   ├── Enemy
│   └── ...
└── Visuals
    └── EffectsLayer  (EffectsLayer component, Target = self)
        └── (all scene effects park here)
```

If a particular enemy needs effects to stay local (move with it):
```
Enemy
├── EffectsLayer  (Target = LocalEffects)
│   └── LocalEffects
│       └── (effects spawned on this enemy park here)
└── Mesh
```

---

## Edge cases

| Scenario | Behavior |
|---|---|
| `Spawn(null, ...)` | `Logger.Error` + null. target is required by contract |
| `Spawn(target, prefab=null)` | `Logger.Error` + null |
| `Spawn(target, "unknown-key")` | `Logger.Error` + null. Key not found in catalog |
| `Spawn(target, key)` without a registered catalog | `Logger.Error` + null. Spawn-by-prefab still works |
| Prefab without `EffectView` | `Logger.Error` + Destroy of the instance; null returned |
| `EffectsLayer.Target` not set | Parking into the marker's transform |
| `EffectsLayer` on an inactive object | `GetComponentInParent` skips it (default), fallback to target |
| Multiple `EffectsLayer`s in the parent chain | Closest one wins (default `GetComponentInParent` behavior) |
| Spawn during `GameStates.Paused` | `OnEnable` immediately freezes the view (`_paused = true`); the duration counter waits for unpause |
| Double `view.Release()` | Second call: `enabled` is already false → early return |
| TweenerHub pause | Not implemented in v1 — TweenerHub continues to play. Depends on Pause/Resume support in TweenerHub (TODO) |
| `Object.Destroy(view.gameObject)` from outside | Next Acquire `_idle.Pop()` returns a null reference, Pool drops it and Instantiates a new one |
| Editor domain reload | Static fields reset, `_pool = null`, lazy-create on next Spawn |

---

## Editor side of the catalog

`EffectsCatalog` uses Odin attributes directly on the SO — there is no separate `CustomEditor`:

- `[InfoBox]` on the `effects` array explaining "key = prefab.name";
- `[Button] Scan Project` — `AssetDatabase.FindAssets("t:Prefab")` + filter by `GetComponent<EffectView>()`, adds new ones without removing existing, result logged to Console;
- `[Button] Validate` — checks for null entries, missing `EffectView`, name duplicates; result is `Debug.Log` (success) or `Debug.LogError` (list of issues);
- `[ShowInInspector] IndexPreview` — read-only dictionary "key → asset path", always visible in the inspector under the buttons; rendered by Odin's DictionaryDrawer.

`EffectKeyAttributeDrawer` — popup of all keys. Sources:
- if a catalog is registered in `EffectSpawn` → uses `AllKeys`;
- otherwise (Editor mode without registration) → unions keys from all `EffectsCatalog` assets in the project via `AssetDatabase`.

---

## Public API

```csharp
namespace Vortex.Sdk.EffectSpawnSystem.Bus
{
    public static class EffectSpawn
    {
        public static IReadOnlyList<string> AllKeys { get; }
        public static bool IsPaused { get; }

        public static void RegisterCatalog(EffectsCatalog catalog);
        public static void UnregisterCatalog(EffectsCatalog catalog);

        public static EffectView Spawn(Transform target, GameObject prefab,
            Vector3? position = null, Quaternion? rotation = null);
        public static EffectView Spawn(Transform target, string key,
            Vector3? position = null, Quaternion? rotation = null);
        public static void Release(EffectView view);
    }
}

namespace Vortex.Sdk.EffectSpawnSystem.Components
{
    public class EffectView : MonoBehaviour
    {
        public float Duration { get; }
        public void Release();
    }

    public sealed class EffectsLayer : MonoBehaviour
    {
        public Transform Target { get; }
    }
}

namespace Vortex.Sdk.EffectSpawnSystem.Catalog
{
    public class EffectsCatalog : ScriptableObject
    {
        public IReadOnlyList<string> Keys { get; }
        public IReadOnlyList<GameObject> Effects { get; }
        public GameObject GetPrefab(string key);
    }
}

namespace Vortex.Sdk.EffectSpawnSystem.Attributes
{
    public class EffectKeyAttribute : PropertyAttribute { }
}
```

`EffectPool` is internal, not part of the public API.
