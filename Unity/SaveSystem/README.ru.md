# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`
**Сборка:** `ru.vortex.unity.save`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы сохранений. Предоставляет драйвер на основе `PlayerPrefs` с XML-сериализацией и сжатием данных, а также UI-компонент индикации прогресса.

Возможности:

- `SaveSystemDriver` — драйвер: хранение в `PlayerPrefs`, XML-сериализация, сжатие через `Compress`/`Decompress`
- `SavePreset` — XML-сериализуемая обёртка для `SaveFolder[]`
- `UISaveLoadComponent` — MonoBehaviour для отображения прогресса save/load
- Индекс сохранений: список GUID через `PlayerPrefs` (ключ `SavesData`)
- Метаданные (`SaveSummary`) хранятся отдельно от данных

Вне ответственности:

- `SaveController`, `ISaveable`, модели данных — Core
- Логика сбора/раздачи данных — Core
- Шифрование (за пределами сжатия) — прикладной уровень

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.SaveSystem` | `SaveController`, `IDriver`, `SaveData`, `SaveFolder`, `SaveSummary`, `SaveProcessData` |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `StringExtensions.Compress/Decompress` |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` (в `UISaveLoadComponent`) |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]` атрибут |
| `Vortex.Unity.UI.UIComponents` | `UIComponent` (в `UISaveLoadComponent`) |

---

## Архитектура

```
SaveSystemDriver : Singleton<SaveSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>   ← индекс в памяти
  ├── _saveDataIndex → ссылка на SaveController.SaveDataIndex
  │
  ├── Init()
  │    ├── PlayerPrefs.GetString("SavesData") → "guid1;guid2;..."
  │    └── Для каждого GUID → GetSaveSummary() → Saves
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML)
  │    ├── XML → string → Compress(guid) → PlayerPrefs "Save-{guid}"
  │    ├── SaveSummary → XML → PlayerPrefs "SaveSummary-{guid}"
  │    └── Обновление "SavesData"
  │
  ├── Load(guid)
  │    ├── PlayerPrefs "Save-{guid}" → Decompress(guid) → XML
  │    ├── XML → SavePreset → _saveDataIndex
  │    └── Каждый SaveFolder → Dictionary<string, string>
  │
  ├── Remove(guid)
  │    ├── Saves.Remove(guid)
  │    ├── PlayerPrefs.DeleteKey "Save-{guid}", "SaveSummary-{guid}"
  │    └── Обновление "SavesData"
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()

SavePreset [XmlRoot]
  └── Data: List<SaveFolder>                 ← XML-сериализуемый контейнер

UISaveLoadComponent : MonoBehaviour
  ├── title: UIComponent                     ← "Загрузка" / "Сохранение"
  ├── progress: UIComponent                  ← форматированный прогресс
  ├── loadingText, savingText: string        ← [LocalizationKey]
  ├── progressTextPattern: string            ← [LocalizationKey], pattern для string.Format
  └── Run() → Coroutine: обновление текста каждый кадр
```

### Формат хранения в PlayerPrefs

| Ключ | Содержимое |
|------|-----------|
| `SavesData` | `"guid1;guid2;guid3"` — список всех GUID через `;` |
| `Save-{guid}` | Сжатая XML-строка (`SavePreset`) с ключом сжатия = GUID |
| `SaveSummary-{guid}` | XML-строка (`SaveSummary`) — имя и дата |

### Сжатие

Данные сохранения сжимаются через `string.Compress(guid)` и распаковываются через `string.Decompress(guid)`. GUID используется как ключ сжатия.

### UISaveLoadComponent

Компонент для отображения прогресса. При `OnEnable` запускает Coroutine, каждый кадр обновляющий текст:
- `title` — "Загрузка" или "Сохранение" (по `SaveController.State`)
- `progress` — форматированная строка: `string.Format(pattern, globalProgress, globalSize, moduleName, modulePercent)`

---

## Контракт

### Вход

- Драйвер регистрируется автоматически через `[RuntimeInitializeOnLoadMethod]`
- `SaveController.Save/Load/Remove` делегируют драйверу

### Выход

- Данные хранятся в `PlayerPrefs`
- `GetIndex()` — `Dictionary<string, SaveSummary>` из памяти

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Хранение в `PlayerPrefs` | Ограничение размера (зависит от платформы) |
| XML-сериализация | `SavePreset`, `SaveSummary` — `[XmlRoot]` / `[XmlElement]` |
| Сжатие через GUID как ключ | `Compress`/`Decompress` из `StringExtensions` |
| `Saves` — Dictionary in memory | Индекс загружается при `Init()`, обновляется при `Save`/`Remove` |
| `UISaveLoadComponent` — Coroutine | Обновление каждый кадр, не UniTask |

---

## Использование

### Индикация прогресса

1. Добавить `UISaveLoadComponent` на UI-элемент
2. Назначить `title` и `progress` (`UIComponent`)
3. Указать ключи локализации: `loadingText`, `savingText`, `progressTextPattern`
4. Формат `progressTextPattern`: `"{0}/{1} — {2} ({3}%)"` — глобальный прогресс, имя модуля, процент модуля

### Работа с сохранениями

```csharp
// Все сохранения
var saves = SaveController.GetIndex();

// Сохранение
SaveController.Save("Слот 1");

// Загрузка
SaveController.Load(selectedGuid);

// Удаление
SaveController.Remove(selectedGuid);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| GUID не найден в `PlayerPrefs` при `Load` | `LogError`, `_saveDataIndex` остаётся пустым |
| GUID не найден при `Remove` | `LogError`, no-op |
| Повреждённый XML при десериализации | `SavePreset = null`, `LogError` |
| `PlayerPrefs` переполнен | Поведение зависит от платформы |
| `UISaveLoadComponent` выключен во время процесса | `OnDisable` → `StopAllCoroutines`, `_process = false` |
| `SaveSummary` GUID не найден при `Init` | `LogError`, возвращает `default(SaveSummary)` |
| `SavesData` пуст при `Init` | Пустой `Saves`, корректное поведение |
| Дубликат GUID при `Save` | `Saves.Add` выбросит исключение (не обрабатывается) |
