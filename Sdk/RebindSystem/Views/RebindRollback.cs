using System;
using System.Collections.Generic;
using System.Text;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Unity.UI.RollbackSystem;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Источник отката переназначений клавиш, включая активность групп. Точка отката — снимок
    /// (<c>Export</c>), откат — закрыть открытый перехват и <c>Import</c> снимка.
    ///
    /// Изменения сравниваются не по тексту снимка: разделы снимка сжаты ZIP, а записи архива несут время
    /// создания — два экспорта одного состояния дают разные строки. Сравнивается детерминированный отпечаток
    /// модели: значения всех слотов и активность групп. Пересчёт — только по событиям шины.
    ///
    /// До загрузки системы точки нет: изменений нет, откат пустой.
    /// </summary>
    [Serializable]
    public class RebindRollback : RollbackSource
    {
        private string _snapshot;
        private string _fingerprint;

        protected override void Subscribe()
        {
            RebindBus.OnSlotsChanged += OnSlotsChanged;
            RebindBus.OnRebuilt += Refresh;
            RebindBus.OnGroupsChanged += Refresh;
        }

        protected override void Unsubscribe()
        {
            RebindBus.OnSlotsChanged -= OnSlotsChanged;
            RebindBus.OnRebuilt -= Refresh;
            RebindBus.OnGroupsChanged -= Refresh;
        }

        protected override void MakeCheckpoint()
        {
            if (!RebindBus.IsReady)
            {
                _snapshot = null;
                _fingerprint = null;
                return;
            }

            _snapshot = RebindBus.Controller.Export();
            _fingerprint = Fingerprint();
        }

        protected override bool DiffersFromCheckpoint() => _fingerprint != null && Fingerprint() != _fingerprint;

        protected override void Restore()
        {
            if (_snapshot == null)
                return;
            // Открытый перехват дописал бы клавишу поверх отката.
            RebindBus.Controller.CancelSaving();
            RebindBus.Controller.Import(_snapshot);
        }

        private void OnSlotsChanged(IReadOnlyList<string> bindKeys) => Refresh();

        /// <summary>Отпечаток состояния: активность групп и значения слотов в порядке конфига и ассета.</summary>
        private static string Fingerprint()
        {
            var data = RebindBus.Data;
            var builder = new StringBuilder();
            foreach (var group in data.Groups)
                builder.Append(data.IsGroupActive(group.Key) ? '+' : '-').Append(group.Key).Append('\n');
            foreach (var command in data.GetCommands())
            foreach (var group in data.Groups)
            foreach (var slot in command.GetSlots(group.Key))
                builder.Append(slot.BindKey).Append('=').Append(slot.Value).Append('\n');
            return builder.ToString();
        }
    }
}
