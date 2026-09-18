# GalleryStoriesSystem

**Namespace:** `Vortex.NaniExtensions.GalleryStoriesSystem.*`
**Assembly:** `ru.vortex.nani.gallery.stories` (define: `USING_NANINOVELL`)
**Тип пакета:** consumer для [GallerySystem](../../Sdk/GallerySystem/README.ru.md) — нарративные галлерейные карточки

---

## Назначение

Третий тип галлерейного контента — **нарративные карточки**: превью в галлерейной сетке, при клике игрок повторно «проходит» короткий nani-скрипт (сцену диалога, воспоминание, флешбэк). Пример: галлерея сцен в визуальной новелле, где ранее просмотренная реплика или диалог доступны для повторного проигрывания.

Возможности:

- **`GalleryStoryModel`** реализует `IGalleryEntry` из GallerySystem — попадает в галлерейный пул автоматически.
- **`GalleryStoryPreset`** держит стандартные поля `RecordPreset<T>` + строковый путь к nani-скрипту (`Script.Path`).
- **Driver-нейтральный путь** вместо прямой ссылки на `Naninovel.Script`. Причина: naninovel сам управляет жизненным циклом Script-инстансов (`ScriptPlayer.ResetService()` в `OnLoadGame` может выгрузить загруженный скрипт), после чего прямая ссылка становится fake-null. Строка живёт всегда.
- **Дизайнерский UX**: вспомогательное поле `Naninovel.Script script` (non-serialized) в инспекторе. Бросил ассет — Odin-callback скопировал `Path` в сериализуемое поле и обнулил ссылку.
- **InfoBox-валидация**: пресет показывает статус пути (пусто / ok / не найден) с найденным именем скрипта.
- **Прямой вызов `NaniWrapper`** без промежуточного bus/viewer'а: `model.Show()` → `NaniWrapper.PlayScript(scriptPath)`, `model.Hide()` → `ScriptPlayer.Stop()`. Naninovel сам держит UI, координировать нечего.

Вне ответственности:

- **UI показа nani-скрипта** — Naninovel предоставляет свой UI (`ScriptPlayer` + `TextPrinter` + прочие сервисы) целиком. Пакет туда не вмешивается.
- **Разблокировка карточек** — `RecordMarksSystem`, пакет туда не пишет.
- **Управление ScriptPlayer** — делегируется в `NaniWrapper.PlayScript` (тот шлюзует Load→Play, глушит ложные Stop-события, корректно обрабатывает пустой путь).

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `Vortex.Sdk.GallerySystem` | Контракт `IGalleryEntry` |
| `Vortex.Core.DatabaseSystem` | `Record`, попадание в галлерейный пул |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` |
| `Vortex.Core.Extensions.LogicExtensions` | `ObjectExtCopy.CopyFrom` |
| `Vortex.NaniExtensions.Core` | `NaniWrapper.PlayScript`, `NaniWrapper.ScriptPlayer.Stop` |
| Naninovel | `Script` (auto-referenced) |
| Odin Inspector | `[ShowInInspector]`, `[OnValueChanged]`, `[InfoBox]` (auto-referenced) |
| UniTask | Для `Forget()` fire-and-forget |

---

## Архитектура

```
GalleryStoryPreset (SO)
    ├── [SerializeField] Icon (превью)          — из базы RecordPreset<T>
    ├── [SerializeField, ReadOnly, InfoBox]     — driver-нейтральная строка Script.Path
    │   private string scriptPath
    └── [NonSerialized, ShowInInspector, OnValueChanged]  — UX-помощник (drag&drop)
        private Script script
                    │
                    │   OnValueChanged: scriptPath = script.Path; script = null
                    │
                    │   Database.GetRecords → RecordPreset<T>.GetData()
                    │       ↓
                    ▼
GalleryStoryModel (Record, IGalleryEntry)
    ├── GuidPreset, Icon, Name, Description     — публичные (из Record + CopyFrom)
    ├── internal ScriptPath                     — копия строки из пресета
    ├── [NonSerialized] _active                 — наш скрипт реально стартовал (гонка-гард)
    ├── Show()      → Show(CancellationToken.None)
    ├── Show(ct)    → _active=false; +sub OnNaniStart/OnNaniStop → PlayScript(ScriptPath, token: ct).Forget()
    │                     │
    │                     ├─ HandleNaniStart ─(PlayedScript.Path == наш)─→ _active=true; -unsub Start
    │                     │
    │                     └─ HandleAutoStop  ─(!_active — чужой стоп до старта)─→ игнор
    │                          └─(_active && наш скрипт больше не играет)─→ -unsub оба → Hide() (no-op)
    │
    └── Hide()      → если PlayedScript.Path == ScriptPath → ScriptPlayer.Stop() иначе no-op
                    │
                    ▼
Naninovel (сам держит UI-канал: принтеры, актёры, звук — вне ответственности пакета)
```

По сравнению с `GallerySpritesSystem` — на два уровня короче: нет bus, нет viewer'а, нет controller-extension'а. Naninovel сама себе UI — координировать нечего.

---

## Контракт

### `GalleryStoryModel : Record, IGalleryEntry`

```csharp
public class GalleryStoryModel : Record, IGalleryEntry
{
    public Sprite Icon { get; protected set; }
    internal string ScriptPath { get; set; }

    public bool CopyFrom(SoData source);       // копирует Path из пресета
    public void Show();                        // NaniWrapper.PlayScript(ScriptPath).Forget()
    public void Show(CancellationToken ct);    // с токеном отмены
    public void Hide();                        // Stop только если PlayedScript.Path == ScriptPath
}
```

### `GalleryStoryPreset : RecordPreset<GalleryStoryModel>`

```csharp
public class GalleryStoryPreset : RecordPreset<GalleryStoryModel>
{
    [SerializeField, ReadOnly, InfoBox(...)] private string scriptPath;
    [NonSerialized, ShowInInspector, OnValueChanged(nameof(OnScriptChanged))]
    private Script script;

    public string ScriptPathTemplate => scriptPath;
}
```

### Инварианты

- **I1.** `ScriptPath` — internal; извне сборки недоступен.
- **I2.** В сериализованном SO хранится только строка `scriptPath`; ссылки на `Naninovel.Script` не сохраняются.
- **I3.** `Show`/`Hide` — синхронные (требование `IGalleryEntry`). Асинхронный `PlayScript` уходит в `Forget()` — модель не блокирует вызывающего.
- **I4.** `Show` подписывает модель на `NaniWrapper.OnNaniStart` и `OnNaniStop`; при завершении **нашего** скрипта — авто-`Hide()`. Lifecycle симметричен: `Show → (nani закончилась) → Hide()`, без ручного вмешательства.
- **I5.** Auto-`Hide` идемпотентен с ручным `Hide`: если игрок нажал «Закрыть» → `Stop()` → OnNaniStop → HandleAutoStop → `Hide()` — второй `Hide` no-op'нется (PlayedScript уже null).
- **I6.** Гонка-гард `_active`: авто-`Hide` срабатывает только после того, как наш скрипт реально стартовал (`OnNaniStart` с `PlayedScript.Path == ScriptPath` → `_active = true`, отписка от Start). Чужой `OnNaniStop` в окне между `Show` и стартом нашего скрипта (пока `PlayScript` шлюзует Load→Play) при `_active == false` игнорируется — подписка не рвётся преждевременно. `_active` — `[NonSerialized]`, в сейв не входит; сбрасывается в начале каждого `Show`, а стейл-подписка при `_active == false` инертна.

---

## Создание карточки (для дизайнера)

1. **Создать пресет:** Project → Create → Vortex → Presets → Gallery → Story. Получится `GalleryStoryPreset`.
2. **Заполнить базовые поля:** Name, Description, Icon (превью для галлерейной плитки).
3. **Перетащить nani-скрипт** в поле `script` в инспекторе. Odin-callback сразу:
   - Копирует `script.Path` в поле `scriptPath` (сериализуемое).
   - Обнуляет `script` (не сериализуется).
   - Помечает пресет dirty для сохранения.
4. **Проверить InfoBox под полем `scriptPath`:**
   - Зелёный (Info) — «OK: `<имя_скрипта>` (Path: `<путь>`)» — путь корректен, скрипт найден в проекте.
   - Жёлтый (Warning) — «Путь к скрипту не задан».
   - Красный (Error) — «Скрипт с Path не найден в проекте» — путь есть, но nani-скрипт с таким Path не существует. Возможно, скрипт был удалён или переименован.
5. **Метка разблокировки:** в `RecordMarksSettings` должна быть метка, которой сюжет пометит `GuidPreset` карточки. Дизайнер галлерейной сцены указывает эту метку в `GalleryView.marks`.

Карточка автоматически попадает в `GalleryView`, если её тип разрешён `allowedTypes`, а `GuidPreset` помечен нужной меткой. Клик «Смотреть» в галлерейной карточке (`GalleryCardView.Show`) вызывает `entry.Show()` → nani-скрипт стартует.

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| Пустой `ScriptPath` | `NaniWrapper.PlayScript` выведет LogError и вернётся; скрипт не запустится |
| `ScriptPath` указывает на удалённый nani-скрипт | Naninovel бросит «Failed to get resource» в лог; NaniWrapper обработает как отсутствие |
| Дизайнер перетащил Script вручную | `OnValueChanged` копирует `Path` в `scriptPath` и очищает `script`. Дальше видимо только сериализованное поле |
| Nani-скрипт выгружен между показами | Пакет хранит только строку; следующий `Show` заставит Naninovel перезагрузить скрипт по пути. Проблема выгрузки решена этой архитектурой |
| Клик «Смотреть» во время активного скрипта | `NaniWrapper.PlayScript` шлюзует запуски: пока один Load→Play не завершился, следующий ждёт |
| Наш скрипт реально стартовал | `OnNaniStart` с `PlayedScript.Path == ScriptPath` → `_active = true`, отписка от `OnNaniStart` |
| Чужой `OnNaniStop` между `Show` и стартом нашего скрипта | `_active == false` → `HandleAutoStop` игнорирует; подписка сохраняется, ждём наш `OnNaniStart` (гонка-гард) |
| `Hide()` без активного скрипта | Проверка `PlayedScript == null` — no-op |
| `Hide()` пока играет чужой скрипт (нарративный запуск между Show и Hide) | Проверка `PlayedScript.Path != ScriptPath` — no-op; чужой скрипт не трогается |
| Nani-скрипт завершился сам (кончились команды) | `_active == true` → `OnNaniStop` fires → `HandleAutoStop` отписывается от обоих + вызывает `Hide()` (no-op в этой точке, но симметрично замыкает цикл) |
| Пока играет наш скрипт, стартовал другой (@goto на внешний скрипт) | `OnNaniStop` fires с новым `PlayedScript.Path != ScriptPath` → HandleAutoStop отписывается + `Hide()` no-op |
| Повторный `Show` того же инстанса | `-=` перед `+=` в Show защищает от двойной подписки |
| Проект без `USING_NANINOVELL` | Пакет не компилируется (define constraint) — карточки-истории просто не подключены |
