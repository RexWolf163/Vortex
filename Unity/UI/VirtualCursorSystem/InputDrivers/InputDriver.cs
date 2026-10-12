using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.InputBusSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Controllers;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Абстрактный драйвер ввода курсора. POCO (не MonoBehaviour): список реализаций живёт в
    /// <see cref="InputDriverSet"/> (<c>[SerializeReference]</c>), подключается контроллером
    /// <see cref="CursorInputLoader"/> на старте приложения. Роль — поймать сигнал ввода и отрепортить
    /// его в <see cref="Controllers.VirtualCursorController"/>. Гейта нет: курсор надсистемный, драйвер всегда активен.
    ///
    /// Экшены резолвятся по строковому id «Карта/Экшен» через <see cref="InputController"/> (Vortex-стандарт),
    /// а не через <c>InputActionProperty</c>. Драйвер, будучи подключённым, активен всегда; ситуативного
    /// отсечения ввода на уровне пакета нет. Карты экшенов драйвер не включает — их включение на стороне
    /// проекта (например, <c>InputMapHandler</c>); без включённой карты события экшенов не приходят.
    /// </summary>
    [Serializable]
    public abstract class InputDriver
    {
        /// <summary>Подключение: резолв экшенов и подписка на их сигналы.</summary>
        public abstract void Connect();

        /// <summary>Отключение: отписка от сигналов, сброс своего состояния. Зовётся на выходе из приложения.</summary>
        public abstract void Disconnect();

        /// <summary>Нужен ли покадровый <see cref="Tick"/> (интеграция скорости у направленного драйвера).</summary>
        public virtual bool NeedsTick => false;

        /// <summary>Покадровый апдейт (только при <see cref="NeedsTick"/>). dt — <c>Time.unscaledDeltaTime</c>.</summary>
        public virtual void Tick(float unscaledDeltaTime)
        {
        }

        /// <summary>
        /// Скрывает ли курсор этот источник (прямой контакт — касание): его репорт скрывает курсор, а репорт нескрывающего
        /// источника возвращает курсор значимым сдвигом (фильтр дрейфа в <c>VirtualCursorController.ReportPointer</c>).
        /// </summary>
        public virtual bool HidesCursor => false;

        /// <summary>Поддержка текущей платформы (гейт на подключение). По умолчанию — везде.</summary>
        public virtual bool SupportsPlatform(RuntimePlatform platform) => true;

        // --- helpers для наследников (Vortex-стандарт работы с вводом) ---

        /// <summary>Резолв <see cref="InputAction"/> по id «Карта/Экшен». null — не найден.</summary>
        protected static InputAction ResolveAction(string actionId) => InputController.GetAction(actionId);

        /// <summary>
        /// Подписка на сигналы экшена через LIFO-шину <see cref="InputController"/>. Валидность id
        /// не проверяется — проверку делает драйвер до вызова.
        /// </summary>
        /// <param name="actionId">Id экшена «Карта/Экшен».</param>
        /// <param name="performed">Колбэк на performed.</param>
        /// <param name="canceled">Колбэк на canceled (может быть null).</param>
        protected void SubscribeAction(string actionId, Action performed, Action canceled)
        {
            InputController.AddActionUser(actionId, this, performed, canceled);
        }

        /// <summary>Отписка от сигналов экшена.</summary>
        protected void UnsubscribeAction(string actionId)
        {
            InputController.RemoveActionUser(actionId, this);
        }

        /// <summary>Отрепортить позицию в контроллер (с флагом скрытия текущего источника).</summary>
        protected void Report(Vector2 screen)
            => VirtualCursorController.ReportPointer(screen, HidesCursor);

        /// <summary>Выделить id карты из id экшена «Карта/Экшен». false — id пуст или без карты.</summary>
        private static bool TryMapId(string actionId, out string mapId)
        {
            mapId = null;
            if (string.IsNullOrEmpty(actionId)) return false;
            var i = actionId.IndexOf('/');
            if (i <= 0) return false;
            mapId = actionId.Substring(0, i);
            return true;
        }

#if UNITY_EDITOR
        /// <summary>Источник дропдауна id экшенов для инспектора (ValueSelector).</summary>
        protected string[] GetInputActions() => InputController.GetActions();
#endif
    }
}