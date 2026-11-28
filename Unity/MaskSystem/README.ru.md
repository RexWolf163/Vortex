# MaskSystem (Unity)

**Пространство имён:** `Vortex.Unity.MaskSystem`
**Сборка:** `ru.vortex.unity.masksystem`

---

## Назначение

Маски, настраиваемые в инспекторе, поверх Core-пакета `ru.vortex.core.masksystem` (правила сопоставления, контракт и гарантии описаны в его README).

Возможности:

- Три вида маски, выбираемые в поле `[SerializeReference] Mask`:
  - `StringEqualMask` — строка равна образцу (способ сравнения, регистр, обрезка входа, нормализация NFC)
  - `UrlMask` — части адреса: схема, хост, порт, путь
  - `RegexMask` — регулярное выражение .NET с захватами
- `UnityMask` — общая база видов: заголовок, текст правил, проверка перед сопоставлением, панель проверки в инспекторе
- Инспектор: подсветка ошибки с позицией, подсказка правил на полях настройки, тестовая строка с «совпало / нет» и захватами

Вне зоны ответственности:

- Логика сопоставления — статические контроллеры Core-пакета
- Списки масок и «первое совпадение» — держатель списка

Где маски применять, а где нет (в том числе — не для контроля ввода игрока), — README Core-пакета, раздел «Зона применимости».

---

## Зависимости

| Зависимость | Назначение |
|-------------|------------|
| `ru.vortex.core.masksystem` | `Mask`, результаты, контроллеры |
| `ru.vortex.unity.editortools` | `[ClassLabel]` |
| Odin Inspector | атрибуты инспектора |

---

## Использование

```csharp
public class PartnerRules : MonoBehaviour
{
    [SerializeReference] private Mask partnerUrl;

    private void Awake()
    {
        var validation = partnerUrl.Validate();
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.Message);
    }

    public bool IsPartner(string url) => partnerUrl.IsMatch(url);
}
```

В инспекторе выберите вид в списке типов поля и заполните настройки. Маска создаётся только в инспекторе; создание настроенной маски кодом не предусмотрено.

### Инспектор

- **Заголовок** — название вида (`GetLabel`, по умолчанию имя типа).
- **Ошибка** — красный блок над тестовой строкой: сообщение и, где применимо, «(позиция N)».
- **Подсказка** — наведение на поле настройки показывает правила вида (`RulesHint`).
- **Тестовая строка** — поле редактора, на сопоставление не влияет. «Результат» — «совпало» / «нет», «Захваты» — словарь захватов, если он не пуст.
- `RegexMask` проверяет тестовую строку по кнопке **«Проверить»**, а не при вводе (защита от тяжёлых выражений). Остальные виды обновляются при вводе.

---

## Архитектура

```
Mask (Core)
  └── UnityMask (abstract, [Serializable, ClassLabel("$GetLabel"), HideReferenceObjectPicker])
        ├── GetLabel()            — заголовок (virtual, по умолчанию имя типа)
        ├── RulesHint             — текст правил вида (abstract)
        ├── TestOnDemand          — тестовая строка по кнопке (virtual, false)
        ├── ResetEditorCache()    — сброс итогов панели проверки (protected)
        ├── RequireValid(input)   — fast-fail перед сопоставлением: вход null, неверная настройка (protected)
        ├── StringEqualMask       → StringEqualController
        ├── UrlMask               → UrlController
        └── RegexMask             → RegexController
```

Каждый вид:
- держит настройки в полях `[SerializeField] private` с геттерами только на чтение;
- перед сопоставлением вызывает `RequireValid(input)`;
- вызывает контроллер своего вида; вход готовит контроллер (`StringEqualController.TryPrepareInput`, `UrlController.TryParseInput`), маска только передаёт свои настройки;
- кэширует итог проверки и подготовленные данные (`[NonSerialized]`); в редакторе правка поля настройки (Odin `[OnValueChanged]`) сбрасывает кэши и панель проверки.

### Свой вид

```csharp
[Serializable]
public class PrefixMask : UnityMask
{
    [SerializeField, PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
    private string prefix = "";

    [NonSerialized] private ValidationResult _validation;

    public override string RulesHint => "Строка начинается с префикса.";

    public override ValidationResult Validate() => _validation ??= /* контроллер вида */;
    public override bool IsMatch(string input)
    {
        RequireValid(input);
        return /* контроллер вида */;
    }

    public override MatchResult Match(string input) => IsMatch(input) ? MatchResult.Matched() : MatchResult.NoMatch();

#if UNITY_EDITOR
    private void OnSettingsChanged()
    {
        _validation = null;
        ResetEditorCache();
    }
#endif
}
```

`ValidationResult` создают только контроллеры Core, поэтому проверку настройки своего вида реализует контроллер.

---

## Контракт

### Вход

- Настройки — через инспектор.
- Вход `IsMatch` / `Match` — любая строка; `null` — ошибка программиста.

### Выход

- Итог контроллера своего вида; маска не меняет смысл сопоставления.

### Гарантии

- В сборке настройки изменить нельзя: поля `private`, наружу только чтение.
- В редакторе (в том числе в Play Mode) правка настройки перепроверяет маску на лету.
- Сопоставление на неверной настройке — `Debug.LogError` + `InvalidOperationException`; вход `null` — `Debug.LogError` + `ArgumentNullException`.
- Входные данные, которые нельзя сопоставить (не URL, невалидный Unicode), — «не совпало».

### Ограничения

| Ограничение | Причина |
|---|---|
| URL-вход — только `схема://…` без `\`, управляющих символов и пробелов по краям | `System.Uri` мягче HTTP-стека и мог бы прочитать такой вход иначе |
| Хосты и пути в настройке — в той форме, в которой их отдаёт `System.Uri` | Иначе внешне верная настройка не совпала бы никогда; проверка подсказывает нужную форму |
| Поле тестовой строки сериализуется и в сборке | Раскладка сериализации в редакторе и в сборке должна совпадать |

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| Настройка изменена через Undo, откат префаба или кодом | Кэши сбрасывает только Odin `[OnValueChanged]`; панель проверки может показывать устаревший итог |
| `IsMatch` и `Match` вида расходятся на тестовой строке | `Debug.LogError` один раз на изменение |
| Тестовая строка пуста | Проверяется пустая строка (это обычный вход) |
| Проверка или сопоставление бросают исключение при отрисовке панели | Панель пуста до следующего изменения тестовой строки или настройки |
