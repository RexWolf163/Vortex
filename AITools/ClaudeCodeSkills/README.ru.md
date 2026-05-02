---
title: ClaudeCodeSkills
platform: Claude Code (CLI, Anthropic)
language: ru
---

# ClaudeCodeSkills — скиллы для разработки и оценки пакетов Vortex

Набор скиллов для [Claude Code](https://docs.claude.com/claude-code) — CLI-агента от Anthropic. Скиллы автоматизируют типовые операции вокруг Vortex: проектирование новых пакетов по канону, определение архитектурного слоя для новой системы, жёсткую оценку качества кода по формулам.

Папка содержит **исходные тексты** скиллов. Чтобы Claude Code их использовал, скиллы нужно скопировать в одну из стандартных директорий — см. раздел «Установка».

## Состав

| Скилл | Назначение | Триггеры |
|-------|-----------|----------|
| [vortex-layer-detect](vortex-layer-detect/SKILL.md) | Определение архитектурного слоя (1/2/3) для новой системы Vortex до генерации кода | «сделаем систему X», «добавим подсистему Y», «хочу пакет для Z» в контексте Vortex |
| [vortex-initial-architecture](vortex-initial-architecture/SKILL.md) | Проектирование первичной архитектуры пакета Vortex по каноническому алгоритму (Stage 0 + 7 шагов) | «спроектируй архитектуру», «сформируй структуру пакета», «набросай каркас системы X» |
| [code-quality](code-quality/SKILL.md) | Жёсткая оценка качества Unity-кода по формулам с архитектурным множителем. 4 ветки по объёму (≤3000 LOC / 3000–12000 / частичный / субъективный) | «оцени качество кода», «прогони по критериям оценки», «проверь пакет на качество» |

## Когда какой скилл применять

```
Новая система в Vortex
  └─► vortex-layer-detect           (определить слой)
        └─► vortex-initial-architecture  (спроектировать пакет)
              └─► (написать код)
                    └─► code-quality   (оценить готовый пакет)
```

- **vortex-layer-detect** — первый шаг при добавлении любой новой системы. MUST run до генерации кода.
- **vortex-initial-architecture** — после определения слоя. Вырабатывает структуру (шина, конфиг, контроллер, модель, пресеты, интерфейс, представления).
- **code-quality** — после реализации (или для аудита существующего пакета). Возвращает балл по 10-балльной шкале с разбором архитектурных дефектов.

## Установка

Claude Code загружает скиллы из двух мест:

| Уровень | Путь | Когда использовать |
|---------|------|-------------------|
| User | `~/.claude/skills/<skill-name>/SKILL.md` | На все проекты текущего пользователя |
| Project | `<project-root>/.claude/skills/<skill-name>/SKILL.md` | Только в этом проекте (коммитится в репозиторий) |

**Установка вручную:**

```bash
# User-level (на все проекты)
cp -r Vortex/AITools/ClaudeCodeSkills/vortex-layer-detect ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/vortex-initial-architecture ~/.claude/skills/
cp -r Vortex/AITools/ClaudeCodeSkills/code-quality ~/.claude/skills/

# Project-level (только в текущем проекте)
mkdir -p .claude/skills
cp -r Vortex/AITools/ClaudeCodeSkills/* .claude/skills/
```

После копирования Claude Code увидит скиллы при следующем запуске. Список активных скиллов доступен через `/skills` в CLI.

## Платформа

Скиллы написаны под **Claude Code** и используют его инструменты (`Agent`, `Bash`, `Grep`, `Read`, `Edit`, `Write`, `Glob`). Соглашения о делегировании — через `subagent_type=Explore`/`general-purpose` с указанием модели (`haiku`/`sonnet`/`opus`).

Для других LLM-агентских платформ (Cursor, Continue, OpenAI Assistants, Cline, Aider) требуется адаптация:
- Замена вызовов инструментов на платформенные эквиваленты.
- Замена логики делегирования (subagent с моделью X) на доступный механизм.
- Содержательная часть (формулы, чек-листы, канон Vortex) переносится без изменений.

## Жизненный цикл скиллов

Папка `Vortex/AITools/ClaudeCodeSkills/` — **источник истины**. При изменении скилла:

1. Править файл в этой папке.
2. Пересинхронизировать в `~/.claude/skills/<name>/` (или `.claude/skills/<name>/`).

Симлинк через `mklink` (Windows) или `ln -s` (Unix) тоже работает, но затрудняет правки в IDE и git-историю — поэтому рекомендован обычный copy.

## Связь с другими файлами проекта

- [`F:\Claude\Vortex\AITools\Prompt code quality analysis.md`](../Prompt%20code%20quality%20analysis.md) — оригинальный LLM-промпт оценки качества (более ранняя ручная версия). Скилл `code-quality` расширяет его веткой A+ и формализованным протоколом.
- [`F:\Claude\Критерии_оценки_кода_Unity.md`](../../../Критерии_оценки_кода_Unity.md) — канон формул оценки. Скилл `code-quality` содержит его инлайн.
- [`F:\Claude\vortex-context.md`](../../../vortex-context.md) и [`F:\Claude\architecture_context.md`](../../../architecture_context.md) — архитектурные основания, на которых построены скиллы `vortex-layer-detect` и `vortex-initial-architecture`.

## Версионирование

Скиллы версионируются вместе с пакетом Vortex (через git-историю файлов в папке). Frontmatter `description` поля скиллов служит триггером для Claude Code — менять его осторожно, т.к. это влияет на распознавание скилла моделью.
