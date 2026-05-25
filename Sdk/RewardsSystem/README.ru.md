# RewardsSystem

Sdk-пакет фреймворка Vortex для конфигурируемой выдачи наград: пресет → взвешенный розыгрыш группы → поштучная выдача через полиморфные стратегии → события на шине.

## Назначение

- Конфигурация наград в виде ScriptableObject-пресета с взвешенными группами
- Полиморфные стратегии выдачи (`[SerializeReference]`-дропдаун в инспекторе) под доменные правила проекта — инвентарь, валюта, прогресс
- Унифицированный контракт результата (`RewardResult`) с типом награды (`RewardType : ExtensibleEnum`) для последующей фильтрации
- Событийная шина `RewardBus` для UI-реакций (floating-text, achievements, save-marker) без прямой связности

Вне ответственности:
- Конкретные правила выдачи (инвентарь, кошелёк, лут-таблицы) — это задача доменных пакетов, реализующих наследников `RewardStrategy`
- Батч-валидация и батч-выдача нескольких наград как единой транзакции — зависит от принимающей системы (см. ниже)
- UI-представление награды — подписка на `RewardBus`, отображение — задача потребителя

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `ru.vortex.system` | Базовые абстракции через ссылку asmdef |
| `ru.vortex.extensions` | `DeepCopy`, `ActionExt` |
| `ru.vortex.extenums` | `ExtensibleEnum` для `RewardType` |
| Sirenix Odin Inspector | `[OnInspectorInit]`, `[OnValueChanged]`, `[HideReferenceObjectPicker]`, `[ShowInInspector]`, `[HideLabel]`, `[HorizontalGroup]` |
| UnityEngine | `ScriptableObject`, `Random`, `Debug.LogException` |

Assembly: `ru.vortex.sdk.game.rewards`. Constraints отсутствуют — пакет компилируется всегда.

## Архитектура

```
RewardPreset (ScriptableObject)               ← конфиг дизайнера
   └─ RewardPack[]                            ← взвешенные группы
         └─ RewardData[]                      ← запись с именем
                └─ RewardStrategy             ← полиморфная логика выдачи
                       │
                       │  (доменное расширение Sdk/проекта)
                       ▼
                  RewardStrategy.Type → RewardType (ExtensibleEnum, partial)

Поток выдачи:
   preset.GetReward()  →  IReadOnlyList<RewardData>  (DeepCopy выбранного пака)
   reward.GiveReward() →  RewardResult { Success, FailReason, AppliedAmount, Type }
                          ↓
                       RewardBus.OnRewardGiven / OnRewardFailed
                          ↓
                       подписчики: UI, achievements, save, analytics
```

Source of truth для типа награды — `RewardStrategy.Type` (abstract property). `RewardResult.Type` — снимок этого значения, заполняется автоматически в `RewardsExtLogic.GiveReward`, чтобы потребитель мог фильтровать пакет результатов без обратной связки с исходным `RewardData` (стратегия `internal`).

## Ключевые концепции

| Концепция | Описание | Пример |
|-----------|----------|--------|
| Пресет | ScriptableObject с N группами и их весами | `Assets → Create → Database → Reward Preset` |
| Пак (RewardPack) | Группа наград, выдаётся как одно целое при выборе | Вес 30, содержит [«Меч», «50 монет»] |
| Награда (RewardData) | Имя + полиморфная стратегия | `[SerializeReference] RewardStrategy` |
| Стратегия (RewardStrategy) | Конкретное правило выдачи: валидация + мутация модели | `GiveItemReward : RewardStrategy` |
| Тип награды (RewardType) | `ExtensibleEnum` для группировки/фильтрации | `RewardType.Item`, `RewardType.Currency` |
| Результат (RewardResult) | `struct { Success, FailReason, AppliedAmount, Type }` | Возвращается из `GiveReward` |
| Шина (RewardBus) | События `OnRewardGiven` / `OnRewardFailed` | Подписка UI / achievements |

## Критические требования

1. **Каждая стратегия обязана декларировать `Type`.** `RewardStrategy.Type` — abstract; компилятор не пропустит реализацию без него. Без `Type` фильтрация результатов теряет смысл.
2. **Тип награды объявляется в пакете стратегии, не в этом пакете.** `RewardType` — `partial class` без встроенных значений. Каждое доменное расширение (Sdk-пакет инвентаря, кошелька, прогресса) добавляет свои значения через partial-расширение.
3. **`GetReward` может вернуть `null`.** Если сумма весов всех паков равна 0 — пресет считается некорректным и розыгрыш отменяется. Потребитель обязан проверять результат.
4. **Стратегии не имеют права писать в шину `RewardBus`.** Эмит событий — `internal`, делает только `RewardsExtLogic.GiveReward`. Стратегия мутирует модель, шина уведомляет о свершившемся факте.
5. **Прямой вызов `RewardStrategy.GiveReward()` оставляет `RewardResult.Type == null`.** Заполнение типа — задача extension-логики. Стратегия не обязана помнить про тип в каждом возврате.

## Контракт

### Вход

- `RewardPreset`-ассет с настроенными `RewardPack[]` и весами
- Для каждой `RewardData` — выбранный наследник `RewardStrategy` в инспекторе через `[SerializeReference]`-дропдаун
- Опционально для `GiveReward`/`ValidateRewardConditions`: `targetId` (ID получателя) и `power` (целочисленный множитель силы)

### Выход

- `preset.GetReward()` → `IReadOnlyList<RewardData>` или `null` (см. граничные случаи)
- `reward.GiveReward()` → `RewardResult { Success, FailReason, AppliedAmount, Type }`
- `reward.ValidateRewardConditions()` → `bool` без побочных эффектов
- События `RewardBus.OnRewardGiven` / `OnRewardFailed` — после фактической мутации модели или отказа

### Гарантии

- `GetReward` возвращает DeepCopy выбранного пака. Мутация результата не затрагивает исходный пресет-ассет.
- При успехе из `GiveReward` эмитится ровно одно событие — либо `OnRewardGiven`, либо `OnRewardFailed`. Не оба.
- Исключение из стратегии перехватывается, логируется через `Debug.LogException`, эмитится `OnRewardFailed`, возвращается `RewardResult.Fail("Logic error")`. Поведение шины и возвращаемого значения согласовано (один и тот же `Result`).
- `RewardResult.Type` заполняется автоматически в `RewardsExtLogic.GiveReward` для всех трёх ветвей (validation-fail, обычный результат, исключение).

### Ограничения

- **Только синхронная выдача.** `GiveReward` — `RewardResult`, не `UniTask<RewardResult>`. Долгие операции (сетевой коммит, асинхронная анимация) — за пределами стратегии, в подписчиках шины.
- **Награды дискретны.** `AppliedAmount` — `int`. `power` — целочисленный множитель шагов выдачи, не дробный размер. Стратегия сама округляет/обрезает дробный остаток.
- **Батч-выдача `GiveAll` намеренно отсутствует.** Корректность пакетной выдачи требует знания доменной модели приёмника (инвентарь с одним слотом и две награды-предмета — каждая валидна в одиночку, вместе не помещаются). Такую проверку должна делать принимающая система, не обобщённая шина.
- **`RewardData.RewardStrategy` — `internal`.** Снаружи сборки стратегию не достать; вся работа идёт через extension-методы `RewardsExtLogic`.

## API Reference

```csharp
// Получение группы наград из пресета (DeepCopy, без побочных эффектов)
IReadOnlyList<RewardData> GetReward(this RewardPreset preset);

// Проверка одной награды без побочных эффектов (для UI-превью)
bool ValidateRewardConditions(this RewardData reward, string targetId = null, float power = 1f);

// Синхронная выдача одной награды (мутация модели + эмит события)
RewardResult GiveReward(this RewardData reward, string targetId = null, float power = 1f);

// События шины
event Action<RewardEventData> RewardBus.OnRewardGiven;
event Action<RewardEventData> RewardBus.OnRewardFailed;

// Расширение реестра типов наград (в доменном пакете)
public partial class RewardType
{
    public static readonly RewardType Item     = new(nameof(Item), 100);
    public static readonly RewardType Currency = new(nameof(Currency), 110);
}

// Новая стратегия (в доменном пакете)
public class GiveItemReward : RewardStrategy
{
    [SerializeField] private string itemId;
    [SerializeField] private int amount = 1;

    public override string   GetLabel() => $"Item {itemId} x{amount}";
    public override RewardType Type     => RewardType.Item;

    public override bool Validation(string targetId, float power)
        => Inventory.HasSlotFor(targetId, itemId, (int)(amount * power));

    public override RewardResult GiveReward(string targetId, float power)
    {
        var applied = Inventory.Give(targetId, itemId, (int)(amount * power));
        return applied > 0 ? RewardResult.Ok(applied) : RewardResult.Fail("InventoryFull");
    }
}
```

## Использование

### 1. Базовый сценарий (один пресет, одна награда из пака)

```csharp
[SerializeField] private RewardPreset chestPreset;

public void OpenChest(string playerId)
{
    var rewards = chestPreset.GetReward();
    if (rewards == null) return;                    // все веса нулевые

    foreach (var reward in rewards)
        reward.GiveReward(targetId: playerId);
}
```

> 💡 **Важно:** `foreach + GiveReward` допустим, когда кумулятивные эффекты заведомо отсутствуют (валюта, очки, флаги прогресса). Для предметов в инвентарь с ограниченным числом слотов используй pre-check на стороне принимающей системы — см. сценарий 3.

### 2. UI-превью доступности награды

```csharp
foreach (var reward in chestPreset.RewardPacks[0].Rewards)
{
    var canGive = reward.ValidateRewardConditions(playerId);
    button.interactable = canGive;
}
```

### 3. Батч-выдача с пре-проверкой на стороне принимающей системы

```csharp
var rewards = chestPreset.GetReward();
if (rewards == null) return;

// Группируем по типу — каждый тип валидируется своей системой
var items     = rewards.Where(r => r.GetType() == typeof(GiveItemReward));
var currency  = rewards.Where(r => r.GetType() == typeof(GiveCurrencyReward));

// Принимающая система проверяет кумулятивный эффект
if (!Inventory.CanFitAll(playerId, items))
{
    UI.ShowOverflowDialog();
    return;
}

foreach (var reward in rewards)
    reward.GiveReward(playerId);
```

После выдачи фильтрация результатов идёт по `RewardResult.Type`:

```csharp
var results = rewards.Select(r => r.GiveReward(playerId)).ToList();
var itemsApplied = results.Where(r => r.Type == RewardType.Item).Sum(r => r.AppliedAmount);
```

### 4. UI-реакция через шину

```csharp
private void OnEnable()  => RewardBus.OnRewardGiven += ShowFloatingText;
private void OnDisable() => RewardBus.OnRewardGiven -= ShowFloatingText;

private void ShowFloatingText(RewardEventData data)
{
    if (data.Result.Type == RewardType.Currency)
        floatingTextPool.Spawn($"+{data.Result.AppliedAmount}");
}
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `preset.RewardPacks == null` или пусто | `GetReward` бросает `NullReferenceException` (fail-fast: конфиг сломан) |
| Один пак в пресете | Отдаётся всегда, его `Weight` игнорируется |
| Сумма весов всех паков = 0 | `GetReward` возвращает `null` |
| `strategy.Validation` вернула `false` | `GiveReward` → `RewardResult.Fail("ValidationFailed")` + `OnRewardFailed` |
| `strategy.GiveReward` бросил исключение | `Debug.LogException` + `OnRewardFailed` + возврат `RewardResult.Fail("Logic error")` |
| Прямой вызов `RewardStrategy.GiveReward()` мимо extension | `RewardResult.Type == null`, шина не нотифицируется |
| `targetId == null` | Глобальная награда — стратегия сама решает, поддерживается ли |

## Файловая структура

```
RewardsSystem/
├── RewardBus.cs                # static-шина OnRewardGiven/OnRewardFailed + RewardEventData
├── RewardPreset.cs             # ScriptableObject-конфиг
├── RewardsExtLogic.cs          # extension-методы GetReward/Validate/GiveReward
├── Model/
│   ├── RewardData.cs           # имя + [SerializeReference] стратегия
│   ├── RewardPack.cs           # вес + RewardData[]
│   ├── RewardResult.cs         # struct { Success, FailReason, AppliedAmount, Type }
│   ├── RewardStrategy.cs       # abstract: GetLabel, Type, Validation, GiveReward
│   └── RewardType.cs           # partial ExtensibleEnum для доменных типов
└── ru.vortex.sdk.game.rewards.asmdef
```
