# LocalizationSystem (Core)

**Namespace:** `Vortex.Core.LocalizationSystem.Bus`, `Vortex.Core.LocalizationSystem`
**Сборка:** `ru.vortex.localization`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Ядро системы локализации. Хранит текущий язык и индекс переводов (ключ → текст), предоставляет API для доступа к переводам.

Возможности:

- Хранение и переключение текущего языка приложения
- Доступ к переводу по строковому ключу
- Extension-методы для строк: `Translate()`, `TryTranslate()`
- Событие `OnLocalizationChanged` при смене языка
- Editor API: список языков, список ключей, переключение локали

Вне ответственности:

- Загрузка данных из источника (Google Sheets, файлы) — драйвер
- Хранение языковых предпочтений — драйвер
- UI-компоненты локализации

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ISystemDriver` |
| `Vortex.Core.Extensions` | `StringExt.IsNullOrWhitespace()`, `DictionaryExt.AddNew()` |

---

## Архитектура

```
Localization (SystemController<Localization, IDriver>)
  ├── index: Dictionary<string, string>     ← ключ → перевод
  ├── CurrentLanguage: string               ← текущий язык (lazy)
  ├── GetTranslate(key) → string            ← поиск в индексе
  └── OnLocalizationChanged                 ← событие смены языка

IDriver (интерфейс)
  ├── GetDefaultLanguage() → string
  ├── SetLanguage(string)
  ├── SetIndex(Dictionary<string, string>)
  ├── GetLanguages() → string[]
  └── OnLocalizationChanged (event)

StringExt (extension-методы)
  ├── "KEY".Translate()      → индекс[key] или "##!key!##"
  └── "KEY".TryTranslate()   → индекс[KEY] или исходная строка
```

### Индекс переводов

- `Dictionary<string, string>` — создаётся в `Localization`, передаётся драйверу через `SetIndex()`
- Драйвер заполняет словарь при загрузке и при смене языка
- Ядро не знает о формате хранения — только читает индекс

### IDriver

| Метод | Описание |
|-------|----------|
| `GetDefaultLanguage()` | Язык по умолчанию (системный или сохранённый) |
| `SetLanguage(string)` | Установка языка. Драйвер перезагружает данные и вызывает `OnLocalizationChanged` |
| `SetIndex(Dictionary<string, string>)` | Привязка индекса из ядра к драйверу |
| `GetLanguages()` | Список доступных языков |
| `OnLocalizationChanged` | Событие — вызывается драйвером после смены языка |

---

## Контракт

### Вход

- Драйвер подключается через `Localization.SetDriver()`
- Драйвер заполняет индекс переводами для текущего языка

### Выход

- `GetTranslate(key)` — перевод или `"##!key!##"` при отсутствии ключа
- `HasTranslate(key)` — проверка наличия ключа
- `GetCurrentLanguage()` — текущий язык
- `OnLocalizationChanged` — оповещение подписчиков

### API

| Метод | Описание |
|-------|----------|
| `GetCurrentLanguage()` | Текущий язык. Lazy-инициализация через `Driver.GetDefaultLanguage()` |
| `SetCurrentLanguage(string)` | Установка языка. Делегирует драйверу |
| `GetTranslate(string key)` | Перевод по ключу. `"##!key!##"` если ключ не найден |
| `HasTranslate(string key)` | `true` если ключ есть в индексе |

### Extension-методы (StringExt)

| Метод | Описание |
|-------|----------|
| `"KEY".Translate()` | `Localization.GetTranslate(key)`. Пустая строка → `""` |
| `"KEY".TryTranslate()` | Если перевод есть для `KEY.ToUpper()` — возвращает перевод, иначе исходную строку |

### Editor API (partial Localization)

| Метод | Описание |
|-------|----------|
| `GetLanguages()` | `List<string>` доступных языков |
| `GetLocalizationKeys()` | `List<string>` всех ключей в индексе |
| `SetDefaultLocale()` | Сбросить на язык по умолчанию (меню `Vortex/Localization/Set Default Locale`) |
| `SetNextLocale()` | Переключить на следующий язык циклически (меню `Vortex/Localization/Set Next Locale`) |

### Событие

| Событие | Момент |
|---------|--------|
| `OnLocalizationChanged` | После смены языка (проксирует событие драйвера) |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Один язык одновременно | Один индекс на приложение |
| Ключи не регистрозависимы только в `TryTranslate` | `TryTranslate` вызывает `ToUpper()`, `Translate` — нет |
| `"##!key!##"` при отсутствии ключа | Визуальный маркер для отладки |
| Индекс перестраивается полностью при смене языка | Драйвер очищает и заполняет заново |

---

## Использование

### Перевод строки

```csharp
// Строгий перевод — "##!key!##" если ключа нет
string text = "MENU_START".Translate();

// Мягкий перевод — оригинальная строка если ключа нет
string label = "Settings".TryTranslate();
```

### Смена языка

```csharp
Localization.SetCurrentLanguage("Russian");
```

### Подписка на смену языка

```csharp
Localization.OnLocalizationChanged += RefreshUI;
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Ключ не найден в индексе | `GetTranslate` → `"##!key!##"` |
| Пустая/null строка в `Translate()` | `""` |
| `TryTranslate` — ключ без перевода | Возвращает исходную строку без изменений |
| `GetCurrentLanguage()` — язык не установлен | Lazy-инициализация через `Driver.GetDefaultLanguage()` |
| Драйвер не подключён | Обращение к `Driver` → `NullReferenceException` |
