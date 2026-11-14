using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.CutsceneSystem.Catalog;
using Vortex.Sdk.CutsceneSystem.Models;
using Vortex.Unity.Extensions;

namespace Vortex.Sdk.CutsceneSystem
{
    /// <summary>
    /// Шина управления катсценой: хранит, какая катсцена открыта и на какой она фазе, и сообщает об изменениях
    /// событиями. Ассеты не грузит и ничего не показывает — это делает представление (<c>CutsceneView</c>).
    ///
    /// Катсцена в моменте одна: <see cref="Open(CutsceneData)"/> поверх открытой сначала закрывает прежнюю.
    /// Выход из игровой сессии (<see cref="GameStates.Off"/>) закрывает катсцену.
    ///
    /// Состояние меняется до рассылки события — подписчик всегда видит в <see cref="Data"/> итог команды
    /// </summary>
    public static class CutsceneController
    {
        private static CutsceneCatalog _catalog;

        /// <summary>
        /// Состояние показа
        /// </summary>
        public static CutsceneModel Data { get; } = new();

        public static bool IsOpen => Data.Cutscene != null;

        /// <summary>
        /// Катсцена открыта: <see cref="Data"/> указывает на неё, фаза — первая
        /// </summary>
        public static event Action OnOpened;

        /// <summary>
        /// Сменилась фаза открытой катсцены
        /// </summary>
        public static event Action OnPhaseChanged;

        /// <summary>
        /// Катсцена закрыта
        /// </summary>
        public static event Action OnClosed;

        /// <summary>
        /// Все ключи зарегистрированного каталога. Пусто, если каталог не найден
        /// </summary>
        public static IReadOnlyList<string> AllKeys => _catalog != null ? _catalog.Keys : Array.Empty<string>();

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            _catalog = AssetDatabaseExt.GetSingletonAsset<CutsceneCatalog>();
            Data.Cutscene = null;
            Data.PhaseIndex = 0;
            GameController.OnGameStateChanged -= OnGameStateChanged;
            GameController.OnGameStateChanged += OnGameStateChanged;
        }

        private static void OnGameStateChanged()
        {
            if (GameController.GetState() == GameStates.Off)
                Close();
        }

        /// <summary>
        /// Открыть катсцену с первой фазы. Открытая катсцена сначала закрывается
        /// </summary>
        public static void Open(CutsceneData cutscene)
        {
            if (cutscene == null)
            {
                Debug.LogError("[CutsceneController] cutscene == null");
                return;
            }

            if (cutscene.Phases.Count == 0)
            {
                Debug.LogError($"[CutsceneController] У катсцены '{cutscene.name}' нет фаз.", cutscene);
                return;
            }

            Close();

            Data.Cutscene = cutscene;
            Data.PhaseIndex = 0;
            OnOpened?.Invoke();
        }

        /// <summary>
        /// Открыть катсцену по ключу каталога (имя ассета катсцены)
        /// </summary>
        public static void Open(string key)
        {
            if (_catalog == null)
            {
                Debug.LogError(
                    $"[CutsceneController] Каталог катсцен не найден, открыть по ключу '{key}' невозможно.");
                return;
            }

            var cutscene = _catalog.Get(key);
            if (cutscene == null)
            {
                Debug.LogError($"[CutsceneController] Ключ '{key}' не найден в каталоге '{_catalog.name}'.",
                    _catalog);
                return;
            }

            Open(cutscene);
        }

        /// <summary>
        /// Выйти из зафиксированного лупа на следующую фазу. На сквозной фазе ничего не делает — она переключится
        /// сама; катсцена не открыта или фаза последняя — тоже ничего
        /// </summary>
        public static void Next()
        {
            if (!IsOpen || !Data.Cutscene.Phases[Data.PhaseIndex].AnimationLooped)
                return;

            Advance();
        }

        /// <summary>
        /// Сообщение представления: сквозная фаза <paramref name="phaseIndex"/> отыграла проход. Переключает
        /// на следующую фазу. Сообщение не о текущей фазе или о зафиксированном лупе игнорируется
        /// </summary>
        public static void CompletePhase(int phaseIndex)
        {
            if (!IsOpen || phaseIndex != Data.PhaseIndex || Data.Cutscene.Phases[phaseIndex].AnimationLooped)
                return;

            Advance();
        }

        /// <summary>
        /// Закрыть катсцену — принудительное прерывание показа. Катсцена не открыта — ничего не делает
        /// </summary>
        public static void Close()
        {
            if (!IsOpen)
                return;

            Data.Cutscene = null;
            Data.PhaseIndex = 0;
            OnClosed?.Invoke();
        }

        private static void Advance()
        {
            if (Data.PhaseIndex >= Data.Cutscene.Phases.Count - 1)
                return;

            Data.PhaseIndex++;
            OnPhaseChanged?.Invoke();
        }
    }
}
