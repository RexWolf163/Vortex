# StateAxisSystem (Unity)

**Namespace:** `Vortex.Unity.StateAxisSystem.*`
**Сборки:** `ru.vortex.unity.stateaxis` (runtime), `ru.vortex.unity.stateaxis.editor` (Editor-only)

---

## Назначение

Unity-обвязка над абстракцией `StateAxis` (Layer 1):

- Парный ассет `StateAxisPreset` — источник истины для генерации.
- Кодогенерация `.cs` рядом с ассетом по кнопке Save в кастом-инспекторе.
- Жизненный цикл генерата: смена имени → удаление старого, удаление пресета → удаление парного `.cs`.
- Принудительная инициализация всех `StateAxis`-наследников при старте (рантайм + Editor).
- Inspector-атрибут `[StateKey(typeof(MoveState))]` с дропдауном допустимых ключей.
- Мост `StateValueSwitcherHandler` для связки `StateValue<T>` → `UIStateSwitcher`.
- Editor-валидация согласованности пресет ↔ сгенерированный класс при входе в Play Mode.
- Окно поиска беспризорных `.cs`-файлов (без парного пресета).

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.StateAxisSystem` | `StateAxis`, `StateValue<T>` |
| `Vortex.Core.Extensions.ReactiveValues` | `IReactiveData` для подписки в `StateValueSwitcherHandler` |
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` для моста |
| Sirenix Odin Inspector | `[ValueDropdown]` в `StateValueSwitcherHandler` |

---

## Workflow

### Создание оси

1. **Project window → Create → Vortex → StateAxis Preset.**
   Создаётся `.asset` в выбранной папке.
2. В инспекторе пресета заполнить:
   - **Axis Name** — имя класса, PascalCase, валидный C#-идентификатор (например, `MoveState`).
   - **Target Namespace** — namespace класса (например, `MyGame.States`).
   - **Keys** — упорядоченный список ключей (например, `Idle`, `Walk`, `Run`, `Jump`).
3. **Кнопка Save.**
   Генерируется `{папка_ассета}/{AxisName}.cs` с классом, статическими readonly-инстансами,
   `All` свойством и nested-меню `Vortex/StateAxis/{AxisName}` для быстрого открытия пресета.

### Редактирование оси

1. Найти пресет через меню **Vortex/StateAxis/{AxisName}** (создаётся автоматически
   в каждом сгенерированном классе) или вручную в Project window.
2. Изменить поля → **Save**.
3. Если **Axis Name** изменилось — старый `.cs` удаляется, новый создаётся.

### Восстановление пресета из кода (Load)

Если пресет удалён или потерян, но `.cs` существует:
1. Создать новый пресет.
2. Указать тот же Axis Name и Namespace, что в существующем классе.
3. **Кнопка Load.** Считывает ключи из текущего класса через рефлексию и записывает в пресет.
4. Установить `lastGeneratedPath` через **Save** (Save при этом перегенерирует .cs идентичным содержанием).

### Удаление оси

Удаление пресета через Project window → парный `.cs` удаляется автоматически
через `StateAxisAssetPostprocessor.OnWillDeleteAsset`.

Если `.cs` оставили вручную или удалили без пресета — используется
**Tools → Vortex → StateAxis → Find orphans** для поиска и зачистки.

---

## Архитектура

### Runtime

```
Presets/
  StateAxisPreset                — ScriptableObject: AxisName, Namespace, Keys[], LastGeneratedPath

Initialization/
  StateAxisInitializer            — [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] +
                                    [InitializeOnLoadMethod] (Editor) — RunClassConstructor для
                                    всех не-абстрактных StateAxis-наследников

Attributes/
  StateKeyAttribute               — [StateKey(typeof(TAxis))] для string-полей в Inspector

Handlers/
  StateValueSwitcherHandler       — MonoBehaviour: source + property name + UIStateSwitcher;
                                    подписка на IReactiveData.OnUpdateData,
                                    switcher.Set(stateValue.Index) при изменении
```

### Editor

```
StateAxisCodeGenerator            — Generate(name, ns, keys) → .cs string; валидация имён
StateAxisPresetEditor             — Custom inspector с Save/Load, live-чисткой точек
StateAxisAssetPostprocessor       — OnWillDeleteAsset → удаление парного .cs
StateAxisOrphanFinder             — окно "Find orphans"
StateAxisValidator                — [InitializeOnLoad], хук на playModeStateChanged → ValidateAll
                                    + меню Tools/Vortex/StateAxis/Validate all presets
StateKeyAttributeDrawer           — popup с ключами оси для [StateKey]
```

### Поток генерации

```
Save в Inspector
  ↓
Validate (имена, дубликаты, пустые ключи)
  ↓
Если LastGeneratedPath ≠ {folder}/{AxisName}.cs → DeleteAsset(старый)
  ↓
StateAxisCodeGenerator.Generate(...) → string
  ↓
File.WriteAllText("{folder}/{AxisName}.cs", content)
  ↓
preset.lastGeneratedPath = новый путь
  ↓
AssetDatabase.SaveAssets() + Refresh() → перекомпиляция
  ↓
Static-инициализаторы класса → регистрация в StateAxis-реестре
```

### Поток валидации (вход в Play Mode)

```
EditorApplication.playModeStateChanged → ExitingEditMode
  ↓
Все StateAxisPreset-ассеты в проекте
  ↓
Для каждого: резолвить тип по {Namespace}.{AxisName}
  ↓
RuntimeHelpers.RunClassConstructor → реестр заполнен
  ↓
Сравнение: множество preset.Keys vs StateAxis.GetAll(type).Select(s => s.Key)
  ↓
Расхождение → Debug.LogError(preset, "what's missing on each side")
```

Валидация **не блокирует** вход в Play — это решение разработчика.

---

## Шаблон сгенерированного класса

```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//   Этот файл сгенерирован Vortex StateAxis Code Generator.
//   НЕ РЕДАКТИРОВАТЬ ВРУЧНУЮ. Все ручные изменения будут потеряны при следующей
//   регенерации. Для изменения набора значений отредактируйте парный пресет
//   (MoveState.asset) в инспекторе и нажмите Save.
// </auto-generated>
//------------------------------------------------------------------------------

using System.Collections.Generic;
using Vortex.Core.StateAxisSystem.Abstractions;

namespace MyGame.States
{
    public sealed class MoveState : StateAxis
    {
        public static readonly MoveState Idle = new(nameof(Idle), 0);
        public static readonly MoveState Walk = new(nameof(Walk), 1);
        public static readonly MoveState Run  = new(nameof(Run),  2);
        public static readonly MoveState Jump = new(nameof(Jump), 3);

        public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

        private MoveState(string key, int order) : base(key, order) { }

#if UNITY_EDITOR
        private static class EditorMenu
        {
            [UnityEditor.MenuItem("Vortex/StateAxis/MoveState")]
            private static void OpenPreset()
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("MoveState t:StateAxisPreset");
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var preset = UnityEditor.AssetDatabase.LoadAssetAtPath<Vortex.Unity.StateAxisSystem.Presets.StateAxisPreset>(path);
                    if (preset != null && preset.AxisName == "MoveState")
                    {
                        UnityEditor.Selection.activeObject = preset;
                        UnityEditor.EditorGUIUtility.PingObject(preset);
                        return;
                    }
                }
            }
        }
#endif
    }
}
```

Меню `Vortex/StateAxis/{AxisName}` появляется автоматически по факту перекомпиляции —
никаких отдельных хаков с `Menu.AddMenuItem` не используется.

---

## Inspector-атрибут `[StateKey]`

```csharp
public class CharacterPreset : ScriptableObject
{
    [StateKey(typeof(MoveState))]
    [SerializeField] private string defaultMoveState;

    public MoveState GetDefault() => StateAxis.GetByKey<MoveState>(defaultMoveState);
}
```

Drawer показывает popup с ключами оси, считанными из `StateAxis.GetAll(typeof(MoveState))`.
Перед обращением к реестру делается `RuntimeHelpers.RunClassConstructor` —
дропдаун работает в Editor-режиме сразу после открытия Inspector,
без необходимости запуска Play.

---

## `StateValueSwitcherHandler` — мост

Конфигурация в инспекторе:
- **source** — MonoBehaviour, на котором есть свойство типа `StateValue<TAxis>`.
- **property** — имя свойства (выпадашка фильтрует по типу).
- **switcher** — целевой `UIStateSwitcher`.

При изменении значения подписка на `IReactiveData.OnUpdateData` вызывает
`switcher.Set(stateValue.Index)`. Связь между ключом и слотом switcher'а — через
осевой `Order`, заданный в пресете. Слоты switcher'а должны быть в том же порядке,
что и ключи в пресете.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Save в пустом пресете (Axis Name пустой) | Диалог с ошибками валидации, файл не создаётся |
| Save с дубликатом ключа | Диалог, файл не создаётся |
| Save при ренейме AxisName | Старый `.cs` удаляется, новый создаётся, `lastGeneratedPath` обновляется |
| Save без сохранённого пресета (in-memory `.asset`) | Диалог "Пресет не сохранён в проекте" |
| Load до первого Save | Диалог "Тип не найден. Сначала Save" |
| Load для типа, который не наследует StateAxis | Диалог "не наследует StateAxis" |
| Удаление пресета | `.cs` удаляется автоматически через AssetPostprocessor |
| Удаление `.cs` вручную (мимо пресета) | Validator при Play выдаст error: "тип не существует" |
| Расхождение пресет ↔ класс | Validator при Play печатает оба расхождения, не блокируя вход |
| Find orphans без пресетов в проекте | Окно показывает "беспризорных не найдено" |
| `[StateKey]` для оси без значений | Popup пустой, рисуется HelpBox "Сохраните пресет" |
| `StateValueSwitcherHandler` без source/property | Awake логирует ошибку, `enabled = false` |

---

## Контракт публичного API

```csharp
// Runtime
namespace Vortex.Unity.StateAxisSystem.Presets
{
    public class StateAxisPreset : ScriptableObject
    {
        public string AxisName { get; }
        public string TargetNamespace { get; }
        public IReadOnlyList<string> Keys { get; }
        public string LastGeneratedPath { get; }
    }
}

namespace Vortex.Unity.StateAxisSystem.Initialization
{
    public static class StateAxisInitializer
    {
        public static void Initialize();
    }
}

namespace Vortex.Unity.StateAxisSystem.Attributes
{
    public class StateKeyAttribute : PropertyAttribute
    {
        public Type AxisType { get; }
        public StateKeyAttribute(Type axisType);
    }
}

namespace Vortex.Unity.StateAxisSystem.Handlers
{
    public class StateValueSwitcherHandler : MonoBehaviour { }
}
```

Editor-классы (`StateAxisCodeGenerator`, `StateAxisPresetEditor`, …) — internal.
