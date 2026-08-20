using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Core.StateValve
{
    /// <summary>
    /// Реактивный вентиль состояния: сводит N именованных булевых створок («ключей») в один булев итог
    /// по режиму свёртки (<see cref="ValveMode"/>) с опциональной инверсией. Нейтрален к домену —
    /// оперирует «открыто/закрыто», смысл («закрыто = пауза» и т.п.) придаёт применяющий.
    ///
    /// Итог живёт в реактивном <see cref="State"/> под owner-lock: снаружи его напрямую не переписать,
    /// только через <see cref="Open"/>/<see cref="Close"/>. Пересчёт синхронный после каждой мутации.
    /// Пустой набор ключей → открыт (вентиль не закрывали); инверсия применяется после свёртки.
    ///
    /// Ре-энтрантность (однопоточно, главный поток): своих гвардов клапан не добавляет намеренно.
    /// Безобидная вложенность (подписчик на <see cref="State"/> в колбэке зовёт Open/Close) отрабатывает
    /// — <see cref="BoolData"/> дедупит, порядок last-write-wins. Осциллирующая (подписчик крутит ключ
    /// так, что итог переворачивается обратно) — ошибка композиции вызывающего: проявится stack overflow
    /// как fail-fast сигнал о встречной петле.
    /// </summary>
    public class StateValve
    {
        private readonly Dictionary<string, bool> _keys = new();
        private readonly ValveMode _mode;
        private readonly bool _invert;
        private readonly object _ownerKey = new();

        /// <summary>Реактивный итог свёртки: открыт (true) / закрыт (false). Пишется только пересчётом (owner-lock).</summary>
        public BoolData State { get; }

        /// <summary>Створки «имя → открыт» только для чтения (инспектор-отладка хендлера).</summary>
        public IReadOnlyDictionary<string, bool> Keys => _keys;

        public StateValve(ValveMode mode = ValveMode.And, bool invert = false)
        {
            _mode = mode;
            _invert = invert;
            // Пусто → открыт; замок вешаем сразу — примитив не копируется (DeepCopy/CopyFrom к нему неприменимы).
            State = new BoolData(true, _ownerKey);
        }

        /// <summary>Открыть створку по имени. Пустой/<c>null</c> id — fail-fast (баг вызывающего).</summary>
        public void Open(string id) => Set(id, true);

        /// <summary>Закрыть створку по имени. Пустой/<c>null</c> id — fail-fast (баг вызывающего).</summary>
        public void Close(string id) => Set(id, false);

        private void Set(string id, bool open)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("StateValve key must be a non-empty string.", nameof(id));

            _keys[id] = open;
            Recompute();
        }

        private void Recompute()
        {
            var raw = _keys.Count == 0 || Combine();
            State.Set(_invert ? !raw : raw, _ownerKey);
        }

        private bool Combine() => _mode switch
        {
            ValveMode.And => _keys.Values.All(open => open),
            ValveMode.Or => _keys.Values.Any(open => open),
            ValveMode.Xor => _keys.Values.Count(open => open) == 1,
            _ => throw new ArgumentOutOfRangeException(nameof(_mode), _mode, "Unknown ValveMode.")
        };
    }
}
