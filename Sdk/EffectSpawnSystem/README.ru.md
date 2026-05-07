# EffectSpawnSystem

**Namespace:** `Vortex.Sdk.EffectSpawnSystem.*`
**Сборка:** `ru.vortex.sdk.effectspawn`

---

## Назначение

Пул GameObject'ов для визуальных эффектов «выстрел и забыл» (взрывы, искры, попадания, облачка пыли). Spawn → проигрывание → авто-возврат в пул, без аллокаций в hot path.

Управление визуалом — через `TweenerHub` на префабе (он сам дёргает Animator/Spine/ParticleSystem). Сама система пула не знает, чем именно играет эффект — только запускает Forward/Back на TweenerHub при активации/деактивации.

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `Vortex.Sdk.Core.GameCore` | `GameController.OnGameStateChanged`, `GameStates.Paused` — для broadcast паузы активным эффектам |
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` — анимационная сторона эффекта |

---

## Архитектура

### Двухслойное хранилище (lazy auto-create)

```
[EffectPool]                          ← root, active, DontDestroyOnLoad
    └── Storage                       ← child, INACTIVE — все idle-инстансы тут
            ├── ExplosionPrefab(Clone)    (parent inactive → ребёнок не Update'ит)
            ├── DustPrefab(Clone)
            └── ...
```

`EffectPool` создаётся **lazy** при первом обращении в `EffectSpawn` (по образцу `MapLevelsController.VoidParent`). Никаких сценных объектов руками — пул возникает сам, когда нужен.

**Idle**: ребёнок неактивного `Storage` → `activeInHierarchy = false` → `Update`/`OnEnable` не вызываются. Никаких ручных `SetActive`.

**Active**: при `Spawn` инстанс перекладывается из `Storage` в активный target → Unity сам вызовет `OnEnable` на `EffectView`.

**Возврат**: при `Release` инстанс перекладывается обратно в `Storage` → Unity сам вызовет `OnDisable`.

### Поиск parent для активного эффекта

Spawn принимает обязательный `target` — `Transform` заказчика (где визуально появляется эффект). Где он будет припаркован в иерархии:

```
1. owner.GetComponentInParent<EffectsLayer>()      ← поиск маркера вверх по цепочке
2. layer != null:
   ├── layer.Target != null  → паркуем в layer.Target
   └── layer.Target == null  → паркуем в layer.transform
3. layer == null              → fallback: паркуем в сам target
```

Позиция/ротация всегда копируются из `target`. Эффект ставится в **низ списка детей** (`SetAsLastSibling`).

### EffectsLayer — маркер парковки

Пустой компонент-маркер на сцене (по образцу `MapsView`). Обозначает «парковать сюда эффекты любого потомка». Опциональное поле `Target` — куда фактически класть, если оно не задано — в transform самого маркера.

Типичные сценарии:
- Один `EffectsLayer` на корне сцены — все эффекты сцены идут туда.
- `EffectsLayer` на под-сцене (логически удобно) с `Target` на дочерний `Visuals/EffectsParent` (визуально удобно).
- `EffectsLayer` на враге → его-личные хиты прицепляются к локальному узлу, а не куда-то вверх по сцене.

### EffectView — на префабе

```
EffectView (RequireComponent TweenerHub)
  ├── duration: float                 — длительность активного цикла, unscaled-время
  ├── tweenerHub: TweenerHub
  │
  ├── OnEnable  → enabled=true, _spawnTime=Time.unscaledTime, tweenerHub.Forward()
  │              + проверка EffectSpawn.IsPaused → старт сразу в _paused=true если игра в паузе
  ├── OnDisable → tweenerHub.Back(skip:true)
  ├── Update    → отсчитывает duration в unscaled-времени, копит _pausedAccum при паузе,
  │               по достижении вызывает Release()
  └── Release() → enabled=false (флаг "уже освобождён в этом цикле")
                  + EffectSpawn.Release(this)
```

Никаких `LifetimeStrategy`/`maxPoolSize`/`prewarmCount`/`useUnscaledTime` — только `duration` и связь с `TweenerHub`. Активация всегда через `OnEnable` (parent stack даёт это автоматически).

### EffectsCatalog — индекс по ключу

```
EffectsCatalog : ScriptableObject
  ├── effects: GameObject[]
  ├── Keys → IReadOnlyList<string>     ← prefab.name каждого
  ├── GetPrefab(key)
  └── ScanProject() / Validate()        — Editor-only
```

Регистрируется явно: `EffectSpawn.RegisterCatalog(catalog)` из стартового кода.

`[EffectKey]` атрибут на string-поле даёт popup из `EffectSpawn.AllKeys`. В Editor-mode без регистрации drawer собирает union ключей из всех `EffectsCatalog`-ассетов проекта (через AssetDatabase).

### EffectSpawn — Bus

```
EffectSpawn (static)
  ├── RegisterCatalog(catalog) / UnregisterCatalog(catalog)
  ├── AllKeys → IReadOnlyList<string>
  ├── IsPaused → GameController.GetState() == Paused
  │
  ├── Spawn(target, prefab) → EffectView      ← target обязателен
  ├── Spawn(target, key)    → EffectView
  ├── Release(view)
  │
  └── (static-ctor)
      └── GameController.OnGameStateChanged += OnGameStateChanged
              ├── Paused  → Pool.PauseAll()
              └── иначе   → Pool.ResumeAll()
```

Spawn первым параметром принимает `target` — без него API не вызывается (Logger.Error + null).

---

## Поток

```
GAME-CODE
    | EffectSpawn.Spawn(bullet.transform, "Explosion")
    v
[Bus] catalog.GetPrefab("Explosion") → prefab
    v
[Pool] Acquire:
    ├── stack.Pop() или Instantiate(prefab, Storage)
    ├── ResolveLayerTarget(target): GetComponentInParent<EffectsLayer> → Target | layer.transform | target
    ├── view.transform.SetParent(layer)             ← parent active → Unity дёрнет OnEnable
    ├── SetAsLastSibling
    ├── SetPositionAndRotation(target.position, target.rotation)
    ↓
[Unity] OnEnable вызывается автоматически:
    ├── enabled = true (сброс с прошлого цикла)
    ├── _spawnTime = Time.unscaledTime
    ├── _paused = EffectSpawn.IsPaused              ← при спауне в паузе сразу замораживается
    ├── tweenerHub.Forward()
    ↓
[View.Update] каждый кадр:
    ├── if (_paused) _pausedAccum += unscaledDeltaTime; return;
    ├── if (now - _spawnTime - _pausedAccum >= duration) Release();
    ↓
[View.Release] enabled = false → EffectSpawn.Release(this)
    ↓
[Pool] Return:
    ├── _activePrefab.Remove(view)
    ├── view.transform.SetParent(Storage)            ← parent inactive → Unity дёрнет OnDisable
    ├── _idle[prefab].Push(view)
    ↓
[Unity] OnDisable вызывается автоматически:
    └── tweenerHub.Back(skip: true)                   ← мгновенный сброс анимации в исходное
```

При `GameController.OnGameStateChanged` → `Paused`:
```
[Bus] OnGameStateChanged() → state == Paused
    ↓
[Pool] PauseAll: foreach view in _activePrefab → view.OnPause() → _paused = true
    ↓
[View.Update] начинает копить _pausedAccum, не Release'ит
```

При выходе из паузы — `Pool.ResumeAll`, `_paused = false`, отсчёт duration возобновляется.

---

## Использование

### Простой случай — снаряд взрывается

```csharp
public class Bullet : MonoBehaviour
{
    [SerializeField] private GameObject explosionPrefab;

    private void OnCollisionEnter(Collision c)
    {
        EffectSpawn.Spawn(transform, explosionPrefab);
        Destroy(gameObject);
    }
}
```

### По ключу из каталога

```csharp
public class Weapon : MonoBehaviour
{
    [EffectKey] [SerializeField] private string muzzleFlash;
    [EffectKey] [SerializeField] private string impactSparks;

    private void Fire(Transform muzzle) => EffectSpawn.Spawn(muzzle, muzzleFlash);
    private void OnImpact(Transform hit) => EffectSpawn.Spawn(hit, impactSparks);
}
```

### Регистрация каталога при старте

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

### EffectsLayer на сцене

```
Scene Root
├── World
│   ├── Player
│   ├── Enemy
│   └── ...
└── Visuals
    └── EffectsLayer  (EffectsLayer-компонент, Target = self)
        └── (сюда паркуются все эффекты сцены)
```

Если у конкретного врага нужно держать эффекты локально (двигаются вместе с ним):
```
Enemy
├── EffectsLayer  (Target = LocalEffects)
│   └── LocalEffects
│       └── (сюда паркуются эффекты, заспауненные на этом враге)
└── Mesh
```

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| `Spawn(null, ...)` | `Logger.Error` + null. target обязателен по контракту |
| `Spawn(target, prefab=null)` | `Logger.Error` + null |
| `Spawn(target, "unknown-key")` | `Logger.Error` + null. Ключ не найден в каталоге |
| `Spawn(target, key)` без зарегистрированного каталога | `Logger.Error` + null. Spawn-by-prefab продолжает работать |
| Префаб без `EffectView` | `Logger.Error` + Destroy инстанса; null возврат |
| `EffectsLayer.Target` не задан | Парковка в transform самого маркера |
| `EffectsLayer` на неактивном объекте | `GetComponentInParent` его пропустит (по умолчанию), fallback на target |
| Несколько `EffectsLayer` в parent-цепочке | Берётся ближайший (поведение `GetComponentInParent` по умолчанию) |
| Spawn во время `GameStates.Paused` | `OnEnable` сразу замораживает view (`_paused = true`); счётчик duration ждёт unpause |
| Двойной `view.Release()` | Второй вызов: `enabled` уже false → ранний return |
| Pause TweenerHub'а | Не реализовано в первой версии — TweenerHub продолжает играть. Завязано на наличие Pause/Resume в TweenerHub (TODO) |
| `Object.Destroy(view.gameObject)` извне | При следующем Acquire `_idle.Pop()` отдаст null-ссылку, Pool отбросит её и Instantiate новый |
| Перезагрузка домена в Editor | static-поля сбросятся, `_pool = null`, lazy-create при следующем Spawn |

---

## Editor-сторона каталога

`EffectsCatalogEditor` — custom inspector:
- стандартный массив `effects`;
- кнопка **Scan Project** — `AssetDatabase.FindAssets("t:Prefab")` + фильтр `GetComponent<EffectView>()`, добавляет новые без удаления существующих;
- кнопка **Validate** — проверка null-элементов, отсутствующих `EffectView`, дубликатов имён; результат в Console;
- live-таблица «key → asset path» под массивом.

`EffectKeyAttributeDrawer` — popup со всеми ключами. Источники:
- если каталог зарегистрирован в `EffectSpawn` → берёт `AllKeys`;
- иначе (Editor-mode без регистрации) → собирает union из всех `EffectsCatalog`-ассетов проекта через `AssetDatabase`.

---

## Контракт публичного API

```csharp
namespace Vortex.Sdk.EffectSpawnSystem.Bus
{
    public static class EffectSpawn
    {
        public static IReadOnlyList<string> AllKeys { get; }
        public static bool IsPaused { get; }

        public static void RegisterCatalog(EffectsCatalog catalog);
        public static void UnregisterCatalog(EffectsCatalog catalog);

        public static EffectView Spawn(Transform target, GameObject prefab);
        public static EffectView Spawn(Transform target, string key);
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

`EffectPool` — internal, не входит в публичный API.
