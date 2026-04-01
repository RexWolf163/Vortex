# LogicConditionsSystem

**Namespace:** `Vortex.Unity.LogicConditionsSystem.Conditions`
**Сборка:** `ru.vortex.unity.logicconditions`

## Назначение

Unity-реализации условий (`Condition`) для системы логических цепочек (`LogicChains`). Три готовых условия для типовых сценариев ожидания: таймер, загрузка сцены, инициализация приложения.

Возможности:
- Ожидание по таймеру с произвольной задержкой
- Ожидание загрузки конкретной сцены
- Ожидание завершения инициализации всех систем (`AppStates.Running`)
- Абстрактная база `UnityCondition` для создания пользовательских условий

Вне ответственности:
- Оркестрация цепочек (реализована в `Vortex.Core.LogicChainsSystem`)
- Доменно-специфичные условия (уровень 3/4)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LogicChainsSystem` | `Condition` — базовый абстрактный класс |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `AppStates` |
| `Vortex.Unity.AppSystem` | `TimeController.Call()`, `TimeController.RemoveCall()` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` — отображение имени в Inspector |
| Odin Inspector | `[ShowInInspector]`, `[DisplayAsString]`, `[MinValue]`, `[ValueDropdown]` |

---

## Архитектура

```
LogicConditionsSystem/
└── Conditions/
    ├── _UnityCondition.cs      # Абстрактная база: Inspector-отображение
    ├── MinTimeCondition.cs     # Ожидание по таймеру
    ├── SceneLoaded.cs          # Ожидание загрузки сцены
    └── SystemsLoaded.cs        # Ожидание AppStates.Running
```

### Базовый контракт (Core)

`Condition` (из `Vortex.Core.LogicChainsSystem.Model`):
- `Init(Action callback)` — инициализация, вызывает `Start()`
- `Start()` — абстрактный хук настройки (подписки, начальная проверка)
- `Check()` — текущее состояние условия
- `DeInit()` — очистка (отписки, таймеры)
- `RunCallback()` — сигнал выполнения условия

### UnityCondition (абстрактная)

Обёртка над `Condition`, добавляет Inspector-визуализацию через `[ClassLabel("@ConditionName")]`. Наследники реализуют `ConditionName` для отображения имени в списке условий.

---

## Условия

### MinTimeCondition

Ожидание заданного количества секунд.

| Поле | Тип | Описание |
|------|-----|----------|
| `seconds` | `float` | Задержка (≥ 0) |

При `Start()` вычисляет целевое время (`DateTime.UtcNow + seconds`). Планирует проверку через `TimeController.Call()` с owner-привязкой (замена предыдущего вызова от того же owner). При достижении целевого времени вызывает `RunCallback()`. Если `seconds = 0`, срабатывает немедленно.

### SceneLoaded

Ожидание загрузки конкретной сцены.

| Поле | Тип | Описание |
|------|-----|----------|
| `SceneName` | `string` | Имя сцены (`[ValueDropdown]` из Build Settings) |

При `Start()` проверяет активную сцену. Если целевая сцена уже загружена — `RunCallback()` немедленно. Иначе подписывается на `SceneManager.sceneLoaded` и ожидает совпадения имени. После срабатывания отписывается.

### SystemsLoaded

Ожидание завершения инициализации приложения.

Без параметров. При `Start()` проверяет `App.GetState() == AppStates.Running`. Если уже `Running` — `RunCallback()` немедленно. Иначе подписывается на `App.OnStateChanged`.

---

## Контракт

### Вход
- `Condition.Init(Action callback)` — вызывается системой `LogicChains`
- Конфигурация через сериализованные поля (Inspector)

### Выход
- `RunCallback()` — сигнал выполнения условия
- `Check()` — синхронный опрос текущего состояния

### Гарантии
- Все условия проверяют состояние в `Start()` — если уже выполнено, callback вызывается немедленно
- `DeInit()` корректно снимает все подписки и отменяет таймеры

---

## Создание пользовательского условия

```csharp
public class MyCondition : UnityCondition
{
    protected override string ConditionName => "My Condition";

    protected override void Start()
    {
        if (Check())
        {
            RunCallback();
            return;
        }
        // подписка на событие...
    }

    public override bool Check() => /* проверка */;

    public override void DeInit()
    {
        // отписка от событий...
    }
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `MinTimeCondition` с `seconds = 0` | Срабатывает немедленно в `Start()` |
| `SceneLoaded` — сцена уже загружена | Callback немедленно, подписка не создаётся |
| `SystemsLoaded` — приложение уже `Running` | Callback немедленно |
| `DeInit()` вызван до срабатывания | Подписки снимаются, callback не вызывается |
| `MinTimeCondition` — повторный `Start()` | `TimeController` заменяет предыдущий вызов (owner-привязка) |
