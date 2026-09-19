# AssetSwapSystem

> ⚠️ **Экспериментальный пакет.** Публичное API (`AssetSwapController.NewGroup/AddVariant/RemoveGroup/Validate/ValidateAll/Apply`), формат `ProjectSettings/VortexAssetSwapSettings.asset` и схема labels могут измениться без обратной совместимости. До боевой обкатки на нескольких publish-циклах — не полагаться в критичном CI-пайплайне; держать git-состояние до `Apply`, чтобы был штатный откат.

**Namespace:** `Vortex.Unity.AssetSwapSystem.*`
**Assembly:** `ru.vortex.unity.assetswap` (`includePlatforms: ["Editor"]`)
**Слой:** Unity (Editor-only утилита build-pipeline)
**Конфиг:** `ProjectSettings/VortexAssetSwapSettings.asset` (`ScriptableSingleton`, вне `Assets`, в билд не попадает, в git — да)

---

## Назначение

Editor-only утилита для подмены ассетов на разные варианты перед сборкой для конкретной паблишинговой платформы (Steam / GOG / mobile / консольные сторы / censored-версия / etc). Дизайнер держит все варианты в Editor-папках, помечает их и target-ассеты штатными Unity Asset Labels, а одна кнопка перед билдом атомарно перезаписывает target-содержимое нужным вариантом — **без потери GUID**.

Возможности:

- **Штатные Asset Labels** — те же, что видны в инспекторе Unity (`Prop`, `Vegetation`). Никаких side-channel меток.
- **Побитовая замена содержимого через `File.Copy`** — `.meta` target'а не трогается, GUID сохраняется, все существующие ссылки (`[SerializeField]`, `AssetReference`, Addressables, сцены, префабы) остаются валидными.
- **Multi-file case (Spine, FBX-mesh + material)** — работает штатно: каждый файл мультифайловой структуры маркируется отдельно, идентификация пар по имени файла делает связки автоматически.
- **Валидатор перед Apply** — проверяет полноту пар, обратку, совпадение типов импортёров, уникальность имён, отсутствие runtime-ссылок на variant-ассеты. При ошибках Apply блокируется.
- **CI-hook API** — `AssetSwapController.Apply(int groupIndex, int variantIndex)` вызывается из внешних build-хэндлеров (`unity -executeMethod` и batch-скриптов).
- **Отдельное окно битового соответствия** — диагностика «какому варианту сейчас соответствует содержимое target'ов».

Вне ответственности:

- **Runtime-подгрузка вариантов** — этой утилиты не существует в билде. Все подмены — только editor-time перед сборкой.
- **Смена импорт-настроек `.meta`** — не меняется никогда. Если варианты требуют разных `TextureImporter`-настроек (sRGB, compression, sprite mode) — это ответственность дизайнера через отдельные `.presetlike`.
- **State «текущий активный вариант»** — не сохраняется. Один ассет может участвовать в нескольких группах; порядок применения — за пользователем; конечное состояние на дисках — вопрос git.
- **Rollback при частичной ошибке `File.Copy`** — не выполняется. При исключении посреди батча: LogError с указанием файла + восстановление через git.

---

## Зависимости

Никаких — только штатный `UnityEditor` API (SettingsProvider, ScriptableSingleton, AssetDatabase, AssetImporter).

---

## Модель данных

### `AssetSwapSettings` (`ScriptableSingleton<T>`)

```csharp
[FilePath("ProjectSettings/VortexAssetSwapSettings.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class AssetSwapSettings : ScriptableSingleton<AssetSwapSettings>
{
    [SerializeField] private List<AssetSwapGroup> groups;
}
```

### `AssetSwapGroup` (POCO)

```csharp
[Serializable]
internal class AssetSwapGroup
{
    public int index;             // N — порядковый номер, уникален в списке
    public int variantsCount;     // K регистрированных вариантов
    public string comment;        // опционально — «Steam», «GOG», «Censored»
}
```

Имя группы **вычисляется** как `$"AssetGroup{index}"`, а не хранится строкой — единая формула, нет drift'а.

### Схема labels

| Роль | Label |
|---|---|
| Target-ассет | `AssetGroup{N}` |
| Variant K для группы N | `AssetGroup{N}_Variant{K}` |

### Инварианты

- **I1.** `AssetSwapGroup.index` уникален. Дубликат — LogError, дубль дропается (кроме первого вхождения).
- **I2.** Весь пакет — Editor-only. Runtime к нему не обращается.
- **I3.** `AssetGroup{N}` label существует **только** на ассетах, зарегистрированных дизайнером в группу N.
- **I4.** `.meta` target'ов не мутируется — только `File.Copy` над содержимым.
- **I5.** Batch применения одного Variant K атомарен на уровне Unity import (`StartAssetEditing`/`StopAssetEditing`). Файловая атомарность (все `File.Copy` прошли) — не гарантируется; при частичной ошибке — LogError, откат через git.
- **I6.** Идентификация пары target ↔ variant — точное совпадение `Path.GetFileName` с расширением, регистрозависимо.

---

## Использование дизайнером

> **Важно про labels.** Наши labels (`AssetGroup{N}`, `AssetGroup{N}_Variant{K}`) — штатные Unity Asset Labels, но **не появляются в выпадашке predefined-списка инспектора** (Unity кэширует этот список и не пересканирует его после SetLabels до перезагрузки редактора). Поэтому **все операции с labels пакета — только через страницу `Project Settings → Vortex/AssetSwap`**, кнопками ниже. Не пытайтесь набирать `AssetGroup1` руками в инспекторе: опечатаетесь — Validator промолчит, файлы пойдут не в ту группу.
>
> **`.cs`-скрипты пакетом не помечаются и не свапаются.** Кнопки `Add label to Selected` / `Add label` пропускают их с `LogWarning`, `FindAssetsByLabel` (и, соответственно, Validate / Apply / Match) их не видит. Свап кода `File.Copy`-ом сломал бы синхронизацию `.meta`, Assembly Definitions и compile-фазы; ветвление кодовой базы — задача git-веток, а не AssetSwap.

### Первое создание группы

1. **Открыть настройки:** `Project Settings → Vortex/AssetSwap`.
2. **Выделить в Project View target-ассеты** (те, что будут заменяться) → нажать `New Group`. Создастся `AssetGroupN` с уже проставленным label на выделенных ассетах.
3. **Опционально: заполнить Comment группы** — «Steam», «GOG», «Censored», ...
4. **Положить variant-файлы в Editor-папку** (пример: `Assets/Editor/AssetSwapVariants/Group1/Variant1/hero.png`).
   Имя файла должно **точно совпадать** с именем target'а (с расширением).
5. **Выделить variant-файлы в Project View** → на странице AssetSwap нажать `Add Variant` возле нужной группы. Появится `Variant1` с уже проставленным label на выделенных.
6. **Проверить:** нажать `Validate` возле группы. Ошибки идут в консоль. Кнопка `Validate` — единственный триггер, обновляющий счётчики `Targets` / `Variant K` на странице; после ручных изменений labels в инспекторе цифры не сдвинутся до следующей валидации.
7. **Применить:** после успешной валидации нажать `Apply` возле нужного Variant.
8. **Проверить результат:** `Match Window` покажет, какому варианту сейчас соответствует содержимое target'ов.

### Как проставить / снять label постфактум

Все действия — со страницы `Project Settings → Vortex/AssetSwap`, через кнопки возле нужной группы или варианта.

| Задача | Действие |
|---|---|
| Расширить набор **target'ов** группы новыми ассетами | Выделить их в Project View → возле группы кнопка **`Add label to Selected`** (в строке `Targets:`). |
| Расширить набор файлов **VariantK** | Выделить их в Project View → возле нужного `VariantK` кнопка **`Add label`**. |
| Посмотреть, какие ассеты помечены **target-label** группы | Возле группы кнопка **`Select in Project`** — выделит их в Project View. |
| Посмотреть, какие файлы помечены **variant-label** | Возле нужного `VariantK` кнопка **`Select`**. |
| Снять target-label / variant-label с одного ассета | В инспекторе Unity, панель `Asset Labels`, крестик рядом с меткой. После — нажать `Validate` на странице AssetSwap, чтобы обновить счётчики. |
| Снять все labels группы (target + все variants) со всех ассетов | На странице AssetSwap кнопка **`Remove Group`** возле группы (диалог подтверждения). Содержимое файлов остаётся, снимаются только labels + удаляется запись группы. |
| Добавить новый вариант в существующую группу | Выделить новые variant-файлы → кнопка **`Add Variant`** возле группы. |

Кнопка `Add label to Selected` / `Add label` — merge, не reset: существующие labels ассетов сохраняются, дубликаты не создаются. Папки в выделении пропускаются. Если выделение пустое — в консоли будет `LogWarning` и никаких изменений не произойдёт.

---

## Использование в CI (build-pipeline)

Public API:

```csharp
Vortex.Unity.AssetSwapSystem.AssetSwapController.Apply(int groupIndex, int variantIndex);
Vortex.Unity.AssetSwapSystem.AssetSwapController.Validate(int groupIndex);
Vortex.Unity.AssetSwapSystem.AssetSwapController.ValidateAll();
```

Пример вызова из command-line:

```
Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ValidateAll
```

Для сложных хэндлеров — обёртка:

```csharp
public static class MyPublishHooks
{
    public static void BuildForGog()
    {
        AssetSwapController.Apply(1, 2); // AssetGroup1 → Variant2
        AssetSwapController.Apply(3, 1); // AssetGroup3 → Variant1
        BuildPipeline.BuildPlayer(...);
    }
}
```

---

## Валидатор

Кнопки `Validate` (по группе) и `Validate All` (все группы) прогоняют:

| Проверка | Что ищет | Логика |
|---|---|---|
| Уникальность имён | Внутри targets и каждого Variant K — файлы с одинаковым `Path.GetFileName` | LogError на каждый дубль |
| Полнота пар | У каждого target есть пара в каждом Variant K | LogError на непокрытые |
| Обратка | У каждого variant есть target | LogError на висячие |
| Совпадение типов | `AssetImporter.GetAtPath(target).GetType() == GetAtPath(variant).GetType()` | LogError на разницу |
| Runtime-держатели | Ни один runtime-ассет (prefab/scene/SO/mat/controller/anim вне `Editor/`) не должен ссылаться на variant-ассет | LogError с указанием holder + variant |

Если хоть одна ошибка — Apply **блокируется** до устранения.

---

## Ограничения (для дизайнера)

- **Labels пакета не попадают в predefined-список инспектора Unity автоматически.** Unity кэширует список известных меток и не пересканирует после `SetLabels+SaveAssets` до перезагрузки редактора. Практическое следствие: **проставлять и снимать наши labels — только со страницы `Project Settings → Vortex/AssetSwap`** (кнопки `Add label to Selected` / `Add label`), не через инспектор Unity. Инспектор принимает произвольную строку молча — опечатка «AssetGroup1» → «AsetGroup1» останется незамеченной Validator'ом.
- **`.cs`-файлы не свапаются никогда.** `AddLabelToSelected` пропускает их в выделении с `LogWarning`, `FindAssetsByLabel` не отдаёт их наружу — Validate / Apply / Match Window их не увидят, даже если label остался на скрипте после ручной правки в инспекторе. Свап кода `File.Copy`-ом рвёт `.meta`, Assembly Definitions и compile-фазу; ветвление кодовой базы — через git branch.
- **Точное совпадение имён с расширением.** `hero.png` ↔ `hero.png`, не `hero.jpg`, не `Hero.png`. Регистр важен.
- **Идентичная sub-asset структура вариантов.** Sprite-multiple, FBX, prefab-с-embedded-mesh — если у одного варианта 5 sub-ассетов, у другого 3, `.meta` останется старой и ссылки на sub-asset'ы сломаются. Правило: экспортировать варианты **из одного исходника** (для Spine — из одного skeleton).
- **Импорт-настройки `.meta` не меняются.** Sprite mode, compression, sRGB — от target'а. Если варианты требуют разных настроек — держать pre-set профили и применять отдельно.
- **Variant-файлы должны жить в папках с именем `Editor/`** — Unity автоматически исключит их из билда. Прямые ссылки на variant-файлы из runtime-ассетов запрещены; валидатор проверяет.
- **Никакого состояния «сейчас активен».** Инструмент подмёл — что осталось на дисках, знает только git. Порядок применения между группами — на дизайнере.
- **File.Copy может не пройти** (файл занят, право доступа) — LogError; часть батча уже применилась. Откат — `git checkout`.

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| `AssetSwapSettings.groups` содержит два `AssetGroup{N}` с одним `index` | При открытии страницы — LogError, дубль дропается (первое вхождение остаётся) |
| В проекте есть Asset Label `AssetGroup5` от другой системы | Наши target'ы засчитываются вместе с чужими — Validate поймает несоответствие как «Duplicate file name» или «Type mismatch». LogError достаточно |
| Спрайты выделены как target, но какая-то часть — `Font` вместо `Texture2D` | Validate → LogError «Importer type mismatch» |
| Variant-файл лежит НЕ в Editor-папке | Валидатор проверит ссылки runtime; если такой variant кем-то заиспользован — LogError с holder-путём |
| `File.Copy` упал на 5 из 10 файлов | LogError на 5-й; 4 первых уже применены. Откат через git |
| Apply при пустом Variant K (0 variant-файлов) | Validate поймает «Target has no pair in Variant K», Apply заблокирован |
| `New Group` без выделенных ассетов | Группа создаётся пустой; label не проставляется никому. Нормально: дизайнер добавит потом |
| Удаление группы через `Remove Group` | Labels `AssetGroup{N}` и все `AssetGroup{N}_Variant{K}` снимаются со всех помеченных ассетов проекта. Содержимое target'ов не меняется |
