#if USING_NANINOVELL
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.Core;
using Vortex.NaniExtensions.GalleryStoriesSystem.Presets;
using Vortex.Sdk.GallerySystem;
using Vortex.Unity.Extensions.Abstractions;

namespace Vortex.NaniExtensions.GalleryStoriesSystem.Models
{
    /// <summary>
    /// Модель нарративной галлерейной карточки. Реализация <see cref="IGalleryEntry"/>
    /// для GallerySystem — попадает в галлерейный пул через <c>GalleryView</c>.
    ///
    /// Nani-скрипт хранится driver-нейтрально — строкой пути (<c>Script.Path</c>).
    /// Причина: <c>Naninovel.Script</c>-инстанс может быть выгружен из памяти после
    /// проигрывания (см. NaniWrapper: <c>ScriptPlayer.ResetService()</c> в OnLoadGame),
    /// после чего прямая ссылка на ScriptableObject становится fake-null. Строка живёт всегда.
    ///
    /// В отличие от <c>GallerySpritesSystem</c>, здесь **нет промежуточного bus/viewer'а**:
    /// naninovel сама предоставляет весь UI-канал (принтеры, актёры, звук). <see cref="Show"/>
    /// напрямую делегирует в <c>NaniWrapper.PlayScript</c>, <see cref="Hide"/> — в
    /// <c>ScriptPlayer.Stop()</c>.
    /// </summary>
    public class GalleryStoryModel : Record, IGalleryEntry
    {
        /// <summary>Иконка-превью в галлерее (из <see cref="Presets.GalleryStoryPreset"/> базовым CopyFrom).</summary>
        public Sprite Icon { get; protected set; }

        /// <summary>
        /// Путь к nani-скрипту в формате <see cref="Naninovel.Script.Path"/>. Owner-lock: internal —
        /// доступ только внутри сборки пакета.
        /// </summary>
        internal string ScriptPath { get; set; }

        /// <summary>
        /// Флаг: наш скрипт реально стартовал в Naninovel (пришёл <c>OnNaniStart</c> с совпадением <c>PlayedScript.Path</c>).
        /// Защищает от гонки: если между <c>Show</c> и стартом нашего скрипта приходит чужой <c>OnNaniStop</c>,
        /// <c>HandleAutoStop</c> его игнорирует. Non-serialized — runtime-only, в сохранение не входит.
        /// </summary>
        [NonSerialized] private bool _active;

        /// <summary>
        /// Кастомный <c>CopyFrom</c>: базовое <see cref="ObjectExtCopy.CopyFrom"/> копирует public
        /// property (Guid, Name, Description, Icon) через reflection. <see cref="ScriptPath"/>
        /// — internal, public reflection его не видит; поэтому копируем явно из
        /// <see cref="GalleryStoryPreset.ScriptPathTemplate"/>. String immutable — deep-clone не нужен.
        /// </summary>
        public bool CopyFrom(SoData source)
        {
            var ok = ObjectExtCopy.CopyFrom(this, source);
            if (source is GalleryStoryPreset preset)
                ScriptPath = preset.ScriptPathTemplate;
            return ok;
        }

        /// <summary>Каталог статичен, в тело сейва не входит. Статус «разблокирована» живёт в RecordMarksSystem.</summary>
        public override string GetDataForSave() => null;

        /// <summary>Каталог статичен — восстанавливать нечего.</summary>
        public override void LoadFromSaveData(string data)
        {
        }

        /// <summary>
        /// IGalleryEntry.Show — синхронный. Запускает nani-скрипт fire-and-forget:
        /// <c>NaniWrapper.PlayScript</c> сам шлюзует Load→Play и корректно обрабатывает пустой путь
        /// (LogError + return). Контракт <see cref="IGalleryEntry"/> синхронный — async уходит в <c>Forget()</c>.
        ///
        /// Подписки на <c>NaniWrapper.OnNaniStart</c>/<c>OnNaniStop</c> — для авто-<see cref="Hide"/>
        /// когда nani сама завершит скрипт. <c>OnNaniStart</c> нужен, чтобы отличить наш реальный
        /// старт от чужих скриптов (закрывает гонку: чужой OnNaniStop между Show и загрузкой нашего
        /// скрипта не рвёт подписку). NaniWrapper уже фильтрует ложные Stop через
        /// <c>ConfirmStopDeferred</c>, поэтому события приходят только на реальные переходы.
        /// </summary>
        public void Show() => Show(CancellationToken.None);

        /// <summary>Overload с CancellationToken — токен пробрасывается в <c>PlayScript</c> (отменяет ожидание шлюза).</summary>
        public void Show(CancellationToken ct)
        {
            // Сбрасываем гонка-флаг: до OnNaniStart наш скрипт ещё не считается активным.
            _active = false;

            // -= перед += страхует от двойной подписки при повторном Show того же инстанса.
            NaniWrapper.OnNaniStart -= HandleNaniStart;
            NaniWrapper.OnNaniStart += HandleNaniStart;
            NaniWrapper.OnNaniStop -= HandleAutoStop;
            NaniWrapper.OnNaniStop += HandleAutoStop;

            NaniWrapper.PlayScript(ScriptPath, token: ct).Forget();
        }

        /// <summary>
        /// Handler на <c>OnNaniStart</c>: событие приходит на реальный старт любого скрипта.
        /// Если стартовал именно наш — переводим модель в active-фазу и отписываемся от Start
        /// (больше не интересует). Иначе — это чужой скрипт, ждём дальше.
        /// </summary>
        private void HandleNaniStart()
        {
            var current = NaniWrapper.ScriptPlayer.PlayedScript;
            if (current == null || current.Path != ScriptPath) return;

            _active = true;
            NaniWrapper.OnNaniStart -= HandleNaniStart;
        }

        /// <summary>
        /// Handler на <c>OnNaniStop</c>: событие приходит на любой останов nani. Логика:
        /// <list type="bullet">
        ///   <item><c>_active == false</c> — наш скрипт ещё не стартовал, чужой останов игнорируем.</item>
        ///   <item>Наш скрипт по-прежнему в <c>PlayedScript</c> — это чужой останов между нашими фазами; ждём.</item>
        ///   <item>Наш скрипт больше не активен — отписываемся полностью и симметрично закрываем через <see cref="Hide"/>.</item>
        /// </list>
        /// </summary>
        private void HandleAutoStop()
        {
            if (!_active) return;

            var current = NaniWrapper.ScriptPlayer.PlayedScript;
            if (current != null && current.Path == ScriptPath) return;

            _active = false;
            NaniWrapper.OnNaniStop -= HandleAutoStop;
            NaniWrapper.OnNaniStart -= HandleNaniStart; // safety: если Start не пришёл, всё равно снимаем подписку
            Hide();
        }

        /// <summary>
        /// IGalleryEntry.Hide — синхронный. Останавливает nani-скрипт **только если** сейчас
        /// проигрывается именно этот. Naninovel держит один активный скрипт на весь проект —
        /// глобальный <c>Stop()</c> без проверки прервал бы чужой сюжетный запуск, случившийся
        /// в промежутке между галлерейным Show и Hide. Вызов ниже auto-Hide (когда nani уже
        /// сама остановилась) отработает как no-op — идемпотентно.
        /// </summary>
        public void Hide()
        {
            var current = NaniWrapper.ScriptPlayer.PlayedScript;
            if (current == null) return;                 // ничего не играет — нечего останавливать
            if (current.Path != ScriptPath) return;      // играет чужой скрипт — не трогаем
            NaniWrapper.ScriptPlayer.Stop();
        }
    }
}
#endif
