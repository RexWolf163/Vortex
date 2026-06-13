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
    ├── UnityCondition.cs       # Абстрактная база: Inspector-отображение
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

Ожидание загрузки конкретной сцены по имени.

| Поле | Тип | Описание |
|------|-----|----------|
| `SceneName` | `string` | Имя сцены (`[ValueDropdown]` из Build Settings) |

`Check()` использует `SceneManager.GetSceneByName(SceneName)` и проверяет `IsValid() && isLoaded` — это **живое** состояние, а не кешированный флаг, и оно видит **Additive-сцены**, не только активную. Это важно для загрузочных цепочек: основная сцена обычно грузится аддитивно поверх загрузчика, и `GetActiveScene` её бы не увидела.

При `Start()`, если сцена уже загружена — `RunCallback()` немедленно. Иначе подписка на `SceneManager.sceneLoaded`; в обработчике сравнивается имя именно загруженной сцены (из аргумента события, в т. ч. Additive), после совпадения — отписка и callback.

### SystemsLoaded

Ожидание завершения инициализации приложения.

Без параметров. При `Start()` проверяет `App.GetState() == AppStates.Running`. Если уже `Running` — `RunCallback()` немедленно. Иначе подписывается на `App.OnStateChanged`.

### Условия в других пакетах

Условие — это `[SerializeReference]`-наследник `Condition`, поэтому пакеты могут добавлять свои условия рядом со своей функциональностью; в дропдаунах цепочек они появляются автоматически (type-scanning Odin).

| Условие | Пакет | Что ждёт |
|---------|-------|----------|
| `NaninovelInitialized` | `ru.vortex.nani.core` | `Engine.Initialized` — движок Naninovel поднялся. Симметрично `SystemsLoaded`; на одном коннекторе вместе дают конъюнкцию «готовы И Vortex, И Naninovel» |

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
| `SceneLoaded` — сцена уже загружена (в т. ч. Additive) | Callback немедленно, подписка не создаётся |
| `SceneLoaded` — целевая сцена грузится аддитивно поверх другой | Срабатывает: проверка по `GetSceneByName`, не по активной сцене |
| `SystemsLoaded` — приложение уже `Running` | Callback немедленно |
| `DeInit()` вызван до срабатывания | Подписки снимаются, callback не вызывается |
| `MinTimeCondition` — повторный `Start()` | `TimeController` заменяет предыдущий вызов (owner-привязка) |
