#if USING_VORTEX_CURSOR
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.InputBusSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Абстрактный драйвер ввода курсора. POCO (не MonoBehaviour): список реализаций живёт в
    /// <see cref="InputDriverSet"/> (<c>[SerializeReference]</c>), подключается контроллером
    /// <see cref="CursorInputLoader"/> на старте приложения. Роль — поймать сигнал ввода и отрепортить
    /// его в <see cref="VirtualCursorController"/>. Гейта нет: курсор надсистемный, драйвер всегда активен.
    ///
    /// Экшены резолвятся по строковому id «Карта/Экшен» через <see cref="InputController"/> (Vortex-стандарт),
    /// а не через <c>InputActionProperty</c>. Драйвер, будучи подключённым, активен всегда; ситуативного
    /// отсечения ввода на уровне пакета нет.
    /// </summary>
    [Serializable]
    public abstract class InputDriver
    {
        /// <summary>Подключение: резолв экшенов, включение карт, подписка на сигналы.</summary>
        public abstract void Connect();

        /// <summary>Отключение: отписка, отключение карт. Зовётся на тердауне/hotplug-removal.</summary>
        public abstract void Disconnect();

        /// <summary>Нужен ли покадровый <see cref="Tick"/> (интеграция скорости у направленного драйвера).</summary>
        public virtual bool NeedsTick => false;

        /// <summary>Покадровый апдейт (только при <see cref="NeedsTick"/>). dt — <c>Time.unscaledDeltaTime</c>.</summary>
        public virtual void Tick(float unscaledDeltaTime) { }

        /// <summary>Скрывает ли курсор этот источник (прямой контакт — касание). Применяется last-source-wins.</summary>
        public virtual bool HidesCursor => false;

        /// <summary>Поддержка текущей платформы (гейт на подключение). По умолчанию — везде.</summary>
        public virtual bool SupportsPlatform(RuntimePlatform platform) => true;

        // --- helpers для наследников (Vortex-стандарт работы с вводом) ---

        /// <summary>Резолв <see cref="InputAction"/> по id «Карта/Экшен». null — не найден.</summary>
        protected static InputAction ResolveAction(string actionId) => InputController.GetAction(actionId);

        /// <summary>Включить карту экшена (регистрируемся её пользователем). No-op, если экшен не резолвится.</summary>
        protected void EnableMap(string actionId)
        {
            if (ResolveAction(actionId) == null || !TryMapId(actionId, out var mapId)) return;
            InputController.AddMapUser(mapId, this);
        }

        /// <summary>Выключить карту экшена (снимаемся с пользователей; карта гаснет, если пользователей нет).</summary>
        protected void DisableMap(string actionId)
        {
            if (ResolveAction(actionId) == null || !TryMapId(actionId, out var mapId)) return;
            InputController.RemoveMapUser(mapId, this);
        }

        /// <summary>Подписка на сигналы экшена через LIFO-шину <see cref="InputController"/>.</summary>
        protected void SubscribeAction(string actionId, Action performed, Action canceled)
        {
            if (ResolveAction(actionId) == null) return;
            InputController.AddActionUser(actionId, this, performed, canceled);
        }

        /// <summary>Отписка от сигналов экшена.</summary>
        protected void UnsubscribeAction(string actionId)
        {
            if (ResolveAction(actionId) == null) return;
            InputController.RemoveActionUser(actionId, this);
        }

        /// <summary>Отрепортить позицию в контроллер (с флагом скрытия текущего источника).</summary>
        protected void Report(Vector2 screen, PointerSourceKind source)
            => VirtualCursorController.ReportPointer(screen, source, HidesCursor);

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
#endif
