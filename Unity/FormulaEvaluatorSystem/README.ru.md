# FormulaEvaluator

Пакет вычисления математических формул, заданных строкой, с привязкой параметров к членам класса через Inspector.

## Назначение

- Объявление формулы строковым полем с атрибутом `[Formula]`
- Привязка параметров `{0}`, `{1}`, ... к полям, свойствам, методам и константам класса через выпадающий список
- Предварительный просмотр результата вычисления по тестовым значениям в Inspector
- Рекурсивный парсер математических выражений без внешних зависимостей

Вне ответственности: runtime-вычисление формулы (парсер доступен, привязка — ответственность проекта).

## Зависимости

| Пакет | Назначение |
|-------|-----------|
| `ru.vortex.unity.editortools` | MultiDrawer, PropertyData, DrawingUtility, SearchablePopup |

## Формат формулы

```
"sqrt(3 + {0}^2) * ({1} / {2})"
```

- `{N}` — слот параметра, N — индекс (от 0)
- Операторы: `+`, `-`, `*`, `/`, `^` (возведение в степень, правоассоциативное)
- Скобки: `(`, `)`
- Константы: `pi`, `e`
- Функции (1 аргумент): `sqrt`, `abs`, `sin`, `cos`, `tan`, `log`, `floor`, `ceil`, `round`
- Функции (2 аргумента): `min`, `max`, `pow`
- Функции (3 аргумента): `clamp(value, min, max)`

## Компоненты

```
FormulaEvaluatorSystem/
├── Attributes/
│   └── FormulaAttribute.cs          → атрибут [Formula("slotsFieldName")]
├── Model/
│   └── FormulaSlot.cs               → сериализуемая привязка: memberName + testValue
├── FormulaParser.cs                  → рекурсивный парсер выражений
├── FormulaReflectionResolver.cs      → #if UNITY_EDITOR: сбор числовых членов класса
└── FormulaDrawer.cs                  → #if UNITY_EDITOR: отрисовка через MultiDrawer
```

## Использование

```csharp
using Vortex.Unity.FormulaEvaluatorSystem.Attributes;
using Vortex.Unity.FormulaEvaluatorSystem.Model;

public class DamageCalculator : MonoBehaviour
{
    [SerializeField] private float baseAttack;
    [SerializeField] private int level;
    protected float defenseModifier = 0.5f;

    [Formula(nameof(damageSlots))]
    [SerializeField] private string damageFormula = "sqrt({0}) * {1} + {2}";
    [SerializeField, HideInInspector] private FormulaSlot[] damageSlots;
}
```

### Внешний вид Inspector (сверху вниз)

1. **Info bubble** — результат вычисления по тестовым значениям (или текст ошибки)
2. **Label** — имя поля (жирный, на всю ширину)
3. **Поле формулы** — строковый ввод
4. **Список слотов** — по одной строке на каждый `{N}`:
   - Слева: метка `{N}` + выпадающий список членов класса (SearchablePopup)
   - Справа: поле ввода тестового значения (float, по умолчанию 0)

### Группировка в выпадающем списке

Члены класса группируются по категории и происхождению:

```
Fields — Own/
  health : int
  mana : float
Fields — Inherited/
  maxHealth : float
Properties — Own/
  DamageMultiplier : float
Methods — Own/
  GetBaseAttack : float
Constants/
  MAX_LEVEL : int
```

### Допустимые члены

- **Поля**: любой модификатор доступа, instance и static
- **Свойства**: с геттером, любой модификатор доступа
- **Методы**: без параметров, с числовым возвращаемым типом
- **Константы**: `const` и `static readonly` числовых типов
- Числовые типы: `int`, `float`, `double`, `long`, `decimal`, `byte`, `short`, `uint`, `ulong`, `ushort`, `sbyte`
- Унаследованные `private` члены исключаются (недоступны)

## API парсера

| Метод | Описание |
|-------|----------|
| `FormulaParser.Evaluate(formula, parameters)` | Вычисление. Исключение при ошибке |
| `FormulaParser.TryEvaluate(formula, parameters, out result, out error)` | Безопасное вычисление |
| `FormulaParser.GetMaxSlotIndex(formula)` | Максимальный индекс `{N}` в формуле (-1 если нет) |

### Пример runtime-вычисления

```csharp
var parameters = new double[] { 10.0, 5.0, 2.0 };
if (FormulaParser.TryEvaluate("sqrt({0}) * {1} + {2}", parameters, out var result, out var error))
    Debug.Log($"Result: {result}");
else
    Debug.LogError(error);
```

## Граничные случаи

- Пустая формула — topper не отрисовывается, слотов нет
- Несмежные индексы (`{0} + {5}`) — создаётся 6 слотов, промежуточные не привязаны
- Изменение формулы — массив слотов автоматически ресайзится, существующие привязки сохраняются
- Деление на 0 — возвращает `Infinity` (поведение IEEE 754)
- Неизвестная функция или константа — ошибка парсинга в topper
- Тестовое значение не задано — считается 0
- Отрицательные подставляемые значения оборачиваются в скобки для корректного парсинга
