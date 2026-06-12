# ExtensibleEnumSystem (Unity)

**Namespace:** `Vortex.Unity.ExtensibleEnumSystem.*`
**Сборка:** `ru.vortex.unity.extenums` (Editor-only логика под `#if UNITY_EDITOR` в том же asmdef)

---

## Назначение

Тонкая Unity-обвязка над абстракцией `ExtensibleEnum` (Layer 1):

- Inspector-атрибут `[ExtEnumKey(typeof(MoveState))]` с дропдауном допустимых ключей.
- Мост `ExtensibleEnumValueSwitcherHandler` для связки `ExtEnumData<T>` → `UIStateSwitcher` через рефлексию.

Eager-инициализация реестра (`[RuntimeInitializeOnLoadMethod]` + `[InitializeOnLoadMethod]`) уже выполняется в L1 — отдельный Unity-инициализатор не нужен.

Вне ответственности:

- Кодогенерация, парные SO-ассеты, валидаторы — **отсутствуют намеренно**. Конкретные `ExtensibleEnum`-наследники пишутся руками (см. README Layer 1).
- UI-интеграция (overload `UIStateSwitcher.Set(ExtensibleEnum)` и расширение `StateSwitcherAttribute`) — на стороне пакета `Vortex.Unity.UI.StateSwitcher`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.ExtensibleEnumSystem` | `ExtensibleEnum`, `ExtEnumData<T>` |
| `Vortex.Core.Extensions.ReactiveValues` | `IReactiveData` для подписки в `ExtensibleEnumValueSwitcherHandler` |
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` для моста |

---

## Архитектура

```
Attributes/
  ExtEnumKeyAttribute               — [ExtEnumKey(typeof(TEnum))] для string-полей в Inspector;
                                      хранит ExtEnumType, валидирует наследование от ExtensibleEnum

Handlers/
  ExtensibleEnumValueSwitcherHandler — MonoBehaviour: source + property name + UIStateSwitcher;
                                       подписка на IReactiveData.OnUpdateData,
                                       switcher.Set(extEnumData.Index) при изменении

Editor/                              (под #if UNITY_EDITOR, в том же runtime asmdef)
  ExtensibleEnumKeyAttributeDrawer  — popup с ключами для [ExtEnumKey].
                                      Реестр ExtensibleEnum уже наполнен eager-инициализацией
                                      в L1 — drawer просто читает GetAll(Type).
```

---

## `[ExtEnumKey]` Inspector-атрибут

```csharp
public class CharacterPreset : ScriptableObject
{
    [ExtEnumKey(typeof(MoveState))]
    [SerializeField] private string defaultMoveState;

    public MoveState GetDefault() => ExtensibleEnum.GetByKey<MoveState>(defaultMoveState);
}
```

Drawer показывает popup с ключами, считанными из `ExtensibleEnum.GetAll(typeof(MoveState))`. Реестр уже наполнен L1-инициализатором при загрузке домена / старте сцены — никаких отдельных триггеров на стороне drawer'а не требуется.

Конструктор атрибута проверяет, что переданный `Type` наследует `ExtensibleEnum`; в противном случае — `ArgumentException`.

---

## `ExtensibleEnumValueSwitcherHandler` — мост

Late-binding мост `ExtEnumData<T>` → `UIStateSwitcher`. Конфигурируется в инспекторе:

- **source** — MonoBehaviour-источник, на котором есть свойство типа `ExtEnumData<TEnum>`.
- **property** — имя этого свойства.
- **switcher** — целевой `UIStateSwitcher`.

Жизненный цикл:
- `Awake` — резолвит `PropertyInfo` для `property`, читает значение в `_extEnumData`, кеширует `Index`-property через рефлексию, подписывается на `IReactiveData.OnUpdateData`. На любую ошибку — `Debug.LogError` + `enabled = false`.
- `Start` — первый `OnDataChanged()` (после всех `Awake` сцены потребитель уже подписался на switcher).
- `OnDataChanged` — читает `Index` через рефлексию, при `index >= 0` дёргает `switcher.Set(index)`.
- `OnDestroy` — отписка + зануление кеша.

Связь между ключом и слотом switcher'а — через `Index` значения: handler читает `ExtEnumData.Index` и при `index >= 0` дёргает `switcher.Set(index)`. Слоты switcher'а должны быть в том же порядке, что и значения в типе.

Editor-helper `GetExtEnumDataProperties()` под `#if UNITY_EDITOR` собирает список свойств `source`, тип которых — наследник `ExtEnumData<>`. Готов для подключения к будущему drawer'у с дропдауном (сейчас поле `property` — обычный string-input).

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `[ExtEnumKey]` для не-`ExtensibleEnum` типа | `ArgumentException` в конструкторе атрибута |
| `[ExtEnumKey]` на не-string поле | Drawer пишет `"[ExtEnumKey] only on string fields"` |
| `[ExtEnumKey]` для типа без значений | Popup пустой, рисуется warning-helpbox |
| `ExtensibleEnumValueSwitcherHandler` без source / property | Awake логирует ошибку, `enabled = false` |
| `property` у source не найден / возвращает null / не `ExtEnumData<>` | Awake логирует ошибку, `enabled = false` |
| `ExtEnumData.Index == -1` (значение не зарегистрировано) | Switcher не переключается, no-op |

---

## Контракт публичного API

```csharp
namespace Vortex.Unity.ExtensibleEnumSystem.Attributes
{
    public class ExtEnumKeyAttribute : PropertyAttribute
    {
        public Type ExtEnumType { get; }
        public ExtEnumKeyAttribute(Type extEnumType);
    }
}

namespace Vortex.Unity.ExtensibleEnumSystem.Handlers
{
    public class ExtensibleEnumValueSwitcherHandler : MonoBehaviour { }
}
```

Editor-классы (`ExtensibleEnumKeyAttributeDrawer`) — internal, под `#if UNITY_EDITOR` в том же asmdef.
