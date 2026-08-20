# StateValve (Unity)

**Namespace:** `Vortex.Unity.StateValve`
**Сборка:** `ru.vortex.unity.statevalve`

## Назначение

Unity-обёртка над Core-`StateValve`: `MonoBehaviour`-хендлер с настройкой в инспекторе, whitelist-фильтром входящих ключей и слабой привязкой через `IDataStorage`. Примитив (`StateValve`, `ValveMode`, свёртка, таблицы истинности, ре-энтрантность) — в Core-README.

## Зависимости

- `ru.vortex.core.statevalve` — `StateValve`, `ValveMode`
- `ru.vortex.extensions` — `BoolData`
- `ru.vortex.system` — `IDataStorage` / `IDataSource`

## `StateValveHandler : MonoBehaviour, IStateValve, IDataStorage`

**Поля инспектора:** `mode : ValveMode`, `invert : bool`, `whiteList : string[]`.

- **`Awake`** — создаёт `StateValve(mode, invert)`, поднимает `OnUpdateLink` (ссылки готовы).
- **whitelist-фильтр** — непустой список: входящий `Open/Close` с ключом не из списка отклоняется с `Debug.LogError` (ошибка проводки не должна прятаться). Пустой список — фильтра нет. Пустой/`null` ключ уходит к ядру → fail-fast.
- **`GetWhiteList()`** → whitelist — источник дропдауна у запирающих вьюшек.
- **`IStateValve`** — `Open` / `Close` / `State` / `GetWhiteList`, точка ссылки для производителей и потребителей.
- **`IDataStorage`** — слабая привязка: `GetData<IStateValve>()` → сам хендлер (производителям), `GetData<BoolData>()` → `State` (потребителям). `OnUpdateLink` — один раз в `Awake` (link-level: ссылки готовы; значение `State` слушают через её `OnUpdate`).
- **Инспектор-отладка** — рантайм-список `Keys` (`[ShowInInspector, ReadOnly]`, только Play Mode).

## Применение: клапан паузы (пример, вне пакета)

Пакет нейтрален; интеграция паузы — на проектном уровне.

- Один `StateValveHandler` на систему, режим `And`: «идёт» = все ключи открыты; `State == closed` → пауза.
- **Производители-держатели** (тутор, компонент отсчёта) зовут `Close(key)` на входе и `Open(key)` на выходе.
- **Потребитель** подписан на `State` → держит паузу, пока закрыт хоть один ключ.
- **Хэндофф «тутор → отсчёт» без дырки:** порядок вызовов не важен — пока закрыт любой ключ, итог закрыт.
