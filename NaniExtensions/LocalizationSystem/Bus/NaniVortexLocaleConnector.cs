using Naninovel;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.NaniExtensions.Core;

namespace Vortex.NaniExtensions.LocalizationSystem.Bus
{
    /// <summary>
    /// Коннектор Нани-Vortex для передачи сигнала о смене локализации.
    ///
    /// Применение локали упорядочено в ОДИН await-флоу (см. <see cref="ApplyLocaleAsync"/>): сначала
    /// дожидаемся <c>SelectLocale</c> (пока Naninovel не догрузит managed-text документы новой локали),
    /// и только потом трогаем голосовой лоадер и делаем один <c>SaveGlobal</c>. Раньше всё шло fire-and-forget
    /// внахлёст: <c>SelectLocale</c> (не await), <c>SaveGlobal</c> и <c>OverrideLocale</c> запускались поверх
    /// недо-завершённой смены локали, и стейт/бэклог резолвили текст по наполовину выгруженным документам →
    /// «managed text document is not loaded». Повторные сигналы во время применения коалесятся флагом, а не
    /// плодят второй конкурентный флоу.
    /// </summary>
    public static class NaniVortexLocaleConnector
    {
        // true, пока идёт ApplyLocaleAsync — второй сигнал не запускает конкурентный флоу.
        private static bool _applying;

        // Локаль сменили во время применения — переприменить в конце текущего прогона с актуальными значениями.
        private static bool _pendingReapply;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            App.OnStart -= Init;
            App.OnStart += Init;
        }

        private static void Init()
        {
            App.OnStart -= Init;

            Engine.OnInitializationFinished -= Init;
            Localization.OnLocalizationChanged -= OnLocaleChanged;
            Localization.OnLocalizationChanged += OnLocaleChanged;

            if (Engine.Initialized)
                OnLocaleChanged();
            else
                Engine.OnInitializationFinished += Init;
            //SetNaniVoiceLocale(); Установка локали диалога запустит каскад
        }

        private static void Exit()
        {
            Localization.OnLocalizationChanged -= OnLocaleChanged;
        }

        // Синхронный вход из события смены локали. Запускает упорядоченное применение, либо помечает
        // переприменение, если предыдущее ещё идёт (без наложения асинхронных операций).
        private static void OnLocaleChanged()
        {
            if (App.GetState() == AppStates.Stopping)
            {
                Exit();
                return;
            }

            if (_applying)
            {
                _pendingReapply = true;
                return;
            }

            ApplyLocaleAsync().Forget(Debug.LogException);
        }

        private static async UniTask ApplyLocaleAsync()
        {
            _applying = true;
            try
            {
                do
                {
                    _pendingReapply = false;

                    if (App.GetState() == AppStates.Stopping)
                    {
                        Exit();
                        return;
                    }

                    // 1) Диалог: ДОЖДАТЬСЯ полной смены локали — Naninovel догружает managed-text документы
                    //    новой локали. До завершения ничего не резолвим и не сохраняем.
                    await NaniWrapper.L10N.SelectLocale(
                        Localization.GetCurrentChannelLanguage(LocaleChannels.Dialogue));

                    if (App.GetState() == AppStates.Stopping)
                    {
                        Exit();
                        return;
                    }

                    // 2) Голос: лоадер берём СВЕЖИМ (не держим ссылку — Naninovel может его пересоздать/выгрузить).
                    //    Паттерн is-cast заодно гасит неверный тип/​null без исключения.
                    if (NaniWrapper.AudioManager.VoiceLoader is LocalizableResourceLoader<AudioClip> voiceLoader)
                        voiceLoader.OverrideLocale =
                            Localization.GetCurrentChannelLanguage(LocaleChannels.Voice);

                    // 3) Один SaveGlobal в самом конце — документы новой локали уже загружены.
                    await NaniWrapper.StateManager.SaveGlobal();
                }
                // Локаль сменили, пока применяли — переприменяем актуальные значения (без наложения флоу).
                while (_pendingReapply);
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
