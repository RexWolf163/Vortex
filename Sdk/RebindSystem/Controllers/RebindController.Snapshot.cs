using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Снимок отличий: сохранение, загрузка по правилам ТЗ 2.1, экспорт и импорт.
    ///
    /// Формат — сериализатор Vortex, вложенные разделы хранятся сжатыми строками. Корень — словарь строк
    /// (без POCO-класса и его <c>AssemblyQualifiedName</c> в файле: переименование типов снимок не ломает):
    /// <c>version</c>, <c>active</c> (сжатый список активных групп) и по ключу <c>g:&lt;группа&gt;</c> на
    /// каждую группу. Раздел группы — сжатый словарь «команда → сжатый раздел команды», раздел команды —
    /// сжатый список записей слотов. Каждый уровень разжимается и разбирается отдельно, поэтому нечитаемость
    /// изолирована своим уровнем: слот → команда → группа → весь снимок.
    ///
    /// Запись слота: <c>b[e]:&lt;id заводского биндинга&gt;=&lt;значение&gt;</c> или
    /// <c>a[e]:&lt;позиция&gt;=&lt;значение&gt;</c> для добавленного; <c>e</c> — назначен игроком явно;
    /// значение — <c>триггер|модификатор|модификатор</c>, пусто — клавиша снята.
    /// </summary>
    public sealed partial class RebindController
    {
        private const int SnapshotVersion = 1;
        private const string VersionKey = "version";
        private const string ActiveKey = "active";
        private const string GroupPrefix = "g:";
        private const string PackKey = "rebind";

        /// <summary>
        /// Исходный текст снимка, в котором при загрузке нашлась нечитаемость. Сохраняется копией перед первой
        /// перезаписью основного снимка.
        /// </summary>
        private string _pendingBackup;

        /// <summary>
        /// Хранилище не прочиталось: содержимое снимка неизвестно, перезаписывать его нельзя. Запись отключена до
        /// перезапуска, изменения действуют в памяти.
        /// </summary>
        private bool _storageUnreadable;

        /// <summary>
        /// Изменения сохраняются. <c>false</c> — работа только в памяти: драйвер не подключён или хранилище не
        /// прочиталось при загрузке.
        /// </summary>
        public bool CanPersist => RebindBus.HasDriver() && !_storageUnreadable;

        // ── Загрузка ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Прочитать снимок из хранилища и наложить на заводскую модель. Индекс и ассет пересобирает вызывающий.
        /// </summary>
        private void LoadSnapshot()
        {
            _pendingBackup = null;
            _storageUnreadable = false;
            var status = RebindBus.LoadSnapshot(out var data);
            switch (status)
            {
                case StorageReadStatus.NoData:
                    return;
                case StorageReadStatus.Error:
                    // Без драйвера сохранять некуда — об этом уже сказано при запуске.
                    if (RebindBus.HasDriver())
                    {
                        _storageUnreadable = true;
                        Debug.LogError("[RebindController] Хранилище снимка не прочиталось — действуют заводские " +
                                       "настройки, запись снимка отключена до перезапуска.");
                    }

                    return;
            }

            if (!ApplySnapshotText(data))
            {
                _pendingBackup = data;
                Debug.LogWarning("[RebindController] Снимок частично нечитаем: нечитаемое заменено заводским, " +
                                 "копия исходника будет сохранена перед первой перезаписью.");
            }
        }

        /// <summary>
        /// Наложить текст снимка на текущую (заводскую) модель. Загруженное применяется как есть, даже если
        /// нарушает правила. <c>false</c> — в тексте была нечитаемость.
        /// </summary>
        private bool ApplySnapshotText(string text)
        {
            Dictionary<string, string> root = null;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    root = text.DeserializeProperties<Dictionary<string, string>>();
                }
                catch (Exception)
                {
                    root = null;
                }
            }

            // Корень нечитаем или формат новее — весь снимок к заводскому.
            if (root == null
                || !root.TryGetValue(VersionKey, out var versionText)
                || !int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
                || version < 1 || version > SnapshotVersion)
                return false;

            var clean = true;

            if (root.TryGetValue(ActiveKey, out var packedActive))
            {
                var active = TryUnpack<List<string>>(packedActive);
                if (active == null)
                {
                    clean = false;
                }
                else
                {
                    // Неизвестные ключи — тихо; нарушение наборов переключения — как есть.
                    Model.ActiveGroups.Clear();
                    foreach (var key in active)
                        if (key != null && Model.GroupsByKey.ContainsKey(key))
                            Model.ActiveGroups.Add(key);
                }
            }

            foreach (var section in root)
            {
                if (!section.Key.StartsWith(GroupPrefix, StringComparison.Ordinal))
                    continue;

                var groupKey = section.Key.Substring(GroupPrefix.Length);
                if (!Model.GroupsByKey.ContainsKey(groupKey))
                    continue; // группы больше нет — тихо

                // Пустых разделов система не пишет: пустой результат — мусор, который разборщик свёл к пустой коллекции.
                var commands = TryUnpack<Dictionary<string, string>>(section.Value);
                if (commands == null || commands.Count == 0)
                {
                    clean = false; // раздел группы нечитаем — группа остаётся заводской
                    continue;
                }

                foreach (var entry in commands)
                {
                    if (!Model.Commands.TryGetValue(entry.Key, out var command) || !command.Serviced)
                        continue; // команды нет или она пропускается — тихо

                    var records = TryUnpack<List<string>>(entry.Value);
                    if (records == null || records.Count == 0)
                    {
                        clean = false; // раздел команды нечитаем — команда в группе остаётся заводской
                        continue;
                    }

                    foreach (var record in records)
                        if (!ApplyRecord(command, groupKey, record))
                            clean = false; // запись нечитаема — слот остаётся заводским
                }
            }

            return clean;
        }

        /// <summary>Наложить запись слота. <c>false</c> — запись нечитаема.</summary>
        private bool ApplyRecord(RebindCommand command, string sectionGroup, string record)
        {
            if (!DecodeRecord(record, out var factory, out var isExplicit, out var id, out var value))
                return false;

            var device = value.IsEmpty ? null : ServiceRule.DeviceLayout(value.Trigger);

            if (factory)
            {
                if (!Guid.TryParseExact(id, "N", out var bindingId))
                    return false;

                // Поиск по всем группам команды: если конфиг перенёс биндинг в другую группу, запись переезжает с ним.
                var slot = command.AllSlots.FirstOrDefault(s => s.FactoryBindingId == bindingId);
                if (slot == null)
                    return true; // заводского биндинга больше нет — тихо

                if (value.IsEmpty || GroupResolver.Matches(Model.GroupsByKey[slot.Group], device))
                {
                    slot.Value = value;
                    slot.IsExplicit = isExplicit;
                    return true;
                }

                // Устройство больше не подходит группе слота — значение уходит в первую подходящую группу.
                var target = FirstMatchingGroup(device);
                if (target != null)
                    PlaceAdded(command, target, -1, value, isExplicit);
                return true;
            }

            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return false;
            if (value.IsEmpty)
                return true; // пустой добавленный — восстанавливать нечего

            var group = GroupResolver.Matches(Model.GroupsByKey[sectionGroup], device)
                ? sectionGroup
                : FirstMatchingGroup(device);
            if (group == null)
                return true; // устройство не входит ни в одну группу — тихо

            PlaceAdded(command, group, group == sectionGroup ? index : -1, value, isExplicit);
            return true;
        }

        /// <summary>
        /// Поставить добавленное значение в свободный незаводской слот группы: сперва на записанную позицию,
        /// иначе в первый свободный, иначе — новым слотом сверх X (загруженное — загружено; нормализация при
        /// первой операции над командой).
        /// </summary>
        private void PlaceAdded(RebindCommand command, string groupKey, int preferredIndex, BindingValue value,
            bool isExplicit)
        {
            var list = command.Groups[groupKey];
            BindSlot target = null;
            if (preferredIndex >= 0 && preferredIndex < list.Count && IsFreeAddedSlot(list[preferredIndex]))
                target = list[preferredIndex];
            target ??= list.FirstOrDefault(IsFreeAddedSlot);
            if (target == null)
            {
                target = new BindSlot(command.Id, command.Map, groupKey, list.Count);
                list.Add(target);
                Model.Slots[target.BindKey] = target;
            }

            target.Value = value;
            target.IsExplicit = isExplicit;
        }

        private static bool IsFreeAddedSlot(BindSlot slot) => slot.FactoryBindingId == Guid.Empty && slot.Value.IsEmpty;

        private string FirstMatchingGroup(string deviceLayout)
        {
            if (deviceLayout == null)
                return null;
            foreach (var group in Model.Groups)
                if (GroupResolver.Matches(group, deviceLayout))
                    return group.Key;
            return null;
        }

        // ── Сохранение ──────────────────────────────────────────────────────────────────────────────

        partial void Persist()
        {
            if (Model == null || _storageUnreadable)
                return;

            // Нечитаемый исходник — копией до первой перезаписи. Копия не записалась — не перезаписываем,
            // повторим на следующей операции.
            if (_pendingBackup != null)
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                if (!RebindBus.BackupSnapshot(_pendingBackup, stamp))
                {
                    if (RebindBus.HasDriver())
                        Debug.LogError("[RebindController] Не удалось сохранить копию нечитаемого снимка — " +
                                       "перезапись снимка отложена.");
                    return;
                }

                _pendingBackup = null;
            }

            if (!RebindBus.SaveSnapshot(BuildSnapshotText()) && RebindBus.HasDriver())
                Debug.LogError("[RebindController] Не удалось сохранить снимок — изменения действуют до перезапуска.");
        }

        /// <summary>Текст снимка по текущей модели: только отличия, явно назначенные слоты и активность групп.</summary>
        private string BuildSnapshotText()
        {
            var root = new Dictionary<string, string>
            {
                [VersionKey] = SnapshotVersion.ToString(CultureInfo.InvariantCulture),
                [ActiveKey] = Pack(Model.ActiveGroups.ToList())
            };

            foreach (var group in Model.Groups)
            {
                var commands = new Dictionary<string, string>();
                foreach (var command in Model.Commands.Values)
                {
                    if (!command.Serviced)
                        continue;
                    var records = new List<string>();
                    foreach (var slot in command.GetSlots(group.Key))
                    {
                        var record = EncodeRecord(slot);
                        if (record != null)
                            records.Add(record);
                    }

                    if (records.Count > 0)
                        commands[command.Id] = Pack(records);
                }

                if (commands.Count > 0)
                    root[GroupPrefix + group.Key] = Pack(commands);
            }

            return root.SerializeProperties();
        }

        /// <summary>Запись слота или <c>null</c> — слот совпадает с заводским и не назначался игроком.</summary>
        private static string EncodeRecord(BindSlot slot)
        {
            var origin = slot.Origin;
            var factory = slot.FactoryBindingId != Guid.Empty;
            if (!factory && slot.Value.IsEmpty)
                return null;
            if (!slot.IsExplicit && origin != SlotOrigin.Overridden && origin != SlotOrigin.Added
                && origin != SlotOrigin.Cleared)
                return null;

            var kind = factory ? "b" : "a";
            var id = factory
                ? slot.FactoryBindingId.ToString("N")
                : slot.Index.ToString(CultureInfo.InvariantCulture);
            var flags = slot.IsExplicit ? "e" : string.Empty;
            var value = slot.Value.IsEmpty
                ? string.Empty
                : string.Join("|", new[] { slot.Value.Trigger }.Concat(slot.Value.Modifiers));
            return $"{kind}{flags}:{id}={value}";
        }

        /// <summary>Разбор записи слота. Значение должно быть структурно допустимым — иначе запись нечитаема.</summary>
        private static bool DecodeRecord(string record, out bool factory, out bool isExplicit, out string id,
            out BindingValue value)
        {
            factory = false;
            isExplicit = false;
            id = null;
            value = null;
            if (string.IsNullOrEmpty(record))
                return false;

            var colon = record.IndexOf(':');
            if (colon < 1)
                return false;
            var equals = record.IndexOf('=', colon + 1);
            if (equals < 0)
                return false;

            var head = record.Substring(0, colon);
            if (head[0] == 'b')
                factory = true;
            else if (head[0] != 'a')
                return false;
            for (var i = 1; i < head.Length; i++)
            {
                if (head[i] != 'e')
                    return false;
                isExplicit = true;
            }

            id = record.Substring(colon + 1, equals - colon - 1);
            var body = record.Substring(equals + 1);
            if (body.Length == 0)
            {
                value = BindingValue.Empty;
                return true;
            }

            var parts = body.Split('|');
            if (parts.Length > 3)
                return false;
            value = new BindingValue(parts[0], parts.Skip(1).ToArray());
            return ServiceRule.ValidateCandidate(value) == RejectReason.None;
        }

        private static string Pack(object value) => value.SerializeProperties().Compress(PackKey);

        /// <summary>Разжать и разобрать раздел. <c>null</c> — раздел нечитаем.</summary>
        private static T TryUnpack<T>(string packed) where T : class
        {
            if (string.IsNullOrEmpty(packed))
                return null;
            try
            {
                var text = packed.Decompress(PackKey);
                return string.IsNullOrEmpty(text) ? null : text.DeserializeProperties<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ── Экспорт и импорт ────────────────────────────────────────────────────────────────────────

        /// <summary>Текст текущего снимка — точка отката. <c>null</c> — система не загружена.</summary>
        public string Export() => Model == null ? null : BuildSnapshotText();

        /// <summary>
        /// Массовый ремап: полностью заменяет пользовательское состояние, включая активность групп. Разбор — по
        /// правилам загрузки; нечитаемые части — заводские, полностью нечитаемый текст — заводские целиком.
        /// </summary>
        public RebindResult Import(string json)
        {
            if (!IsInitialized)
                return RebindResult.Cancelled();

            BuildModel();
            if (!ApplySnapshotText(json))
                Debug.LogWarning("[RebindController] Импортируемый снимок частично нечитаем — нечитаемое заменено заводским.");
            RebuildIndex();
            ApplyAllToAsset();

            Persist();
            RebindBus.RaiseRebuilt();
            RebindBus.RaiseGroupsChanged();
            return RebindResult.Applied(Model.Slots.Keys.ToArray(), Array.Empty<ConflictInfo>());
        }
    }
}
