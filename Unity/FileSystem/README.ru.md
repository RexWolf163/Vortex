# FileSystem

**Namespace:** `Vortex.Unity.FileSystem`
**Сборка:** `ru.vortex.unity.filesystem`

## Назначение

Платформонезависимое определение и создание директории для файлового вывода приложения.

Возможности:
- Автоматическое определение пути хранения при старте приложения
- На Android — доступ к папке Downloads через Java-интероп с запросом разрешений
- На остальных платформах — папка `_OutputFiles` рядом с корнем приложения
- Создание директорий по произвольному пути

Вне ответственности:
- Чтение и запись файлов
- Управление правами доступа (кроме `WRITE_EXTERNAL_STORAGE` на Android)
- Работа с `Application.persistentDataPath`

## Зависимости

Нет внешних зависимостей. Сборка автономна.

---

## Архитектура

```
FileSystem/
├── Bus/
│   └── File.cs                    # Статический API: GetAppPath(), CreateFolders()
└── Controllers/
    └── AndroidPathResolver.cs     # Android-интероп: Downloads, WRITE_EXTERNAL_STORAGE
```

### File (статический класс)

Шина доступа к файловой системе. Инициализируется автоматически через `[RuntimeInitializeOnLoadMethod]`.

Определение пути:
- Берёт `Application.dataPath`, отбрасывает последний компонент
- Заменяет его на `_OutputFiles`
- На Android (не в редакторе) — переопределяет путь через `AndroidPathResolver.GetAndroidPath()`

Путь вычисляется один раз и кэшируется.

### AndroidPathResolver (internal)

Активен только при `#if UNITY_ANDROID && !UNITY_EDITOR`. Через `AndroidJavaClass` обращается к `android.os.Environment.getExternalStoragePublicDirectory("Download")`. Перед обращением проверяет и запрашивает `android.permission.WRITE_EXTERNAL_STORAGE`.

---

## API

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `File.GetAppPath()` | `public static string` | Путь к директории вывода (кэшированный) |
| `File.CreateFolders(directory)` | `public static void` | Создаёт директорию, если не существует |

---

## Платформенное поведение

| Платформа | Путь |
|-----------|------|
| Windows / macOS / Linux | `{AppRoot}/_OutputFiles` |
| Android (устройство) | `/storage/emulated/0/Download` (или аналог) |
| Android (редактор) | `{AppRoot}/_OutputFiles` |

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `GetAppPath()` до инициализации | Ленивая инициализация при первом вызове |
| `_path` остался `null` после инициализации | Возвращает пустую строку |
| `CreateFolders()` — директория существует | Идемпотентно, ничего не происходит |
| Android — разрешение уже выдано | `checkSelfPermission` возвращает 0, запрос пропускается |
| Android — разрешение отклонено | `requestPermissions` вызывается, путь всё равно возвращается |
